using System.Net.Http.Headers;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Cliente SOAP real para comunicación con AEAT VERI*FACTU.
///
/// Implementa:
/// - Construcción del SOAP envelope con namespaces oficiales (document/literal)
/// - Parser real de RespuestaRegFactuSistemaFacturacion
/// - Detección y lanzamiento de SOAP Fault como excepción diferenciada
/// - Clasificación de errores transitorios vs permanentes
///
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
/// Ref: /VERIFACTU/SuministroLR.xsd.xml
/// Ref: /VERIFACTU/RespuestaSuministro.xsd.xml
/// </summary>
public sealed class VeriFactuGatewaySoapClient : IVeriFactuGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VeriFactuGatewaySoapClient> _logger;
    private readonly VeriFactuOptions _options;

    // Namespaces oficiales — Ref: SistemaFacturacion.wsdl.xml y RespuestaSuministro.xsd.xml
    private static readonly XNamespace NsSoap   = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace NsSf     = RegistroAltaXmlBuilder.NsSf;
    private static readonly XNamespace NsSfLr   = RegistroAltaXmlBuilder.NsSfLr;
    private static readonly XNamespace NsResp   =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    public VeriFactuGatewaySoapClient(
        HttpClient httpClient,
        IOptions<VeriFactuOptions> options,
        ILogger<VeriFactuGatewaySoapClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options    = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger     = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ?? IVeriFactuGateway ????????????????????????????????????????????????????

    /// <summary>
    /// Envía un RegistroAlta a AEAT (RegFactuSistemaFacturacion).
    /// El XML del documento ya viene construido en SignedXmlContent.
    /// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml — operación RegFactuSistemaFacturacion
    /// </summary>
    public async Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = _options.GetEndpoint();
        _logger.LogInformation(
            "Enviando RegistroAlta a AEAT [{Entorno}] para NIF {Nif}",
            _options.Environment, request.TaxpayerNif);

        var soapDoc  = BuildRegFactuSoapEnvelope(request.SignedXmlContent);
        var respDoc  = await SendSoapAsync(endpoint, soapDoc, cancellationToken);
        return ParseSubmissionResponse(respDoc);
    }

    public Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
               "Consulta no implementada en esta fase. Solo se soporta RegistroAlta.");

    public Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
        VeriFactuCancellationRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
               "Anulacion no implementada en esta fase. Solo se soporta RegistroAlta.");

    // ?? Construcción SOAP envelope ???????????????????????????????????????????

    /// <summary>
    /// Construye el SOAP envelope para RegFactuSistemaFacturacion.
    /// El elemento RegFactuSistemaFacturacion (SuministroLR.xsd) se inserta directamente en Body
    /// con uso literal (document/literal) según el WSDL.
    /// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
    /// </summary>
    private static XDocument BuildRegFactuSoapEnvelope(string regFactuXml)
    {
        // Parsear el XML ya construido (no concatenación de strings)
        var regFactuElement = XElement.Parse(regFactuXml);

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(NsSoap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", NsSoap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sfLR",    NsSfLr.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sf",      NsSf.NamespaceName),
                new XElement(NsSoap + "Header"),
                new XElement(NsSoap + "Body", regFactuElement)));
    }

    // ?? HTTP ?????????????????????????????????????????????????????????????????

    private async Task<XDocument> SendSoapAsync(
        string endpoint,
        XDocument soapEnvelope,
        CancellationToken cancellationToken)
    {
        var xmlBytes = Encoding.UTF8.GetBytes(
            soapEnvelope.Declaration?.ToString() + soapEnvelope.ToString());
        using var content = new ByteArrayContent(xmlBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "UTF-8" };
        // SOAPAction vacío según WSDL: <soap:operation soapAction=""/>
        content.Headers.Add("SOAPAction", "\"\"");

        _logger.LogDebug("POST SOAP a {Endpoint} ({Bytes} bytes)", endpoint, xmlBytes.Length);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VeriFactuCommunicationException(
                "Timeout al conectar con AEAT.", ex, isTransient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new VeriFactuCommunicationException(
                $"Error de red al conectar con AEAT: {ex.Message}", ex, isTransient: true);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("Respuesta AEAT: HTTP {StatusCode}", (int)response.StatusCode);

        // Intentar parsear siempre: incluso un error HTTP puede traer SOAP Fault
        XDocument? doc = null;
        try { doc = XDocument.Parse(body); }
        catch (XmlException) { /* body no es XML */ }

        // SOAP Fault tiene prioridad sobre el código HTTP
        if (doc != null)
        {
            var fault = doc.Descendants(NsSoap + "Fault").FirstOrDefault();
            if (fault != null)
                throw new VeriFactuSoapFaultException(ExtractFaultMessage(fault));
        }

        if (!response.IsSuccessStatusCode)
        {
            var isTransient = (int)response.StatusCode >= 500;
            throw new VeriFactuCommunicationException(
                $"AEAT devolvio HTTP {(int)response.StatusCode}. {Truncate(body, 500)}",
                isTransient: isTransient);
        }

        if (doc == null)
            throw new VeriFactuCommunicationException(
                "La respuesta de AEAT no es XML valido.", isTransient: false);

        return doc;
    }

    // ?? Parsing de respuesta ?????????????????????????????????????????????????

    /// <summary>
    /// Parsea RespuestaRegFactuSistemaFacturacion conforme a RespuestaSuministro.xsd.
    /// Ref: /VERIFACTU/RespuestaSuministro.xsd.xml
    /// Campos mapeados: CSV, TiempoEsperaEnvio, EstadoEnvio, RespuestaLinea[]
    /// </summary>
    private VeriFactuSubmissionResult ParseSubmissionResponse(XDocument doc)
    {
        // Buscar dentro del Body SOAP
        var respuesta = doc
            .Descendants(NsResp + "RespuestaRegFactuSistemaFacturacion")
            .FirstOrDefault()
            ?? throw new VeriFactuCommunicationException(
                "No se encontro RespuestaRegFactuSistemaFacturacion en la respuesta SOAP.",
                isTransient: false);

        // CSV: identificador asignado por AEAT (solo cuando no hay rechazo total)
        var csv = respuesta.Element(NsResp + "CSV")?.Value;

        var tiempoEspera = respuesta.Element(NsResp + "TiempoEsperaEnvio")?.Value;

        // EstadoEnvio: "Correcto" | "ParcialmenteCorrecto" | "Incorrecto"
        var estadoEnvio = respuesta.Element(NsResp + "EstadoEnvio")?.Value ?? "Incorrecto";

        // RespuestaLinea — detalle por cada registro enviado
        var lineas = respuesta
            .Elements(NsResp + "RespuestaLinea")
            .Select(ParseRespuestaLinea)
            .ToList();

        var isAccepted   = estadoEnvio is "Correcto" or "ParcialmenteCorrecto";
        var descripcion  = BuildDescripcion(estadoEnvio, lineas);
        var responseCode = ClasificarEstado(estadoEnvio, lineas);

        _logger.LogInformation(
            "Respuesta AEAT: EstadoEnvio={Estado}, CSV={Csv}, Lineas={Count}",
            estadoEnvio, csv ?? "(sin CSV)", lineas.Count);

        return new VeriFactuSubmissionResult
        {
            // CSV es el identificador real del envío asignado por AEAT
            SubmissionId      = csv ?? $"SIN-CSV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            IsAccepted        = isAccepted,
            ResponseCode      = responseCode,
            StatusCode        = estadoEnvio,
            StatusDescription = descripcion,
            AdditionalDetails = tiempoEspera != null ? $"TiempoEspera={tiempoEspera}s" : null,
            ServerTimestamp   = DateTime.UtcNow
        };
    }

    private static RespuestaLineaDto ParseRespuestaLinea(XElement linea)
    {
        var idFactura       = linea.Element(NsResp + "IDFactura");
        var numSerie        = idFactura?.Element(NsSf + "NumSerieFactura")?.Value ?? string.Empty;
        var estadoRegistro  = linea.Element(NsResp + "EstadoRegistro")?.Value ?? string.Empty;
        var codigoError     = linea.Element(NsResp + "CodigoErrorRegistro")?.Value;
        var descripcionError = linea.Element(NsResp + "DescripcionErrorRegistro")?.Value;
        return new RespuestaLineaDto(numSerie, estadoRegistro, codigoError, descripcionError);
    }

    private static string BuildDescripcion(string estadoEnvio, List<RespuestaLineaDto> lineas)
    {
        var sb = new StringBuilder($"EstadoEnvio: {estadoEnvio}");
        foreach (var l in lineas)
        {
            sb.Append($" | {l.NumSerie}: {l.EstadoRegistro}");
            if (!string.IsNullOrWhiteSpace(l.CodigoError))
                sb.Append($" (Cod:{l.CodigoError} {l.DescripcionError})");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Clasifica el estado AEAT para decisiones de retry.
    /// Errores 4108/4112 = certificado/autenticación ? no reintentable.
    /// Ref: /VERIFACTU/errores.properties.txt
    /// </summary>
    private static AeatResponseCode ClasificarEstado(
        string estadoEnvio, List<RespuestaLineaDto> lineas)
        => estadoEnvio switch
        {
            "Correcto" or "ParcialmenteCorrecto" => AeatResponseCode.Success,
            "Incorrecto" when lineas.Any(l => l.CodigoError is "4108" or "4112")
                => AeatResponseCode.AuthenticationError,
            "Incorrecto" => AeatResponseCode.BusinessRejection,
            _ => AeatResponseCode.Unknown
        };

    private static string ExtractFaultMessage(XElement fault)
    {
        var code   = fault.Element("faultcode")?.Value   ?? "UNKNOWN";
        var detail = fault.Element("faultstring")?.Value ?? "SOAP Fault sin detalle";
        return $"SOAP Fault [{code}]: {detail}";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";

    private sealed record RespuestaLineaDto(
        string  NumSerie,
        string  EstadoRegistro,
        string? CodigoError,
        string? DescripcionError);
}

// ?? Excepciones diferenciadas de Infrastructure ???????????????????????????????

/// <summary>
/// Fallo de comunicación con AEAT (timeout, red, HTTP 5xx).
/// IsTransient indica si puede reintentarse.
/// </summary>
public sealed class VeriFactuCommunicationException : Exception
{
    public bool IsTransient { get; }

    public VeriFactuCommunicationException(string message, bool isTransient = true)
        : base(message) => IsTransient = isTransient;

    public VeriFactuCommunicationException(string message, Exception inner, bool isTransient = true)
        : base(message, inner) => IsTransient = isTransient;
}

/// <summary>
/// SOAP Fault devuelto explícitamente por AEAT.
/// No debe reintentarse hasta resolver el problema subyacente.
/// </summary>
public sealed class VeriFactuSoapFaultException : Exception
{
    public VeriFactuSoapFaultException(string message) : base(message) { }
}

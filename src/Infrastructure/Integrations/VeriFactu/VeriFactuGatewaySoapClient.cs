using System.Globalization;
using System.Net;
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
/// Cliente SOAP 1.1 real para AEAT VERI*FACTU.
/// </summary>
public sealed class VeriFactuGatewaySoapClient : IVeriFactuGateway
{
    private readonly HttpClient? _httpClient;
    private readonly IVeriFactuHttpClientProvider? _httpClientProvider;
    private readonly IXmlSchemaValidator _xmlSchemaValidator;
    private readonly ILogger<VeriFactuGatewaySoapClient> _logger;
    private readonly VeriFactuOptions _options;

    private static readonly XNamespace NsSoap =
        "http://schemas.xmlsoap.org/soap/envelope/";

    private static readonly XNamespace NsSf =
        RegistroAltaXmlBuilder.NsSf;

    private static readonly XNamespace NsSfLr =
        RegistroAltaXmlBuilder.NsSfLr;

    private static readonly XNamespace NsResp =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    private static readonly XNamespace NsQuery =
        VeriFactuQueryXmlCodec.NsQuery;

    private static readonly XNamespace NsQueryResponse =
        VeriFactuQueryXmlCodec.NsResponse;

    /// <summary>
    /// Constructor usado por pruebas/E2E con un HttpClient ya configurado.
    /// </summary>
    public VeriFactuGatewaySoapClient(
        HttpClient httpClient,
        IOptions<VeriFactuOptions> options,
        IXmlSchemaValidator xmlSchemaValidator,
        ILogger<VeriFactuGatewaySoapClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _xmlSchemaValidator = xmlSchemaValidator ??
            throw new ArgumentNullException(nameof(xmlSchemaValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Constructor de producción: selecciona HttpClient/certificado por NIF.
    /// </summary>
    public VeriFactuGatewaySoapClient(
        IVeriFactuHttpClientProvider httpClientProvider,
        IOptions<VeriFactuOptions> options,
        IXmlSchemaValidator xmlSchemaValidator,
        ILogger<VeriFactuGatewaySoapClient> logger)
    {
        _httpClientProvider = httpClientProvider ??
            throw new ArgumentNullException(nameof(httpClientProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _xmlSchemaValidator = xmlSchemaValidator ??
            throw new ArgumentNullException(nameof(xmlSchemaValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SignedXmlContent))
            throw new ArgumentException("El XML RegistroAlta no puede estar vacío.", nameof(request));

        var endpoint = _options.GetEndpoint();

        _logger.LogInformation(
            "Enviando RegistroAlta a AEAT [{Environment}] para NIF {TaxpayerNif}",
            _options.Environment,
            request.TaxpayerNif);

        var envelope = BuildRegFactuSoapEnvelope(request.SignedXmlContent);
        var response = await SendSoapAsync(
            request.TaxpayerNif,
            endpoint,
            envelope,
            cancellationToken);

        var responseElement = response.Document
            .Descendants(NsResp + "RespuestaRegFactuSistemaFacturacion")
            .FirstOrDefault()
            ?? throw new VeriFactuCommunicationException(
                "La respuesta SOAP no contiene RespuestaRegFactuSistemaFacturacion.",
                isTransient: false);

        // La respuesta también se valida contra el XSD oficial antes de mapearla.
        var validation = await _xmlSchemaValidator.ValidateAsync(
            responseElement.ToString(SaveOptions.DisableFormatting),
            VeriFactuXmlSchemaType.SubmissionResponse,
            cancellationToken);

        if (!validation.IsValid)
        {
            throw new VeriFactuCommunicationException(
                "La respuesta AEAT no cumple el XSD oficial: " +
                string.Join(" | ", validation.Errors.Select(e => e.Message)),
                isTransient: false);
        }

        return ParseSubmissionResponse(
            responseElement,
            response.RawBody);
    }

    public async Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queryXml = VeriFactuQueryXmlCodec.BuildRequest(request);

        var requestValidation = await _xmlSchemaValidator.ValidateAsync(
            queryXml,
            VeriFactuXmlSchemaType.QueryRecord,
            cancellationToken);

        if (!requestValidation.IsValid)
        {
            throw new VeriFactuCommunicationException(
                "La consulta AEAT no cumple ConsultaLR.xsd: " +
                string.Join(
                    " | ",
                    requestValidation.Errors.Select(x => x.Message)),
                isTransient: false);
        }

        _logger.LogInformation(
            "Consultando AEAT [{Environment}] para NIF {TaxpayerNif}, periodo {FiscalYear}/{Period}",
            _options.Environment,
            request.TaxpayerNif,
            request.FiscalYear,
            request.Period);

        var envelope = BuildQuerySoapEnvelope(queryXml);
        var response = await SendSoapAsync(
            request.TaxpayerNif,
            _options.GetEndpoint(),
            envelope,
            cancellationToken);

        var responseElement = response.Document
            .Descendants(
                NsQueryResponse + "RespuestaConsultaFactuSistemaFacturacion")
            .FirstOrDefault()
            ?? throw new VeriFactuCommunicationException(
                "La respuesta SOAP no contiene RespuestaConsultaFactuSistemaFacturacion.",
                isTransient: false);

        var responseValidation = await _xmlSchemaValidator.ValidateAsync(
            responseElement.ToString(SaveOptions.DisableFormatting),
            VeriFactuXmlSchemaType.QueryResponse,
            cancellationToken);

        if (!responseValidation.IsValid)
        {
            throw new VeriFactuCommunicationException(
                "La respuesta de consulta AEAT no cumple RespuestaConsultaLR.xsd: " +
                string.Join(
                    " | ",
                    responseValidation.Errors.Select(x => x.Message)),
                isTransient: false);
        }

        return VeriFactuQueryXmlCodec.ParseResponse(
            responseElement,
            response.RawBody);
    }


    private static XDocument BuildQuerySoapEnvelope(string queryXml)
    {
        XElement queryElement;

        try
        {
            queryElement = XElement.Parse(
                queryXml,
                LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new VeriFactuCommunicationException(
                "El XML de consulta no está bien formado.",
                ex,
                isTransient: false);
        }

        if (queryElement.Name !=
            NsQuery + "ConsultaFactuSistemaFacturacion")
        {
            throw new VeriFactuCommunicationException(
                "El XML de consulta no tiene la raíz oficial ConsultaFactuSistemaFacturacion.",
                isTransient: false);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                NsSoap + "Envelope",
                new XAttribute(
                    XNamespace.Xmlns + "soapenv",
                    NsSoap.NamespaceName),
                new XAttribute(
                    XNamespace.Xmlns + "sfLRC",
                    NsQuery.NamespaceName),
                new XAttribute(
                    XNamespace.Xmlns + "sf",
                    NsSf.NamespaceName),
                new XElement(NsSoap + "Header"),
                new XElement(NsSoap + "Body", queryElement)));
    }

    private static XDocument BuildRegFactuSoapEnvelope(string regFactuXml)
    {
        XElement regFactuElement;

        try
        {
            regFactuElement = XElement.Parse(
                regFactuXml,
                LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new VeriFactuCommunicationException(
                "El XML RegistroAlta proporcionado al gateway no está bien formado.",
                ex,
                isTransient: false);
        }

        if (regFactuElement.Name != NsSfLr + "RegFactuSistemaFacturacion")
        {
            throw new VeriFactuCommunicationException(
                "El XML a remitir no tiene como raíz sfLR:RegFactuSistemaFacturacion.",
                isTransient: false);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                NsSoap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", NsSoap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sfLR", NsSfLr.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sf", NsSf.NamespaceName),
                new XElement(NsSoap + "Header"),
                new XElement(NsSoap + "Body", regFactuElement)));
    }

    private async Task<SoapResponse> SendSoapAsync(
        string taxpayerNif,
        string endpoint,
        XDocument soapEnvelope,
        CancellationToken cancellationToken)
    {
        var xml = soapEnvelope.Declaration?.ToString() + soapEnvelope.ToString(SaveOptions.DisableFormatting);
        var xmlBytes = Encoding.UTF8.GetBytes(xml);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new ByteArrayContent(xmlBytes);
        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("text/xml") { CharSet = "UTF-8" };

        // WSDL oficial: soapAction=""
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        HttpResponseMessage response;

        try
        {
            var client = _httpClient ??
                _httpClientProvider?.GetClient(taxpayerNif) ??
                throw new InvalidOperationException(
                    "No hay HttpClient VERI*FACTU configurado.");

            response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VeriFactuCommunicationException(
                "Timeout al conectar con AEAT.",
                ex,
                isTransient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new VeriFactuCommunicationException(
                $"Error de red al conectar con AEAT: {ex.Message}",
                ex,
                isTransient: true);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            XDocument? document = null;
            try
            {
                document = XDocument.Parse(body, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                // Se clasifica después según el código HTTP.
            }

            if (document is not null)
            {
                var fault = document
                    .Descendants(NsSoap + "Fault")
                    .FirstOrDefault();

                if (fault is not null)
                {
                    throw new VeriFactuSoapFaultException(
                        ExtractFaultMessage(fault),
                        body);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                var transient =
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    (int)response.StatusCode == 429 ||
                    (int)response.StatusCode >= 500;

                throw new VeriFactuCommunicationException(
                    $"AEAT devolvió HTTP {(int)response.StatusCode}. {Truncate(body, 500)}",
                    isTransient: transient);
            }

            if (document is null)
            {
                throw new VeriFactuCommunicationException(
                    "La respuesta HTTP 2xx de AEAT no contiene XML válido.",
                    isTransient: false);
            }

            return new SoapResponse(document, body);
        }
    }

    private VeriFactuSubmissionResult ParseSubmissionResponse(
        XElement response,
        string rawSoap)
    {
        var csv = NullIfWhiteSpace(response.Element(NsResp + "CSV")?.Value);
        var tiempoEspera = NullIfWhiteSpace(
            response.Element(NsResp + "TiempoEsperaEnvio")?.Value);

        var estadoEnvio = NullIfWhiteSpace(
            response.Element(NsResp + "EstadoEnvio")?.Value)
            ?? throw new VeriFactuCommunicationException(
                "Respuesta AEAT sin EstadoEnvio.",
                isTransient: false);

        var lineas = response
            .Elements(NsResp + "RespuestaLinea")
            .Select(ParseRespuestaLinea)
            .ToList();

        if (lineas.Count > 1)
        {
            throw new VeriFactuCommunicationException(
                $"gesFactu remitió un único registro pero AEAT devolvió {lineas.Count} RespuestaLinea.",
                isTransient: false);
        }

        var linea = lineas.SingleOrDefault();

        if (estadoEnvio is "Correcto" or "ParcialmenteCorrecto" && linea is null)
        {
            throw new VeriFactuCommunicationException(
                "AEAT informó un envío procesado sin devolver RespuestaLinea para el registro remitido.",
                isTransient: false);
        }

        var isAccepted =
            linea?.EstadoRegistro is "Correcto" or "AceptadoConErrores";

        if (isAccepted && csv is null)
        {
            throw new VeriFactuCommunicationException(
                "AEAT aceptó el registro pero la respuesta no contiene el CSV exigido para un envío no rechazado.",
                isTransient: false);
        }

        var errorCode = linea?.CodigoError;
        var isDuplicate = errorCode == "3000";

        var responseCode = ClassifyResponse(
            isAccepted,
            errorCode);

        var presentationTimestamp = ParsePresentationTimestamp(response);

        var description = BuildDescription(
            estadoEnvio,
            linea);

        var extra = new List<string>();

        if (tiempoEspera is not null)
            extra.Add($"TiempoEsperaEnvio={tiempoEspera}");

        if (linea?.DuplicateRecordStatus is not null)
            extra.Add($"EstadoRegistroDuplicado={linea.DuplicateRecordStatus}");

        if (linea?.DuplicateRequestId is not null)
            extra.Add($"IdPeticionRegistroDuplicado={linea.DuplicateRequestId}");

        _logger.LogInformation(
            "Respuesta AEAT procesada: EstadoEnvio={EstadoEnvio}, EstadoRegistro={EstadoRegistro}, CSV={Csv}, CodigoError={CodigoError}",
            estadoEnvio,
            linea?.EstadoRegistro ?? "(sin línea)",
            csv ?? "(sin CSV)",
            errorCode ?? "(sin error)");

        return new VeriFactuSubmissionResult
        {
            SubmissionId = csv,
            IsAccepted = isAccepted,
            ResponseCode = responseCode,
            StatusCode = estadoEnvio,
            RecordStatus = linea?.EstadoRegistro,
            StatusDescription = description,
            ErrorCode = errorCode,
            IsDuplicate = isDuplicate,
            DuplicateRecordStatus = linea?.DuplicateRecordStatus,
            DuplicateRequestId = linea?.DuplicateRequestId,
            AdditionalDetails = extra.Count == 0
                ? null
                : string.Join(" | ", extra),
            ServerTimestamp = presentationTimestamp,
            RawResponsePayload = rawSoap
        };
    }

    private static RespuestaLinea ParseRespuestaLinea(XElement line)
    {
        var idFactura = line.Element(NsResp + "IDFactura");

        var numSerie = NullIfWhiteSpace(
            idFactura?.Element(NsSf + "NumSerieFactura")?.Value);

        var estadoRegistro = NullIfWhiteSpace(
            line.Element(NsResp + "EstadoRegistro")?.Value)
            ?? throw new VeriFactuCommunicationException(
                "RespuestaLinea sin EstadoRegistro.",
                isTransient: false);

        var codigoError = NullIfWhiteSpace(
            line.Element(NsResp + "CodigoErrorRegistro")?.Value);

        var descripcionError = NullIfWhiteSpace(
            line.Element(NsResp + "DescripcionErrorRegistro")?.Value);

        var duplicate = line.Element(NsResp + "RegistroDuplicado");

        var duplicateStatus = NullIfWhiteSpace(
            duplicate?.Element(NsSf + "EstadoRegistroDuplicado")?.Value);

        var duplicateRequestId = NullIfWhiteSpace(
            duplicate?.Element(NsSf + "IdPeticionRegistroDuplicado")?.Value);

        return new RespuestaLinea(
            numSerie,
            estadoRegistro,
            codigoError,
            descripcionError,
            duplicateStatus,
            duplicateRequestId);
    }

    private static DateTime? ParsePresentationTimestamp(XElement response)
    {
        var raw = response
            .Element(NsResp + "DatosPresentacion")
            ?.Element(NsSf + "TimestampPresentacion")
            ?.Value;

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            return timestamp.UtcDateTime;
        }

        return null;
    }

    private static AeatResponseCode ClassifyResponse(
        bool accepted,
        string? errorCode)
    {
        if (accepted)
            return AeatResponseCode.Success;

        return errorCode switch
        {
            "3000" => AeatResponseCode.DuplicateError,
            "4108" or "4112" => AeatResponseCode.AuthenticationError,
            "4134" or "4139" or "4141" => AeatResponseCode.PermanentError,
            null => AeatResponseCode.BusinessRejection,
            _ => AeatResponseCode.BusinessRejection
        };
    }

    private static string BuildDescription(
        string estadoEnvio,
        RespuestaLinea? linea)
    {
        var builder = new StringBuilder();
        builder.Append($"EstadoEnvio: {estadoEnvio}");

        if (linea is null)
            return builder.ToString();

        builder.Append($" | {linea.NumSerie ?? "(sin NumSerieFactura)"}: {linea.EstadoRegistro}");

        if (linea.CodigoError is not null)
        {
            builder.Append($" | Error {linea.CodigoError}");

            if (linea.DescripcionError is not null)
                builder.Append($": {linea.DescripcionError}");
        }

        return builder.ToString();
    }

    private static string ExtractFaultMessage(XElement fault)
    {
        var code = fault.Element("faultcode")?.Value ?? "UNKNOWN";
        var message = fault.Element("faultstring")?.Value ?? "SOAP Fault sin detalle";

        return $"SOAP Fault [{code}]: {message}";
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    private sealed record SoapResponse(
        XDocument Document,
        string RawBody);

    private sealed record RespuestaLinea(
        string? NumSerie,
        string EstadoRegistro,
        string? CodigoError,
        string? DescripcionError,
        string? DuplicateRecordStatus,
        string? DuplicateRequestId);
}

/// <summary>
/// Fallo técnico/de transporte. IsTransient decide si el Outbox puede reintentar.
/// </summary>
public sealed class VeriFactuCommunicationException : Exception
{
    public bool IsTransient { get; }

    public VeriFactuCommunicationException(
        string message,
        bool isTransient = true)
        : base(message)
    {
        IsTransient = isTransient;
    }

    public VeriFactuCommunicationException(
        string message,
        Exception inner,
        bool isTransient = true)
        : base(message, inner)
    {
        IsTransient = isTransient;
    }
}

/// <summary>
/// Fault SOAP explícito de AEAT.
/// </summary>
public sealed class VeriFactuSoapFaultException : Exception
{
    public string? RawResponsePayload { get; }

    public VeriFactuSoapFaultException(
        string message,
        string? rawResponsePayload = null)
        : base(message)
    {
        RawResponsePayload = rawResponsePayload;
    }
}

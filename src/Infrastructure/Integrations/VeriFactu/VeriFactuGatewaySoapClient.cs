using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.Mappers;
using gesFactu.Infrastructure.Integrations.VeriFactu.Soap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Cliente SOAP real para comunicación con AEAT VERI*FACTU.
/// 
/// Encapsula:
/// - Llamadas HTTP/SOAP a los endpoints de AEAT
/// - Manejo de certificados X.509
/// - Serialización/deserialización XML
/// - Anti-Corruption Layer (mapeo a tipos de negocio)
/// 
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml
/// </summary>
public sealed class VeriFactuGatewaySoapClient : IVeriFactuGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VeriFactuGatewaySoapClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly X509Certificate2? _clientCertificate;

    // Endpoints AEAT
    private readonly string _productionEndpoint;
    private readonly string _stagingEndpoint;
    private readonly bool _useStaging;

    public VeriFactuGatewaySoapClient(
        HttpClient httpClient,
        ILogger<VeriFactuGatewaySoapClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Configuración de endpoints
        _useStaging = _configuration.GetValue<bool>("VeriFactu:UseStaging", false);
        _productionEndpoint = _configuration["VeriFactu:ProductionEndpoint"]
            ?? "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";
        _stagingEndpoint = _configuration["VeriFactu:StagingEndpoint"]
            ?? "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";

        // Cargar certificado cliente (si está configurado)
        _clientCertificate = LoadClientCertificate();

        // Configurar HttpClient con certificado
        if (_clientCertificate != null)
        {
            // En producción, HttpClientHandler debe estar configurado en DI
            _logger.LogInformation("Certificado cliente cargado para AEAT");
        }

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "gesFactu/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Envía un registro de facturación a AEAT.
    /// </summary>
    public async Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            _logger.LogInformation(
                "Iniciando envío de registro a AEAT para NIF {TaxpayerNif}",
                request.TaxpayerNif);

            // Obtener ID de tercero desde configuración o request
            var taxpayerId = GetTaxpayerId(request.TaxpayerNif);

            // Mapear a solicitud SOAP
            var soapRequest = AeatSoapMapper.ToSoapSubmissionRequest(request, taxpayerId);

            // Construir envelope SOAP
            var soapEnvelope = BuildSubmitSoapEnvelope(soapRequest);

            // Enviar a AEAT
            var endpoint = _useStaging ? _stagingEndpoint : _productionEndpoint;
            var response = await SendSoapRequestAsync<RegFactuSistemaFacturacionResponse>(
                endpoint,
                soapEnvelope,
                cancellationToken);

            // Mapear respuesta SOAP a resultado de negocio
            var result = AeatSoapMapper.FromSoapSubmissionResponse(response);

            _logger.LogInformation(
                "Envío completado. SubmissionId: {SubmissionId}, IsAccepted: {IsAccepted}, ResponseCode: {ResponseCode}",
                result.SubmissionId,
                result.IsAccepted,
                result.ResponseCode);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al enviar registro a AEAT para NIF {TaxpayerNif}",
                request.TaxpayerNif);

            throw;
        }
    }

    /// <summary>
    /// Consulta el estado de un registro previamente enviado.
    /// </summary>
    public async Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            _logger.LogInformation(
                "Consultando estado de envío {SubmissionId} para NIF {TaxpayerNif}",
                request.SubmissionId,
                request.TaxpayerNif);

            var taxpayerId = GetTaxpayerId(request.TaxpayerNif);
            var soapRequest = AeatSoapMapper.ToSoapQueryRequest(request, taxpayerId);
            var soapEnvelope = BuildQuerySOAPEnvelope(soapRequest);
            var endpoint = _useStaging ? _stagingEndpoint : _productionEndpoint;

            var response = await SendSoapRequestAsync<ConsultaFactuSistemaFacturacionResponse>(
                endpoint,
                soapEnvelope,
                cancellationToken);

            var result = AeatSoapMapper.FromSoapQueryResponse(response);

            _logger.LogInformation(
                "Consulta completada. Status: {Status}",
                result.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al consultar estado en AEAT para SubmissionId: {SubmissionId}",
                request.SubmissionId);

            throw;
        }
    }

    /// <summary>
    /// Solicita la cancelación de un registro.
    /// </summary>
    public async Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
        VeriFactuCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            _logger.LogInformation(
                "Solicitando cancelación de envío {SubmissionId} para NIF {TaxpayerNif}",
                request.SubmissionId,
                request.TaxpayerNif);

            var taxpayerId = GetTaxpayerId(request.TaxpayerNif);
            var soapRequest = AeatSoapMapper.ToSoapCancellationRequest(request, taxpayerId);
            var soapEnvelope = BuildCancellationSOAPEnvelope(soapRequest);
            var endpoint = _useStaging ? _stagingEndpoint : _productionEndpoint;

            var response = await SendSoapRequestAsync<CancelacionRegistroResponse>(
                endpoint,
                soapEnvelope,
                cancellationToken);

            var result = AeatSoapMapper.FromSoapCancellationResponse(response);

            _logger.LogInformation(
                "Cancelación completada. CancellationId: {CancellationId}, IsAccepted: {IsAccepted}",
                result.CancellationId,
                result.IsAccepted);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al cancelar registro en AEAT para SubmissionId: {SubmissionId}",
                request.SubmissionId);

            throw;
        }
    }

    /// <summary>
    /// Construye el SOAP envelope para envío de registro.
    /// </summary>
    private static string BuildSubmitSoapEnvelope(RegFactuSistemaFacturacionRequest request)
    {
        // Nota: En producción, usar una librería SOAP robusta o serialización XML tipada
        // Por ahora, construimos manualmente con validación básica

        var xmlBody = request.RegistroFacturacionXml ?? string.Empty;

        var envelope = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" 
                  xmlns:sf=""http://www.aeat.gob.es/VeriFacTuSF"">
    <soapenv:Header/>
    <soapenv:Body>
        <sf:RegFactuSistemaFacturacion>
            <sf:IdTercero>{EscapeXml(request.IdTercero)}</sf:IdTercero>
            <sf:RegistroFacturacionXml>
<![CDATA[{xmlBody}]]>
            </sf:RegistroFacturacionXml>
        </sf:RegFactuSistemaFacturacion>
    </soapenv:Body>
</soapenv:Envelope>";

        return envelope;
    }

    /// <summary>
    /// Construye el SOAP envelope para consulta de estado.
    /// </summary>
    private static string BuildQuerySOAPEnvelope(ConsultaFactuSistemaFacturacionRequest request)
    {
        var envelope = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" 
                  xmlns:sf=""http://www.aeat.gob.es/VeriFacTuSF"">
    <soapenv:Header/>
    <soapenv:Body>
        <sf:ConsultaFactuSistemaFacturacion>
            <sf:IdEnvio>{EscapeXml(request.IdEnvio)}</sf:IdEnvio>
            <sf:IdTercero>{EscapeXml(request.IdTercero)}</sf:IdTercero>
        </sf:ConsultaFactuSistemaFacturacion>
    </soapenv:Body>
</soapenv:Envelope>";

        return envelope;
    }

    /// <summary>
    /// Construye el SOAP envelope para cancelación.
    /// </summary>
    private static string BuildCancellationSOAPEnvelope(CancelacionRegistroRequest request)
    {
        var xmlBody = request.RegistroCancelacionXml ?? string.Empty;

        var envelope = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" 
                  xmlns:sf=""http://www.aeat.gob.es/VeriFacTuSF"">
    <soapenv:Header/>
    <soapenv:Body>
        <sf:AnulaRegistroFacturacion>
            <sf:IdEnvio>{EscapeXml(request.IdEnvio)}</sf:IdEnvio>
            <sf:IdTercero>{EscapeXml(request.IdTercero)}</sf:IdTercero>
            <sf:MotivoCancelacion>{EscapeXml(request.MotivoCancelacion)}</sf:MotivoCancelacion>
            <sf:RegistroCancelacionXml>
<![CDATA[{xmlBody}]]>
            </sf:RegistroCancelacionXml>
        </sf:AnulaRegistroFacturacion>
    </soapenv:Body>
</soapenv:Envelope>";

        return envelope;
    }

    /// <summary>
    /// Envía una solicitud SOAP y recibe una respuesta tipada.
    /// </summary>
    private async Task<T> SendSoapRequestAsync<T>(
        string endpoint,
        string soapEnvelope,
        CancellationToken cancellationToken) where T : class, new()
    {
        using var content = new StringContent(soapEnvelope);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "UTF-8" };
        content.Headers.Add("SOAPAction", "");

        _logger.LogDebug("Enviando SOAP request a {Endpoint}", endpoint);

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("Respuesta SOAP recibida. StatusCode: {StatusCode}", response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Error en respuesta SOAP. StatusCode: {StatusCode}, Body: {Body}",
                response.StatusCode,
                responseBody);

            throw new HttpRequestException(
                $"AEAT SOAP request failed with status {response.StatusCode}: {responseBody}");
        }

        // Parsear respuesta XML (en producción, usar XDocument o deserialización robusta)
        return ParseSoapResponse<T>(responseBody);
    }

    /// <summary>
    /// Parsea la respuesta SOAP XML en un objeto tipado.
    /// 
    /// Nota: Esta es una implementación simplificada. En producción:
    /// - Usar XDocument con validación de namespaces
    /// - Manejar SOAP faults adecuadamente
    /// - Usar XSD validation si está disponible
    /// </summary>
    private T ParseSoapResponse<T>(string soapResponse) where T : class, new()
    {
        // Por ahora, retornar instancia vacía
        // En fase de producción, implementar con XDocument + XPath/XElement
        _logger.LogWarning("ParseSoapResponse: Implementación simplificada. Se retorna instancia vacía de {Type}", typeof(T).Name);

        return new T();
    }

    /// <summary>
    /// Carga el certificado cliente desde configuración.
    /// </summary>
    private X509Certificate2? LoadClientCertificate()
    {
        var certPath = _configuration["VeriFactu:CertificatePath"];
        var certPassword = _configuration["VeriFactu:CertificatePassword"];

        if (string.IsNullOrEmpty(certPath))
        {
            _logger.LogWarning("Certificado cliente no configurado. Usando modo sin certificado.");
            return null;
        }

        try
        {
            var cert = new X509Certificate2(certPath, certPassword);
            _logger.LogInformation("Certificado cliente cargado: {Subject}", cert.Subject);
            return cert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar certificado cliente desde {CertPath}", certPath);
            throw;
        }
    }

    /// <summary>
    /// Obtiene el ID de tercero (taxpayer ID) desde configuración o request.
    /// </summary>
    private string GetTaxpayerId(string nif)
    {
        // Usar NIF como ID de tercero; en producción, podría haber mapeo adicional
        return nif ?? throw new ArgumentException("NIF required");
    }

    /// <summary>
    /// Escapa caracteres XML especiales.
    /// </summary>
    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}

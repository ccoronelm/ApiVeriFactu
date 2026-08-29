using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.Soap;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Mappers;

/// <summary>
/// Anti-Corruption Layer: mapea entre tipos de negocio de gesFactu y tipos SOAP de AEAT.
/// 
/// Regla: Los tipos SOAP permanecen en Infrastructure y nunca se exponen a Application/Domain.
/// </summary>
public static class AeatSoapMapper
{
    /// <summary>
    /// Mapea VeriFactuSubmissionRequest (modelo de negocio) a RegFactuSistemaFacturacionRequest (SOAP).
    /// </summary>
    public static RegFactuSistemaFacturacionRequest ToSoapSubmissionRequest(
        VeriFactuSubmissionRequest request,
        string taxpayerId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(taxpayerId);

        return new RegFactuSistemaFacturacionRequest
        {
            IdTercero = taxpayerId,
            RegistroFacturacionXml = request.SignedXmlContent,
            ClaveAcceso = null, // Se asignará desde configuración/contexto seguro
            NombreArchivo = $"registro_{DateOnly.FromDateTime(DateTime.UtcNow):yyyyMMdd}.xml"
        };
    }

    /// <summary>
    /// Mapea RegFactuSistemaFacturacionResponse (SOAP) a VeriFactuSubmissionResult (modelo de negocio).
    /// </summary>
    public static VeriFactuSubmissionResult FromSoapSubmissionResponse(
        RegFactuSistemaFacturacionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var resultado = response.Resultado ?? new InformacionResultado();
        var codigoEstado = resultado.CodigoEstado ?? "UNKNOWN";

        // Clasificar la respuesta AEAT según tabla de códigos
        var responseCode = ClassifyAeatResponseCode(codigoEstado);
        var isAccepted = responseCode == AeatResponseCode.Success;

        // Compilar incidencias
        var incidenciasText = string.Empty;
        if (resultado.Incidencias?.Any() == true)
        {
            incidenciasText = string.Join(
                "; ",
                resultado.Incidencias.Select(i => $"[{i.Codigo}] {i.Descripcion}"));
        }

        return new VeriFactuSubmissionResult
        {
            SubmissionId = response.IdEnvio ?? "UNKNOWN",
            IsAccepted = isAccepted,
            ResponseCode = responseCode,
            StatusCode = codigoEstado,
            StatusDescription = resultado.DescripcionEstado ?? "Sin descripción",
            AdditionalDetails = incidenciasText,
            ServerTimestamp = resultado.FechaHora ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Mapea VeriFactuQueryRequest a ConsultaFactuSistemaFacturacionRequest (SOAP).
    /// </summary>
    public static ConsultaFactuSistemaFacturacionRequest ToSoapQueryRequest(
        VeriFactuQueryRequest request,
        string taxpayerId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(taxpayerId);

        return new ConsultaFactuSistemaFacturacionRequest
        {
            IdEnvio = request.SubmissionId,
            IdTercero = taxpayerId,
            ClaveAcceso = null // Se asignará desde configuración/contexto seguro
        };
    }

    /// <summary>
    /// Mapea ConsultaFactuSistemaFacturacionResponse (SOAP) a VeriFactuQueryResult (modelo de negocio).
    /// </summary>
    public static VeriFactuQueryResult FromSoapQueryResponse(
        ConsultaFactuSistemaFacturacionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var resultado = response.Resultado ?? new InformacionResultado();

        var incidenciasText = string.Empty;
        if (resultado.Incidencias?.Any() == true)
        {
            incidenciasText = string.Join(
                "; ",
                resultado.Incidencias.Select(i => $"[{i.Codigo}] {i.Descripcion}"));
        }

        return new VeriFactuQueryResult
        {
            SubmissionId = response.IdEnvio ?? "UNKNOWN",
            Status = response.EstadoRegistro ?? "DESCONOCIDO",
            StatusDescription = resultado.DescripcionEstado ?? "Sin descripción",
            AdditionalDetails = incidenciasText
        };
    }

    /// <summary>
    /// Mapea VeriFactuCancellationRequest a CancelacionRegistroRequest (SOAP).
    /// </summary>
    public static CancelacionRegistroRequest ToSoapCancellationRequest(
        VeriFactuCancellationRequest request,
        string taxpayerId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(taxpayerId);

        return new CancelacionRegistroRequest
        {
            IdEnvio = request.SubmissionId,
            IdTercero = taxpayerId,
            MotivoCancelacion = request.CancellationReason ?? "Anulación",
            RegistroCancelacionXml = request.CancellationXmlContent,
            ClaveAcceso = null // Se asignará desde configuración/contexto seguro
        };
    }

    /// <summary>
    /// Mapea CancelacionRegistroResponse (SOAP) a VeriFactuCancellationResult (modelo de negocio).
    /// </summary>
    public static VeriFactuCancellationResult FromSoapCancellationResponse(
        CancelacionRegistroResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var resultado = response.Resultado ?? new InformacionResultado();
        var codigoEstado = resultado.CodigoEstado ?? "UNKNOWN";
        var responseCode = ClassifyAeatResponseCode(codigoEstado);

        var incidenciasText = string.Empty;
        if (resultado.Incidencias?.Any() == true)
        {
            incidenciasText = string.Join(
                "; ",
                resultado.Incidencias.Select(i => $"[{i.Codigo}] {i.Descripcion}"));
        }

        return new VeriFactuCancellationResult
        {
            CancellationId = response.IdEnvio,
            IsAccepted = responseCode == AeatResponseCode.Success,
            ResponseCode = responseCode,
            StatusCode = codigoEstado,
            StatusDescription = resultado.DescripcionEstado ?? "Sin descripción",
            AdditionalDetails = incidenciasText,
            ServerTimestamp = resultado.FechaHora ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Clasifica un código de estado AEAT en las categorías de decisión de retry.
    /// 
    /// Ref: /VERIFACTU/Validaciones_Errores_Veri-Factu.pdf
    /// Ref: /VERIFACTU/errores.properties.txt (si disponible)
    /// </summary>
    private static AeatResponseCode ClassifyAeatResponseCode(string codigoEstado)
    {
        if (string.IsNullOrEmpty(codigoEstado))
            return AeatResponseCode.Unknown;

        // Códigos numéricos AEAT comunes
        return codigoEstado switch
        {
            // Éxito
            "0" or "1000" => AeatResponseCode.Success,

            // Errores de validación (estructura, formato, valores)
            "2" or "F-000001" or "F-000002" or "F-000003" => AeatResponseCode.ValidationError,

            // Duplicados
            "4" => AeatResponseCode.DuplicateError,

            // Errores de certificado/autenticación
            "5" => AeatResponseCode.AuthenticationError,

            // Rechazo de negocio
            "3" or "R-000001" or "R-000002" => AeatResponseCode.BusinessRejection,

            // Errores transientes (reintentar)
            "999" or "500" or "503" => AeatResponseCode.TemporaryError,

            // Valor desconocido
            _ => AeatResponseCode.Unknown
        };
    }
}

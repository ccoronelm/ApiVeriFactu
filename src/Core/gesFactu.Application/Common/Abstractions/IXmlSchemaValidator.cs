namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para validación de esquemas XML (XSD) conforme a AEAT.
/// 
/// Garantiza que los registros cumplen exactamente con la estructura AEAT.
/// </summary>
public interface IXmlSchemaValidator
{
    /// <summary>
    /// Valida un documento XML contra un esquema XSD.
    /// </summary>
    /// <param name="xmlContent">Contenido XML a validar</param>
    /// <param name="schemaType">Tipo de esquema (RegistroFacturacion, RegistroAnulacion, etc.)</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Resultado de validación con errores si las hay</returns>
    Task<XmlValidationResult> ValidateAsync(
        string xmlContent,
        VeriFactuXmlSchemaType schemaType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tipos de esquemas soportados para validación.
/// 
/// Ref: /VERIFACTU - Archivos .xsd
/// </summary>
public enum VeriFactuXmlSchemaType
{
    /// <summary>
    /// Registro de facturación (SuministroInformacion.xsd)
    /// </summary>
    BillingRecord,

    /// <summary>
    /// Registro de anulación (SuministroLR.xsd)
    /// </summary>
    CancellationRecord,

    /// <summary>
    /// Consulta de registro (ConsultaLR.xsd)
    /// </summary>
    QueryRecord,

    /// <summary>
    /// Respuesta de suminist (RespuestaSuministro.xsd)
    /// </summary>
    SubmissionResponse
}

/// <summary>
/// Resultado de la validación XSD.
/// </summary>
public record XmlValidationResult
{
    /// <summary>
    /// Indica si el documento es válido según el esquema.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Lista de errores encontrados (vacía si IsValid=true).
    /// </summary>
    public required List<ValidationError> Errors { get; init; }

    /// <summary>
    /// Lista de advertencias (validaciones no críticas).
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Detalles de un error de validación XSD.
/// </summary>
public record ValidationError
{
    /// <summary>
    /// Descripción del error.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Número de línea en el XML (si está disponible).
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Número de columna en el XML.
    /// </summary>
    public int? LinePosition { get; init; }
}

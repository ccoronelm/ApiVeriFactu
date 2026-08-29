namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Códigos de error y respuesta de AEAT para VERI*FACTU.
/// 
/// Ref: /VERIFACTU - Códigos de respuesta y errores AEAT
/// </summary>
public enum AeatResponseCode
{
    /// <summary>
    /// Procesamiento exitoso.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Error de validación en el XML (formato, estructura, valores inválidos).
    /// </summary>
    ValidationError = 1,

    /// <summary>
    /// Error de duplicado: el registro ya fue enviado anteriormente.
    /// </summary>
    DuplicateError = 2,

    /// <summary>
    /// Error de autenticación o certificado inválido.
    /// </summary>
    AuthenticationError = 3,

    /// <summary>
    /// Error de autorización: el contribuyente no tiene permisos.
    /// </summary>
    AuthorizationError = 4,

    /// <summary>
    /// Error temporal: timeout, servidor no disponible, etc.
    /// </summary>
    TemporaryError = 5,

    /// <summary>
    /// Error permanente: no recuperable.
    /// </summary>
    PermanentError = 6,

    /// <summary>
    /// El registro fue rechazado por reglas de negocio AEAT.
    /// </summary>
    BusinessRejection = 7,

    /// <summary>
    /// Error desconocido o no clasificado.
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Clasificación de errores AEAT para decisiones de retry.
/// </summary>
public enum AeatErrorCategory
{
    /// <summary>
    /// Error transiente: se debe reintentar.
    /// </summary>
    Transient,

    /// <summary>
    /// Error permanente: no se debe reintentar.
    /// </summary>
    Permanent
}

/// <summary>
/// Extensiones para clasificar errores AEAT.
/// </summary>
public static class AeatResponseCodeExtensions
{
    /// <summary>
    /// Determina si un código de respuesta es transiente (reintentable).
    /// </summary>
    public static bool IsTransient(this AeatResponseCode code) =>
        GetCategory(code) == AeatErrorCategory.Transient;

    /// <summary>
    /// Determina si un código de respuesta es permanente (no reintentable).
    /// </summary>
    public static bool IsPermanent(this AeatResponseCode code) =>
        GetCategory(code) == AeatErrorCategory.Permanent;

    /// <summary>
    /// Obtiene la categoría de un código de respuesta.
    /// </summary>
    public static AeatErrorCategory GetCategory(AeatResponseCode code) =>
        code switch
        {
            AeatResponseCode.Success => AeatErrorCategory.Permanent,
            AeatResponseCode.TemporaryError => AeatErrorCategory.Transient,
            AeatResponseCode.ValidationError => AeatErrorCategory.Permanent,
            AeatResponseCode.DuplicateError => AeatErrorCategory.Permanent,
            AeatResponseCode.AuthenticationError => AeatErrorCategory.Permanent,
            AeatResponseCode.AuthorizationError => AeatErrorCategory.Permanent,
            AeatResponseCode.BusinessRejection => AeatErrorCategory.Permanent,
            AeatResponseCode.PermanentError => AeatErrorCategory.Permanent,
            _ => AeatErrorCategory.Permanent
        };

    /// <summary>
    /// Obtiene una descripción legible de un código de respuesta.
    /// </summary>
    public static string GetDescription(this AeatResponseCode code) =>
        code switch
        {
            AeatResponseCode.Success => "Procesamiento exitoso",
            AeatResponseCode.ValidationError => "Error de validación en el XML",
            AeatResponseCode.DuplicateError => "Registro duplicado",
            AeatResponseCode.AuthenticationError => "Error de autenticación",
            AeatResponseCode.AuthorizationError => "Error de autorización",
            AeatResponseCode.TemporaryError => "Error temporal (reintentar)",
            AeatResponseCode.PermanentError => "Error permanente",
            AeatResponseCode.BusinessRejection => "Rechazo de negocio",
            _ => "Error desconocido"
        };
}

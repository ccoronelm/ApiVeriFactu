namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Configuración para la integración AEAT VERI*FACTU.
/// 
/// Se carga desde appsettings.json bajo la sección "VeriFactu".
/// </summary>
public sealed class VeriFactuOptions
{
    public const string SectionName = "VeriFactu";

    /// <summary>
    /// Indica si usar el endpoint de staging (pruebas) o producción.
    /// </summary>
    public bool UseStaging { get; set; } = true;

    /// <summary>
    /// Endpoint de producción (si no se usa el por defecto).
    /// </summary>
    public string? ProductionEndpoint { get; set; }

    /// <summary>
    /// Endpoint de staging/pruebas (si no se usa el por defecto).
    /// </summary>
    public string? StagingEndpoint { get; set; }

    /// <summary>
    /// Ruta del archivo de certificado cliente (.pfx o .p12).
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Contraseña del certificado cliente (debe estar en sistema seguro de secretos).
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Timeout en segundos para llamadas SOAP a AEAT.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Modo de cliente: "Stub" (desarrollo), "SoapClient" (producción).
    /// </summary>
    public string ClientMode { get; set; } = "Stub";

    /// <summary>
    /// Número máximo de reintentos para llamadas transientes.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Milisegundos de espera inicial entre reintentos.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}

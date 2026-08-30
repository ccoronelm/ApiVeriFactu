using System.Security.Cryptography.X509Certificates;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;

/// <summary>
/// Carga el certificado X.509 del cliente para autenticación mTLS con AEAT.
///
/// Estrategia de carga (en orden de preferencia):
///   1. Windows Certificate Store: CurrentUser/My + Thumbprint (User Secrets en Development)
///   2. Archivo PFX/P12 + contraseña (alternativa no recomendada en Development)
///
/// NOTA: VERI*FACTU utiliza autenticación mTLS (certificado en capa HTTPS).
/// NO implementa firma XML/XAdES, que no es requerida para sistemas VERI*FACTU.
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
///
/// Validaciones aplicadas:
///   - El certificado debe existir
///   - El certificado debe tener clave privada
///   - El certificado debe estar dentro de su período de validez
/// </summary>
public sealed class CertificateLoader
{
    private readonly ILogger<CertificateLoader> _logger;

    public CertificateLoader(ILogger<CertificateLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Carga el certificado según la configuración proporcionada.
    /// Devuelve null si no hay configuración de certificado (modo sin mTLS, solo para tests).
    /// Lanza InvalidOperationException si la configuración es inválida o el certificado no es utilizable.
    /// </summary>
    public X509Certificate2? Load(CertificateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.Thumbprint))
            return LoadFromWindowsStore(options.Thumbprint);

        if (!string.IsNullOrWhiteSpace(options.PfxPath))
            return LoadFromPfx(options.PfxPath, options.PfxPassword);

        _logger.LogWarning(
            "No se ha configurado certificado de cliente. " +
            "Configure VeriFactu:Certificate:Thumbprint (Windows Store) o VeriFactu:Certificate:PfxPath.");
        return null;
    }

    /// <summary>
    /// Carga desde el almacén Windows CurrentUser/My buscando por Thumbprint.
    /// </summary>
    private X509Certificate2 LoadFromWindowsStore(string rawThumbprint)
    {
        // Normalizar: eliminar espacios, convertir a mayúsculas
        var thumbprint = rawThumbprint
            .Replace(" ", string.Empty)
            .Replace(":", string.Empty)
            .ToUpperInvariant();

        _logger.LogInformation("Buscando certificado por Thumbprint en CurrentUser/My: {Thumbprint}", thumbprint);

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false); // validOnly=false para poder dar error descriptivo

        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"No se encontró ningún certificado con Thumbprint '{thumbprint}' en CurrentUser/My. " +
                "Verifique que el certificado está instalado y que el Thumbprint es correcto.");

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Se encontraron {matches.Count} certificados con Thumbprint '{thumbprint}'. " +
                "El Thumbprint debe identificar un único certificado.");

        var cert = matches[0];
        ValidateCertificate(cert, thumbprint);

        // Devolvemos una copia con la marca de exportación ephemeral para seguridad
        _logger.LogInformation(
            "Certificado cargado desde Windows Store: Subject={Subject}, NotAfter={NotAfter}",
            cert.Subject,
            cert.NotAfter.ToString("yyyy-MM-dd"));

        return cert;
    }

    /// <summary>
    /// Carga desde archivo PFX/P12.
    /// </summary>
    private X509Certificate2 LoadFromPfx(string pfxPath, string? password)
    {
        if (!File.Exists(pfxPath))
            throw new InvalidOperationException(
                $"El archivo de certificado no existe: {pfxPath}");

        _logger.LogInformation("Cargando certificado desde PFX: {Path}", pfxPath);

        X509Certificate2 cert;
        try
        {
            cert = new X509Certificate2(pfxPath, password,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.MachineKeySet);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Error al cargar el certificado desde {pfxPath}. Compruebe la ruta y la contraseña.", ex);
        }

        ValidateCertificate(cert, pfxPath);
        return cert;
    }

    private void ValidateCertificate(X509Certificate2 cert, string identifier)
    {
        if (!cert.HasPrivateKey)
            throw new InvalidOperationException(
                $"El certificado '{identifier}' no tiene clave privada asociada. " +
                "La autenticación mTLS requiere la clave privada.");

        var now = DateTime.Now;
        if (now < cert.NotBefore)
            throw new InvalidOperationException(
                $"El certificado '{identifier}' aún no es válido. " +
                $"Válido desde: {cert.NotBefore:yyyy-MM-dd HH:mm:ss}. Ahora: {now:yyyy-MM-dd HH:mm:ss}.");

        if (now > cert.NotAfter)
            throw new InvalidOperationException(
                $"El certificado '{identifier}' ha caducado. " +
                $"Caducó el: {cert.NotAfter:yyyy-MM-dd HH:mm:ss}. Ahora: {now:yyyy-MM-dd HH:mm:ss}.");

        // Aviso de próxima caducidad (30 días)
        if (now.AddDays(30) > cert.NotAfter)
            _logger.LogWarning(
                "El certificado caduca en menos de 30 días: {NotAfter}. Renuévelo antes de esa fecha.",
                cert.NotAfter.ToString("yyyy-MM-dd"));
    }
}

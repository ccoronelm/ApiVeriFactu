using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Signature;

/// <summary>
/// Implementación stub de firma XML para desarrollo y testing.
/// 
/// Simula el proceso de firma sin usar criptografía real.
/// En producción, será reemplazada por XmlSignatureServiceReal con librerías de firma.
/// 
/// Ref: /VERIFACTU/EspecTecGenerFirmaElectRfact.pdf
/// </summary>
public sealed class XmlSignatureServiceStub : IXmlSignatureService
{
    private readonly ILogger<XmlSignatureServiceStub> _logger;
    private readonly X509Certificate2? _certificate;

    public XmlSignatureServiceStub(
        ILogger<XmlSignatureServiceStub> logger,
        X509Certificate2? certificate = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _certificate = certificate;
    }

    /// <summary>
    /// Firma simulada: añade un elemento &lt;Firma&gt; stub al XML.
    /// </summary>
    public async Task<string> SignXmlAsync(string xmlContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlContent);

        _logger.LogInformation("STUB: Firmando documento XML");

        await Task.Delay(100, cancellationToken); // Simular latencia

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root;

            if (root == null)
                throw new InvalidOperationException("XML vacío o inválido");

            // Crear elemento de firma stub
            var firmaElement = new XElement("Firma",
                new XElement("MetodoFirma", "XADES-EPES (STUB)"),
                new XElement("Certificado",
                    _certificate?.Subject ?? "CN=STUB, O=gesFactu"),
                new XElement("Huella",
                    GenerateStubSignature(xmlContent)),
                new XElement("Timestamp",
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement("Nota", "Simulada para desarrollo"));

            // Agregar firma al documento
            root.Add(firmaElement);

            return doc.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al firmar XML");
            throw;
        }
    }

    /// <summary>
    /// Verifica existencia de elemento firma (sin validación criptográfica real).
    /// </summary>
    public async Task<bool> VerifyXmlSignatureAsync(string signedXmlContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(signedXmlContent);

        await Task.Delay(50, cancellationToken);

        try
        {
            var doc = XDocument.Parse(signedXmlContent);
            var firmaElement = doc.Root?.Element("Firma");

            return firmaElement != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar firma XML");
            return false;
        }
    }

    /// <summary>
    /// Retorna información del certificado stub.
    /// </summary>
    public async Task<CertificateInfo> GetCertificateInfoAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        if (_certificate == null)
        {
            return new CertificateInfo
            {
                Subject = "CN=STUB, O=gesFactu (No certificate loaded)",
                Issuer = "STUB Issuer",
                NotBefore = DateTime.UtcNow.AddYears(-1),
                NotAfter = DateTime.UtcNow.AddYears(1),
                SerialNumber = "STUB-0000000000",
                Thumbprint = "0000000000000000000000000000000000000000"
            };
        }

        return new CertificateInfo
        {
            Subject = _certificate.Subject,
            Issuer = _certificate.Issuer,
            NotBefore = _certificate.NotBefore,
            NotAfter = _certificate.NotAfter,
            SerialNumber = _certificate.SerialNumber,
            Thumbprint = _certificate.Thumbprint
        };
    }

    /// <summary>
    /// Genera firma stub: hash simple del contenido.
    /// </summary>
    private static string GenerateStubSignature(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}

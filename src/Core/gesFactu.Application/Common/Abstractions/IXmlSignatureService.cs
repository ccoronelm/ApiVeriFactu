namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para la firma digital de registros de facturación.
/// 
/// La firma XAdES-4j es requerida por AEAT para:
/// - Autenticar el origen del documento
/// - Garantizar integridad
/// - Proporcionar no repudio
/// 
/// Ref: /VERIFACTU/EspecTecGenerFirmaElectRfact.pdf
/// Ref: /VERIFACTU - Ejemplos de registros firmados
/// </summary>
public interface IXmlSignatureService
{
    /// <summary>
    /// Firma un documento XML conforme a XAdES-EPES Level.
    /// 
    /// El resultado es un XML firmado que incluye:
    /// - Firma digital (RSA SHA256)
    /// - Certificado del firmante
    /// - Timestamp (si está disponible servidor TSA)
    /// - Elementos de firma según XAdES-4j
    /// </summary>
    /// <param name="xmlContent">Contenido XML sin firmar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Contenido XML firmado (XAdES-EPES)</returns>
    Task<string> SignXmlAsync(string xmlContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida la firma de un documento XML.
    /// 
    /// Verifica:
    /// - Integridad de la firma
    /// - Validez del certificado
    /// - Conformidad con XAdES
    /// </summary>
    /// <param name="signedXmlContent">Contenido XML firmado</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>True si la firma es válida</returns>
    Task<bool> VerifyXmlSignatureAsync(string signedXmlContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene información del certificado actualmente cargado.
    /// </summary>
    Task<CertificateInfo> GetCertificateInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Información del certificado usado para firma.
/// </summary>
public record CertificateInfo
{
    /// <summary>
    /// Nombre distinguido del certificado (CN=..., O=..., etc).
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Nombre del emisor del certificado.
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Fecha de inicio de validez.
    /// </summary>
    public required DateTime NotBefore { get; init; }

    /// <summary>
    /// Fecha de fin de validez.
    /// </summary>
    public required DateTime NotAfter { get; init; }

    /// <summary>
    /// Número de serie del certificado.
    /// </summary>
    public required string SerialNumber { get; init; }

    /// <summary>
    /// Huella digital SHA256 del certificado.
    /// </summary>
    public required string Thumbprint { get; init; }

    /// <summary>
    /// Indica si el certificado es válido en este momento.
    /// </summary>
    public bool IsValid => DateTime.UtcNow >= NotBefore && DateTime.UtcNow <= NotAfter;
}

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para generación de códigos QR conforme a VERI*FACTU.
/// 
/// Ref: /VERIFACTU/DetalleEspecificacTecnCodigoQRfactura.pdf
/// </summary>
public interface IQRCodeGenerator
{
    /// <summary>
    /// Genera un código QR conforme a VERI*FACTU.
    /// 
    /// El QR debe contener:
    /// - URL de acceso al registro en AEAT
    /// - SubmissionId
    /// - Hash del registro
    /// - Timestamp
    /// 
    /// Según especificación, el formato es:
    /// https://www.aeat.es/verifactu?ID={SubmissionId}&HASH={Hash}&FECHA={Fecha}
    /// </summary>
    /// <param name="submissionId">Identificador asignado por AEAT.</param>
    /// <param name="recordHash">Hash/huella del registro.</param>
    /// <param name="issueDate">Fecha de emisión de la factura.</param>
    /// <returns>Array de bytes con la imagen PNG del QR.</returns>
    Task<byte[]> GenerateAsync(
        string submissionId,
        string recordHash,
        DateOnly issueDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera solo el contenido del QR (la URL) sin codificarlo a imagen.
    /// Útil para pruebas y para cuando se necesita solo el texto.
    /// </summary>
    string GenerateQRContent(
        string submissionId,
        string recordHash,
        DateOnly issueDate);
}

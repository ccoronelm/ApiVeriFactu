using Xunit;
using gesFactu.Infrastructure.Integrations.QRCode;

namespace gesFactu.Infrastructure.Tests.QRCode;

/// <summary>
/// Tests para el generador de códigos QR VERI*FACTU.
/// </summary>
public class QRCodeGeneratorTests
{
    [Fact]
    public void GenerateQRContent_ReturnsValidURL()
    {
        // Arrange
        var generator = new QRCodeGenerator();
        var submissionId = "ABC123";
        var hash = "ABCDEF0123456789";
        var issueDate = new DateOnly(2026, 8, 29);

        // Act
        var qrContent = generator.GenerateQRContent(submissionId, hash, issueDate);

        // Assert
        Assert.NotNull(qrContent);
        Assert.StartsWith("https://www.aeat.es/verifactu", qrContent);
        Assert.Contains("ID=ABC123", qrContent);
        Assert.Contains("HASH=ABCDEF0123456789", qrContent);
        Assert.Contains("FECHA=20260829", qrContent);
    }

    [Fact]
    public void GenerateQRContent_EscapesSpecialCharacters()
    {
        // Arrange
        var generator = new QRCodeGenerator();
        var submissionId = "SUB-2026/08/29";
        var hash = "hash+with/special&chars";

        // Act
        var qrContent = generator.GenerateQRContent(submissionId, hash, new DateOnly(2026, 8, 29));

        // Assert
        Assert.DoesNotContain(" ", qrContent); // No espacios sin codificar
        Assert.Contains("ID=SUB-2026%2F08%2F29", qrContent); // Diagonal codificada
        Assert.Contains("HASH=hash%2Bwith%2Fspecial%26chars", qrContent); // Caracteres especiales codificados
    }

    [Fact]
    public void GenerateQRContent_ThrowsOnNullSubmissionId()
    {
        // Arrange
        var generator = new QRCodeGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            generator.GenerateQRContent(null!, "hash", new DateOnly(2026, 8, 29)));
    }

    [Fact]
    public void GenerateQRContent_ThrowsOnNullHash()
    {
        // Arrange
        var generator = new QRCodeGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            generator.GenerateQRContent("SUB123", null!, new DateOnly(2026, 8, 29)));
    }

    [Fact]
    public async Task GenerateAsync_ReturnsUTF8Bytes()
    {
        // Arrange
        var generator = new QRCodeGenerator();
        var submissionId = "SUB123";
        var hash = "HASH123";
        var issueDate = new DateOnly(2026, 8, 29);

        // Act
        var bytes = await generator.GenerateAsync(submissionId, hash, issueDate);

        // Assert
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        // Decodificar y verificar que es el contenido esperado
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Equal(generator.GenerateQRContent(submissionId, hash, issueDate), content);
    }
}

using Xunit;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.VeriFactu;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

/// <summary>
/// Tests para validar que el cálculo de hash es determinista y correcto.
/// 
/// El hash es crítico para VERI*FACTU, por lo que debe ser exhaustivamente testeado
/// contra valores conocidos y ejemplos oficiales.
/// </summary>
public sealed class Sha256HashCalculatorTests
{
    private readonly Sha256HashCalculator _calculator;

    public Sha256HashCalculatorTests()
    {
        _calculator = new Sha256HashCalculator();
    }

    [Fact]
    public void CalculateSha256_WithString_ReturnsValidHash()
    {
        // Arrange
        var data = "hello world";

        // Act
        var hash = _calculator.CalculateSha256(data);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // SHA256 = 256 bits = 64 hex chars
        Assert.True(hash.All(c => char.IsAsciiHexDigitUpper(c)), "Hash debe estar en hexadecimal mayúsculas");
    }

    [Fact]
    public void CalculateSha256_IsDeterministic()
    {
        // Arrange
        var data = "test data";

        // Act
        var hash1 = _calculator.CalculateSha256(data);
        var hash2 = _calculator.CalculateSha256(data);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalculateSha256_DifferentDataProducesDifferentHash()
    {
        // Arrange & Act
        var hash1 = _calculator.CalculateSha256("data1");
        var hash2 = _calculator.CalculateSha256("data2");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateSha256_WithKnownValue()
    {
        // Este test valida contra un valor SHA256 conocido
        // "hello world" en SHA256 = b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9

        // Arrange
        var data = "hello world";
        var expectedHash = "B94D27B9934D3E08A52E52D7DA7DABFAC484EFE37A5380EE9088F7ACE2EFCDE9";

        // Act
        var hash = _calculator.CalculateSha256(data);

        // Assert
        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public void CalculateChainHash_WithMinimalInput_ReturnsValidHash()
    {
        // Arrange
        var input = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            InvoiceType = "",
            TotalAmount = 100.00m,
            TotalTaxAmount = 21.00m,
            Description = "",
            RegisterTimestamp = "2025-02-03T14:30:00Z",
            SoftwareId = ""
        };

        // Act
        var hash = _calculator.CalculateChainHash(input);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(c => char.IsAsciiHexDigitUpper(c)));
    }

    [Fact]
    public void CalculateChainHash_IsDeterministic()
    {
        // Arrange
        var input = new BillingRecordHashInput
        {
            PreviousHash = "ABC123",
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            InvoiceType = "F1",
            TotalAmount = 250.50m,
            TotalTaxAmount = 52.61m,
            Description = "Servicios de consultoría",
            RegisterTimestamp = "2025-02-03T14:30:00+01:00",
            SoftwareId = "gesFactu-1.0"
        };

        // Act
        var hash1 = _calculator.CalculateChainHash(input);
        var hash2 = _calculator.CalculateChainHash(input);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalculateChainHash_DifferentPreviousHashProducesDifferentHash()
    {
        // Arrange
        var baseInput = new BillingRecordHashInput
        {
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100.00m,
            TotalTaxAmount = 21.00m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        var input1 = baseInput with { PreviousHash = "" };
        var input2 = baseInput with { PreviousHash = "ABC123DEF456" };

        // Act
        var hash1 = _calculator.CalculateChainHash(input1);
        var hash2 = _calculator.CalculateChainHash(input2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateChainHash_WithDecimalVariations_ProducesCorrectFormats()
    {
        // Arrange
        var input1 = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        var input2 = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100.00m,
            TotalTaxAmount = 21.00m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        // Act
        var hash1 = _calculator.CalculateChainHash(input1);
        var hash2 = _calculator.CalculateChainHash(input2);

        // Assert
        // Ambos deben ser iguales porque 100 y 100.00 se formatean igual (100.00)
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalculateChainHash_ThrowsWhenNifIsEmpty()
    {
        // Arrange
        var input = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.CalculateChainHash(input));
    }

    [Fact]
    public void CalculateChainHash_NormalizesNifToUppercase()
    {
        // Arrange
        var inputLower = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "12345678a",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        var inputUpper = new BillingRecordHashInput
        {
            PreviousHash = "",
            IssuerNif = "12345678A",
            InvoiceSeries = "A",
            InvoiceNumber = "001",
            IssueDate = "03-02-2025",
            TotalAmount = 100m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00Z"
        };

        // Act
        var hashLower = _calculator.CalculateChainHash(inputLower);
        var hashUpper = _calculator.CalculateChainHash(inputUpper);

        // Assert
        Assert.Equal(hashLower, hashUpper);
    }
}

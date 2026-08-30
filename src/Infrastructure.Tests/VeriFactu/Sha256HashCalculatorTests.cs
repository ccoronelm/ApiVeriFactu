using Xunit;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.VeriFactu;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

/// <summary>
/// Tests de huella VERI*FACTU.
/// Los dos primeros casos reproducen exactamente los vectores oficiales del documento AEAT:
/// /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf, apartados 6.1 y 6.2.
/// </summary>
public sealed class Sha256HashCalculatorTests
{
    private readonly Sha256HashCalculator _calculator = new();

    [Fact]
    public void CalculateChainHash_OfficialCase1_FirstRegistro_MatchesAeatVector()
    {
        var input = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "12345678/",
            InvoiceNumber = "G33",
            IssueDate = "01-01-2024",
            InvoiceType = "F1",
            TotalTaxAmount = 12.35m,
            TotalAmount = 123.45m,
            RegisterTimestamp = "2024-01-01T19:20:30+01:00"
        };

        var hash = _calculator.CalculateChainHash(input);

        Assert.Equal(
            "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            hash);
    }

    [Fact]
    public void CalculateChainHash_OfficialCase2_WithPreviousRegistro_MatchesAeatVector()
    {
        var input = new BillingRecordHashInput
        {
            PreviousHash = "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            IssuerNif = "89890001K",
            InvoiceSeries = "12345679/",
            InvoiceNumber = "G34",
            IssueDate = "01-01-2024",
            InvoiceType = "F1",
            TotalTaxAmount = 12.35m,
            TotalAmount = 123.45m,
            RegisterTimestamp = "2024-01-01T19:20:35+01:00"
        };

        var hash = _calculator.CalculateChainHash(input);

        Assert.Equal(
            "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97",
            hash);
    }

    [Fact]
    public void CalculateChainHash_WholeAmounts_UsesTwoDecimalsLikeXml()
    {
        var input = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "VF/",
            InvoiceNumber = "000001",
            IssueDate = "30-08-2026",
            InvoiceType = "F1",
            TotalTaxAmount = 21m,
            TotalAmount = 121m,
            RegisterTimestamp = "2026-08-30T12:32:26+02:00"
        };

        var hash = _calculator.CalculateChainHash(input);

        Assert.Equal(
            "C8318DAF719A9A7E6508D0181111E88890DECA414E6175CA9B422006CB1783D7",
            hash);
    }

    [Fact]
    public void CalculateCancellationHash_OfficialCase3_MatchesAeatVector()
    {
        var input = new CancellationRecordHashInput
        {
            PreviousHash = "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97",
            IssuerNif = "89890001K",
            InvoiceSeries = "12345679/",
            InvoiceNumber = "G34",
            IssueDate = "01-01-2024",
            RegisterTimestamp = "2024-01-01T19:20:40+01:00"
        };

        var hash = _calculator.CalculateCancellationHash(input);

        Assert.Equal(
            "177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68",
            hash);
    }

    [Fact]
    public void CalculateSha256_WithKnownValue_ReturnsUppercaseHex()
    {
        var hash = _calculator.CalculateSha256("hello world");

        Assert.Equal(
            "B94D27B9934D3E08A52E52D7DA7DABFAC484EFE37A5380EE9088F7ACE2EFCDE9",
            hash);
    }

    [Fact]
    public void CalculateChainHash_IsDeterministic()
    {
        var input = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "A",
            InvoiceNumber = "0001",
            IssueDate = "03-02-2025",
            InvoiceType = "F1",
            TotalAmount = 121m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00+01:00"
        };

        Assert.Equal(
            _calculator.CalculateChainHash(input),
            _calculator.CalculateChainHash(input));
    }

    [Fact]
    public void CalculateChainHash_TreatsTrailingDecimalZerosAsEquivalent()
    {
        var input1 = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "A",
            InvoiceNumber = "0001",
            IssueDate = "03-02-2025",
            InvoiceType = "F1",
            TotalAmount = 123.1m,
            TotalTaxAmount = 12.3m,
            RegisterTimestamp = "2025-02-03T14:30:00+01:00"
        };

        var input2 = input1 with
        {
            TotalAmount = 123.10m,
            TotalTaxAmount = 12.30m
        };

        Assert.Equal(
            _calculator.CalculateChainHash(input1),
            _calculator.CalculateChainHash(input2));
    }

    [Fact]
    public void CalculateChainHash_TrimsXmlFieldValues()
    {
        var clean = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "12345678/",
            InvoiceNumber = "G33",
            IssueDate = "01-01-2024",
            InvoiceType = "F1",
            TotalTaxAmount = 12.35m,
            TotalAmount = 123.45m,
            RegisterTimestamp = "2024-01-01T19:20:30+01:00"
        };

        var padded = clean with
        {
            IssuerNif = " 89890001K ",
            InvoiceSeries = " 12345678/ ",
            InvoiceNumber = " G33 ",
            IssueDate = " 01-01-2024 ",
            InvoiceType = " F1 ",
            RegisterTimestamp = " 2024-01-01T19:20:30+01:00 "
        };

        Assert.Equal(
            _calculator.CalculateChainHash(clean),
            _calculator.CalculateChainHash(padded));
    }

    [Fact]
    public void CalculateChainHash_ThrowsWhenInvoiceTypeIsMissing()
    {
        var input = new BillingRecordHashInput
        {
            PreviousHash = string.Empty,
            IssuerNif = "89890001K",
            InvoiceSeries = "A",
            InvoiceNumber = "0001",
            IssueDate = "03-02-2025",
            InvoiceType = string.Empty,
            TotalAmount = 121m,
            TotalTaxAmount = 21m,
            RegisterTimestamp = "2025-02-03T14:30:00+01:00"
        };

        Assert.Throws<ArgumentException>(() => _calculator.CalculateChainHash(input));
    }
}

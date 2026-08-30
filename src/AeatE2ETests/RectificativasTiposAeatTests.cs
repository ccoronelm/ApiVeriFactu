using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class RectificativasTiposAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task R1I_DebeSerCorrectaEnAeatTest()
        => ExecuteRectificationAsync("R1");

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task R2I_DebeSerCorrectaEnAeatTest()
        => ExecuteRectificationAsync("R2");

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task R3I_DebeSerCorrectaEnAeatTest()
        => ExecuteRectificationAsync("R3");

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task R4I_DebeSerCorrectaEnAeatTest()
        => ExecuteRectificationAsync("R4");

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task R5I_SobreF2_DebeSerCorrectaEnAeatTest()
        => ExecuteRectificationAsync("R5");

    private static async Task ExecuteRectificationAsync(string type)
    {
        using var ctx = AeatE2ETestContext.Create($"E2E{type}");

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        var sourceType = type == "R5" ? "F2" : "F1";
        var sourceSeries = $"E2E{type}-O/";
        var sourceNumber = runId;

        var sourceTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var sourceHash = ctx.HashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = ctx.TaxpayerNif,
                InvoiceSeries = sourceSeries,
                InvoiceNumber = sourceNumber,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = sourceType,
                TotalAmount = sourceType == "F2" ? 12.10m : 121m,
                TotalTaxAmount = sourceType == "F2" ? 2.10m : 21m,
                RegisterTimestamp = sourceTimestamp
            });

        var source = BuildSource(
            ctx,
            sourceType,
            sourceSeries,
            sourceNumber,
            issueDate,
            sourceHash,
            sourceTimestamp);

        var sourceResult = await ctx.SubmitAltaAsync(source);

        Assert.True(sourceResult.IsAccepted, sourceResult.StatusDescription);
        Assert.Equal("Correcto", sourceResult.RecordStatus);

        var rectificationSeries = $"E2E{type}-R/";
        var rectificationNumber = runId;
        var rectificationTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        const decimal rectBase = -10m;
        const decimal rectTax = -2.10m;
        const decimal rectTotal = -12.10m;

        var rectificationHash = ctx.HashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = sourceHash,
                IssuerNif = ctx.TaxpayerNif,
                InvoiceSeries = rectificationSeries,
                InvoiceNumber = rectificationNumber,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = type,
                TotalAmount = rectTotal,
                TotalTaxAmount = rectTax,
                RegisterTimestamp = rectificationTimestamp
            });

        var rectification = new RegistroAltaData
        {
            IssuerNif = ctx.TaxpayerNif,
            IssuerName = ctx.TaxpayerName,
            InvoiceSeries = rectificationSeries,
            InvoiceNumber = rectificationNumber,
            IssueDate = issueDate,
            RecipientNif = type == "R5"
                ? string.Empty
                : ctx.Settings.RecipientNif,
            RecipientName = type == "R5"
                ? string.Empty
                : ctx.Settings.RecipientName,
            IsSubsanacion = false,
            TipoFactura = type,
            TipoRectificativa = "I",
            FacturasRectificadas =
            [
                new FacturaRectificadaData
                {
                    IssuerNif = ctx.TaxpayerNif,
                    InvoiceSeries = sourceSeries,
                    InvoiceNumber = sourceNumber,
                    IssueDate = issueDate
                }
            ],
            ImporteRectificacion = null,
            Description = $"Rectificativa {type} por diferencias E2E",
            CuotaTotal = rectTax,
            ImporteTotal = rectTotal,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = rectBase,
                    CuotaRepercutida = rectTax
                }
            ],
            ComputedHash = rectificationHash,
            PreviousRecordHash = sourceHash,
            PreviousIssueDate = issueDate,
            PreviousIssuerNif = ctx.TaxpayerNif,
            PreviousInvoiceSeries = sourceSeries,
            PreviousInvoiceNumber = sourceNumber,
            FechaHoraHusoGenRegistro = rectificationTimestamp
        };

        var result = await ctx.SubmitAltaAsync(rectification);

        Assert.True(result.IsAccepted, result.StatusDescription);
        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.SubmissionId));
    }

    private static RegistroAltaData BuildSource(
        AeatE2ETestContext ctx,
        string invoiceType,
        string series,
        string number,
        DateOnly issueDate,
        string hash,
        string timestamp)
    {
        var simplified = invoiceType == "F2";
        var baseAmount = simplified ? 10m : 100m;
        var taxAmount = simplified ? 2.10m : 21m;

        return new RegistroAltaData
        {
            IssuerNif = ctx.TaxpayerNif,
            IssuerName = ctx.TaxpayerName,
            InvoiceSeries = series,
            InvoiceNumber = number,
            IssueDate = issueDate,
            RecipientNif = simplified
                ? string.Empty
                : ctx.Settings.RecipientNif,
            RecipientName = simplified
                ? string.Empty
                : ctx.Settings.RecipientName,
            IsSubsanacion = false,
            TipoFactura = invoiceType,
            Description = $"Factura origen {invoiceType} para rectificación",
            CuotaTotal = taxAmount,
            ImporteTotal = baseAmount + taxAmount,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = baseAmount,
                    CuotaRepercutida = taxAmount
                }
            ],
            ComputedHash = hash,
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = timestamp
        };
    }
}

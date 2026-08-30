using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class DesglosesEspecialesAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task OperacionExentaE1_DebeSerCorrectaEnAeatTest()
        => SubmitAsync(
            "E2EEX",
            "Exenta E1 E2E",
            100m,
            0m,
            new DetalleDesgloseData
            {
                Impuesto = "01",
                ClaveRegimen = "01",
                CalificacionOperacion = string.Empty,
                OperacionExenta = "E1",
                BaseImponible = 100m
            });

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task OperacionNoSujetaN1_DebeSerCorrectaEnAeatTest()
        => SubmitAsync(
            "E2ENS",
            "No sujeta N1 E2E",
            100m,
            0m,
            new DetalleDesgloseData
            {
                Impuesto = "01",
                ClaveRegimen = "01",
                CalificacionOperacion = "N1",
                BaseImponible = 100m
            });

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public Task RecargoEquivalencia_DebeSerCorrectoEnAeatTest()
        => SubmitAsync(
            "E2ERE",
            "Recargo equivalencia E2E",
            126.20m,
            26.20m,
            new DetalleDesgloseData
            {
                Impuesto = "01",
                ClaveRegimen = "01",
                CalificacionOperacion = "S1",
                TipoImpositivo = 21m,
                BaseImponible = 100m,
                CuotaRepercutida = 21m,
                TipoRecargoEquivalencia = 5.2m,
                CuotaRecargoEquivalencia = 5.2m
            });

    private static async Task SubmitAsync(
        string prefix,
        string description,
        decimal total,
        decimal totalTax,
        DetalleDesgloseData detail)
    {
        using var ctx = AeatE2ETestContext.Create(prefix);

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);
        var series = $"{prefix}/";
        var timestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var hash = ctx.HashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = ctx.TaxpayerNif,
                InvoiceSeries = series,
                InvoiceNumber = runId,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = total,
                TotalTaxAmount = totalTax,
                RegisterTimestamp = timestamp
            });

        var data = new RegistroAltaData
        {
            IssuerNif = ctx.TaxpayerNif,
            IssuerName = ctx.TaxpayerName,
            InvoiceSeries = series,
            InvoiceNumber = runId,
            IssueDate = issueDate,
            RecipientNif = ctx.Settings.RecipientNif,
            RecipientName = ctx.Settings.RecipientName,
            IsSubsanacion = false,
            TipoFactura = "F1",
            Description = description,
            CuotaTotal = totalTax,
            ImporteTotal = total,
            Detalles = [detail],
            ComputedHash = hash,
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = timestamp
        };

        var result = await ctx.SubmitAltaAsync(data);

        Assert.True(result.IsAccepted, result.StatusDescription);
        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.SubmissionId));
    }
}

using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class DuplicadoAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task MismoRegistroDosVeces_DebeDevolverDuplicado3000()
    {
        using var ctx = AeatE2ETestContext.Create("E2EDUP");

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);
        const string series = "E2EDUP/";
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
                TotalAmount = 121m,
                TotalTaxAmount = 21m,
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
            Description = "Prueba duplicado AEAT E2E",
            CuotaTotal = 21m,
            ImporteTotal = 121m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = 100m,
                    CuotaRepercutida = 21m
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

        var first = await ctx.SubmitAltaAsync(data);
        Assert.True(first.IsAccepted, first.StatusDescription);
        Assert.Equal("Correcto", first.RecordStatus);

        var duplicate = await ctx.SubmitAltaAsync(data);

        Assert.True(duplicate.IsDuplicate);
        Assert.Equal("3000", duplicate.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(
            duplicate.DuplicateRecordStatus));
        Assert.False(string.IsNullOrWhiteSpace(
            duplicate.DuplicateRequestId));
    }
}

using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class SubsanacionAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task AltaF1_YSubsanacion_DebenSerCorrectasEnAeatTest()
    {
        using var ctx = AeatE2ETestContext.Create("E2ESUB");

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);
        const string series = "E2ESUB/";
        var number = runId;

        var firstTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var firstHash = ctx.HashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = ctx.TaxpayerNif,
                InvoiceSeries = series,
                InvoiceNumber = number,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = 121m,
                TotalTaxAmount = 21m,
                RegisterTimestamp = firstTimestamp
            });

        var original = CreateF1(
            ctx,
            issueDate,
            series,
            number,
            firstHash,
            firstTimestamp,
            isSubsanacion: false,
            description: "Factura origen E2E subsanación");

        var firstResult = await ctx.SubmitAltaAsync(original);

        Assert.True(firstResult.IsAccepted, firstResult.StatusDescription);
        Assert.Equal("Correcto", firstResult.RecordStatus);

        var correctionTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var correctionHash = ctx.HashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = firstHash,
                IssuerNif = ctx.TaxpayerNif,
                InvoiceSeries = series,
                InvoiceNumber = number,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = 121m,
                TotalTaxAmount = 21m,
                RegisterTimestamp = correctionTimestamp
            });

        var subsanation = CreateF1(
            ctx,
            issueDate,
            series,
            number,
            correctionHash,
            correctionTimestamp,
            isSubsanacion: true,
            description: "Factura subsanada automáticamente E2E");

        subsanation = new RegistroAltaData
        {
            IssuerNif = subsanation.IssuerNif,
            IssuerName = subsanation.IssuerName,
            InvoiceSeries = subsanation.InvoiceSeries,
            InvoiceNumber = subsanation.InvoiceNumber,
            IssueDate = subsanation.IssueDate,
            RecipientNif = subsanation.RecipientNif,
            RecipientName = subsanation.RecipientName,
            IsSubsanacion = true,
            TipoFactura = subsanation.TipoFactura,
            Description = subsanation.Description,
            CuotaTotal = subsanation.CuotaTotal,
            ImporteTotal = subsanation.ImporteTotal,
            Detalles = subsanation.Detalles,
            ComputedHash = correctionHash,
            PreviousRecordHash = firstHash,
            PreviousIssueDate = issueDate,
            PreviousIssuerNif = ctx.TaxpayerNif,
            PreviousInvoiceSeries = series,
            PreviousInvoiceNumber = number,
            FechaHoraHusoGenRegistro = correctionTimestamp
        };

        var correctionResult = await ctx.SubmitAltaAsync(subsanation);

        Assert.True(
            correctionResult.IsAccepted,
            correctionResult.StatusDescription);
        Assert.Equal("Correcto", correctionResult.StatusCode);
        Assert.Equal("Correcto", correctionResult.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(
            correctionResult.SubmissionId));
    }

    private static RegistroAltaData CreateF1(
        AeatE2ETestContext ctx,
        DateOnly issueDate,
        string series,
        string number,
        string hash,
        string timestamp,
        bool isSubsanacion,
        string description)
        => new()
        {
            IssuerNif = ctx.TaxpayerNif,
            IssuerName = ctx.TaxpayerName,
            InvoiceSeries = series,
            InvoiceNumber = number,
            IssueDate = issueDate,
            RecipientNif = ctx.Settings.RecipientNif,
            RecipientName = ctx.Settings.RecipientName,
            IsSubsanacion = isSubsanacion,
            TipoFactura = "F1",
            Description = description,
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
}

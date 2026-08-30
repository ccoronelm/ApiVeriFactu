using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class ConsultaFiltrosAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task ConsultaFacturaInexistente_DebeDevolverSinDatos()
    {
        using var ctx = AeatE2ETestContext.Create("E2EQNONE");

        var now = DateOnly.FromDateTime(DateTime.Now);
        var impossibleInvoice =
            "NOEXISTE/" +
            Guid.NewGuid().ToString("N").ToUpperInvariant();

        var result = await ctx.Gateway.QueryBillingRecordAsync(
            new VeriFactuQueryRequest
            {
                TaxpayerNif = ctx.TaxpayerNif,
                TaxpayerName = ctx.TaxpayerName,
                FiscalYear = now.Year.ToString(
                    "0000",
                    CultureInfo.InvariantCulture),
                Period = now.Month.ToString(
                    "00",
                    CultureInfo.InvariantCulture),
                InvoiceNumber = impossibleInvoice,
                IssueDate = now
            });

        Assert.Equal("SinDatos", result.Result);
        Assert.Empty(result.Records);
        Assert.False(result.HasMorePages);
        Assert.Null(result.NextPageKey);
    }

    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task ConsultaConContraparteRangoYSistema_DebeEncontrarLaFactura()
    {
        using var ctx = AeatE2ETestContext.Create("E2EQF");

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);
        const string series = "E2EQF/";
        var fullNumber = series + runId;
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
            Description = "Factura E2E para filtros de consulta",
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

        var submit = await ctx.SubmitAltaAsync(data);
        Assert.True(submit.IsAccepted, submit.StatusDescription);

        VeriFactuQueryResult? result = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await ctx.Gateway.QueryBillingRecordAsync(
                new VeriFactuQueryRequest
                {
                    TaxpayerNif = ctx.TaxpayerNif,
                    TaxpayerName = ctx.TaxpayerName,
                    FiscalYear = issueDate.Year.ToString(
                        "0000",
                        CultureInfo.InvariantCulture),
                    Period = issueDate.Month.ToString(
                        "00",
                        CultureInfo.InvariantCulture),
                    InvoiceNumber = fullNumber,
                    CounterpartyNif = ctx.Settings.RecipientNif,
                    CounterpartyName = ctx.Settings.RecipientName,
                    IssueDateFrom = issueDate.AddDays(-1),
                    IssueDateTo = issueDate.AddDays(1),
                    System = new VeriFactuSystemFilter
                    {
                        ProducerName =
                            ctx.Options.SistemaInformatico.NombreRazon,
                        ProducerNif =
                            ctx.Options.SistemaInformatico.Nif,
                        SystemName =
                            ctx.Options.SistemaInformatico.NombreSistemaInformatico,
                        SystemId =
                            ctx.Options.SistemaInformatico.IdSistemaInformatico,
                        Version =
                            ctx.Options.SistemaInformatico.Version,
                        InstallationNumber =
                            ctx.Options.SistemaInformatico.NumeroInstalacion
                    },
                    ShowIssuerName = true,
                    ShowSystemInformation = true
                });

            if (result.Records.Any(x =>
                    string.Equals(
                        x.InvoiceNumber,
                        fullNumber,
                        StringComparison.Ordinal)))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.NotNull(result);
        Assert.Equal("ConDatos", result!.Result);

        var record = Assert.Single(
            result.Records.Where(x =>
                string.Equals(
                    x.InvoiceNumber,
                    fullNumber,
                    StringComparison.Ordinal)));

        Assert.Equal(ctx.TaxpayerNif, record.IssuerNif);
        Assert.Equal("F1", record.InvoiceType);
        Assert.Equal("Correcto", record.RecordStatus);
        Assert.Equal(hash, record.Hash);
    }
}

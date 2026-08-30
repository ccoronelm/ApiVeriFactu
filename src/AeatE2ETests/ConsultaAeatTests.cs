using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.AeatE2ETests;

public sealed class ConsultaAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task AltaF1_YConsultaPorFactura_DebenSerCorrectasEnAeatTest()
    {
        var settings = AeatE2ETestSettings.Load();
        settings.ValidateSafety();

        var options = settings.VeriFactu;
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        options.SistemaInformatico.NumeroInstalacion = $"E2EQ-{runId}";

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var registerTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        const string invoiceSeries = "E2EQ/";
        var invoiceNumber = runId;
        var fullInvoiceNumber = invoiceSeries + invoiceNumber;

        const decimal baseAmount = 100m;
        const decimal taxAmount = 21m;
        const decimal totalAmount = 121m;

        var hashCalculator = new Sha256HashCalculator();
        var hash = hashCalculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = options.Taxpayer.Nif,
                InvoiceSeries = invoiceSeries,
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = totalAmount,
                TotalTaxAmount = taxAmount,
                RegisterTimestamp = registerTimestamp
            });

        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(options));

        var xml = xmlBuilder.BuildRegFactuXml(
            new RegistroAltaData
            {
                IssuerNif = options.Taxpayer.Nif,
                IssuerName = options.Taxpayer.Name,
                InvoiceSeries = invoiceSeries,
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate,
                RecipientNif = settings.RecipientNif,
                RecipientName = settings.RecipientName,
                IsSubsanacion = false,
                TipoFactura = "F1",
                Description = "Factura E2E para consulta AEAT",
                CuotaTotal = taxAmount,
                ImporteTotal = totalAmount,
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
                FechaHoraHusoGenRegistro = registerTimestamp
            });

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var validation = await validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            validation.IsValid,
            string.Join(" | ", validation.Errors.Select(x => x.Message)));

        using var certificate = new CertificateLoader(
            NullLogger<CertificateLoader>.Instance)
            .Load(options.Certificate)
            ?? throw new InvalidOperationException(
                "No se pudo cargar el certificado CurrentUser/My.");

        using var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificate);

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("gesFactu-E2E/1.0");

        var gateway = new VeriFactuGatewaySoapClient(
            httpClient,
            Options.Create(options),
            validator,
            NullLogger<VeriFactuGatewaySoapClient>.Instance);

        var submit = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = xml,
                PreviousRecordHash = null
            });

        Assert.True(submit.IsAccepted, submit.StatusDescription);
        Assert.Equal("Correcto", submit.RecordStatus);

        VeriFactuQueryResult? queryResult = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            queryResult = await gateway.QueryBillingRecordAsync(
                new VeriFactuQueryRequest
                {
                    TaxpayerNif = options.Taxpayer.Nif,
                    TaxpayerName = options.Taxpayer.Name,
                    FiscalYear = issueDate.Year.ToString(
                        "0000",
                        CultureInfo.InvariantCulture),
                    Period = issueDate.Month.ToString(
                        "00",
                        CultureInfo.InvariantCulture),
                    InvoiceNumber = fullInvoiceNumber,
                    IssueDate = issueDate,
                    ShowIssuerName = true
                });

            if (queryResult.Records.Any(x =>
                    string.Equals(
                        x.InvoiceNumber,
                        fullInvoiceNumber,
                        StringComparison.Ordinal)))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.NotNull(queryResult);
        Assert.Equal("ConDatos", queryResult!.Result);

        var record = Assert.Single(
            queryResult.Records.Where(x =>
                string.Equals(
                    x.InvoiceNumber,
                    fullInvoiceNumber,
                    StringComparison.Ordinal)));

        Assert.Equal(options.Taxpayer.Nif, record.IssuerNif);
        Assert.Equal(issueDate, record.IssueDate);
        Assert.Equal("F1", record.InvoiceType);
        Assert.Equal("Correcto", record.RecordStatus);
        Assert.Equal(hash, record.Hash);
    }
}

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

public sealed class RegistroRectificativaAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task RegistroR1S_TrasF1_DebeSerCorrectoEnAeatTest()
    {
        var settings = AeatE2ETestSettings.Load();
        settings.ValidateSafety();

        var options = settings.VeriFactu;
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        options.SistemaInformatico.NumeroInstalacion = $"E2ER1-{runId}";

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        const string originalSeries = "E2ER1-O/";
        var originalNumber = runId;
        const decimal originalBase = 100.00m;
        const decimal originalTax = 21.00m;
        const decimal originalTotal = 121.00m;

        var calculator = new Sha256HashCalculator();
        var builder = new RegistroAltaXmlBuilderAdapter(Options.Create(options));
        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        using var certificate = new CertificateLoader(
            NullLogger<CertificateLoader>.Instance)
            .Load(options.Certificate)
            ?? throw new InvalidOperationException(
                "No se pudo cargar el certificado de cliente CurrentUser/My.");

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

        var originalTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var originalHash = calculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = options.Taxpayer.Nif,
                InvoiceSeries = originalSeries,
                InvoiceNumber = originalNumber,
                IssueDate = issueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = originalTotal,
                TotalTaxAmount = originalTax,
                RegisterTimestamp = originalTimestamp
            });

        var originalXml = builder.BuildRegFactuXml(
            new RegistroAltaData
            {
                IssuerNif = options.Taxpayer.Nif,
                IssuerName = options.Taxpayer.Name,
                InvoiceSeries = originalSeries,
                InvoiceNumber = originalNumber,
                IssueDate = issueDate,
                RecipientNif = settings.RecipientNif,
                RecipientName = settings.RecipientName,
                IsSubsanacion = false,
                TipoFactura = "F1",
                Description = "Factura origen E2E para rectificativa",
                CuotaTotal = originalTax,
                ImporteTotal = originalTotal,
                Detalles =
                [
                    new DetalleDesgloseData
                    {
                        Impuesto = "01",
                        ClaveRegimen = "01",
                        CalificacionOperacion = "S1",
                        TipoImpositivo = 21.00m,
                        BaseImponible = originalBase,
                        CuotaRepercutida = originalTax
                    }
                ],
                ComputedHash = originalHash,
                PreviousRecordHash = null,
                PreviousIssueDate = null,
                PreviousIssuerNif = null,
                PreviousInvoiceSeries = null,
                PreviousInvoiceNumber = null,
                FechaHoraHusoGenRegistro = originalTimestamp
            });

        var originalValidation = await validator.ValidateAsync(
            originalXml,
            VeriFactuXmlSchemaType.BillingRecord);
        Assert.True(
            originalValidation.IsValid,
            string.Join(" | ", originalValidation.Errors.Select(x => x.Message)));

        var originalResult = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = originalXml,
                PreviousRecordHash = null
            });

        Assert.True(originalResult.IsAccepted, originalResult.StatusDescription);
        Assert.Equal("Correcto", originalResult.RecordStatus);

        var rectificationTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);
        const string rectificationSeries = "E2ER1-R/";
        var rectificationNumber = runId;
        const decimal rectifiedBase = 80.00m;
        const decimal rectifiedTax = 16.80m;
        const decimal rectifiedTotal = 96.80m;

        var rectificationHash = calculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = originalHash,
                IssuerNif = options.Taxpayer.Nif,
                InvoiceSeries = rectificationSeries,
                InvoiceNumber = rectificationNumber,
                IssueDate = issueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                InvoiceType = "R1",
                TotalAmount = rectifiedTotal,
                TotalTaxAmount = rectifiedTax,
                RegisterTimestamp = rectificationTimestamp
            });

        var rectificationXml = builder.BuildRegFactuXml(
            new RegistroAltaData
            {
                IssuerNif = options.Taxpayer.Nif,
                IssuerName = options.Taxpayer.Name,
                InvoiceSeries = rectificationSeries,
                InvoiceNumber = rectificationNumber,
                IssueDate = issueDate,
                RecipientNif = settings.RecipientNif,
                RecipientName = settings.RecipientName,
                IsSubsanacion = false,
                TipoFactura = "R1",
                TipoRectificativa = "S",
                FacturasRectificadas =
                [
                    new FacturaRectificadaData
                    {
                        IssuerNif = options.Taxpayer.Nif,
                        InvoiceSeries = originalSeries,
                        InvoiceNumber = originalNumber,
                        IssueDate = issueDate
                    }
                ],
                ImporteRectificacion = new ImporteRectificacionData
                {
                    BaseRectificada = originalBase,
                    CuotaRectificada = originalTax
                },
                Description = "Rectificativa R1 sustitutiva E2E",
                CuotaTotal = rectifiedTax,
                ImporteTotal = rectifiedTotal,
                Detalles =
                [
                    new DetalleDesgloseData
                    {
                        Impuesto = "01",
                        ClaveRegimen = "01",
                        CalificacionOperacion = "S1",
                        TipoImpositivo = 21.00m,
                        BaseImponible = rectifiedBase,
                        CuotaRepercutida = rectifiedTax
                    }
                ],
                ComputedHash = rectificationHash,
                PreviousRecordHash = originalHash,
                PreviousIssueDate = issueDate,
                PreviousIssuerNif = options.Taxpayer.Nif,
                PreviousInvoiceSeries = originalSeries,
                PreviousInvoiceNumber = originalNumber,
                FechaHoraHusoGenRegistro = rectificationTimestamp
            });

        var rectificationValidation = await validator.ValidateAsync(
            rectificationXml,
            VeriFactuXmlSchemaType.BillingRecord);
        Assert.True(
            rectificationValidation.IsValid,
            string.Join(" | ", rectificationValidation.Errors.Select(x => x.Message)));

        var result = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = rectificationXml,
                PreviousRecordHash = originalHash
            });

        Assert.True(result.IsAccepted, result.StatusDescription);
        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.SubmissionId));
    }
}

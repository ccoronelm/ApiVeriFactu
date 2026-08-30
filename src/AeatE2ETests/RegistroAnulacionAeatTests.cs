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

public sealed class RegistroAnulacionAeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task AltaYAnulacion_DebenSerCorrectasEnAeatTest()
    {
        var settings = AeatE2ETestSettings.Load();
        settings.ValidateSafety();

        var options = settings.VeriFactu;
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        options.SistemaInformatico.NumeroInstalacion = $"E2EC-{runId}";

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        const string invoiceSeries = "E2EC/";
        var invoiceNumber = runId;

        var calculator = new Sha256HashCalculator();
        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var altaBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(options));

        var cancellationBuilder = new RegistroAnulacionXmlBuilderAdapter(
            Options.Create(options));

        using var certificate = new CertificateLoader(
            NullLogger<CertificateLoader>.Instance)
            .Load(options.Certificate)
            ?? throw new InvalidOperationException(
                "No se pudo cargar el certificado de cliente.");

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

        var altaTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var altaHash = calculator.CalculateChainHash(
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
                TotalAmount = 121.00m,
                TotalTaxAmount = 21.00m,
                RegisterTimestamp = altaTimestamp
            });

        var altaXml = altaBuilder.BuildRegFactuXml(
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
                Description = "Alta previa a anulación automática AEAT TEST",
                CuotaTotal = 21.00m,
                ImporteTotal = 121.00m,
                Detalles =
                [
                    new DetalleDesgloseData
                    {
                        Impuesto = "01",
                        ClaveRegimen = "01",
                        CalificacionOperacion = "S1",
                        TipoImpositivo = 21.00m,
                        BaseImponible = 100.00m,
                        CuotaRepercutida = 21.00m
                    }
                ],
                ComputedHash = altaHash,
                PreviousRecordHash = null,
                PreviousIssueDate = null,
                PreviousIssuerNif = null,
                PreviousInvoiceSeries = null,
                PreviousInvoiceNumber = null,
                FechaHoraHusoGenRegistro = altaTimestamp
            });

        var altaValidation = await validator.ValidateAsync(
            altaXml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            altaValidation.IsValid,
            string.Join(" | ", altaValidation.Errors.Select(x => x.Message)));

        var altaResult = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = altaXml,
                PreviousRecordHash = null
            });

        Assert.True(altaResult.IsAccepted, altaResult.StatusDescription);
        Assert.Equal("Correcto", altaResult.RecordStatus);

        var cancellationTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var cancellationHash = calculator.CalculateCancellationHash(
            new CancellationRecordHashInput
            {
                PreviousHash = altaHash,
                IssuerNif = options.Taxpayer.Nif,
                InvoiceSeries = invoiceSeries,
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                RegisterTimestamp = cancellationTimestamp
            });

        var cancellationXml = cancellationBuilder.BuildRegFactuXml(
            new RegistroAnulacionData
            {
                IssuerNif = options.Taxpayer.Nif,
                IssuerName = options.Taxpayer.Name,
                InvoiceSeries = invoiceSeries,
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate,
                ComputedHash = cancellationHash,
                PreviousRecordHash = altaHash,
                PreviousIssueDate = issueDate,
                PreviousIssuerNif = options.Taxpayer.Nif,
                PreviousInvoiceSeries = invoiceSeries,
                PreviousInvoiceNumber = invoiceNumber,
                FechaHoraHusoGenRegistro = cancellationTimestamp
            });

        var cancellationValidation = await validator.ValidateAsync(
            cancellationXml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            cancellationValidation.IsValid,
            string.Join(
                " | ",
                cancellationValidation.Errors.Select(x => x.Message)));

        var cancellationResult = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = cancellationXml,
                PreviousRecordHash = altaHash
            });

        Assert.True(
            cancellationResult.IsAccepted,
            cancellationResult.StatusDescription);

        Assert.Equal("Correcto", cancellationResult.StatusCode);
        Assert.Equal("Correcto", cancellationResult.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(cancellationResult.SubmissionId));
    }
}

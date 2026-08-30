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

public sealed class RegistroAltaF2AeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task RegistroAltaF2_SinDestinatario_DebeSerCorrectoEnAeatTest()
    {
        var settings = AeatE2ETestSettings.Load();
        settings.ValidateSafety();

        var options = settings.VeriFactu;
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        options.SistemaInformatico.NumeroInstalacion = $"E2EF2-{runId}";

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var timestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        const string series = "E2EF2/";
        var number = runId;

        var calculator = new Sha256HashCalculator();
        var hash = calculator.CalculateChainHash(
            new BillingRecordHashInput
            {
                PreviousHash = string.Empty,
                IssuerNif = options.Taxpayer.Nif,
                InvoiceSeries = series,
                InvoiceNumber = number,
                IssueDate = issueDate.ToString(
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture),
                InvoiceType = "F2",
                TotalAmount = 12.10m,
                TotalTaxAmount = 2.10m,
                RegisterTimestamp = timestamp
            });

        var builder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(options));

        var xml = builder.BuildRegFactuXml(
            new RegistroAltaData
            {
                IssuerNif = options.Taxpayer.Nif,
                IssuerName = options.Taxpayer.Name,
                InvoiceSeries = series,
                InvoiceNumber = number,
                IssueDate = issueDate,
                RecipientNif = string.Empty,
                RecipientName = string.Empty,
                IsSubsanacion = false,
                TipoFactura = "F2",
                Description = "Factura simplificada automática AEAT TEST",
                CuotaTotal = 2.10m,
                ImporteTotal = 12.10m,
                Detalles =
                [
                    new DetalleDesgloseData
                    {
                        Impuesto = "01",
                        ClaveRegimen = "01",
                        CalificacionOperacion = "S1",
                        TipoImpositivo = 21.00m,
                        BaseImponible = 10.00m,
                        CuotaRepercutida = 2.10m
                    }
                ],
                ComputedHash = hash,
                PreviousRecordHash = null,
                PreviousIssueDate = null,
                PreviousIssuerNif = null,
                PreviousInvoiceSeries = null,
                PreviousInvoiceNumber = null,
                FechaHoraHusoGenRegistro = timestamp
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

        var result = await gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = options.Taxpayer.Nif,
                SignedXmlContent = xml,
                PreviousRecordHash = null
            });

        Assert.True(result.IsAccepted, result.StatusDescription);
        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.SubmissionId));
    }
}

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

public sealed class RegistroAltaF1AeatTests
{
    [AeatE2EFact]
    [Trait("Category", "AEAT-E2E")]
    public async Task RegistroAltaF1_DebeSerCorrectoEnAeatTest()
    {
        var settings = AeatE2ETestSettings.Load();
        settings.ValidateSafety();

        var options = settings.VeriFactu;

        // Cada ejecución se identifica como una instalación de pruebas distinta.
        // De este modo puede declarar PrimerRegistro=S sin depender de la cadena
        // persistida por ejecuciones anteriores.
        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            CultureInfo.InvariantCulture);

        options.SistemaInformatico.NumeroInstalacion = $"E2E-{runId}";

        var issueDate = DateOnly.FromDateTime(DateTime.Now);
        var registerTimestamp = DateTimeOffset.Now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        const string invoiceSeries = "E2E/";
        var invoiceNumber = runId;
        const decimal baseImponible = 100.00m;
        const decimal taxAmount = 21.00m;
        const decimal totalAmount = 121.00m;

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
                Description = "Prueba automatizada gesFactu AEAT TEST",
                CuotaTotal = taxAmount,
                ImporteTotal = totalAmount,
                Detalles =
                [
                    new DetalleDesgloseData
                    {
                        Impuesto = "01",
                        ClaveRegimen = "01",
                        CalificacionOperacion = "S1",
                        TipoImpositivo = 21.00m,
                        BaseImponible = baseImponible,
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
            "XML inválido: " +
            string.Join(" | ", validation.Errors.Select(x => x.Message)));

        var certificateLoader = new CertificateLoader(
            NullLogger<CertificateLoader>.Instance);

        using var certificate = certificateLoader.Load(options.Certificate)
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

        Assert.True(
            result.IsAccepted,
            $"AEAT rechazó el registro. {result.StatusDescription} {result.AdditionalDetails}");

        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.SubmissionId));
    }
}

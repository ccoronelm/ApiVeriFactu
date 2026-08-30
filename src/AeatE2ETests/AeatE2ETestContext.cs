using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace gesFactu.AeatE2ETests;

internal sealed class AeatE2ETestContext : IDisposable
{
    private readonly System.Security.Cryptography.X509Certificates.X509Certificate2 _certificate;
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _httpClient;

    private AeatE2ETestContext(
        AeatE2ETestSettings settings,
        string installationPrefix)
    {
        settings.ValidateSafety();

        Settings = settings;
        Options = settings.VeriFactu;

        var runId = DateTimeOffset.Now.ToString(
            "yyyyMMddHHmmssfff",
            System.Globalization.CultureInfo.InvariantCulture);

        Options.SistemaInformatico.NumeroInstalacion =
            $"{installationPrefix}-{runId}";

        Validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        AltaBuilder = new RegistroAltaXmlBuilderAdapter(
            Microsoft.Extensions.Options.Options.Create(Options));

        CancellationBuilder = new RegistroAnulacionXmlBuilderAdapter(
            Microsoft.Extensions.Options.Options.Create(Options));

        HashCalculator = new Sha256HashCalculator();

        _certificate = new CertificateLoader(
            NullLogger<CertificateLoader>.Instance)
            .Load(Options.Certificate)
            ?? throw new InvalidOperationException(
                "No se pudo cargar el certificado de cliente CurrentUser/My.");

        _handler = new HttpClientHandler();
        _handler.ClientCertificates.Add(_certificate);

        _httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "gesFactu-E2E/1.0");

        Gateway = new VeriFactuGatewaySoapClient(
            _httpClient,
            Microsoft.Extensions.Options.Options.Create(Options),
            Validator,
            NullLogger<VeriFactuGatewaySoapClient>.Instance);
    }

    public AeatE2ETestSettings Settings { get; }
    public VeriFactuOptions Options { get; }
    public XmlSchemaValidator Validator { get; }
    public RegistroAltaXmlBuilderAdapter AltaBuilder { get; }
    public RegistroAnulacionXmlBuilderAdapter CancellationBuilder { get; }
    public Sha256HashCalculator HashCalculator { get; }
    public VeriFactuGatewaySoapClient Gateway { get; }

    public string TaxpayerNif => Options.Taxpayer.Nif;
    public string TaxpayerName => Options.Taxpayer.Name;

    public static AeatE2ETestContext Create(string installationPrefix)
        => new(AeatE2ETestSettings.Load(), installationPrefix);

    public async Task<VeriFactuSubmissionResult> SubmitAltaAsync(
        RegistroAltaData data,
        CancellationToken cancellationToken = default)
    {
        var xml = AltaBuilder.BuildRegFactuXml(data);

        var validation = await Validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord,
            cancellationToken);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "XML E2E inválido contra XSD oficial: " +
                string.Join(
                    " | ",
                    validation.Errors.Select(x => x.Message)));
        }

        return await Gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = data.IssuerNif,
                SignedXmlContent = xml,
                PreviousRecordHash = data.PreviousRecordHash
            },
            cancellationToken);
    }

    public async Task<VeriFactuSubmissionResult> SubmitCancellationAsync(
        RegistroAnulacionData data,
        CancellationToken cancellationToken = default)
    {
        var xml = CancellationBuilder.BuildRegFactuXml(data);

        var validation = await Validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord,
            cancellationToken);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "XML de anulación E2E inválido contra XSD oficial: " +
                string.Join(
                    " | ",
                    validation.Errors.Select(x => x.Message)));
        }

        return await Gateway.SubmitBillingRecordAsync(
            new VeriFactuSubmissionRequest
            {
                TaxpayerNif = data.IssuerNif,
                SignedXmlContent = xml,
                PreviousRecordHash = data.PreviousRecordHash
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _certificate.Dispose();
    }
}

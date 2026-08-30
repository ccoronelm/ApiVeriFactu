using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

public interface IVeriFactuHttpClientProvider
{
    HttpClient GetClient(string taxpayerNif);
}

/// <summary>
/// Mantiene un pool de HttpClient aislado por obligado tributario. Cada handler
/// tiene exactamente el certificado mTLS configurado para ese obligado.
/// </summary>
public sealed class VeriFactuHttpClientProvider
    : IVeriFactuHttpClientProvider, IDisposable
{
    private readonly VeriFactuOptions _options;
    private readonly CertificateLoader _certificateLoader;
    private readonly ConcurrentDictionary<string, ClientHolder> _clients =
        new(StringComparer.OrdinalIgnoreCase);

    public VeriFactuHttpClientProvider(
        IOptions<VeriFactuOptions> options,
        CertificateLoader certificateLoader)
    {
        _options = options?.Value ??
            throw new ArgumentNullException(nameof(options));
        _certificateLoader = certificateLoader ??
            throw new ArgumentNullException(nameof(certificateLoader));
    }

    public HttpClient GetClient(string taxpayerNif)
    {
        var profile = _options.ResolveTaxpayerByNif(taxpayerNif);
        var key = profile.Nif.Trim().ToUpperInvariant();

        return _clients.GetOrAdd(
            key,
            _ => CreateClient(profile)).Client;
    }

    private ClientHolder CreateClient(
        VeriFactuTaxpayerProfileOptions profile)
    {
        var certificate = _certificateLoader.Load(profile.Certificate)
            ?? throw new InvalidOperationException(
                $"No se pudo cargar el certificado mTLS del obligado {profile.Nif}.");

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificate);

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("gesFactu/1.0");

        return new ClientHolder(client, certificate);
    }

    public void Dispose()
    {
        foreach (var holder in _clients.Values)
        {
            holder.Client.Dispose();
            holder.Certificate.Dispose();
        }

        _clients.Clear();
    }

    private sealed record ClientHolder(
        HttpClient Client,
        X509Certificate2 Certificate);
}

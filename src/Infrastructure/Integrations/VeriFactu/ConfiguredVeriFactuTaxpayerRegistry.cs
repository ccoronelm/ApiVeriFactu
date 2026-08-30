using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

public sealed class ConfiguredVeriFactuTaxpayerRegistry
    : IVeriFactuTaxpayerRegistry
{
    private readonly VeriFactuOptions _options;

    public ConfiguredVeriFactuTaxpayerRegistry(
        IOptions<VeriFactuOptions> options)
    {
        _options = options?.Value ??
            throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyList<VeriFactuTaxpayerIdentity> GetAll()
        => _options.GetConfiguredTaxpayers()
            .Select(x => new VeriFactuTaxpayerIdentity(
                x.Key.Trim(),
                x.Nif.Trim().ToUpperInvariant(),
                x.Name.Trim()))
            .ToArray();

    public VeriFactuTaxpayerIdentity Resolve(string selector)
        => ToIdentity(_options.ResolveTaxpayer(selector));

    public VeriFactuTaxpayerIdentity ResolveByNif(string nif)
        => ToIdentity(_options.ResolveTaxpayerByNif(nif));

    public VeriFactuTaxpayerIdentity ResolveDefault()
    {
        var all = GetAll();

        return all.Count switch
        {
            1 => all[0],
            0 => throw new InvalidOperationException(
                "No hay obligados tributarios configurados."),
            _ => throw new InvalidOperationException(
                "Hay varios obligados tributarios configurados; debe seleccionarse uno explícitamente.")
        };
    }

    private static VeriFactuTaxpayerIdentity ToIdentity(
        VeriFactuTaxpayerProfileOptions profile)
        => new(
            profile.Key.Trim(),
            profile.Nif.Trim().ToUpperInvariant(),
            profile.Name.Trim());
}

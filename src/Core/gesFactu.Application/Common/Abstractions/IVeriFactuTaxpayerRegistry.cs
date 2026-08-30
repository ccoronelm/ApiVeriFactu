namespace gesFactu.Application.Common.Abstractions;

public sealed record VeriFactuTaxpayerIdentity(
    string Key,
    string Nif,
    string Name);

/// <summary>
/// Registro de obligados tributarios habilitados en esta instalación.
/// No expone certificados ni secretos a la capa Application.
/// </summary>
public interface IVeriFactuTaxpayerRegistry
{
    IReadOnlyList<VeriFactuTaxpayerIdentity> GetAll();

    VeriFactuTaxpayerIdentity Resolve(string selector);

    VeriFactuTaxpayerIdentity ResolveByNif(string nif);

    /// <summary>
    /// Solo devuelve un valor implícito cuando existe exactamente un obligado.
    /// En multiempresa obliga a selección explícita.
    /// </summary>
    VeriFactuTaxpayerIdentity ResolveDefault();
}

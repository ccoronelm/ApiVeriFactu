namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Identidad del obligado tributario actualmente configurado.
/// En el bloque multiempresa será sustituible por un contexto por obligado.
/// </summary>
public interface IVeriFactuTaxpayerContext
{
    string Nif { get; }
    string Name { get; }
}

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Identidad del sistema informático configurado para construir filtros de
/// ConsultaFactuSistemaFacturacion sin aceptar estos datos desde el cliente.
/// </summary>
public interface IVeriFactuSystemContext
{
    string ProducerName { get; }
    string ProducerNif { get; }
    string SystemName { get; }
    string SystemId { get; }
    string Version { get; }
    string InstallationNumber { get; }

    string GetInstallationNumber(string taxpayerNif);
}

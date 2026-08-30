namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para construir RegistroAnulacion conforme a los XSD oficiales AEAT.
/// </summary>
public interface IRegistroAnulacionXmlBuilder
{
    string BuildRegFactuXml(RegistroAnulacionData data);
}

public sealed class RegistroAnulacionData
{
    public required string IssuerNif { get; init; }
    public required string IssuerName { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }

    public required string ComputedHash { get; init; }
    public required string? PreviousRecordHash { get; init; }
    public required DateOnly? PreviousIssueDate { get; init; }
    public required string? PreviousIssuerNif { get; init; }
    public required string? PreviousInvoiceSeries { get; init; }
    public required string? PreviousInvoiceNumber { get; init; }

    public required string FechaHoraHusoGenRegistro { get; init; }

    public string? SinRegistroPrevio { get; init; }
    public string? RechazoPrevio { get; init; }
}

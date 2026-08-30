using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ConsultarAEAT;

/// <summary>
/// Consulta síncrona de registros almacenados por AEAT para el obligado
/// tributario configurado en gesFactu.
/// </summary>
public sealed record ConsultarRegistrosAeatQuery(
    string FiscalYear,
    string Period,
    string? InvoiceNumber = null,
    string? CounterpartyNif = null,
    string? CounterpartyName = null,
    string? IssueDate = null,
    string? IssueDateFrom = null,
    string? IssueDateTo = null,
    string? ExternalReference = null,
    string? PageIssuerNif = null,
    string? PageInvoiceNumber = null,
    string? PageIssueDate = null,
    bool FilterCurrentSystem = false,
    bool ShowIssuerName = false,
    bool ShowSystemInformation = false,
    string? Taxpayer = null)
    : IRequest<Result<VeriFactuQueryResult>>;

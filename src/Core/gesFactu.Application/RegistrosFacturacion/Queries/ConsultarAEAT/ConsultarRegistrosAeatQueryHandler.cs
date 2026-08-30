using System.Globalization;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ConsultarAEAT;

public sealed class ConsultarRegistrosAeatQueryHandler
    : IRequestHandler<ConsultarRegistrosAeatQuery, Result<VeriFactuQueryResult>>
{
    private readonly IVeriFactuGateway _gateway;
    private readonly IVeriFactuTaxpayerRegistry _taxpayers;
    private readonly IVeriFactuSystemContext _system;
    private readonly ILogger<ConsultarRegistrosAeatQueryHandler> _logger;

    public ConsultarRegistrosAeatQueryHandler(
        IVeriFactuGateway gateway,
        IVeriFactuTaxpayerRegistry taxpayers,
        IVeriFactuSystemContext system,
        ILogger<ConsultarRegistrosAeatQueryHandler> logger)
    {
        _gateway = gateway;
        _taxpayers = taxpayers;
        _system = system;
        _logger = logger;
    }

    public async Task<Result<VeriFactuQueryResult>> Handle(
        ConsultarRegistrosAeatQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.FiscalYear) ||
            query.FiscalYear.Length != 4 ||
            !int.TryParse(
                query.FiscalYear,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _))
        {
            return Validation(nameof(query.FiscalYear), "FiscalYear debe tener formato YYYY.");
        }

        if (!int.TryParse(
                query.Period,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var period) ||
            period is < 1 or > 12 ||
            query.Period.Length != 2)
        {
            return Validation(nameof(query.Period), "Period debe estar entre 01 y 12.");
        }

        var issueDateResult = ParseOptionalDate(query.IssueDate, nameof(query.IssueDate));
        if (issueDateResult.Error is not null)
            return issueDateResult.Error;

        var issueDateFromResult = ParseOptionalDate(
            query.IssueDateFrom,
            nameof(query.IssueDateFrom));
        if (issueDateFromResult.Error is not null)
            return issueDateFromResult.Error;

        var issueDateToResult = ParseOptionalDate(
            query.IssueDateTo,
            nameof(query.IssueDateTo));
        if (issueDateToResult.Error is not null)
            return issueDateToResult.Error;

        if (issueDateResult.Value.HasValue &&
            (issueDateFromResult.Value.HasValue || issueDateToResult.Value.HasValue))
        {
            return Validation(
                nameof(query.IssueDate),
                "IssueDate no puede combinarse con IssueDateFrom/IssueDateTo.");
        }

        if (issueDateFromResult.Value.HasValue &&
            issueDateToResult.Value.HasValue &&
            issueDateFromResult.Value.Value >= issueDateToResult.Value.Value)
        {
            return Validation(
                nameof(query.IssueDateFrom),
                "IssueDateFrom debe ser anterior a IssueDateTo.");
        }

        var hasCounterpartyNif = !string.IsNullOrWhiteSpace(query.CounterpartyNif);
        var hasCounterpartyName = !string.IsNullOrWhiteSpace(query.CounterpartyName);

        if (hasCounterpartyNif != hasCounterpartyName)
        {
            return Validation(
                nameof(query.CounterpartyNif),
                "CounterpartyNif y CounterpartyName deben informarse juntos.");
        }

        if (hasCounterpartyNif && query.CounterpartyNif!.Trim().Length != 9)
        {
            return Validation(
                nameof(query.CounterpartyNif),
                "CounterpartyNif debe tener 9 caracteres.");
        }

        var pagination = ParsePagination(query);
        if (pagination.Error is not null)
            return pagination.Error;

        VeriFactuTaxpayerIdentity taxpayer;
        try
        {
            taxpayer = string.IsNullOrWhiteSpace(query.Taxpayer)
                ? _taxpayers.ResolveDefault()
                : _taxpayers.Resolve(query.Taxpayer);
        }
        catch (InvalidOperationException ex)
        {
            return Validation(nameof(query.Taxpayer), ex.Message);
        }

        var request = new VeriFactuQueryRequest
        {
            TaxpayerNif = taxpayer.Nif,
            TaxpayerName = taxpayer.Name,
            FiscalYear = query.FiscalYear,
            Period = query.Period,
            InvoiceNumber = NullIfWhiteSpace(query.InvoiceNumber),
            CounterpartyNif = NullIfWhiteSpace(query.CounterpartyNif),
            CounterpartyName = NullIfWhiteSpace(query.CounterpartyName),
            IssueDate = issueDateResult.Value,
            IssueDateFrom = issueDateFromResult.Value,
            IssueDateTo = issueDateToResult.Value,
            ExternalReference = NullIfWhiteSpace(query.ExternalReference),
            PaginationKey = pagination.Value,
            System = query.FilterCurrentSystem
                ? new VeriFactuSystemFilter
                {
                    ProducerName = _system.ProducerName,
                    ProducerNif = _system.ProducerNif,
                    SystemName = _system.SystemName,
                    SystemId = _system.SystemId,
                    Version = _system.Version,
                    InstallationNumber =
                        _system.GetInstallationNumber(taxpayer.Nif)
                }
                : null,
            ShowIssuerName = query.ShowIssuerName,
            ShowSystemInformation = query.ShowSystemInformation
        };

        try
        {
            var result = await _gateway.QueryBillingRecordAsync(
                request,
                cancellationToken);

            return new Result<VeriFactuQueryResult>.SuccessWithValue(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Consulta AEAT rechazada antes del transporte.");
            return Validation("query", ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error consultando AEAT para {FiscalYear}/{Period}.",
                query.FiscalYear,
                query.Period);

            return new Result<VeriFactuQueryResult>.ExternalServiceError(
                "AEAT",
                "No se pudo completar la consulta de registros VERI*FACTU.",
                ex.Message);
        }
    }

    private static (
        DateOnly? Value,
        Result<VeriFactuQueryResult>.ValidationError? Error)
        ParseOptionalDate(string? raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null);

        if (DateOnly.TryParseExact(
                raw.Trim(),
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return (date, null);
        }

        return (
            null,
            Validation(
                field,
                $"{field} debe tener formato dd-MM-yyyy."));
    }

    private static (
        VeriFactuPaginationKey? Value,
        Result<VeriFactuQueryResult>.ValidationError? Error)
        ParsePagination(ConsultarRegistrosAeatQuery query)
    {
        var hasIssuer = !string.IsNullOrWhiteSpace(query.PageIssuerNif);
        var hasInvoice = !string.IsNullOrWhiteSpace(query.PageInvoiceNumber);
        var hasDate = !string.IsNullOrWhiteSpace(query.PageIssueDate);

        if (!hasIssuer && !hasInvoice && !hasDate)
            return (null, null);

        if (!(hasIssuer && hasInvoice && hasDate))
        {
            return (
                null,
                Validation(
                    nameof(query.PageIssuerNif),
                    "La paginación requiere PageIssuerNif, PageInvoiceNumber y PageIssueDate."));
        }

        if (query.PageIssuerNif!.Trim().Length != 9)
        {
            return (
                null,
                Validation(
                    nameof(query.PageIssuerNif),
                    "PageIssuerNif debe tener 9 caracteres."));
        }

        var parsedDate = ParseOptionalDate(
            query.PageIssueDate,
            nameof(query.PageIssueDate));

        if (parsedDate.Error is not null)
            return (null, parsedDate.Error);

        return (
            new VeriFactuPaginationKey
            {
                IssuerNif = query.PageIssuerNif.Trim().ToUpperInvariant(),
                InvoiceNumber = query.PageInvoiceNumber!.Trim(),
                IssueDate = parsedDate.Value!.Value
            },
            null);
    }

    private static Result<VeriFactuQueryResult>.ValidationError Validation(
        string field,
        string message)
        => new(field, message);

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

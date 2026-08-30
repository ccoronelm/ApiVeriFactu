using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Application.RegistrosFacturacion.Queries.ConsultarAEAT;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace gesFactu.Api.Controllers.v1;

/// <summary>
/// Consultas síncronas de registros VERI*FACTU almacenados en AEAT.
/// </summary>
[ApiController]
[Route("api/v1/aeat/records")]
[Produces("application/json")]
public sealed class AeatRecordsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AeatRecordsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Ejecuta ConsultaFactuSistemaFacturacion para el obligado tributario
    /// configurado en el servidor.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(VeriFactuQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Query(
        [FromQuery] AeatRecordsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConsultarRegistrosAeatQuery(
                request.FiscalYear,
                request.Period,
                request.InvoiceNumber,
                request.CounterpartyNif,
                request.CounterpartyName,
                request.IssueDate,
                request.IssueDateFrom,
                request.IssueDateTo,
                request.ExternalReference,
                request.PageIssuerNif,
                request.PageInvoiceNumber,
                request.PageIssueDate,
                request.FilterCurrentSystem,
                request.ShowIssuerName,
                request.ShowSystemInformation,
                request.Taxpayer),
            cancellationToken);

        return result switch
        {
            Result<VeriFactuQueryResult>.SuccessWithValue success =>
                Ok(success.Value),

            Result<VeriFactuQueryResult>.ValidationError validation =>
                BadRequest(new ProblemDetails
                {
                    Title = "Validation Failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = validation.Message,
                    Instance = HttpContext.Request.Path,
                    Extensions = new Dictionary<string, object?>
                    {
                        ["field"] = validation.PropertyName
                    }
                }),

            Result<VeriFactuQueryResult>.ExternalServiceError external =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new ProblemDetails
                    {
                        Title = "AEAT Service Unavailable",
                        Status = StatusCodes.Status503ServiceUnavailable,
                        Detail = external.Message,
                        Instance = HttpContext.Request.Path
                    }),

            Result<VeriFactuQueryResult>.UnexpectedError unexpected =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ProblemDetails
                    {
                        Title = "Internal Server Error",
                        Status = StatusCodes.Status500InternalServerError,
                        Detail = unexpected.Message,
                        Instance = HttpContext.Request.Path
                    }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}

public sealed record AeatRecordsQueryRequest
{
    public required string FiscalYear { get; init; }
    public required string Period { get; init; }

    /// <summary>
    /// Clave o NIF del obligado. Obligatorio cuando hay varios configurados.
    /// </summary>
    public string? Taxpayer { get; init; }

    public string? InvoiceNumber { get; init; }
    public string? CounterpartyNif { get; init; }
    public string? CounterpartyName { get; init; }

    /// <summary>Fecha exacta dd-MM-yyyy.</summary>
    public string? IssueDate { get; init; }

    /// <summary>Inicio del rango dd-MM-yyyy.</summary>
    public string? IssueDateFrom { get; init; }

    /// <summary>Fin del rango dd-MM-yyyy.</summary>
    public string? IssueDateTo { get; init; }

    public string? ExternalReference { get; init; }

    /// <summary>
    /// Filtra por el SistemaInformatico configurado en gesFactu.
    /// </summary>
    public bool FilterCurrentSystem { get; init; }

    public bool ShowIssuerName { get; init; }
    public bool ShowSystemInformation { get; init; }

    /// <summary>Clave de paginación devuelta por la página anterior.</summary>
    public string? PageIssuerNif { get; init; }
    public string? PageInvoiceNumber { get; init; }
    public string? PageIssueDate { get; init; }
}

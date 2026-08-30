using MediatR;
using Microsoft.AspNetCore.Mvc;
using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;
using gesFactu.Application.RegistrosFacturacion.Queries.ObtenerRegistro;

namespace gesFactu.Api.Controllers.v1;

/// <summary>
/// API para operaciones con registros de facturación.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class BillingRecordsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BillingRecordsController> _logger;

    public BillingRecordsController(IMediator mediator, ILogger<BillingRecordsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Crea un nuevo registro de facturación.
    /// </summary>
    /// <param name="request">Datos de la factura a registrar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Registro creado</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBillingRecord(
        [FromBody] CreateBillingRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Validation Failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = "El registro de facturación no cumple con los requisitos",
                Instance = HttpContext.Request.Path
            });
        }

        _logger.LogInformation(
            "Recibida solicitud para crear registro de facturación: {Series}/{Number}",
            request.InvoiceSeries,
            request.InvoiceNumber);

        var command = new CreateBillingRecordCommand(
            request.IssuerNif,
            request.InvoiceSeries,
            request.InvoiceNumber,
            request.IssueDate,
            request.IssuerName,
            request.Description,
            request.TotalAmount,
            request.TotalTaxAmount);

        var result = await _mediator.Send(command, cancellationToken);

        return result switch
        {
            Result<CreateBillingRecordResponse>.SuccessWithValue success =>
                CreatedAtAction(
                    nameof(GetBillingRecord),
                    new { id = success.Value.BillingRecordId },
                    success.Value),

            Result<CreateBillingRecordResponse>.ValidationError validationError =>
                BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Title = "Validation Failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = validationError.Message,
                    Instance = HttpContext.Request.Path,
                    Extensions = new Dictionary<string, object?> { { "field", validationError.PropertyName } }
                }),

            Result<CreateBillingRecordResponse>.DomainError domainError =>
                BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Title = "Business Rule Violation",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domainError.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<CreateBillingRecordResponse>.IdempotencyConflictError idempotencyError =>
                Conflict(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    Title = "Duplicate Fiscal Record",
                    Status = StatusCodes.Status409Conflict,
                    Detail = idempotencyError.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<CreateBillingRecordResponse>.UnexpectedError unexpectedError =>
                StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = unexpectedError.Message,
                    Instance = HttpContext.Request.Path
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Error desconocido",
                Instance = HttpContext.Request.Path
            })
        };
    }

    /// <summary>
    /// Obtiene un registro de facturación por ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBillingRecord(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Obteniendo registro de facturación: {Id}", id);

        var query = new GetBillingRecordQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        return result switch
        {
            Result<BillingRecordDto>.SuccessWithValue success =>
                Ok(success.Value),

            Result<BillingRecordDto>.NotFoundError notFoundError =>
                NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"{notFoundError.ResourceName} con identificador {notFoundError.Identifier} no encontrado",
                    Instance = HttpContext.Request.Path
                }),

            Result<BillingRecordDto>.UnexpectedError unexpectedError =>
                StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = unexpectedError.Message,
                    Instance = HttpContext.Request.Path
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Envía un registro de facturación a AEAT.
    /// 
    /// Precondición: El registro debe existir y tener un hash calculado.
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitToAEAT(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando envío a AEAT del registro: {Id}", id);

        var command = new EnviarRegistroAEATCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        return result switch
        {
            Result<EnviarRegistroAEATResponse>.SuccessWithValue success =>
                Ok(success.Value),

            Result<EnviarRegistroAEATResponse>.NotFoundError notFoundError =>
                NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Registro {notFoundError.Identifier} no encontrado",
                    Instance = HttpContext.Request.Path
                }),

            Result<EnviarRegistroAEATResponse>.ConflictError conflictError =>
                Conflict(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = conflictError.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<EnviarRegistroAEATResponse>.DomainError domainError =>
                BadRequest(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Title = "Bad Request",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domainError.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<EnviarRegistroAEATResponse>.ExternalServiceError externalError =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4",
                    Title = "Service Unavailable",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail = externalError.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<EnviarRegistroAEATResponse>.UnexpectedError unexpectedError =>
                StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = unexpectedError.Message,
                    Instance = HttpContext.Request.Path
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}

/// <summary>
/// Contrato de solicitud para crear un registro de facturación.
/// </summary>
public sealed record CreateBillingRecordRequest
{
    /// <summary>
    /// NIF/CIF del emisor de la factura
    /// </summary>
    public required string IssuerNif { get; init; }

    /// <summary>
    /// Serie de la factura (ej: "A", "2025-001")
    /// </summary>
    public required string InvoiceSeries { get; init; }

    /// <summary>
    /// Número de la factura dentro de la serie
    /// </summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>
    /// Fecha de expedición (formato: dd-MM-yyyy)
    /// </summary>
    public required string IssueDate { get; init; }

    /// <summary>
    /// Nombre o razón social del emisor
    /// </summary>
    public required string IssuerName { get; init; }

    /// <summary>
    /// Descripción de la operación/concepto
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Importe total de la factura (base + impuestos)
    /// </summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>
    /// Cuota total de impuesto
    /// </summary>
    public required decimal TotalTaxAmount { get; init; }
}

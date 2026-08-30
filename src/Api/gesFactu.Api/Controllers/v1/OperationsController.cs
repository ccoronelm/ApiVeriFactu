using System.Security.Cryptography;
using System.Text;
using gesFactu.Api.Configuration;
using gesFactu.Application.Common;
using gesFactu.Application.Operaciones.Commands.RequeueDeadLetter;
using gesFactu.Application.Operaciones.Queries.DeadLetters;
using gesFactu.Application.Operaciones.Queries.Audit;
using gesFactu.Application.Operaciones.Queries.Metrics;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace gesFactu.Api.Controllers.v1;

[ApiController]
[Route("api/v1/operations")]
[Produces("application/json")]
public sealed class OperationsController : ControllerBase
{
    public const string AdminHeader = "X-GesFactu-Admin-Key";

    private readonly IMediator _mediator;
    private readonly OperationsOptions _options;

    public OperationsController(
        IMediator mediator,
        IOptions<OperationsOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    [HttpGet("dead-letters")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDeadLetters(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
            return UnauthorizedProblem();

        return Ok(await _mediator.Send(
            new GetDeadLettersQuery(take),
            cancellationToken));
    }

    [HttpGet("audit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAudit(
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
            return UnauthorizedProblem();

        return Ok(await _mediator.Send(
            new GetAuditLogQuery(
                entityName,
                entityId,
                correlationId,
                take),
            cancellationToken));
    }

    [HttpGet("metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMetrics(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
            return UnauthorizedProblem();

        return Ok(await _mediator.Send(
            new GetOperationalMetricsQuery(),
            cancellationToken));
    }

    [HttpPost("dead-letters/{id:guid}/requeue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Requeue(
        Guid id,
        [FromBody] RequeueDeadLetterRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
            return UnauthorizedProblem();

        var result = await _mediator.Send(
            new RequeueDeadLetterCommand(id, request?.Notes),
            cancellationToken);

        return result switch
        {
            Result<RequeueDeadLetterResponse>.SuccessWithValue success =>
                Ok(success.Value),

            Result<RequeueDeadLetterResponse>.NotFoundError notFound =>
                NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"DLQ {notFound.Identifier} no encontrada",
                    Instance = HttpContext.Request.Path
                }),

            Result<RequeueDeadLetterResponse>.ConflictError conflict =>
                Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = conflict.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<RequeueDeadLetterResponse>.ValidationError validation =>
                BadRequest(new ProblemDetails
                {
                    Title = "Validation Failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = validation.Message,
                    Instance = HttpContext.Request.Path
                }),

            Result<RequeueDeadLetterResponse>.DomainError domain =>
                BadRequest(new ProblemDetails
                {
                    Title = "Recovery Failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domain.Message,
                    Instance = HttpContext.Request.Path
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private bool IsAuthorized()
    {
        var expected = _options.ResolveAdminApiKey();
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        if (!Request.Headers.TryGetValue(AdminHeader, out var supplied) ||
            string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(supplied.ToString()));

        return CryptographicOperations.FixedTimeEquals(
            expectedHash,
            suppliedHash);
    }

    private ObjectResult UnauthorizedProblem()
        => Unauthorized(new ProblemDetails
        {
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = $"Se requiere cabecera {AdminHeader} válida.",
            Instance = HttpContext.Request.Path
        });
}

public sealed record RequeueDeadLetterRequest
{
    public string? Notes { get; init; }
}

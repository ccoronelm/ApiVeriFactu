using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.Operaciones.Commands.RequeueDeadLetter;

public sealed record RequeueDeadLetterCommand(
    Guid DeadLetterId,
    string? Notes = null)
    : IRequest<Result<RequeueDeadLetterResponse>>;

public sealed record RequeueDeadLetterResponse(
    Guid DeadLetterId,
    long OutboxMessageId,
    string CorrelationId,
    DateTime RequeuedAtUtc);

public sealed class RequeueDeadLetterCommandHandler
    : IRequestHandler<
        RequeueDeadLetterCommand,
        Result<RequeueDeadLetterResponse>>
{
    private readonly IDeadLetterStore _deadLetters;
    private readonly IOutboxStore _outbox;

    public RequeueDeadLetterCommandHandler(
        IDeadLetterStore deadLetters,
        IOutboxStore outbox)
    {
        _deadLetters = deadLetters;
        _outbox = outbox;
    }

    public async Task<Result<RequeueDeadLetterResponse>> Handle(
        RequeueDeadLetterCommand request,
        CancellationToken cancellationToken)
    {
        var message = await _deadLetters.GetByIdAsync(
            request.DeadLetterId,
            cancellationToken);

        if (message is null)
        {
            return new Result<RequeueDeadLetterResponse>.NotFoundError(
                "DeadLetterMessage",
                request.DeadLetterId.ToString());
        }

        if (message.IsReviewed)
        {
            return new Result<RequeueDeadLetterResponse>.ConflictError(
                "El mensaje DLQ ya fue revisado y no puede reencolarse automáticamente.");
        }

        if (request.Notes?.Length > 1000)
        {
            return new Result<RequeueDeadLetterResponse>.ValidationError(
                nameof(request.Notes),
                "Notes no puede superar 1000 caracteres.");
        }

        var requeued = await _outbox.RequeueAsync(
            message.OriginalMessageId,
            cancellationToken);

        if (!requeued)
        {
            return new Result<RequeueDeadLetterResponse>.DomainError(
                "OUTBOX_NOT_FOUND",
                "No existe el mensaje Outbox original asociado a la DLQ.");
        }

        var now = DateTime.UtcNow;
        var notes =
            "REQUEUED " + now.ToString("O") +
            (string.IsNullOrWhiteSpace(request.Notes)
                ? string.Empty
                : " | " + request.Notes.Trim());

        await _deadLetters.MarkAsReviewedAsync(
            message.Id,
            notes,
            cancellationToken);

        return new Result<RequeueDeadLetterResponse>.SuccessWithValue(
            new RequeueDeadLetterResponse(
                message.Id,
                message.OriginalMessageId,
                message.CorrelationId,
                now));
    }
}

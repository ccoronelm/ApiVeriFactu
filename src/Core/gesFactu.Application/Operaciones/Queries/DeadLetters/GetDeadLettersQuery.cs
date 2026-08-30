using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.Operaciones.Queries.DeadLetters;

public sealed record GetDeadLettersQuery(int Take = 50)
    : IRequest<IReadOnlyList<DeadLetterSummaryDto>>;

public sealed record DeadLetterSummaryDto(
    Guid Id,
    long OriginalMessageId,
    string CorrelationId,
    string FailureReason,
    string? LastErrorResponse,
    int ProcessingAttempts,
    DateTime CreatedAt,
    DateTime MovedToDlqAt,
    bool IsReviewed,
    string? ReviewNotes);

public sealed class GetDeadLettersQueryHandler
    : IRequestHandler<GetDeadLettersQuery, IReadOnlyList<DeadLetterSummaryDto>>
{
    private readonly IDeadLetterStore _store;

    public GetDeadLettersQueryHandler(IDeadLetterStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<DeadLetterSummaryDto>> Handle(
        GetDeadLettersQuery request,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 100);

        var messages = await _store.GetUnreviewedMessagesAsync(
            take,
            cancellationToken);

        return messages
            .Select(x => new DeadLetterSummaryDto(
                x.Id,
                x.OriginalMessageId,
                x.CorrelationId,
                x.FailureReason,
                x.LastErrorResponse,
                x.ProcessingAttempts,
                x.CreatedAt,
                x.MovedToDlqAt,
                x.IsReviewed,
                x.ReviewNotes))
            .ToArray();
    }
}

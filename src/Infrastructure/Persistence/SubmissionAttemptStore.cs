using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// Implementación EF Core de la auditoría de intentos AEAT.
/// </summary>
public class SubmissionAttemptStore : ISubmissionAttemptStore
{
    private readonly ApplicationDbContext _context;

    public SubmissionAttemptStore(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SubmissionAttemptDto> CreateAsync(
        int billingRecordId,
        int attemptNumber,
        string requestPayload,
        CancellationToken cancellationToken)
    {
        var attempt = new SubmissionAttempt
        {
            Id = Guid.NewGuid(),
            BillingRecordId = billingRecordId,
            AttemptNumber = attemptNumber,
            RequestPayload = requestPayload,
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionAttemptStatus.Pending
        };

        _context.SubmissionAttempts.Add(attempt);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(attempt);
    }

    public Task MarkAsSuccessAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        int durationMilliseconds,
        CancellationToken cancellationToken)
        => CompleteAsync(
            attemptId,
            SubmissionAttemptStatus.Success,
            responseCode,
            responseDescription,
            responsePayload,
            aeatSubmissionId,
            notes: null,
            durationMilliseconds,
            cancellationToken);

    public Task MarkAsPermanentFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
        => CompleteAsync(
            attemptId,
            SubmissionAttemptStatus.PermanentFailure,
            responseCode,
            responseDescription,
            responsePayload,
            aeatSubmissionId: null,
            notes,
            durationMilliseconds,
            cancellationToken);

    public Task MarkAsTransientFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
        => CompleteAsync(
            attemptId,
            SubmissionAttemptStatus.TransientFailure,
            responseCode,
            responseDescription,
            responsePayload: null,
            aeatSubmissionId: null,
            notes,
            durationMilliseconds,
            cancellationToken);

    public Task MarkAsCommunicationErrorAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
        => CompleteAsync(
            attemptId,
            SubmissionAttemptStatus.CommunicationError,
            responseCode,
            responseDescription,
            responsePayload,
            aeatSubmissionId: null,
            notes,
            durationMilliseconds,
            cancellationToken);

    public async Task<List<SubmissionAttemptDto>> GetByBillingRecordIdAsync(
        int billingRecordId,
        CancellationToken cancellationToken)
    {
        var attempts = await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);

        return attempts.Select(MapToDto).ToList();
    }

    public async Task<SubmissionAttemptDto?> GetLastAttemptAsync(
        int billingRecordId,
        CancellationToken cancellationToken)
    {
        var attempt = await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return attempt is null ? null : MapToDto(attempt);
    }

    public async Task<List<SubmissionAttemptDto>> GetFailedAttemptsAsync(
        int billingRecordId,
        CancellationToken cancellationToken)
    {
        var attempts = await _context.SubmissionAttempts
            .Where(x =>
                x.BillingRecordId == billingRecordId &&
                x.Status != SubmissionAttemptStatus.Pending &&
                x.Status != SubmissionAttemptStatus.Success)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        return attempts.Select(MapToDto).ToList();
    }

    private async Task CompleteAsync(
        Guid attemptId,
        SubmissionAttemptStatus status,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var attempt = await _context.SubmissionAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Submission attempt {attemptId} not found");

        attempt.Status = status;
        attempt.ResponseCode = responseCode;
        attempt.ResponseDescription = responseDescription;
        attempt.ResponsePayload = responsePayload;
        attempt.AeatSubmissionId = aeatSubmissionId;
        attempt.Notes = notes;
        attempt.RespondedAt = DateTime.UtcNow;
        attempt.DurationMilliseconds = durationMilliseconds;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static SubmissionAttemptDto MapToDto(SubmissionAttempt attempt)
        => new(
            attempt.Id,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.SubmittedAt,
            attempt.RespondedAt,
            attempt.ResponseCode,
            attempt.ResponseDescription,
            attempt.DurationMilliseconds,
            attempt.AeatSubmissionId,
            attempt.Notes);
}

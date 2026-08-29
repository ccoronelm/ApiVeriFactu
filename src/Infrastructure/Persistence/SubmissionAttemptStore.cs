using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// Implementación de ISubmissionAttemptStore usando EF Core.
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

    public async Task MarkAsSuccessAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        int durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var attempt = await _context.SubmissionAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission attempt {attemptId} not found");

        attempt.Status = SubmissionAttemptStatus.Success;
        attempt.ResponseCode = responseCode;
        attempt.ResponseDescription = responseDescription;
        attempt.ResponsePayload = responsePayload;
        attempt.AeatSubmissionId = aeatSubmissionId;
        attempt.RespondedAt = DateTime.UtcNow;
        attempt.DurationMilliseconds = durationMilliseconds;

        _context.SubmissionAttempts.Update(attempt);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsPermanentFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var attempt = await _context.SubmissionAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission attempt {attemptId} not found");

        attempt.Status = SubmissionAttemptStatus.PermanentFailure;
        attempt.ResponseCode = responseCode;
        attempt.ResponseDescription = responseDescription;
        attempt.ResponsePayload = responsePayload;
        attempt.Notes = notes;
        attempt.RespondedAt = DateTime.UtcNow;
        attempt.DurationMilliseconds = durationMilliseconds;

        _context.SubmissionAttempts.Update(attempt);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsTransientFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var attempt = await _context.SubmissionAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken)
            ?? throw new InvalidOperationException($"Submission attempt {attemptId} not found");

        attempt.Status = SubmissionAttemptStatus.TransientFailure;
        attempt.ResponseCode = responseCode;
        attempt.ResponseDescription = responseDescription;
        attempt.Notes = notes;
        attempt.RespondedAt = DateTime.UtcNow;
        attempt.DurationMilliseconds = durationMilliseconds;

        _context.SubmissionAttempts.Update(attempt);
        await _context.SaveChangesAsync(cancellationToken);
    }

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

        return attempt == null ? null : MapToDto(attempt);
    }

    public async Task<List<SubmissionAttemptDto>> GetFailedAttemptsAsync(
        int billingRecordId,
        CancellationToken cancellationToken)
    {
        var attempts = await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId &&
                       (x.Status == SubmissionAttemptStatus.PermanentFailure ||
                        x.Status == SubmissionAttemptStatus.TransientFailure))
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        return attempts.Select(MapToDto).ToList();
    }

    private static SubmissionAttemptDto MapToDto(SubmissionAttempt attempt)
    {
        return new SubmissionAttemptDto(
            attempt.Id,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.SubmittedAt,
            attempt.RespondedAt,
            attempt.ResponseCode,
            attempt.ResponseDescription,
            attempt.DurationMilliseconds,
            attempt.AeatSubmissionId);
    }
}

using Microsoft.EntityFrameworkCore;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio para SubmissionAttempt (auditoría de envíos a AEAT).
/// </summary>
public class SubmissionAttemptRepository
{
    private readonly ApplicationDbContext _context;

    public SubmissionAttemptRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Registra un nuevo intento de envío.
    /// </summary>
    public async Task<SubmissionAttempt> CreateAsync(
        int billingRecordId,
        int attemptNumber,
        string requestPayload,
        CancellationToken cancellationToken = default)
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

        return attempt;
    }

    /// <summary>
    /// Marca un intento como exitoso.
    /// </summary>
    public async Task MarkAsSuccessAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Marca un intento como fallo permanente.
    /// </summary>
    public async Task MarkAsPermanentFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Marca un intento como fallo transitorio (será reintentado).
    /// </summary>
    public async Task MarkAsTransientFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Obtiene todos los intentos de un registro de facturación.
    /// </summary>
    public async Task<List<SubmissionAttempt>> GetByBillingRecordIdAsync(
        int billingRecordId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtiene el último intento de un registro.
    /// </summary>
    public async Task<SubmissionAttempt?> GetLastAttemptAsync(
        int billingRecordId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Obtiene los últimos intentos fallidos (para auditoría).
    /// </summary>
    public async Task<List<SubmissionAttempt>> GetFailedAttemptsAsync(
        int billingRecordId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SubmissionAttempts
            .Where(x => x.BillingRecordId == billingRecordId && 
                       (x.Status == SubmissionAttemptStatus.PermanentFailure || 
                        x.Status == SubmissionAttemptStatus.TransientFailure))
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);
    }
}

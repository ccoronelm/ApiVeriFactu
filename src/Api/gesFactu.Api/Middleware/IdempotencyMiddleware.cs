using System.Security.Cryptography;
using System.Text;
using gesFactu.Api.Configuration;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace gesFactu.Api.Middleware;

/// <summary>
/// Idempotencia persistente para operaciones mutantes. Evita que un retry del
/// backend Python cree una segunda operación tras un timeout de red.
/// </summary>
public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";

    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext dbContext,
        IOptions<IdempotencyOptions> optionsAccessor)
    {
        if (!IsUnsafeApiRequest(context.Request))
        {
            await _next(context);
            return;
        }

        var options = optionsAccessor.Value;

        if (!context.Request.Headers.TryGetValue(HeaderName, out var supplied) ||
            string.IsNullOrWhiteSpace(supplied))
        {
            if (!options.RequireForUnsafeMethods)
            {
                await _next(context);
                return;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Idempotency-Key required",
                $"Las operaciones mutantes requieren la cabecera {HeaderName}.");
            return;
        }

        var key = supplied.ToString().Trim();
        if (key.Length > options.MaxKeyLength)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid Idempotency-Key",
                $"{HeaderName} no puede superar {options.MaxKeyLength} caracteres.");
            return;
        }

        context.Request.EnableBuffering();
        var requestHash = await CalculateRequestHashAsync(context.Request);

        var method = context.Request.Method.ToUpperInvariant();
        var path = (context.Request.Path + context.Request.QueryString).ToString();

        var existing = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Key == key &&
                     x.Method == method &&
                     x.Path == path,
                context.RequestAborted);

        if (existing is not null)
        {
            await ReplayOrConflictAsync(
                context,
                existing,
                requestHash);
            return;
        }

        var now = DateTime.UtcNow;
        var record = new IdempotencyRecord
        {
            Key = key,
            Method = method,
            Path = path,
            RequestHash = requestHash,
            Status = "Pending",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(
                Math.Clamp(options.RetentionHours, 1, 24 * 30))
        };

        dbContext.IdempotencyRecords.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(record).State = EntityState.Detached;

            existing = await dbContext.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Key == key &&
                         x.Method == method &&
                         x.Path == path,
                    context.RequestAborted);

            if (existing is null)
                throw;

            await ReplayOrConflictAsync(
                context,
                existing,
                requestHash);
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);

            responseBuffer.Position = 0;
            var responseText = await new StreamReader(
                responseBuffer,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true)
                .ReadToEndAsync(context.RequestAborted);

            if (context.Response.StatusCode < 500)
            {
                record.Status = "Completed";
                record.ResponseStatusCode = context.Response.StatusCode;
                record.ResponseContentType = context.Response.ContentType;
                record.ResponseBody = responseText;
                record.ResponseLocation = context.Response.Headers.Location.ToString();
                record.CompletedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(context.RequestAborted);
            }
            else
            {
                dbContext.IdempotencyRecords.Remove(record);
                await dbContext.SaveChangesAsync(context.RequestAborted);
            }

            context.Response.Body = originalBody;
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(
                originalBody,
                context.RequestAborted);
        }
        catch
        {
            context.Response.Body = originalBody;

            if (dbContext.Entry(record).State != EntityState.Detached)
            {
                dbContext.IdempotencyRecords.Remove(record);
                try
                {
                    await dbContext.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // El error original tiene prioridad. Un Pending huérfano se
                    // mantiene fail-closed y será visible operativamente.
                }
            }

            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsUnsafeApiRequest(HttpRequest request)
        => request.Path.StartsWithSegments("/api") &&
           (HttpMethods.IsPost(request.Method) ||
            HttpMethods.IsPut(request.Method) ||
            HttpMethods.IsPatch(request.Method) ||
            HttpMethods.IsDelete(request.Method));

    private static async Task<string> CalculateRequestHashAsync(
        HttpRequest request)
    {
        request.Body.Position = 0;
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(
            request.Body,
            request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    private static async Task ReplayOrConflictAsync(
        HttpContext context,
        IdempotencyRecord existing,
        string requestHash)
    {
        if (!string.Equals(
                existing.RequestHash,
                requestHash,
                StringComparison.Ordinal))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                "La misma Idempotency-Key ya se utilizó con un payload diferente.");
            return;
        }

        if (!string.Equals(
                existing.Status,
                "Completed",
                StringComparison.Ordinal))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Request already in progress",
                "Existe una operación con esta Idempotency-Key cuyo resultado todavía no está confirmado.");
            return;
        }

        context.Response.StatusCode =
            existing.ResponseStatusCode ?? StatusCodes.Status200OK;

        if (!string.IsNullOrWhiteSpace(existing.ResponseContentType))
            context.Response.ContentType = existing.ResponseContentType;

        if (!string.IsNullOrWhiteSpace(existing.ResponseLocation))
            context.Response.Headers.Location = existing.ResponseLocation;

        context.Response.Headers["Idempotency-Replayed"] = "true";

        if (!string.IsNullOrEmpty(existing.ResponseBody))
            await context.Response.WriteAsync(existing.ResponseBody);
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        });
    }
}

namespace gesFactu.Application.Common;

/// <summary>
/// Representa el resultado de una operación, con éxito o error esperado.
/// No utiliza excepciones para flujos normales de negocio.
/// </summary>
public abstract record Result
{
    public sealed record Success : Result;

    public sealed record ValidationError(string PropertyName, string Message) : Result;

    public sealed record DomainError(string Code, string Message) : Result;

    public sealed record NotFoundError(string ResourceName, string Identifier) : Result;

    public sealed record IdempotencyConflictError(string Message) : Result;

    public sealed record ExternalServiceError(string ServiceName, string Message, string? Details = null) : Result;

    public sealed record ConflictError(string Message) : Result;

    public sealed record UnexpectedError(string Message) : Result;
}

/// <summary>
/// Resultado genérico que puede contener un valor en caso de éxito.
/// </summary>
public abstract record Result<T> : Result
{
    public sealed record SuccessWithValue(T Value) : Result<T>;

    public new sealed record ValidationError(string PropertyName, string Message) : Result<T>;

    public new sealed record DomainError(string Code, string Message) : Result<T>;

    public new sealed record NotFoundError(string ResourceName, string Identifier) : Result<T>;

    public new sealed record IdempotencyConflictError(string Message) : Result<T>;

    public new sealed record ExternalServiceError(string ServiceName, string Message, string? Details = null) : Result<T>;

    public new sealed record ConflictError(string Message) : Result<T>;

    public new sealed record UnexpectedError(string Message) : Result<T>;

    public TOut Match<TOut>(
        Func<SuccessWithValue, TOut> onSuccess,
        Func<ValidationError, TOut> onValidationError,
        Func<DomainError, TOut> onDomainError,
        Func<NotFoundError, TOut> onNotFound,
        Func<IdempotencyConflictError, TOut> onIdempotencyConflict,
        Func<ExternalServiceError, TOut> onExternalServiceError,
        Func<ConflictError, TOut> onConflict,
        Func<UnexpectedError, TOut> onUnexpected)
    {
        return this switch
        {
            SuccessWithValue success => onSuccess(success),
            ValidationError error => onValidationError(error),
            DomainError error => onDomainError(error),
            NotFoundError error => onNotFound(error),
            IdempotencyConflictError error => onIdempotencyConflict(error),
            ExternalServiceError error => onExternalServiceError(error),
            ConflictError error => onConflict(error),
            UnexpectedError error => onUnexpected(error),
            _ => throw new InvalidOperationException($"Unknown result type: {GetType().Name}")
        };
    }
}


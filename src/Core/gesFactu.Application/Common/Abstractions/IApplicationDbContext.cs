namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto de persistencia de la aplicación.
/// </summary>
public interface IApplicationDbContext
{
    void AddOutboxMessage(object message);

    /// <summary>
    /// Inicia una transacción SERIALIZABLE.
    /// Se usa para operaciones cuya secuencia fiscal debe ser atómica
    /// (seleccionar último registro + crear y encadenar el siguiente).
    /// </summary>
    Task<IApplicationTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un bloqueo exclusivo, limitado a la transacción actual,
    /// para serializar la generación de la cadena fiscal indicada.
    /// </summary>
    Task AcquireFiscalChainLockAsync(
        string chainKey,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstracción de transacción para no exponer EF Core a Application.
/// </summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto de persistencia de la aplicación.
/// </summary>
public interface IApplicationDbContext
{
    void AddOutboxMessage(object message);

    /// <summary>
    /// Inicia una transacción para una sección crítica protegida además
    /// por un bloqueo exclusivo del proveedor relacional.
    /// </summary>
    Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un bloqueo exclusivo de aplicación, limitado a la transacción actual.
    /// Permite serializar secciones críticas entre varias instancias de la API.
    /// </summary>
    Task AcquireExclusiveLockAsync(
        string resourceKey,
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

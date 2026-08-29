namespace gesFactu.Infrastructure.Outbox;

/// <summary>
/// Define la política de reintentos con backoff exponencial y jitter.
/// 
/// Según "Release It! Design and Deploy Production-Ready Software":
/// - Exponential backoff evita sobrecargar un servicio que ya está bajo stress
/// - Jitter previene "thundering herd" cuando múltiples instancias reintentan simultáneamente
/// 
/// Fórmula: delay = min(baseDelayMs * (2 ^ attemptNumber), maxDelayMs) + random(0, jitterMs)
/// </summary>
public class RetryPolicy
{
    private static readonly Random _random = new();

    /// <summary>
    /// Delay base en milisegundos (default: 1000ms = 1 segundo).
    /// </summary>
    public int BaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Delay máximo en milisegundos para evitar esperas excesivas (default: 60000ms = 1 minuto).
    /// </summary>
    public int MaxDelayMilliseconds { get; set; } = 60000;

    /// <summary>
    /// Jitter máximo en milisegundos (default: 1000ms). Se suma al delay exponencial.
    /// </summary>
    public int MaxJitterMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Número máximo de intentos antes de enviar a Dead Letter Queue (default: 5).
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Calcula el tiempo de espera para el siguiente reintento usando backoff exponencial + jitter.
    /// </summary>
    /// <param name="attemptNumber">Número del intento actual (0-based).</param>
    /// <returns>Tiempo de espera en milisegundos.</returns>
    public int CalculateDelayMilliseconds(int attemptNumber)
    {
        if (attemptNumber < 0)
            throw new ArgumentException("El número de intento no puede ser negativo.", nameof(attemptNumber));

        // Exponential backoff: baseDelay * 2^attemptNumber
        var exponentialDelay = BaseDelayMilliseconds * (1 << attemptNumber); // 2^attemptNumber

        // Capped exponential backoff
        var cappedDelay = Math.Min(exponentialDelay, MaxDelayMilliseconds);

        // Añadir jitter aleatorio para evitar thundering herd
        var jitter = _random.Next(0, MaxJitterMilliseconds + 1);

        return cappedDelay + jitter;
    }
}

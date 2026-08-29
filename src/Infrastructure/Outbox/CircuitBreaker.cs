namespace gesFactu.Infrastructure.Outbox;

/// <summary>
/// Implementa el patrón Circuit Breaker para detectar cuándo AEAT está caído
/// y evitar sobrecargar un servicio que ya tiene problemas.
/// 
/// Estados:
/// - Closed: operando normalmente, intentar enviar
/// - Open: AEAT parece estar caído, rechazar inmediatamente sin intentar
/// - HalfOpen: haciendo prueba para ver si AEAT se ha recuperado
/// 
/// Basado en "Release It! Design and Deploy Production-Ready Software".
/// </summary>
public class CircuitBreaker
{
    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private readonly object _lock = new();

    /// <summary>
    /// Número de fallos consecutivos que activarán el estado Open (default: 5).
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Tiempo después del cual pasar de Open a HalfOpen para reintentar (default: 60 segundos).
    /// </summary>
    public TimeSpan TimeoutDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Estado actual del circuit breaker.
    /// </summary>
    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Registra un fallo y cambia de estado si es necesario.
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= FailureThreshold)
            {
                _state = CircuitBreakerState.Open;
            }
        }
    }

    /// <summary>
    /// Registra un éxito y resetea el estado.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }
    }

    /// <summary>
    /// Intenta pasar a HalfOpen si ha pasado suficiente tiempo desde el último fallo.
    /// Retorna true si el circuito debe intentar una solicitud de prueba.
    /// </summary>
    public bool AllowTestRequest()
    {
        lock (_lock)
        {
            if (_state != CircuitBreakerState.Open)
                return _state == CircuitBreakerState.Closed;

            if (DateTime.UtcNow - _lastFailureTime >= TimeoutDuration)
            {
                _state = CircuitBreakerState.HalfOpen;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Resetea el circuit breaker (útil para pruebas o reset manual).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }
    }
}

/// <summary>
/// Estados del circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Funcionando normalmente, intentar solicitudes.
    /// </summary>
    Closed,

    /// <summary>
    /// AEAT parece caído, rechazar solicitudes inmediatamente.
    /// </summary>
    Open,

    /// <summary>
    /// Probando si AEAT se ha recuperado, permitir una solicitud de prueba.
    /// </summary>
    HalfOpen
}

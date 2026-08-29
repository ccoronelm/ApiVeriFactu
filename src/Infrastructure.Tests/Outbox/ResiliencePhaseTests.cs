using Xunit;
using gesFactu.Infrastructure.Outbox;

namespace gesFactu.Infrastructure.Tests.Outbox;

/// <summary>
/// Tests para la Fase 11: Resiliencia Avanzada
/// - RetryPolicy con exponential backoff + jitter
/// - CircuitBreaker para detectar AEAT caído
/// - DeadLetterQueue para mensajes irrecuperables
/// </summary>
public class ResiliencePhaseTests
{
    [Fact]
    public void RetryPolicy_CalculateDelay_ExponentialBackoff()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 60000,
            MaxJitterMilliseconds = 0 // Sin jitter para test determinista
        };

        // Act
        int delay0 = policy.CalculateDelayMilliseconds(0); // 1000 * 2^0 = 1000
        int delay1 = policy.CalculateDelayMilliseconds(1); // 1000 * 2^1 = 2000
        int delay2 = policy.CalculateDelayMilliseconds(2); // 1000 * 2^2 = 4000
        int delay3 = policy.CalculateDelayMilliseconds(3); // 1000 * 2^3 = 8000

        // Assert
        Assert.Equal(1000, delay0);
        Assert.Equal(2000, delay1);
        Assert.Equal(4000, delay2);
        Assert.Equal(8000, delay3);
    }

    [Fact]
    public void RetryPolicy_CalculateDelay_RespectMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 30000,
            MaxJitterMilliseconds = 0
        };

        // Act
        int delay10 = policy.CalculateDelayMilliseconds(10); // 1000 * 2^10 = 1,024,000 pero capped a 30000

        // Assert
        Assert.Equal(30000, delay10);
    }

    [Fact]
    public void RetryPolicy_CalculateDelay_IncludeJitter()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 60000,
            MaxJitterMilliseconds = 1000
        };

        // Act - multiple calls para verificar jitter aleatorio
        var delays = new int[10];
        for (int i = 0; i < 10; i++)
        {
            delays[i] = policy.CalculateDelayMilliseconds(0); // Todos deben ser ~1000 + jitter
        }

        // Assert - todos entre 1000 y 2000
        foreach (var delay in delays)
        {
            Assert.InRange(delay, 1000, 2000);
        }

        // Verificar que hay variación (al menos algunos diferentes)
        var uniqueDelays = new HashSet<int>(delays);
        Assert.True(uniqueDelays.Count > 1, "Debería haber variación por jitter");
    }

    [Fact]
    public void CircuitBreaker_InitiallyClosedState()
    {
        // Arrange & Act
        var breaker = new CircuitBreaker();

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public void CircuitBreaker_OpensAfterThresholdFailures()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 3 };

        // Act
        breaker.RecordFailure(); // 1
        breaker.RecordFailure(); // 2
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);

        breaker.RecordFailure(); // 3 - should open

        // Assert
        Assert.Equal(CircuitBreakerState.Open, breaker.State);
    }

    [Fact]
    public void CircuitBreaker_ResetsOnSuccess()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 3 };
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.Equal(CircuitBreakerState.Open, breaker.State);

        // Act
        breaker.RecordSuccess();

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public void CircuitBreaker_TransitionsToHalfOpenAfterTimeout()
    {
        // Arrange
        var breaker = new CircuitBreaker
        {
            FailureThreshold = 1,
            TimeoutDuration = TimeSpan.FromMilliseconds(100)
        };

        breaker.RecordFailure();
        Assert.Equal(CircuitBreakerState.Open, breaker.State);

        // Act
        System.Threading.Thread.Sleep(150); // Esperar a que pasen los 100ms
        bool canTest = breaker.AllowTestRequest();

        // Assert
        Assert.True(canTest);
        Assert.Equal(CircuitBreakerState.HalfOpen, breaker.State);
    }

    [Fact]
    public void CircuitBreaker_CanResetManually()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 1 };
        breaker.RecordFailure();
        Assert.Equal(CircuitBreakerState.Open, breaker.State);

        // Act
        breaker.Reset();

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }
}

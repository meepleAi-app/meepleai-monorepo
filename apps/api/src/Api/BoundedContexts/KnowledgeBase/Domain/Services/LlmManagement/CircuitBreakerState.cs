

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Circuit breaker state for LLM provider failure management
/// ISSUE-962 (BGAI-020): Implements circuit breaker pattern for provider resilience
/// </summary>
internal enum CircuitState
{
    /// <summary>Normal operation - requests allowed</summary>
    Closed,

    /// <summary>Failures detected - requests blocked</summary>
    Open,

    /// <summary>Testing recovery - limited requests allowed</summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker state tracking for a single LLM provider
/// </summary>
internal sealed class CircuitBreakerState
{
    private const int FailureThreshold = 5; // Failures before opening circuit
    private const int SuccessThreshold = 3; // Successes to close circuit from half-open
    private const int OpenDurationSeconds = 30; // Time to wait before trying half-open

    public CircuitState State { get; private set; } = CircuitState.Closed;
    public int ConsecutiveFailures { get; private set; }
    public int ConsecutiveSuccesses { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }

    /// <summary>
    /// ISSUE-5086: Callback invoked when the circuit transitions between states.
    /// Parameters: (previousState, newState).
    /// </summary>
    public Action<CircuitState, CircuitState>? OnStateTransition { get; set; }

    /// <summary>
    /// Record a successful request
    /// </summary>
    public void RecordSuccess()
    {
        LastAttemptAt = DateTime.UtcNow;
        var previous = State;
        ConsecutiveFailures = 0;
        ConsecutiveSuccesses++;

        if (State == CircuitState.HalfOpen && ConsecutiveSuccesses >= SuccessThreshold)
        {
            // Recovery confirmed - close circuit
            State = CircuitState.Closed;
            ConsecutiveSuccesses = 0;
            OpenedAt = null;
        }

        if (State != previous)
            OnStateTransition?.Invoke(previous, State);
    }

    /// <summary>
    /// Record a failed request
    /// </summary>
    public void RecordFailure()
    {
        LastAttemptAt = DateTime.UtcNow;
        var previous = State;
        ConsecutiveSuccesses = 0;
        ConsecutiveFailures++;

        if (State == CircuitState.Closed && ConsecutiveFailures >= FailureThreshold)
        {
            // Too many failures - open circuit
            State = CircuitState.Open;
            OpenedAt = DateTime.UtcNow;
        }
        else if (State == CircuitState.HalfOpen)
        {
            // Recovery failed - reopen circuit
            State = CircuitState.Open;
            OpenedAt = DateTime.UtcNow;
            ConsecutiveFailures = FailureThreshold; // Already at threshold
        }

        if (State != previous)
            OnStateTransition?.Invoke(previous, State);
    }

    /// <summary>
    /// Check if requests are allowed based on circuit state
    /// </summary>
    public bool AllowsRequests()
    {
        if (State == CircuitState.Closed)
            return true;

        if (State == CircuitState.HalfOpen)
            return true; // Allow limited testing

        // State == Open: Check if timeout expired
        if (OpenedAt.HasValue &&
            (DateTime.UtcNow - OpenedAt.Value).TotalSeconds >= OpenDurationSeconds)
        {
            // Timeout expired - try half-open
            var previous = State;
            State = CircuitState.HalfOpen;
            ConsecutiveFailures = 0;
            ConsecutiveSuccesses = 0;
            OnStateTransition?.Invoke(previous, CircuitState.HalfOpen);
            return true;
        }

        return false; // Still open, requests blocked
    }

    /// <summary>
    /// Issue #5476: Reset circuit breaker to closed state (admin emergency action).
    /// </summary>
    public void Reset()
    {
        var previous = State;
        State = CircuitState.Closed;
        ConsecutiveFailures = 0;
        ConsecutiveSuccesses = 0;
        OpenedAt = null;
        LastAttemptAt = DateTime.UtcNow;

        if (previous != CircuitState.Closed)
            OnStateTransition?.Invoke(previous, CircuitState.Closed);
    }

    /// <summary>
    /// Get human-readable status
    /// </summary>
    public string GetStatus()
    {
        return State switch
        {
            CircuitState.Closed => $"Healthy (failures: {ConsecutiveFailures})",
            CircuitState.Open => $"Circuit Open (reopens in {GetRemainingOpenSeconds()}s)",
            CircuitState.HalfOpen => $"Testing Recovery (successes: {ConsecutiveSuccesses}/{SuccessThreshold})",
            _ => "Unknown"
        };
    }

    private int GetRemainingOpenSeconds()
    {
        if (!OpenedAt.HasValue) return 0;
        var elapsed = (DateTime.UtcNow - OpenedAt.Value).TotalSeconds;
        var remaining = OpenDurationSeconds - elapsed;
        return Math.Max(0, (int)remaining);
    }
}

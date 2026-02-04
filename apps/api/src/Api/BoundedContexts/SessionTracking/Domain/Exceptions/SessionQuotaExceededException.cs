namespace Api.BoundedContexts.SessionTracking.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to create a session beyond their quota limit.
/// </summary>
public class SessionQuotaExceededException : InvalidOperationException
{
    /// <summary>
    /// Current number of active sessions.
    /// </summary>
    public int CurrentCount { get; }

    /// <summary>
    /// Maximum number of concurrent sessions allowed.
    /// </summary>
    public int MaxAllowed { get; }

    /// <summary>
    /// Creates a new instance of SessionQuotaExceededException.
    /// </summary>
    public SessionQuotaExceededException(string message, int currentCount, int maxAllowed)
        : base(message)
    {
        CurrentCount = currentCount;
        MaxAllowed = maxAllowed;
    }

    /// <summary>
    /// Creates a new instance of SessionQuotaExceededException with inner exception.
    /// </summary>
    public SessionQuotaExceededException(string message, int currentCount, int maxAllowed, Exception innerException)
        : base(message, innerException)
    {
        CurrentCount = currentCount;
        MaxAllowed = maxAllowed;
    }
}

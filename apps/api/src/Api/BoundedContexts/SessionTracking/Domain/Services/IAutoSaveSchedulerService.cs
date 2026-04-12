namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Service for managing per-session auto-save scheduling.
/// Implementations register/remove Quartz jobs that create periodic checkpoints.
/// </summary>
public interface IAutoSaveSchedulerService
{
    /// <summary>
    /// Registers an auto-save job for the specified session (every 60 seconds).
    /// Idempotent — re-registering an already-scheduled session is a no-op.
    /// </summary>
    /// <param name="sessionId">Session to auto-save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RegisterAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Removes the auto-save job for the specified session.
    /// Idempotent — removing a non-existent job is a no-op.
    /// </summary>
    /// <param name="sessionId">Session to stop auto-saving.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(Guid sessionId, CancellationToken ct = default);
}

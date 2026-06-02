namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Sliding-window rate-limit lookup (Task 12): most recent run started by this admin on this doc.
/// Returns null if no runs within the window.
/// </summary>
public interface IEvaluationRateLimitStore
{
    Task<DateTime?> GetLastStartedAtAsync(Guid docId, Guid adminId, TimeSpan window, CancellationToken ct);
}

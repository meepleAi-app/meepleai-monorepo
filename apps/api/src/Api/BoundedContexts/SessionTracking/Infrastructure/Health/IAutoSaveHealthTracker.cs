namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// Tracks the wall-clock time of the most recent AutoSaveSessionJob execution.
/// Used by a Prometheus gauge to surface stalled background jobs.
/// </summary>
public interface IAutoSaveHealthTracker
{
    /// <summary>
    /// Records that AutoSaveSessionJob just executed (successfully or with a handled error).
    /// </summary>
    void RecordRun();

    /// <summary>
    /// Returns the number of whole seconds elapsed since the last <see cref="RecordRun"/> call.
    /// Returns <see langword="null"/> if <see cref="RecordRun"/> has never been called
    /// (process startup or no active sessions to process).
    /// </summary>
    long? GetLastRunAgeSeconds();
}

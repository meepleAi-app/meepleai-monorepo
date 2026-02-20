using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Configuration for automatic snapshot triggers including debounce settings.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class SnapshotTriggerConfig : ValueObject
{
    private const int MinDebounceDurationSeconds = 1;
    private const int MaxDebounceDurationSeconds = 300;
    private const int MinMaxSnapshotsPerMinute = 1;
    private const int MaxMaxSnapshotsPerMinute = 60;

    public IReadOnlyList<SnapshotTrigger> EnabledTriggers { get; }
    public int DebounceDurationSeconds { get; }
    public int MaxSnapshotsPerMinute { get; }

    public SnapshotTriggerConfig(
        IEnumerable<SnapshotTrigger>? enabledTriggers = null,
        int debounceDurationSeconds = 5,
        int maxSnapshotsPerMinute = 12)
    {
        if (debounceDurationSeconds < MinDebounceDurationSeconds || debounceDurationSeconds > MaxDebounceDurationSeconds)
            throw new ValidationException(
                $"Debounce duration must be between {MinDebounceDurationSeconds} and {MaxDebounceDurationSeconds} seconds");

        if (maxSnapshotsPerMinute < MinMaxSnapshotsPerMinute || maxSnapshotsPerMinute > MaxMaxSnapshotsPerMinute)
            throw new ValidationException(
                $"Max snapshots per minute must be between {MinMaxSnapshotsPerMinute} and {MaxMaxSnapshotsPerMinute}");

        EnabledTriggers = (enabledTriggers?.Distinct().ToList() ?? DefaultTriggers()).AsReadOnly();
        DebounceDurationSeconds = debounceDurationSeconds;
        MaxSnapshotsPerMinute = maxSnapshotsPerMinute;
    }

    /// <summary>
    /// Returns true if the given trigger type is enabled.
    /// </summary>
    public bool IsTriggerEnabled(SnapshotTrigger trigger) =>
        EnabledTriggers.Contains(trigger);

    /// <summary>
    /// Returns true if a snapshot should be created based on debounce timing.
    /// </summary>
    public bool ShouldCreateSnapshot(DateTime? lastSnapshotTimestamp, DateTime now)
    {
        if (!lastSnapshotTimestamp.HasValue)
            return true;

        var elapsed = now - lastSnapshotTimestamp.Value;
        return elapsed.TotalSeconds >= DebounceDurationSeconds;
    }

    /// <summary>
    /// Creates a default configuration with TurnAdvanced and PhaseAdvanced triggers.
    /// </summary>
    public static SnapshotTriggerConfig CreateDefault() =>
        new(DefaultTriggers());

    private static List<SnapshotTrigger> DefaultTriggers() =>
        [SnapshotTrigger.TurnAdvanced, SnapshotTrigger.PhaseAdvanced, SnapshotTrigger.ManualSave];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var trigger in EnabledTriggers.OrderBy(t => t))
            yield return trigger;
        yield return DebounceDurationSeconds;
        yield return MaxSnapshotsPerMinute;
    }
}

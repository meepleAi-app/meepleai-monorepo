using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a completed or in-progress turn in a live session.
/// </summary>
internal sealed class TurnRecord : ValueObject
{
    private const int MaxPhaseNameLength = 100;

    public int TurnIndex { get; }
    public Guid PlayerId { get; }
    public int? PhaseIndex { get; }
    public string? PhaseName { get; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; }
    public TimeSpan? Duration { get; }

    public TurnRecord(
        int turnIndex,
        Guid playerId,
        DateTime startedAt,
        int? phaseIndex = null,
        string? phaseName = null,
        DateTime? endedAt = null)
    {
        if (turnIndex < 0)
            throw new ValidationException("Turn index cannot be negative");

        if (playerId == Guid.Empty)
            throw new ValidationException("Player ID cannot be empty");

        if (phaseName != null)
        {
            var trimmed = phaseName.Trim();
            if (trimmed.Length > MaxPhaseNameLength)
                throw new ValidationException($"Phase name cannot exceed {MaxPhaseNameLength} characters");
            phaseName = trimmed;
        }

        if (endedAt.HasValue && endedAt.Value < startedAt)
            throw new ValidationException("Turn end time cannot be before start time");

        TurnIndex = turnIndex;
        PlayerId = playerId;
        StartedAt = startedAt;
        EndedAt = endedAt;
        PhaseIndex = phaseIndex;
        PhaseName = phaseName;
        Duration = endedAt.HasValue ? endedAt.Value - startedAt : null;
    }

    public bool IsCompleted => EndedAt.HasValue;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TurnIndex;
        yield return PlayerId;
        yield return StartedAt;
    }

    public override string ToString() =>
        IsCompleted
            ? $"Turn {TurnIndex} (Player: {PlayerId}, Duration: {Duration})"
            : $"Turn {TurnIndex} (Player: {PlayerId}, In Progress)";
}

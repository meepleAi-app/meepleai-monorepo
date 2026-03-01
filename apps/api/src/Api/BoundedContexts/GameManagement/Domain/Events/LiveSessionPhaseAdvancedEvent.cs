using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a turn phase advances in a live game session.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class LiveSessionPhaseAdvancedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public int TurnIndex { get; }
    public int NewPhaseIndex { get; }
    public string? PhaseName { get; }
    public int TotalPhases { get; }

    public LiveSessionPhaseAdvancedEvent(
        Guid sessionId,
        int turnIndex,
        int newPhaseIndex,
        string? phaseName,
        int totalPhases)
    {
        SessionId = sessionId;
        TurnIndex = turnIndex;
        NewPhaseIndex = newPhaseIndex;
        PhaseName = phaseName;
        TotalPhases = totalPhases;
    }
}

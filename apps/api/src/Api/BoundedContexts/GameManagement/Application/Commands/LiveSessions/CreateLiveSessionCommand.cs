using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to create a new live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
/// <remarks>
/// #4752 (the game night → live game bridge): <c>ExistingTrackingSessionId</c> and
/// <c>InitialPlayers</c> are for internal callers only — the HTTP request never carries them. A game
/// night has already created the <c>SessionTracking.Session</c> that its link points at, so the live
/// session adopts THAT one as its companion (no second companion), and the night's roster seeds the
/// players in the same commit. The creation stays one funnel (ADR-083): the same handler, the same
/// invariants, only the companion comes from the caller.
/// </remarks>
internal record CreateLiveSessionCommand(
    Guid UserId,
    string GameName,
    Guid? GameId = null,
    PlayRecordVisibility Visibility = PlayRecordVisibility.Private,
    Guid? GroupId = null,
    List<string>? ScoringDimensions = null,
    Dictionary<string, string>? DimensionUnits = null,
    AgentSessionMode AgentMode = AgentSessionMode.None,
    Guid? ExistingTrackingSessionId = null,
    IReadOnlyList<LiveSessionInitialPlayer>? InitialPlayers = null
) : ICommand<Guid>;

/// <summary>
/// A player seeded at creation, in table order: the first one becomes the Host (the domain rule of
/// <c>LiveGameSession.AddPlayer</c>). Colour and a unique display name are assigned by the handler.
/// </summary>
internal sealed record LiveSessionInitialPlayer(Guid? UserId, string DisplayName);

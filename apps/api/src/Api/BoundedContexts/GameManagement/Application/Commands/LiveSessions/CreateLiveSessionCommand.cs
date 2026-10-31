using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to create a new live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
/// <remarks>
/// #4752 (the game night → live game bridge): <c>ExistingTrackingSessionId</c>, <c>InitialPlayers</c> and
/// <c>StartImmediately</c> are for internal callers only — the HTTP request never carries them. A game
/// night has already created the <c>SessionTracking.Session</c> that its link points at, so the live
/// session adopts THAT one as its companion (no second companion), and the night's roster seeds the
/// players in the same commit. The creation stays one funnel (ADR-083): the same handler, the same
/// invariants, only the companion comes from the caller. The handler checks that the caller may adopt
/// it: the session is the caller's own, or belongs to a night the caller organizes.
///
/// <para><c>StartImmediately</c>: starting a game from a night means the table is about to play it, and
/// the front-end has no control that starts a live game (the wizards start theirs right after creating
/// it). Left <c>Created</c>, scores, turns and «Termina partita» all answer 409. The live game is
/// started right after it is created, in the caller's transaction, by sending the very command behind
/// <c>POST /live-sessions/{id}/start</c> — <c>StartLiveSessionCommand</c> — and not by a copy of its
/// rules: the session quota by tier and role, the creator-only guard, the correlated <c>GameSession</c>
/// that holds the quota slot and the history record. The tier and the role are the caller's, read from
/// the user record (as the tracking-session quota check of the night start does). A caller over its
/// quota gets <c>QuotaExceededException</c>, and the caller's transaction takes the creation with it. With
/// no players there is nothing to start (the domain refuses), so the game stays <c>Created</c>.</para>
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
    IReadOnlyList<LiveSessionInitialPlayer>? InitialPlayers = null,
    bool StartImmediately = false
) : ICommand<Guid>;

/// <summary>
/// A player seeded at creation, in table order: the first one becomes the Host (the domain rule of
/// <c>LiveGameSession.AddPlayer</c>). Colour and a unique display name are assigned by the handler.
/// </summary>
internal sealed record LiveSessionInitialPlayer(Guid? UserId, string DisplayName);

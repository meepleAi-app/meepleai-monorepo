using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Idempotent command that ensures a <c>SessionTracking.Session</c> companion exists for a
/// live game session. Intended for legacy sessions created before SP0 (ADR-083) that have
/// <c>TrackingSessionId == null</c>.
/// <para>
/// No-op if the session already has a companion or if <c>GameId == null</c> (free-form sessions
/// cannot have a GameSpecific companion).
/// </para>
/// <para>
/// Returns the <c>TrackingSessionId</c> of the companion after the command completes:
/// <list type="bullet">
///   <item>Non-null → companion exists (pre-existing or just created); stream will have events.</item>
///   <item><c>null</c> → free-form session; no companion was created; stream will be empty.</item>
/// </list>
/// The caller uses this to decide whether to emit the <c>X-Warning-Code: stream-not-linked</c>
/// response header, avoiding a stale warning on the subscribe that first links the session.
/// </para>
/// Dispatched by the <c>GET /live-sessions/{id}/stream</c> endpoint before subscribing, so that
/// the SSE gateway can forward domain events via the companion channel (SP5-c, Issue #2600).
/// </summary>
/// <param name="LiveSessionId">Id of the <see cref="Domain.Entities.LiveGameSession"/> to ensure has a companion.</param>
internal sealed record EnsureCompanionCommand(Guid LiveSessionId) : ICommand<Guid?>;

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// WS1 DEC-1/6 (issue #2647): opens live mode on a tracking <c>Session</c> — sets
/// <c>StartedAt</c> and raises <c>SessionStartedDomainEvent</c> so the GameManagement
/// <c>SessionStartedHandler</c> promotes the parent GameNight Published → InProgress
/// (invariante #15).
///
/// <para>Dispatched by the game-night start / gamebook-attach orchestrators as the
/// LAST step, AFTER the session↔night link is committed, so
/// <c>FindByLinkedSessionIdAsync</c> resolves the parent unambiguously (placing it
/// inside <c>CreateSessionCommandHandler</c> would dispatch the event before the link
/// exists and silently skip the promotion — the original #2647 trap).</para>
///
/// <para>Idempotent: a no-op when the session is already live (the use case spans
/// multiple SaveChanges, so a retry can reach an already-live session).</para>
/// </summary>
internal sealed record OpenSessionLiveModeCommand(Guid SessionId) : ICommand;

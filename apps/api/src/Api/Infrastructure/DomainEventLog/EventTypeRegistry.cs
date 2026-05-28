using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.Infrastructure.DomainEventLog;

/// <summary>
/// Maps <see cref="IDomainEvent"/> CLR types to stable string aliases used in
/// the <c>domain_event_logs.EventType</c> column.
///
/// Issue #661 — opt-in registry. Only events whose type is registered here
/// get persisted to the log table. Everything else continues to flow through
/// MediatR.Publish unchanged (in-memory dispatch only).
///
/// <para><b>Why stable aliases instead of <see cref="Type.FullName"/>?</b>
/// A class rename or namespace move would otherwise silently orphan log rows.
/// The alias is the contract; the CLR type is the implementation.</para>
///
/// <para><b>Why opt-in?</b> 100+ existing <see cref="IDomainEvent"/> implementations
/// would each need a deliberate choice. The pragmatic default is "logged only
/// when explicitly chosen". The deliberate-choice principle from spec panel
/// P0-2 is preserved: an author must add an entry here to get persistence.</para>
/// </summary>
public static class EventTypeRegistry
{
    // Mutable storage exposed read-only through AliasByType. Test helpers can
    // augment it through reflection on this field (see RegisterStubAlias in
    // DomainEventLogPersistenceTests) to verify registry-driven behavior
    // without polluting the production registration.
    private static Dictionary<Type, string> _aliasByType = new()
    {
        // Issue #661 PR-B — UserLibrary events powering the activity feed.
        // Adding a type here makes it durably logged AND dispatched via MediatR.
        // Adding a type does NOT change the existing in-memory dispatch behavior.
        [typeof(GameRemovedFromLibraryEvent)] = "library.entry.removed",
        [typeof(GameSessionRecordedEvent)] = "library.session.recorded",

        // BE-3 #1590 — cross-entity activity feed events (user-facing flows only).
        // H1: agent.created is emitted SOLELY from CreateUserAgentCommand (user flow).
        //     NOT from CreateAgentDefinitionCommand (admin/AI-Lab path).
        [typeof(AgentCreatedEvent)] = "agent.created",

        // H2: chat.session.created matches the real command name (CreateChatSessionCommand).
        //     Alias uses "session" not "thread" — the BE has no CreateChatThreadCommand.
        [typeof(ChatSessionCreatedEvent)] = "chat.session.created",

        // H3: kb.doc.indexed fires ONLY when PdfDocument.TransitionTo(Ready) succeeds.
        //     PdfStateChangedEvent (fires on every transition) remains UNREGISTERED to avoid
        //     log explosion (one row per pipeline step). Decision B3 from #1590 spec panel.
        [typeof(KbDocIndexedEvent)] = "kb.doc.indexed",

        // SessionTracking lifecycle. session.created is orthogonal to the session_events diary
        // "session_created" row (#1590 C3 — different consumers). session.finalized is added in
        // Task 7 (after SessionFinalizedEvent implements IDomainEvent).
        [typeof(SessionCreatedEvent)] = "session.created",
    };

    /// <summary>
    /// Read-only snapshot of the current registration. Tests can augment the
    /// underlying storage via reflection; production code should never mutate.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> AliasByType => _aliasByType;

    /// <summary>
    /// Returns the stable alias for events registered for log persistence,
    /// or <c>null</c> when the event should NOT be logged.
    /// </summary>
    public static string? TryResolve(IDomainEvent ev)
    {
        ArgumentNullException.ThrowIfNull(ev);
        return _aliasByType.TryGetValue(ev.GetType(), out var alias) ? alias : null;
    }
}

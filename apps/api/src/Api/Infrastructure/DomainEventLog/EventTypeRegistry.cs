using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
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
        //     KbDocIndexedEvent stays as the user-meaningful "doc indexed" milestone for the
        //     activity rail / Activity Feed.
        [typeof(KbDocIndexedEvent)] = "kb.doc.indexed",

        // Issue #2245 (epic #2242 Sub #3) — register every PDF state transition so the
        // admin LiveEventLog SSE stream can render real-time pipeline progress without
        // the FE polling /api/v1/pdfs/{id} on a 2s tick.
        //
        // NOTE — revisits B3 (#1590 spec panel): the original decision left this UNREGISTERED
        // to avoid log explosion (one row per pipeline step × 6 transitions per PDF).
        // The trade-off is now accepted because: (a) the SSE consumer eliminates polling
        // load, (b) the activity-feed query path already filters by event_type, and
        // (c) DomainEventOutboxRetentionService prunes old rows beyond the retention window.
        // If the row volume becomes a problem, the mitigation is to add an EventBroadcastFilter
        // entry that drops the persistence step for this alias while keeping the SSE fanout.
        [typeof(PdfStateChangedEvent)] = "pdf.state.changed",

        // SessionTracking lifecycle. session.created is orthogonal to the session_events diary
        // "session_created" row (#1590 C3 — different consumers). session.finalized also (re)wires
        // the previously-dormant KnowledgeBase SessionFinalizedEventHandler cascade cleanup (the
        // event was raised for SSE only, never into the MediatR pipeline, until BE-3).
        [typeof(SessionCreatedEvent)] = "session.created",
        [typeof(SessionFinalizedEvent)] = "session.finalized",

        // Issue #1687 — durable log of user-driven KB-doc metadata edits.
        // Without this entry the cache-invalidation handler still fires (MediatR
        // in-memory) but the audit row is silently dropped. Tested explicitly in
        // EventTypeRegistryTests.Registry_resolves_pdf_metadata_changed_alias.
        [typeof(PdfMetadataChangedEvent)] = "pdf.metadata.changed",

        // Issue #1840 SP5 F4-C7 — alert lifecycle events powering the
        // AlertActivityFeed (SSE) in /admin/monitor?tab=alerts. The aliases
        // are durable identifiers: a rename of the CLR type would orphan
        // historical log rows otherwise. Tested in EventTypeRegistryTests.
        [typeof(AlertFiredEvent)] = "alert.fired",
        [typeof(AlertResolvedEvent)] = "alert.resolved",

        // Issue #2494 AC-5 — Mechanic Extractor cost cap state changes.
        // Both admin overrides (OverrunCause=AdminOverride) and pipeline
        // mid-stream overrun detections (OverrunCause=MidStreamOverrun) are
        // durably logged so the audit trail covers both budget intent (admin)
        // and budget breach (system).
        [typeof(MechanicAnalysisCostCapOverriddenEvent)] = "mechanic.analysis.cost_cap.overridden",
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

    /// <summary>
    /// Single-source-of-truth helper for the <c>event_type</c> Prometheus label and
    /// the <c>domain_event_outbox.event_type</c> column. Returns the registered alias
    /// when available, otherwise the CLR <see cref="Type.FullName"/>.
    ///
    /// <para><b>Why FullName and not <c>Type.Name</c>?</b> Name collides across
    /// namespaces (two different bounded contexts can both define an <c>AlertFiredEvent</c>);
    /// FullName disambiguates and survives namespace moves more gracefully. Issue #1535 T6
    /// code review flagged that the DbContext's two event_type emissions (outbox enqueue +
    /// legacy DomainEventLog dispatch-failure) were using different fallbacks (FullName vs
    /// Name) → dashboard JOINs broke for unregistered events. This helper unifies them.</para>
    /// </summary>
    public static string ResolveOrFullName(IDomainEvent ev)
    {
        ArgumentNullException.ThrowIfNull(ev);
        var clrType = ev.GetType();
        return _aliasByType.TryGetValue(clrType, out var alias)
            ? alias
            : (clrType.FullName ?? clrType.Name);
    }
}

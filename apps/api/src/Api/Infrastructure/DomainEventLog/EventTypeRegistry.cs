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
        // PR-A ships infrastructure with an empty registry. PR-B adds:
        //   [typeof(LibraryEntryRemovedEvent)] = "library.entry.removed",
        //   [typeof(GameSessionRecordedEvent)] = "library.session.recorded",
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

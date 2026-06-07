using System.Collections.Frozen;
using System.Reflection;
using Api.Infrastructure.DomainEventLog;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Default implementation of <see cref="IDomainEventTypeResolver"/> (issue #1535).
///
/// <para>Built once at DI bootstrap by scanning the API assembly for
/// <see cref="IDomainEvent"/> implementations. The two lookup dictionaries are
/// frozen — read-only, lock-free, and hot-path safe for the processor's
/// poll-and-dispatch loop.</para>
/// </summary>
internal sealed class DomainEventTypeResolver : IDomainEventTypeResolver
{
    private readonly FrozenDictionary<string, Type> _byAlias;
    private readonly FrozenDictionary<string, Type> _byFullName;

    public DomainEventTypeResolver()
    {
        // F13: the resolver assumes ALL IDomainEvent implementations live in the same
        // assembly as the IDomainEvent interface itself (the Api project). This is the
        // intended monorepo invariant: bounded contexts are folders, not separate
        // assemblies. If a future BC extraction breaks this invariant, the assertion at
        // the end of the constructor fails fast at app startup — preventing the silent
        // dead-letter cascade where cross-assembly events would write outbox rows that
        // resolve to null and immediately MarkFailed.
        Type[] eventTypes;
        try
        {
            eventTypes = typeof(IDomainEvent).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract
                    && !t.IsInterface
                    && typeof(IDomainEvent).IsAssignableFrom(t))
                .ToArray();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            // Defensive: any analyzer-generated type with a missing reference would crash
            // the entire app startup with an opaque stack. Surface the partial-load state
            // and continue with the resolvable subset (better than no resolver at all).
            eventTypes = ex.Types
                .Where(t => t is not null
                    && !t.IsAbstract
                    && !t.IsInterface
                    && typeof(IDomainEvent).IsAssignableFrom(t))
                .Cast<Type>()
                .ToArray();
        }

        // Registry alias takes precedence: the team has explicitly committed to
        // these names as durable identifiers (#661 P0-2). Renaming the CLR class
        // does NOT change the persisted alias.
        _byAlias = EventTypeRegistry.AliasByType
            .ToFrozenDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);

        // Long-tail fallback for events not (yet) enrolled in the registry.
        // FullName collisions are impossible in a single assembly; the
        // ToFrozenDictionary call enforces that at startup.
        _byFullName = eventTypes
            .Where(t => t.FullName is not null)
            .ToFrozenDictionary(t => t.FullName!, t => t, StringComparer.Ordinal);

        // F13 startup invariant: every registered alias MUST resolve through the scanned
        // type set. A non-empty diff means an alias was registered for a type that lives
        // in another assembly (or was deleted) — both cases would silently dead-letter at
        // dispatch time. Fail fast at app startup with the offending entries.
        var scannedTypes = new HashSet<Type>(eventTypes);
        var unresolvedAliases = _byAlias
            .Where(kvp => !scannedTypes.Contains(kvp.Value))
            .Select(kvp => $"{kvp.Key} → {kvp.Value.FullName ?? kvp.Value.Name}")
            .ToList();
        if (unresolvedAliases.Count > 0)
        {
            throw new InvalidOperationException(
                "EventTypeRegistry contains aliases whose CLR types are not in the Api " +
                "assembly — DomainEventTypeResolver cannot resolve them at dispatch time, " +
                "leading to silent dead-letter. Offending entries: " +
                string.Join(", ", unresolvedAliases) +
                ". Either move the event types into Api or extend the resolver to scan " +
                "additional assemblies.");
        }
    }

    /// <inheritdoc />
    public Type? Resolve(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return null;
        }

        if (_byAlias.TryGetValue(eventType, out var byAlias))
        {
            return byAlias;
        }

        return _byFullName.GetValueOrDefault(eventType);
    }
}

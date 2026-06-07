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
        var eventTypes = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract
                && !t.IsInterface
                && typeof(IDomainEvent).IsAssignableFrom(t))
            .ToArray();

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

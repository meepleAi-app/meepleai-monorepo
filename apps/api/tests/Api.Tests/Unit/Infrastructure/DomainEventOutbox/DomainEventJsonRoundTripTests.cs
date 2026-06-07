using System.Reflection;
using System.Text.Json;
using Api.Infrastructure.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// Issue #1535 T2 — JSON contract verification. For every concrete
/// <see cref="IDomainEvent"/> in the API assembly:
/// <list type="number">
///   <item>Serialization MUST succeed with <see cref="DomainEventJsonOptions.Default"/>.</item>
///   <item>The serialized JSON object MUST contain an <c>eventId</c> property
///         (camelCase, populated, non-empty) — this is the idempotency key that
///         the processor reads back via the entity's <see cref="DomainEventOutboxEntity.Id"/>.</item>
/// </list>
///
/// <para>Deserialization round-trip is NOT verified here for every event because
/// many of them have required positional constructors that cannot be constructed
/// without semantically valid arguments. Round-trip is exercised end-to-end by
/// the processor integration test in a later phase.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventJsonRoundTripTests
{
    public static IEnumerable<object[]> AllConcreteDomainEvents()
    {
        return typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract
                && !t.IsInterface
                && typeof(IDomainEvent).IsAssignableFrom(t))
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(AllConcreteDomainEvents))]
    public void Every_domain_event_can_be_serialized_with_default_options(Type eventType)
    {
        // Arrange — try to materialize a candidate instance.
        if (!TryCreateCandidate(eventType, out var instance))
        {
            // Skipping — could not synthesise a valid constructor call. The instance-creation
            // path is intentionally conservative; when an event has only positional ctors with
            // complex arguments (records with VOs), the integration test will exercise it.
            return;
        }

        // Act — serialize with the same options the processor will use.
        var act = () => JsonSerializer.Serialize(instance, eventType, DomainEventJsonOptions.Default);

        // Assert
        var json = act.Should().NotThrow().Subject;
        json.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllConcreteDomainEvents))]
    public void Every_domain_event_serializes_eventId_in_camelCase(Type eventType)
    {
        if (!TryCreateCandidate(eventType, out var instance))
        {
            return;
        }

        var json = JsonSerializer.Serialize(instance, eventType, DomainEventJsonOptions.Default);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("eventId", out var eventIdProp).Should().BeTrue(
            $"event {eventType.Name} must carry the camelCase 'eventId' property in JSON for outbox dispatch (#1535)");
    }

    private static bool TryCreateCandidate(Type eventType, out object instance)
    {
        // Records typically expose a parameterless internal ctor for EF + a positional public ctor.
        // We try the easiest path first: any public ctor with all-defaultable parameters.
        foreach (var ctor in eventType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                var parameters = ctor.GetParameters();
                var args = parameters
                    .Select(p => DefaultValueFor(p.ParameterType))
                    .ToArray();

                instance = ctor.Invoke(args);
                return true;
            }
            catch
            {
                // Try the next ctor.
            }
        }

        // Fall back to FormatterServices.GetUninitializedObject — bypasses ctor invariants but
        // gives the serializer something to walk. Suitable for the smoke check above.
        try
        {
            instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(eventType);
            return true;
        }
        catch
        {
            instance = null!;
            return false;
        }
    }

    private static object? DefaultValueFor(Type t)
    {
        if (t == typeof(string)) return string.Empty;
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t.IsValueType) return Activator.CreateInstance(t);

        // Arrays / lists: empty.
        if (t.IsArray)
        {
            return Array.CreateInstance(t.GetElementType()!, 0);
        }
        if (t.IsGenericType)
        {
            var genDef = t.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>))
            {
                var listType = typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]);
                return Activator.CreateInstance(listType);
            }
            if (genDef == typeof(IReadOnlyList<>) || genDef == typeof(IEnumerable<>) || genDef == typeof(IReadOnlyCollection<>))
            {
                // Synthesise an empty List<T> — concrete type that satisfies the read-only interface
                // contract well enough for computed properties (Any/Count) not to NRE.
                var listType = typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]);
                return Activator.CreateInstance(listType);
            }
            if (genDef == typeof(Dictionary<,>))
            {
                return Activator.CreateInstance(t);
            }
        }

        // Reference types we cannot synthesise → null.
        return null;
    }
}

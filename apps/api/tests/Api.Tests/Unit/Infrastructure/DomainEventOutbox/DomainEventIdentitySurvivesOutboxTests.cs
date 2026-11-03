using System.Reflection;
using System.Text.Json;
using Api.Infrastructure.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// #4841 — l'outbox salva l'evento come JSON e lo ricostruisce con
/// <see cref="DomainEventJsonOptions.Default"/>; la riga ha <c>Id = ev.EventId</c>. Ogni dedupe chiavato
/// sull'<c>EventId</c> (<c>LastProcessedEventId</c>, <c>SourceEventId</c> sotto indici univoci parziali,
/// CF-2 / #1938) funziona solo se la ricostruzione rende lo STESSO id. Prima di #4841 una proprietà
/// <c>{ get; }</c> senza setter faceva rigenerare <c>Guid.NewGuid()</c> al costruttore a ogni consegna.
///
/// <para>Per ogni <see cref="IDomainEvent"/> concreto:</para>
/// <list type="bullet">
///   <item>se si riesce a costruirlo con argomenti di default, il giro serializza → deserializza deve
///         restituire lo stesso <c>EventId</c> e lo stesso <c>OccurredAt</c> (misura);</item>
///   <item>altrimenti (un costruttore con guardie sugli argomenti) si controlla la forma: la proprietà
///         ha un setter, o il costruttore ha un parametro omonimo (che il deserializzatore usa).</item>
/// </list>
/// Nessun evento salta il controllo: o è misurato, o ne è controllata la forma.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventIdentitySurvivesOutboxTests
{
    public static IEnumerable<object[]> AllConcreteDomainEvents() =>
        typeof(IDomainEvent).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IDomainEvent).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllConcreteDomainEvents))]
    public void EventId_and_OccurredAt_survive_the_outbox_round_trip(Type eventType)
    {
        if (!TryConstruct(eventType, out var original))
        {
            AssertShapeSurvivesDeserialization(eventType, "EventId");
            AssertShapeSurvivesDeserialization(eventType, "OccurredAt");
            return;
        }

        var before = (IDomainEvent)original;
        before.EventId.Should().NotBe(Guid.Empty, "una misura con l'id vuoto non distingue «conservato» da «rigenerato»");

        var json = JsonSerializer.Serialize(original, eventType, DomainEventJsonOptions.Default);
        var after = (IDomainEvent)JsonSerializer.Deserialize(json, eventType, DomainEventJsonOptions.Default)!;

        after.EventId.Should().Be(before.EventId,
            $"{eventType.Name}: l'outbox ricostruisce l'evento dal JSON e un EventId nuovo rende debole ogni dedupe (#4841)");
        after.OccurredAt.Ticks.Should().Be(before.OccurredAt.Ticks,
            $"{eventType.Name}: l'istante in cui è avvenuto non deve diventare l'istante della consegna (#4841)");
    }

    [Fact]
    public void Control__a_get_only_event_regenerates_its_id_so_the_measurement_can_fail()
    {
        // Senza questo controllo il Theory sopra potrebbe passare anche se non misurasse niente.
        var original = new GetOnlyEvent();
        var json = JsonSerializer.Serialize(original, DomainEventJsonOptions.Default);
        var rebuilt = JsonSerializer.Deserialize<GetOnlyEvent>(json, DomainEventJsonOptions.Default)!;

        rebuilt.EventId.Should().NotBe(original.EventId);
    }

    private sealed class GetOnlyEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    private static void AssertShapeSurvivesDeserialization(Type eventType, string propertyName)
    {
        var property = eventType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull($"{eventType.Name} espone {propertyName} tramite IDomainEvent");

        var hasSetter = property!.SetMethod is { IsPublic: true };
        var hasCtorParameter = eventType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(c => c.GetParameters().Any(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && p.ParameterType == property.PropertyType));

        (hasSetter || hasCtorParameter).Should().BeTrue(
            $"{eventType.Name}.{propertyName} non ha né setter né parametro di costruttore omonimo: il deserializzatore dell'outbox rigenera il valore (#4841)");
    }

    private static bool TryConstruct(Type eventType, out object instance)
    {
        foreach (var ctor in eventType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                     .OrderByDescending(c => c.GetParameters().Length))
        {
            try
            {
                instance = ctor.Invoke(ctor.GetParameters().Select(p => DefaultFor(p.ParameterType)).ToArray());
                return true;
            }
            catch (Exception ex) when (ex is TargetInvocationException or ArgumentException or InvalidCastException)
            {
                // costruttore con guardie sugli argomenti: prova il successivo
            }
        }

        instance = null!;
        return false;
    }

    private static object? DefaultFor(Type t)
    {
        if (t == typeof(string)) return "x";
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 0);

        if (t.IsGenericType)
        {
            var definition = t.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>)
                || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>))
            {
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]));
            }

            if (definition == typeof(Dictionary<,>))
            {
                return Activator.CreateInstance(t);
            }
        }

        return null;
    }
}

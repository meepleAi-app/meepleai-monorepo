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

    // ── The shape check for events the test cannot construct ─────────────────────────────────────

    public sealed class Unbuildable;

    /// <summary>Cannot be built with default arguments, and takes "occurredAt" as a constructor parameter.</summary>
    private sealed class UnbuildableWithOccurredAtParameter : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; }

        public UnbuildableWithOccurredAtParameter(Unbuildable dependency, DateTime occurredAt)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            OccurredAt = occurredAt;
        }
    }

    private sealed class UnbuildableWithOnlyTheSetter : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

        public UnbuildableWithOnlyTheSetter(Unbuildable dependency) => ArgumentNullException.ThrowIfNull(dependency);
    }

    [Fact]
    public void Shape_check__an_unbuildable_event_with_a_constructor_parameter_named_like_the_property_is_rejected()
    {
        TryConstruct(typeof(UnbuildableWithOccurredAtParameter), out _).Should().BeFalse("the premise: it cannot be measured");

        var act = () => AssertShapeSurvivesDeserialization(typeof(UnbuildableWithOccurredAtParameter), "OccurredAt");

        act.Should().Throw<Exception>("whether the constructor assigns the parameter cannot be seen from the shape");
    }

    [Fact]
    public void Shape_check__an_unbuildable_event_with_only_the_setter_is_accepted()
    {
        TryConstruct(typeof(UnbuildableWithOnlyTheSetter), out _).Should().BeFalse("the premise: it cannot be measured");

        AssertShapeSurvivesDeserialization(typeof(UnbuildableWithOnlyTheSetter), "EventId");
        AssertShapeSurvivesDeserialization(typeof(UnbuildableWithOnlyTheSetter), "OccurredAt");
    }

    [Fact]
    public void Shape_check__an_unbuildable_event_without_a_setter_is_rejected()
    {
        var act = () => AssertShapeSurvivesDeserialization(typeof(GetOnlyEvent), "EventId");

        act.Should().Throw<Exception>();
    }

    private static void AssertShapeSurvivesDeserialization(Type eventType, string propertyName)
    {
        var property = eventType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull($"{eventType.Name} espone {propertyName} tramite IDomainEvent");

        var hasSetter = property!.SetMethod is { IsPublic: true };

        // A constructor parameter with the property's name takes the JSON value away from the setter, and
        // whether the constructor then assigns it cannot be seen from the shape (#4841: GameStateChangedEvent
        // and GameSessionRecordedEvent took "occurredAt" and dropped it). Such an event must be constructible
        // here, so that the round trip above measures it.
        var shadowedByCtorParameter = eventType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(c => c.GetParameters().Any(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase)));

        hasSetter.Should().BeTrue(
            $"{eventType.Name}.{propertyName} non ha un setter pubblico (nemmeno init): il deserializzatore dell'outbox rigenera il valore (#4841)");
        shadowedByCtorParameter.Should().BeFalse(
            $"{eventType.Name} ha un parametro di costruttore chiamato come {propertyName} e non si riesce a costruirlo con argomenti di default: " +
            "non si può sapere se lo assegna. Rendilo costruibile nel test (DefaultFor) così il giro lo misura");
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

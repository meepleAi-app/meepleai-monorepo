using System.Text.Json;
using System.Text.Json.Nodes;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// #4608: gli eventi di configurazione sopravvivono al viaggio dell'outbox, riga compresa.
/// </summary>
/// <remarks>
/// <para>
/// In produzione (<c>OutboxOnly</c>) un handler non riceve l'istanza che l'aggregato ha creato: la riceve
/// <c>DomainEventOutboxProcessor</c>, che risolve il tipo dal valore di <c>EventType</c> (<c>DomainEventTypeResolver</c>)
/// e deserializza <c>PayloadJson</c> con <see cref="DomainEventJsonOptions.Default"/>. Se la deserializzazione lancia,
/// la riga va in retry e poi in <c>Failed</c>, e nessun handler dell'evento parte mai. In <c>Hybrid</c> (sviluppo)
/// la pubblicazione in linea usa l'istanza originale e non passa da qui: un difetto del viaggio si vede solo
/// fuori dallo sviluppo.
/// </para>
/// <para>
/// <c>DomainEventJsonRoundTripTests</c> verifica la sola serializzazione, e dichiara che il viaggio di ritorno non
/// lo prova per ogni evento. Questi quattro eventi portano un <see cref="ConfigKey"/>, un oggetto valore con
/// un solo costruttore pubblico: è il caso che non si vede dalla sola serializzazione.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4608")]
public sealed class ConfigurationEventsJsonRoundTripTests
{
    private static readonly Guid ConfigId = new("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid UserId = new("00000000-0000-0000-0000-0000000000d1");
    private static ConfigKey Key => new("RateLimit:Admin.max-tokens_1");

    // Il nome, non l'istanza: xUnit mostra un caso per nome, e un evento interno non si può passare come argomento.
    public static TheoryData<string> EventNames() => new()
    {
        nameof(ConfigurationCreatedEvent),
        nameof(ConfigurationUpdatedEvent),
        nameof(ConfigurationToggledEvent),
        nameof(ConfigurationDeletedEvent),
    };

    private static IDomainEvent Build(string eventName) => eventName switch
    {
        nameof(ConfigurationCreatedEvent) =>
            new ConfigurationCreatedEvent(ConfigId, Key, "100", "int", "RateLimit", "Production", requiresRestart: true, UserId),
        nameof(ConfigurationUpdatedEvent) =>
            new ConfigurationUpdatedEvent(ConfigId, Key, previousValue: "100", newValue: "200", version: 3, UserId),
        nameof(ConfigurationToggledEvent) =>
            new ConfigurationToggledEvent(ConfigId, Key, isActive: false, UserId),
        nameof(ConfigurationDeletedEvent) =>
            new ConfigurationDeletedEvent(ConfigId, Key, "RateLimit", "Production"),
        _ => throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null),
    };

    private static ConfigKey KeyOf(IDomainEvent e) => e switch
    {
        ConfigurationCreatedEvent c => c.Key,
        ConfigurationUpdatedEvent u => u.Key,
        ConfigurationToggledEvent t => t.Key,
        ConfigurationDeletedEvent d => d.Key,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e.GetType().Name, "evento non previsto da questo test"),
    };

    private static string Payload(IDomainEvent e)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(e, e.GetType(), DomainEventJsonOptions.Default))!.AsObject();
        node.Remove("eventId");
        node.Remove("occurredAt");
        return node.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(EventNames))]
    public void TheEventComesBackFromTheOutboxAsItWentIn(string eventName)
    {
        var original = Build(eventName);

        // Come la scrive MeepleAiDbContext.EnqueueOutboxRows...
        var eventType = EventTypeRegistry.ResolveOrFullName(original);
        var payloadJson = JsonSerializer.Serialize(original, original.GetType(), DomainEventJsonOptions.Default);

        // ...e come la legge DomainEventOutboxProcessor.
        var clrType = new DomainEventTypeResolver().Resolve(eventType);
        clrType.Should().Be(original.GetType(), "the processor resolves the stored EventType to the event class");
        var restored = (IDomainEvent?)JsonSerializer.Deserialize(payloadJson, clrType!, DomainEventJsonOptions.Default);

        restored.Should().NotBeNull();
        // Confronto sul JSON, senza l'identità dell'istanza: un'istanza nuova genera un EventId suo (il processore
        // usa l'id della riga). Non uso BeEquivalentTo: sul tipo dichiarato (IDomainEvent) vedrebbe solo EventId e
        // OccurredAt, cioè niente.
        var expected = Payload(original);
        expected.Should().Contain("RateLimit:Admin.max-tokens_1", "setup: the payload carries the key");
        Payload(restored!).Should().Be(expected, "every property of the event must survive the outbox");
        KeyOf(restored!).Value.Should().Be(KeyOf(original).Value, "the key is what every handler of the event reads first");
    }

    [Fact]
    public void TheKeyIsStillWrittenAsAnObjectWithValueAndCategory()
    {
        // Il JSON che l'outbox scrive (e che l'audit copia in Details) non deve cambiare: la correzione di #4608 tocca
        // solo come si legge. Se cambia la forma, le righe già salvate smettono di essere leggibili.
        var deleted = Build(nameof(ConfigurationDeletedEvent));
        var json = JsonNode.Parse(JsonSerializer.Serialize(deleted, deleted.GetType(), DomainEventJsonOptions.Default))!;

        var key = json["key"]!.AsObject();
        key["value"]!.GetValue<string>().Should().Be("RateLimit:Admin.max-tokens_1");
        key["category"]!.GetValue<string>().Should().Be("RateLimit");
        key.Count.Should().Be(2);
    }

    [Fact]
    public void ARowAlreadyInTheOutbox_StillDeserializes()
    {
        // Così com'era scritta una riga di ConfigurationDeletedEvent prima della correzione: se nella tabella ci sono
        // righe Failed per questo motivo, il riavvio dall'amministrazione le deve poter leggere.
        const string stored =
            """
            {"configurationId":"00000000-0000-0000-0000-0000000000c1","key":{"value":"RateLimit:Admin.max-tokens_1","category":"RateLimit"},"category":"RateLimit","environment":"Production","occurredAt":"2026-10-01T10:00:00Z","eventId":"11111111-1111-1111-1111-111111111111"}
            """;

        var restored = JsonSerializer.Deserialize<ConfigurationDeletedEvent>(stored, DomainEventJsonOptions.Default);

        restored.Should().NotBeNull();
        restored!.ConfigurationId.Should().Be(ConfigId);
        restored.Key.Value.Should().Be("RateLimit:Admin.max-tokens_1");
        restored.Category.Should().Be("RateLimit");
        restored.Environment.Should().Be("Production");
    }
}

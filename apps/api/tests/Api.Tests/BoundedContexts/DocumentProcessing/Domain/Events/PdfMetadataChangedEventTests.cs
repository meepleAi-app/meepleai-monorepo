using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Issue #1687 Task 3 — unit tests for the new <see cref="PdfMetadataChangedEvent"/>.
///
/// The mapper conventions (P117) tested here are exactly the contract the
/// existing DomainEventLogMapper consumes via reflection:
/// 1. <c>typeof(Event).Name.Replace("Event", "")</c> == aggregate_type column value.
/// 2. <c>JsonSerializer.Serialize(event, type, PropertyNamingPolicy.CamelCase)</c> ==
///    payload_json column value.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1687")]
public class PdfMetadataChangedEventTests
{
    private static readonly Guid AggregateId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid GameId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public void Event_implements_IDomainEvent()
    {
        var ev = CreateEvent();

        ev.Should().BeAssignableTo<IDomainEvent>(
            "P116: record:IDomainEvent (NOT DomainEventBase class) preserves immutability");
        ev.EventId.Should().NotBe(Guid.Empty);
        ev.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void AggregateType_via_mapper_convention_is_PdfMetadataChanged()
    {
        var typeName = typeof(PdfMetadataChangedEvent).Name;
        var convention = typeName.EndsWith("Event", StringComparison.Ordinal)
            ? typeName[..^"Event".Length]
            : typeName;

        convention.Should().Be("PdfMetadataChanged",
            "P117 mapper convention: class name minus 'Event' suffix → aggregate_type column");
    }

    [Fact]
    public void Payload_serializes_with_camelCase_keys()
    {
        var ev = new PdfMetadataChangedEvent(
            AggregateId: AggregateId,
            UserId: EditorId,
            EditorRole: "Owner",
            Changes: new[] { new MetadataChange("title", null, "Catan 5th Ed") },
            GameId: GameId);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(ev, ev.GetType(), options);

        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        keys.Should().Contain("aggregateId");
        keys.Should().Contain("userId");
        keys.Should().Contain("editorRole");
        keys.Should().Contain("changes");
        keys.Should().Contain("gameId");
        keys.Should().Contain("requiresReindex");
        keys.Should().Contain("occurredAt");
        keys.Should().Contain("eventId");

        // Inner MetadataChange must also use camelCase
        var firstChange = doc.RootElement.GetProperty("changes")[0];
        firstChange.EnumerateObject().Select(p => p.Name).Should()
            .Contain(new[] { "field", "oldValue", "newValue" });
    }

    [Fact]
    public void RequiresReindex_isTrue_when_changes_contain_documentType()
    {
        var ev = new PdfMetadataChangedEvent(
            AggregateId, EditorId, "Owner",
            new[] { new MetadataChange("documentType", "Rulebook", "QuickStart") },
            GameId);

        ev.RequiresReindex.Should().BeTrue("category change affects RAG pipeline routing (D-12 hint)");
    }

    [Fact]
    public void RequiresReindex_isTrue_when_changes_contain_language()
    {
        var ev = new PdfMetadataChangedEvent(
            AggregateId, EditorId, "Admin",
            new[] { new MetadataChange("language", "en", "it") },
            GameId: null);

        ev.RequiresReindex.Should().BeTrue("language change affects embedding model selection");
    }

    [Fact]
    public void RequiresReindex_isFalse_when_changes_contain_only_title_or_tags()
    {
        var ev = new PdfMetadataChangedEvent(
            AggregateId, EditorId, "Owner",
            new[]
            {
                new MetadataChange("title", "Old", "New"),
                new MetadataChange("tags", "[]", "[\"strategy\"]")
            },
            GameId);

        ev.RequiresReindex.Should().BeFalse(
            "title/tags do not impact RAG: D-12 defers re-indexing entirely");
    }

    [Fact]
    public void AggregateId_property_exists_for_mapper_reflection()
    {
        // The DomainEventLogMapper reflects on the "AggregateId" property by name
        // (see ExtractWellKnown). A rename would silently orphan log rows.
        var prop = typeof(PdfMetadataChangedEvent).GetProperty("AggregateId");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void UserId_property_exists_for_mapper_reflection()
    {
        var prop = typeof(PdfMetadataChangedEvent).GetProperty("UserId");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(Guid));
    }

    private static PdfMetadataChangedEvent CreateEvent()
    {
        return new PdfMetadataChangedEvent(
            AggregateId: AggregateId,
            UserId: EditorId,
            EditorRole: "Owner",
            Changes: Array.Empty<MetadataChange>(),
            GameId: GameId);
    }
}

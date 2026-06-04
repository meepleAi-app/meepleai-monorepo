using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedProvenanceTests
{
    [Fact]
    public void FieldProvenance_RecordEquality()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var a = new FieldProvenance("wikidata", "https://wd/Q1", "labels.en", fetchedAt, "Catan");
        var b = new FieldProvenance("wikidata", "https://wd/Q1", "labels.en", fetchedAt, "Catan");
        a.Should().Be(b);
    }

    [Fact]
    public void Builder_AppliesPrimaryThenFallback()
    {
        var fetchedAt = DateTime.UtcNow;
        var primary = new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("wikidata", "u1", "f1", fetchedAt, "Catan"),
        };
        var fallback = new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("bgg", "u2", "f2", fetchedAt, "Settlers of Catan"),
            ["mechanics"] = new("bgg", "u3", "f3", fetchedAt, new[] { "Trading" }),
        };

        var merged = CatalogSeedProvenance.Merge(primary, fallback);

        merged.GetValue<string>("title").Should().Be("Catan");           // primary wins
        merged.GetProvider("title").Should().Be("wikidata");
        merged.GetValue<string[]>("mechanics").Should().BeEquivalentTo(new[] { "Trading" });
        merged.GetProvider("mechanics").Should().Be("bgg");
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var p = new CatalogSeedProvenance(new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("wikidata", "u1", "f1", fetchedAt, "Catan"),
        });
        var json = p.ToJson();
        var roundTripped = CatalogSeedProvenance.FromJson(json);
        roundTripped.GetValue<string>("title").Should().Be("Catan");
    }
}

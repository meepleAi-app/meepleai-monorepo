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

    [Fact]
    public void FromJson_ThrowsOnNullOrWhitespace()
    {
        Action a = () => CatalogSeedProvenance.FromJson(null!);
        a.Should().Throw<ArgumentException>();
        Action b = () => CatalogSeedProvenance.FromJson("   ");
        b.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Merge_EmptyPrimary_ReturnsFallbackContents()
    {
        var fetchedAt = DateTime.UtcNow;
        var primary = new Dictionary<string, FieldProvenance>();
        var fallback = new Dictionary<string, FieldProvenance>
        {
            ["mechanics"] = new("bgg", "u", "f", fetchedAt, new List<string> { "Worker Placement" }),
        };

        var merged = CatalogSeedProvenance.Merge(primary, fallback);

        merged.GetValue<List<string>>("mechanics").Should().BeEquivalentTo(new[] { "Worker Placement" });
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesListValues()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var p = new CatalogSeedProvenance(new Dictionary<string, FieldProvenance>
        {
            ["mechanics"] = new("bgg", "u1", "f1", fetchedAt, new List<string> { "Trading", "Modular Board" }),
        });
        var json = p.ToJson();
        var rt = CatalogSeedProvenance.FromJson(json);

        var rehydrated = rt.GetValue<List<string>>("mechanics");
        rehydrated.Should().NotBeNull();
        rehydrated.Should().BeEquivalentTo(new[] { "Trading", "Modular Board" });
    }

    [Fact]
    public void FieldProvenance_RecordEquality_OnCollectionValue()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var a = new FieldProvenance("bgg", "u", "f", fetchedAt, new List<string> { "A", "B" });
        var b = new FieldProvenance("bgg", "u", "f", fetchedAt, new List<string> { "A", "B" });
        a.Should().Be(b);

        var c = new FieldProvenance("bgg", "u", "f", fetchedAt, new List<string> { "A", "C" });
        a.Should().NotBe(c);
    }
}

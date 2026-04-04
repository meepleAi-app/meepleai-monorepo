using Api.Infrastructure.Seeders.Catalog;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedManifestLoaderTests
{
    [Fact]
    public void LoadFromString_ParsesValidYaml()
    {
        var yaml = """
            games:
              - title: "Catan"
                bggId: 13
                language: en
                pdfFile: "catan_rulebook.pdf"
                imageUrl: "https://example.com/catan.jpg"
                thumbnailUrl: "https://example.com/catan_thumb.jpg"
              - title: "Chess"
                language: it
                pdfFile: "chess_rulebook.pdf"
            """;

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Equal(2, manifest.Games.Count);
        Assert.Equal("Catan", manifest.Games[0].Title);
        Assert.Equal(13, manifest.Games[0].BggId);
        Assert.Equal("en", manifest.Games[0].Language);
        Assert.Equal("Chess", manifest.Games[1].Title);
        Assert.Null(manifest.Games[1].BggId);
        Assert.Equal("it", manifest.Games[1].Language);
    }

    [Fact]
    public void LoadFromString_EmptyYaml_ReturnsEmptyList()
    {
        var yaml = "games: []";

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Empty(manifest.Games);
    }

    [Fact]
    public void LoadFromString_MissingOptionalFields_UsesDefaults()
    {
        var yaml = """
            games:
              - title: "Test Game"
            """;

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Single(manifest.Games);
        Assert.Equal("en", manifest.Games[0].Language);
        Assert.Null(manifest.Games[0].BggId);
        Assert.Null(manifest.Games[0].ImageUrl);
    }

    [Fact]
    public void Validate_RejectsEmptyTitle()
    {
        var manifest = new SharedGamesManifest
        {
            Games = [new GameManifestEntry { Title = "" }]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("title"));
    }

    [Fact]
    public void Validate_RejectsDuplicateTitles()
    {
        var manifest = new SharedGamesManifest
        {
            Games =
            [
                new GameManifestEntry { Title = "Catan" },
                new GameManifestEntry { Title = "Catan" }
            ]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AcceptsValidManifest()
    {
        var manifest = new SharedGamesManifest
        {
            Games =
            [
                new GameManifestEntry { Title = "Catan", BggId = 13 },
                new GameManifestEntry { Title = "Chess" }
            ]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.Empty(errors);
    }
}

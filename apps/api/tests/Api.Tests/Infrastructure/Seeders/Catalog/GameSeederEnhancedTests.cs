using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class GameSeederEnhancedTests
{
    [Fact]
    public void CreateFromEnhancedData_MapsAllFieldsExceptCovers()
    {
        // Issue #2123 — BGG ToS compliance: cover URLs are NEVER seeded inline.
        // ImageUrl/ThumbnailUrl are always null on every create path. Covers
        // are resolved at runtime via CoverUrlResolver from R2 assets.
        var entry = new SeedManifestGame
        {
            Title = "Catan",
            BggId = 13,
            Language = "en",
            Description = "Trade and build on the island of Catan",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTime = 120,
            MinAge = 10,
            AverageRating = 7.1,
            AverageWeight = 2.32,
            RulesUrl = "https://www.catan.com/rules.pdf"
        };
        var systemUserId = Guid.NewGuid();

        var result = GameSeeder.CreateFromEnhancedData(entry, systemUserId);

        result.BggId.Should().Be(13);
        result.Title.Should().Be("Catan");
        result.Description.Should().Be("Trade and build on the island of Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(120);
        result.MinAge.Should().Be(10);
        result.AverageRating.Should().Be(7.1m);
        result.ComplexityRating.Should().Be(2.32m);
        result.ImageUrl.Should().BeNull("issue #2123 — covers never seeded inline");
        result.ThumbnailUrl.Should().BeNull("issue #2123 — covers never seeded inline");
        result.RulesExternalUrl.Should().Be("https://www.catan.com/rules.pdf");
        result.RulesLanguage.Should().Be("en");
        result.CreatedBy.Should().Be(systemUserId);
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void CreateFromEnhancedData_FallsBackToDefaults_WhenFieldsMissing()
    {
        var entry = new SeedManifestGame
        {
            Title = "Unknown Game",
            BggId = 99999,
            Language = "it",
            Description = "A game"
        };
        var systemUserId = Guid.NewGuid();

        var result = GameSeeder.CreateFromEnhancedData(entry, systemUserId);

        result.YearPublished.Should().Be(2020);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(60);
        result.MinAge.Should().Be(10);
        result.RulesExternalUrl.Should().BeNull();
        result.ImageUrl.Should().BeNull("issue #2123 — covers never seeded inline");
        result.ThumbnailUrl.Should().BeNull("issue #2123 — covers never seeded inline");
    }
}

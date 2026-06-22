using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Models;
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

    // Issue #2272 — BGG returns AverageRating=0 for games with no votes (e.g. Navy Battle
    // Card Game bgg_id=338111). The Postgres constraint chk_shared_games_rating rejects
    // 0 ("average_rating IS NULL OR (>= 1.0 AND <= 10.0)"), so 0 MUST be normalized to null.
    [Fact]
    public void CreateFromBggData_NormalizesAverageRatingZeroToNull_Issue2272()
    {
        var bgg = new BggGameDetailsDto(
            BggId: 338111,
            Name: "Navy Battle Card Game",
            Description: null,
            YearPublished: 1957,
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTime: 30,
            MinPlayTime: 20,
            MaxPlayTime: 40,
            MinAge: 10,
            AverageRating: 0.0,
            BayesAverageRating: 0.0,
            UsersRated: 0,
            AverageWeight: 0.0,
            ThumbnailUrl: null,
            ImageUrl: null,
            Categories: new List<string>(),
            Mechanics: new List<string>(),
            Designers: new List<string>(),
            Publishers: new List<string>());

        var result = GameSeeder.CreateFromBggData(bgg, "en", Guid.NewGuid());

        result.AverageRating.Should().BeNull(
            "BGG sentinel 0 means 'no votes' and would violate chk_shared_games_rating");
        result.ComplexityRating.Should().BeNull(
            "BGG sentinel 0 means 'no weight votes' and would violate chk_shared_games_complexity");
    }

    [Fact]
    public void CreateFromBggData_PreservesPositiveAverageRating()
    {
        var bgg = new BggGameDetailsDto(
            BggId: 13,
            Name: "Catan",
            Description: "Trade and build",
            YearPublished: 1995,
            MinPlayers: 3,
            MaxPlayers: 4,
            PlayingTime: 120,
            MinPlayTime: 60,
            MaxPlayTime: 120,
            MinAge: 10,
            AverageRating: 7.1,
            BayesAverageRating: 6.9,
            UsersRated: 1000,
            AverageWeight: 2.32,
            ThumbnailUrl: null,
            ImageUrl: null,
            Categories: new List<string>(),
            Mechanics: new List<string>(),
            Designers: new List<string>(),
            Publishers: new List<string>());

        var result = GameSeeder.CreateFromBggData(bgg, "en", Guid.NewGuid());

        result.AverageRating.Should().Be(7.1m);
        result.ComplexityRating.Should().Be(2.32m);
    }

    [Fact]
    public void CreateFromEnhancedData_NormalizesAverageRatingZeroToNull_Issue2272()
    {
        // Defensive: same sentinel-0 pattern in the enhanced-YAML path.
        var entry = new SeedManifestGame
        {
            Title = "Navy Battle Card Game",
            BggId = 338111,
            Language = "en",
            Description = "Old wargame",
            YearPublished = 1957,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 30,
            MinAge = 10,
            AverageRating = 0.0,
            AverageWeight = 0.0
        };

        var result = GameSeeder.CreateFromEnhancedData(entry, Guid.NewGuid());

        result.AverageRating.Should().BeNull(
            "manifest sentinel 0 means 'no votes' and would violate chk_shared_games_rating");
        result.ComplexityRating.Should().BeNull(
            "manifest sentinel 0 means 'no weight votes' and would violate chk_shared_games_complexity");
    }
}

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
    public void CreateFromEnhancedData_MapsAllFields()
    {
        var entry = new SeedManifestGame
        {
            Title = "Catan",
            BggId = 13,
            Language = "en",
            BggEnhanced = true,
            Description = "Trade and build on the island of Catan",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTime = 120,
            MinAge = 10,
            AverageRating = 7.1,
            AverageWeight = 2.32,
            ImageUrl = "https://cf.geekdo-images.com/catan.jpg",
            ThumbnailUrl = "https://cf.geekdo-images.com/catan_thumb.jpg",
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
        result.ImageUrl.Should().Be("https://cf.geekdo-images.com/catan.jpg");
        result.ThumbnailUrl.Should().Be("https://cf.geekdo-images.com/catan_thumb.jpg");
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
            BggEnhanced = true,
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
    }
}

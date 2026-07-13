using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

// Issue #2904: SharedGame ids must be deterministic (derived from bggId/title) so that
// re-seeding after a DB wipe reuses the same id — preventing orphan PDFs and preserving
// embeddings across deploys (#964).
[Trait("Category", TestCategories.Unit)]
public sealed class GameSeederDeterministicIdTests
{
    [Fact]
    public void GenerateDeterministicGameId_SameBggId_ReturnsSameGuid()
    {
        var a = GameSeeder.GenerateDeterministicGameId(13, "Catan");
        var b = GameSeeder.GenerateDeterministicGameId(13, "Catan");
        a.Should().Be(b);
    }

    [Fact]
    public void GenerateDeterministicGameId_DifferentBggId_ReturnsDifferentGuid()
    {
        var a = GameSeeder.GenerateDeterministicGameId(13, "Catan");
        var b = GameSeeder.GenerateDeterministicGameId(9209, "Ticket to Ride");
        a.Should().NotBe(b);
    }

    [Fact]
    public void GenerateDeterministicGameId_BggIdWins_OverTitle()
    {
        // Same bggId but a drifting/renamed title must still resolve to the same id.
        var a = GameSeeder.GenerateDeterministicGameId(13, "Catan");
        var b = GameSeeder.GenerateDeterministicGameId(13, "Settlers of Catan");
        a.Should().Be(b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void GenerateDeterministicGameId_NoBggId_FallsBackToTitle_CaseInsensitive(int? bggId)
    {
        var a = GameSeeder.GenerateDeterministicGameId(bggId, "Custom Homebrew");
        var b = GameSeeder.GenerateDeterministicGameId(bggId, "  custom homebrew  ");
        a.Should().Be(b, "title fallback is trimmed + lowercased for stability");
    }

    [Fact]
    public void GenerateDeterministicGameId_IsNotEmpty()
    {
        GameSeeder.GenerateDeterministicGameId(13, "Catan").Should().NotBe(Guid.Empty);
        GameSeeder.GenerateDeterministicGameId(null, "X").Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateFromEnhancedData_IsIdempotent_SameEntryYieldsSameId()
    {
        var systemUserId = Guid.NewGuid();
        var entry = new SeedManifestGame { Title = "Wingspan", BggId = 266192, Language = "en" };

        var first = GameSeeder.CreateFromEnhancedData(entry, systemUserId);
        var second = GameSeeder.CreateFromEnhancedData(entry, systemUserId);

        first.Id.Should().Be(second.Id, "re-seeding the same manifest game must reuse the same SharedGame id");
        first.Id.Should().Be(GameSeeder.GenerateDeterministicGameId(entry.BggId, entry.Title));
    }
}

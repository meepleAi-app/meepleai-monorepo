using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Issue #2123 — end-to-end ToS compliance assertion: the seed-time data plane
/// (manifest YAML → SeedManifestLoader → GameSeeder → shared_games table) does
/// NOT introduce any BGG-hosted URL into <c>image_url</c> / <c>thumbnail_url</c>,
/// regardless of:
/// <list type="bullet">
///   <item>Whether the manifest file <i>happens</i> to contain a BGG URL on disk
///   (would be silently ignored by <see cref="SeedManifestLoader"/> after the
///   Phase A property removal).</item>
///   <item>Whether the live BGG API mock returns a BGG <c>imageUrl</c> field
///   (must be discarded by <see cref="GameSeeder.CreateFromBggData"/>).</item>
/// </list>
/// Pairs with the unit-test schema guards in <see cref="GameSeederEnhancedTests"/>
/// and <see cref="SeedManifestGameSchemaTests"/> for compile-time enforcement.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupA")]
public sealed class BggToSComplianceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fx;
    private string _dbName = string.Empty;
    private string _connStr = string.Empty;

    public BggToSComplianceIntegrationTests(SharedTestcontainersFixture fx) => _fx = fx;

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_bgg_tos_{Guid.NewGuid():N}";
        _connStr = await _fx.CreateIsolatedDatabaseAsync(_dbName);
        await using var ctx = _fx.CreateDbContext(_connStr);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _fx.DropIsolatedDatabaseAsync(_dbName);

    public static IEnumerable<object[]> ProfileCases() => new[]
    {
        new object[] { SeedProfile.Dev,     (string?)null }, // dev.yml
        new object[] { SeedProfile.Staging, (string?)null }, // staging.yml
        new object[] { SeedProfile.Prod,    (string?)null }, // prod.yml
        new object[] { SeedProfile.None,    (string?)"ci" }, // ci.yml via name override
    };

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public async Task SeedFromManifest_PersistsZeroBggHostedUrls_AcrossAllProfiles(
        SeedProfile profile, string? manifestNameOverride)
    {
        var manifest = CatalogSeeder.LoadManifest(profile, manifestNameOverride);

        // Defensive: the unit test ManifestValidationTests already covers loader-level
        // contract; we re-load here against the actual file on disk so a manual
        // edit that re-introduces an `imageUrl: cf.geekdo-…` line is also caught at
        // integration time (via the BGG-pattern SQL guard at the bottom).
        manifest.Catalog.Games.Should().NotBeEmpty(
            $"profile {profile} (override='{manifestNameOverride ?? "(default)"}') must have at least one game entry");

        await using var ctx = _fx.CreateDbContext(_connStr);

        // Mock BGG API: critically, returns BGG-hosted ImageUrl values for every
        // entry — the seeder MUST discard them per issue #2123 Phase A surgery.
        var bggMock = new Mock<IBggApiService>();
        bggMock
            .Setup(b => b.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int bggId, CancellationToken _) => new BggGameDetailsDto(
                BggId: bggId,
                Name: $"Mock BGG Game {bggId}",
                Description: "Mocked",
                YearPublished: 2020,
                MinPlayers: 2,
                MaxPlayers: 4,
                PlayingTime: 60,
                MinPlayTime: null,
                MaxPlayTime: null,
                MinAge: 10,
                AverageRating: null,
                BayesAverageRating: null,
                UsersRated: null,
                AverageWeight: null,
                ThumbnailUrl: "https://cf.geekdo-images.com/should-be-discarded-thumb.jpg",
                ImageUrl: "https://cf.geekdo-images.com/should-be-discarded.jpg",
                Categories: Array.Empty<string>(),
                Mechanics: Array.Empty<string>(),
                Designers: Array.Empty<string>(),
                Publishers: Array.Empty<string>()));

        var systemUserId = Guid.NewGuid();
        await GameSeeder.SeedAsync(
            ctx, bggMock.Object, systemUserId, manifest,
            NullLogger.Instance, CancellationToken.None);

        // SQL-level assertion: no BGG host substring landed in either cover column.
        var pollutedCount = await ctx.SharedGames.CountAsync(g =>
            (g.ImageUrl != null && (EF.Functions.ILike(g.ImageUrl, "%geekdo%") || EF.Functions.ILike(g.ImageUrl, "%boardgamegeek%"))) ||
            (g.ThumbnailUrl != null && (EF.Functions.ILike(g.ThumbnailUrl, "%geekdo%") || EF.Functions.ILike(g.ThumbnailUrl, "%boardgamegeek%"))));

        pollutedCount.Should().Be(0,
            $"profile {profile}: GameSeeder MUST NOT persist any BGG-hosted URL into shared_games (issue #2123)");
    }

    [Fact]
    public async Task NullifyMigration_StatementsNullifyLegacyBggRows()
    {
        // Simulate a pre-migration row carrying a legacy BGG URL by inserting via
        // EF Core (so EF handles column quoting + DB defaults), then run the same
        // nullify UPDATE statements the migration's Up() executes and assert
        // they zero out the BGG-hosted columns without touching anything else.
        await using var ctx = _fx.CreateDbContext(_connStr);

        var legacyId = Guid.NewGuid();
        var systemUserId = Guid.NewGuid();
        ctx.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = legacyId,
            Title = "Legacy Catan",
            YearPublished = 1995,
            Description = "Legacy desc",
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            ImageUrl = "https://cf.geekdo-images.com/catan.jpg",
            ThumbnailUrl = "https://cf.geekdo-images.com/catan-thumb.jpg",
            Status = 1,
            GameDataStatus = 5,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsRagPublic = false,
            HasKnowledgeBase = false
        });

        // Add a clean (non-BGG) sibling row that must survive the UPDATE untouched.
        var cleanId = Guid.NewGuid();
        ctx.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = cleanId,
            Title = "Clean Patchwork",
            YearPublished = 2014,
            Description = "Clean desc",
            MinPlayers = 2,
            MaxPlayers = 2,
            PlayingTimeMinutes = 30,
            MinAge = 8,
            ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/patchwork.jpg",
            ThumbnailUrl = null,
            Status = 1,
            GameDataStatus = 5,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            IsRagPublic = false,
            HasKnowledgeBase = false
        });
        await ctx.SaveChangesAsync();

        // Run the migration's nullify UPDATE statements directly (idempotent).
        await ctx.Database.ExecuteSqlRawAsync(@"
            UPDATE shared_games SET image_url = NULL
            WHERE image_url ILIKE '%geekdo%' OR image_url ILIKE '%boardgamegeek%';
            UPDATE shared_games SET thumbnail_url = NULL
            WHERE thumbnail_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%boardgamegeek%';
        ");

        var legacy = await ctx.SharedGames.AsNoTracking().FirstAsync(g => g.Id == legacyId);
        legacy.ImageUrl.Should().BeNull();
        legacy.ThumbnailUrl.Should().BeNull();

        var clean = await ctx.SharedGames.AsNoTracking().FirstAsync(g => g.Id == cleanId);
        clean.ImageUrl.Should().Be("https://upload.wikimedia.org/wikipedia/commons/patchwork.jpg",
            "Wikimedia-hosted URLs are legitimate and must NOT be nullified");
    }
}

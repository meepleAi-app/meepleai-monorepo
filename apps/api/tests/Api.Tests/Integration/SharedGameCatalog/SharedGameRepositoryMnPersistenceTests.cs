using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Issue #3153 — Testcontainers integration coverage for
/// <see cref="SharedGameRepository.AddAsync(SharedGame, IReadOnlyList{string}, IReadOnlyList{string}, System.Threading.CancellationToken)"/>
/// designer/publisher M:N get-or-create persistence. A mocked-repo unit test cannot
/// exercise <c>MapToEntity</c> / the resolver (that is exactly how the #3147 gap
/// shipped); every assertion here reads the real join rows on a FRESH DbContext.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupC")]
public sealed class SharedGameRepositoryMnPersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fx;
    private string _dbName = string.Empty;
    private string _connStr = string.Empty;

    public SharedGameRepositoryMnPersistenceTests(SharedTestcontainersFixture fx) => _fx = fx;

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_sharedgame_mn_{Guid.NewGuid():N}";
        _connStr = await _fx.CreateIsolatedDatabaseAsync(_dbName);
        await using var ctx = _fx.CreateDbContext(_connStr);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _fx.DropIsolatedDatabaseAsync(_dbName);

    private static SharedGame NewGame(string title) =>
        SharedGame.Create(
            title: title, yearPublished: 1995, description: "Trade & build",
            minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10,
            complexityRating: 2.3m, averageRating: 7.1m,
            imageUrl: "https://example.com/i.jpg", thumbnailUrl: "https://example.com/t.jpg",
            rules: null, createdBy: Guid.NewGuid());

    private async Task<Guid> AddGameWithMnAsync(
        string title, IReadOnlyList<string> designers, IReadOnlyList<string> publishers)
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new SharedGameRepository(ctx, new DomainEventCollector());
        var game = NewGame(title);
        await repo.AddAsync(game, designers, publishers, default);
        await ctx.SaveChangesAsync();
        return game.Id;
    }

    private async Task<(List<string> designers, List<string> publishers)> ReadMnAsync(Guid gameId)
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var game = await ctx.SharedGames
            .AsNoTracking()
            .Include(g => g.Designers)
            .Include(g => g.Publishers)
            .FirstAsync(g => g.Id == gameId);
        return (game.Designers.Select(d => d.Name).OrderBy(n => n).ToList(),
                game.Publishers.Select(p => p.Name).OrderBy(n => n).ToList());
    }

    private async Task<(int designers, int publishers)> MasterCountsAsync()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        return (await ctx.GameDesigners.CountAsync(), await ctx.GamePublishers.CountAsync());
    }

    // ── T1 — new designers + publishers persist as join rows ──────────────
    [Fact]
    public async Task AddAsync_NewDesignersAndPublishers_PersistJoinRows()
    {
        var id = await AddGameWithMnAsync("Catan", new[] { "Klaus Teuber" }, new[] { "Kosmos" });

        var (designers, publishers) = await ReadMnAsync(id);
        designers.Should().Equal("Klaus Teuber");
        publishers.Should().Equal("Kosmos");
        (await MasterCountsAsync()).Should().Be((1, 1));
    }

    // ── T2 — existing designer reused (no duplicate master row, no unique violation) ──
    [Fact]
    public async Task AddAsync_ExistingDesignerName_ReusedNotDuplicated()
    {
        var first = await AddGameWithMnAsync("Catan", new[] { "Klaus Teuber" }, Array.Empty<string>());
        var second = await AddGameWithMnAsync("Catan Junior", new[] { "Klaus Teuber" }, Array.Empty<string>());

        (await MasterCountsAsync()).designers.Should().Be(1, "the same name must resolve to one shared row");
        (await ReadMnAsync(first)).designers.Should().Equal("Klaus Teuber");
        (await ReadMnAsync(second)).designers.Should().Equal("Klaus Teuber");
    }

    // ── T3 — case-insensitive reuse; existing casing authoritative ────────
    [Fact]
    public async Task AddAsync_CaseVariantName_ReusesExistingRow()
    {
        await AddGameWithMnAsync("Catan", new[] { "Klaus Teuber" }, Array.Empty<string>());
        var id2 = await AddGameWithMnAsync("Seafarers", new[] { "klaus teuber" }, Array.Empty<string>());

        (await MasterCountsAsync()).designers.Should().Be(1, "case-insensitive dedup via lower(name)");
        (await ReadMnAsync(id2)).designers.Should().Equal(new[] { "Klaus Teuber" }); // existing row's casing preserved
    }

    // ── T4 — within-list dup + mixed case collapses to one join row ───────
    [Fact]
    public async Task AddAsync_WithinListDuplicateMixedCase_SingleJoinRow()
    {
        var id = await AddGameWithMnAsync(
            "Catan", new[] { "Klaus Teuber", "klaus teuber", "KLAUS TEUBER" }, Array.Empty<string>());

        (await ReadMnAsync(id)).designers.Should().Equal("Klaus Teuber");
        (await MasterCountsAsync()).designers.Should().Be(1);
    }

    // ── T5 — mixed new + existing ─────────────────────────────────────────
    [Fact]
    public async Task AddAsync_MixedNewAndExisting_ResolvesBoth()
    {
        await AddGameWithMnAsync("Catan", new[] { "Klaus Teuber" }, Array.Empty<string>());
        var id2 = await AddGameWithMnAsync("Ticket", new[] { "Klaus Teuber", "Alan R. Moon" }, Array.Empty<string>());

        (await ReadMnAsync(id2)).designers.Should().Equal("Alan R. Moon", "Klaus Teuber");
        (await MasterCountsAsync()).designers.Should().Be(2, "one reused + one created");
    }

    // ── T6 — leniency: whitespace / empty / >200-char skipped, never throws ──
    [Fact]
    public async Task AddAsync_MalformedNames_SkippedNotThrown()
    {
        var overlong = new string('x', 201);
        Guid id = Guid.Empty;
        var act = async () =>
        {
            id = await AddGameWithMnAsync(
                "Catan", new[] { "Reiner Knizia", "   ", "", overlong, "reiner knizia" }, Array.Empty<string>());
        };

        await act.Should().NotThrowAsync();
        (await ReadMnAsync(id)).designers.Should().Equal("Reiner Knizia");
        (await MasterCountsAsync()).designers.Should().Be(1);
    }

    // ── T7 — empty lists persist zero rows, no crash ──────────────────────
    [Fact]
    public async Task AddAsync_EmptyLists_NoJoinRows()
    {
        var id = await AddGameWithMnAsync("Pandemic", Array.Empty<string>(), Array.Empty<string>());

        var (designers, publishers) = await ReadMnAsync(id);
        designers.Should().BeEmpty();
        publishers.Should().BeEmpty();
        (await MasterCountsAsync()).Should().Be((0, 0));
    }

    // ── T9 — publisher parity (reuse works for publishers too) ────────────
    [Fact]
    public async Task AddAsync_ExistingPublisherCaseVariant_Reused()
    {
        await AddGameWithMnAsync("Catan", Array.Empty<string>(), new[] { "Franckh-Kosmos" });
        var id2 = await AddGameWithMnAsync("Seafarers", Array.Empty<string>(), new[] { "franckh-kosmos" });

        (await MasterCountsAsync()).publishers.Should().Be(1);
        (await ReadMnAsync(id2)).publishers.Should().Equal("Franckh-Kosmos");
    }
}

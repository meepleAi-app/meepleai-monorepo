using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Issue #3153 — end-to-end Testcontainers coverage of
/// <see cref="CatalogSeedApprovedEventHandler"/> through the REAL repository +
/// UnitOfWork + Postgres, proving (a) designer/publisher M:N persist through the
/// handler→repo→DB path via the real ProvenanceJson→FromJson round-trip, and
/// (b) the D7 idempotency guard prevents duplicate materialisation on re-dispatch.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupC")]
public sealed class CatalogSeedApprovedEventHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fx;
    private string _dbName = string.Empty;
    private string _connStr = string.Empty;

    public CatalogSeedApprovedEventHandlerIntegrationTests(SharedTestcontainersFixture fx) => _fx = fx;

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_catseedapproved_{Guid.NewGuid():N}";
        _connStr = await _fx.CreateIsolatedDatabaseAsync(_dbName);
        await using var ctx = _fx.CreateDbContext(_connStr);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _fx.DropIsolatedDatabaseAsync(_dbName);

    private static string RichProvenance(string title, int? bggId, string[] designers, string[] publishers)
    {
        const string qidUrl = "https://www.wikidata.org/wiki/Q17271";
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new("wikidata", qidUrl, "labels.en", DateTime.UtcNow, title),
            ["yearPublished"] = new("wikidata", qidUrl, "P577", DateTime.UtcNow, 1995),
            ["minPlayers"] = new("wikidata", qidUrl, "P1872", DateTime.UtcNow, 3),
            ["maxPlayers"] = new("wikidata", qidUrl, "P1873", DateTime.UtcNow, 4),
            ["designers"] = new("wikidata", qidUrl, "P178", DateTime.UtcNow, designers.ToList()),
            ["publishers"] = new("wikidata", qidUrl, "P123", DateTime.UtcNow, publishers.ToList()),
        };
        return new CatalogSeedProvenance(fields).ToJson();
    }

    private async Task<(Guid draftId, Guid placeholderId)> InsertApprovedDraftAsync(
        int? bggId, string title, string[] designers, string[] publishers)
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var placeholder = Guid.NewGuid();
        var draft = new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = bggId,
            Status = "Approved",
            ProvenanceJson = RichProvenance(title, bggId, designers, publishers),
            ResultingSharedGameId = placeholder, // M4.4 placeholder
            ApprovedAt = DateTime.UtcNow,
            ApprovedByUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Set<CatalogSeedDraftEntity>().Add(draft);
        await ctx.SaveChangesAsync();
        return (draft.Id, placeholder);
    }

    // Each dispatch runs in its own DbContext scope, mirroring the real per-request scope.
    private async Task DispatchAsync(CatalogSeedApprovedEvent evt)
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var handler = new CatalogSeedApprovedEventHandler(
            new CatalogSeedDraftRepository(ctx),
            new SharedGameRepository(ctx, new DomainEventCollector()),
            new EfCoreUnitOfWork(ctx),
            TimeProvider.System,
            NullLogger<CatalogSeedApprovedEventHandler>.Instance);
        await handler.Handle(evt, default);
    }

    // ── T10 — double-dispatch of a pure-Wikidata (no-BggId) draft yields exactly
    //          one game + one set of join rows (D7 idempotency, end-to-end). ─────
    [Fact]
    public async Task Handle_DoubleDispatch_NoBggId_MaterialisesExactlyOnce()
    {
        var (draftId, placeholder) = await InsertApprovedDraftAsync(
            bggId: null, "Catan", new[] { "Klaus Teuber" }, new[] { "Kosmos" });
        var evt = new CatalogSeedApprovedEvent(draftId, placeholder, Guid.NewGuid());

        await DispatchAsync(evt);
        await DispatchAsync(evt); // re-dispatch must be a no-op

        await using var ctx = _fx.CreateDbContext(_connStr);
        (await ctx.SharedGames.CountAsync()).Should().Be(1, "the D7 guard must prevent a duplicate materialisation");
        var game = await ctx.SharedGames.Include(g => g.Designers).Include(g => g.Publishers)
            .SingleAsync();
        game.YearPublished.Should().Be(1995);
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.Designers.Select(d => d.Name).Should().Equal("Klaus Teuber");
        game.Publishers.Select(p => p.Name).Should().Equal("Kosmos");
        (await ctx.GameDesigners.CountAsync()).Should().Be(1);
        (await ctx.GamePublishers.CountAsync()).Should().Be(1);
    }

    // ── T12 — existing-game (BggId collision) branch leaves the collision game's
    //          designers untouched; the Wikidata names are NOT applied (D5). ─────
    [Fact]
    public async Task Handle_ExistingGameCollision_LeavesDesignersUntouched()
    {
        // Seed an existing game with a curated designer + the colliding BggId.
        var existingId = Guid.NewGuid();
        await using (var seed = _fx.CreateDbContext(_connStr))
        {
            var repo = new SharedGameRepository(seed, new DomainEventCollector());
            var game = SharedGame.Create(
                title: "Catan", yearPublished: 1995, description: "d",
                minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, minAge: 10,
                complexityRating: 2.3m, averageRating: 7.1m,
                imageUrl: "https://e/i.jpg", thumbnailUrl: "https://e/t.jpg",
                rules: null, createdBy: Guid.NewGuid(), bggId: 13);
            existingId = game.Id;
            await repo.AddAsync(game, new[] { "Curated Designer" }, Array.Empty<string>(), default);
            await seed.SaveChangesAsync();
        }

        var (draftId, placeholder) = await InsertApprovedDraftAsync(
            bggId: 13, "Catan", new[] { "Klaus Teuber" }, new[] { "Kosmos" });
        await DispatchAsync(new CatalogSeedApprovedEvent(draftId, placeholder, Guid.NewGuid()));

        await using var ctx = _fx.CreateDbContext(_connStr);
        (await ctx.SharedGames.CountAsync()).Should().Be(1, "collision reuses the existing game, no new game");
        var game2 = await ctx.SharedGames.Include(g => g.Designers).SingleAsync();
        game2.Id.Should().Be(existingId);
        // existing game's designers must NOT be clobbered by the seed (BGG queue #1874 owns enrichment)
        game2.Designers.Select(d => d.Name).Should().Equal(new[] { "Curated Designer" });
    }
}

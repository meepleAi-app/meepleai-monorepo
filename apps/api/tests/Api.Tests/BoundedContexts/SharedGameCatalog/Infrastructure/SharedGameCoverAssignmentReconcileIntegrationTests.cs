using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Epic #3470 Slice 1c-2 — integration coverage for the per-context cover
/// assignment child-collection reconcile on real Postgres. Proves add / modify /
/// remove all persist correctly and, crucially, that a child NEW since the load
/// is inserted (not silently lost) — the <c>ef-detached-graph-child-loss</c>
/// pitfall the InMemory provider hides.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCoverAssignmentReconcileIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ISharedGameRepository _repository = null!;
    private static readonly Guid AdminId = Guid.NewGuid();

    public SharedGameCoverAssignmentReconcileIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"cover_reconcile_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            // Mirror production (InfrastructureServiceExtensions.cs, PERF-06): the global
            // default is NoTracking, so the reconcile MUST opt back into tracking to persist
            // in-place updates. Without this line the harness would use EF's TrackAll default
            // and hide lost-update bugs (notracking-default-update-gotcha).
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();
        _repository = new SharedGameRepository(_dbContext, eventCollectorMock.Object);
    }

    public async ValueTask DisposeAsync() => await _dbContext.DisposeAsync();

    private static SharedGame NewGame() => SharedGame.Create(
        title: "Catan",
        yearPublished: 1995,
        description: "Trade, build, settle",
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        minAge: 10,
        complexityRating: 2.5m,
        averageRating: 7.8m,
        imageUrl: "https://example.com/c.jpg",
        thumbnailUrl: "https://example.com/c-thumb.jpg",
        rules: null,
        createdBy: AdminId);

    private async Task<Guid> SeedGameAsync()
    {
        var game = NewGame();
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return game.Id;
    }

    [Fact]
    public async Task Reconcile_AddsModifiesAndRemovesChildren_OnPostgres()
    {
        var gameId = await SeedGameAsync();

        // Add two assignments.
        var g1 = await _repository.GetByIdAsync(gameId);
        g1!.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.2, 0.3);
        g1.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);
        await _repository.ReconcileCoverAssignmentsAsync(g1);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterAdd = await _repository.GetByIdAsync(gameId);
        afterAdd!.CoverAssignments.Should().HaveCount(2);
        afterAdd.CoverAssignments.Single(a => a.Context == CoverContext.Card)
            .Source.Should().Be(CoverAssignmentSource.Wikidata);

        // Modify Card's source AND add a brand-new Social child in the same save.
        // The pre-existing Card/Hero rows must survive and Social must be inserted —
        // this is exactly the new-child-loss scenario the InMemory provider hides.
        var g2 = await _repository.GetByIdAsync(gameId);
        g2!.AssignCover(CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);
        g2.AssignCover(CoverContext.Social, CoverAssignmentSource.Bgg, AdminId);
        await _repository.ReconcileCoverAssignmentsAsync(g2);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterModify = await _repository.GetByIdAsync(gameId);
        afterModify!.CoverAssignments.Should().HaveCount(3);
        afterModify.CoverAssignments.Single(a => a.Context == CoverContext.Card)
            .Source.Should().Be(CoverAssignmentSource.Pdf);
        afterModify.CoverAssignments.Select(a => a.Context)
            .Should().Contain(CoverContext.Social);

        // Remove Hero.
        var g3 = await _repository.GetByIdAsync(gameId);
        g3!.RemoveCoverAssignment(CoverContext.Hero);
        await _repository.ReconcileCoverAssignmentsAsync(g3);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterRemove = await _repository.GetByIdAsync(gameId);
        afterRemove!.CoverAssignments.Select(a => a.Context)
            .Should().BeEquivalentTo(new[] { CoverContext.Card, CoverContext.Social });
    }

    [Fact]
    public async Task Reconcile_PersistsFocalPointAndAudit()
    {
        var gameId = await SeedGameAsync();

        var g = await _repository.GetByIdAsync(gameId);
        g!.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.33, 0.66);
        await _repository.ReconcileCoverAssignmentsAsync(g);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(gameId);
        var persisted = reloaded!.CoverAssignments.Single();
        persisted.FocalX.Should().Be(0.33);
        persisted.FocalY.Should().Be(0.66);
        persisted.CreatedBy.Should().Be(AdminId);
        persisted.SharedGameId.Should().Be(gameId);
    }

    [Fact]
    public async Task GetById_WithMultipleDesignersAndAssignments_DoesNotDuplicate_CartesianGuard()
    {
        // Seed a game with TWO designers so GetByIdAsync eager-loads two collections
        // (Designers M:N + CoverAssignments 1:N). Without AsSplitQuery the NoTracking
        // cartesian product would hydrate each cover assignment once PER designer —
        // 2 designers × 2 assignments = 4 phantom assignment rows. This guards the fix.
        var game = NewGame();
        await _repository.AddAsync(
            game,
            new[] { "Klaus Teuber", "Benjamin Teuber" },
            Array.Empty<string>());
        await _dbContext.SaveChangesAsync();
        var gameId = game.Id;
        _dbContext.ChangeTracker.Clear();

        var loaded = await _repository.GetByIdAsync(gameId);
        loaded!.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        loaded.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);
        await _repository.ReconcileCoverAssignmentsAsync(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(gameId);
        reloaded!.CoverAssignments.Should().HaveCount(2, "two collection includes must not duplicate via a cartesian product");
        reloaded.Designers.Should().HaveCount(2);
    }

    [Fact]
    public async Task RevokeManualCover_Cascades_NullsColumnsAndRemovesManualAssignment_OnPostgres()
    {
        // Slice 3f: revoking a manual cover must persist BOTH the nulled ManualCover* scalar
        // columns (via Update) AND the removal of the Manual-pinned assignment row (via the
        // child-safe Reconcile), while a non-Manual pin survives. Proves the Update + Reconcile
        // composition on real Postgres (InMemory would hide a lost scalar write or child delete).
        var gameId = await SeedGameAsync();

        // Set a manual cover + pin Manual to Hero and Wikidata to Card.
        var g1 = await _repository.GetByIdAsync(gameId);
        g1!.SetManualCover("covers/manual/x/cover", "CC-BY-4.0", "Jane Artist", "https://s", AdminId, DateTime.UtcNow);
        g1.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);
        g1.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        _repository.Update(g1);
        await _repository.ReconcileCoverAssignmentsAsync(g1);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterSet = await _repository.GetByIdAsync(gameId);
        afterSet!.ManualCoverR2Key.Should().Be("covers/manual/x/cover");
        afterSet.CoverAssignments.Should().HaveCount(2);

        // Revoke: nulls the manual columns AND drops the Manual assignment (cascade).
        var g2 = await _repository.GetByIdAsync(gameId);
        g2!.RevokeManualCover().Should().BeTrue();
        _repository.Update(g2);
        await _repository.ReconcileCoverAssignmentsAsync(g2);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterRevoke = await _repository.GetByIdAsync(gameId);
        afterRevoke!.ManualCoverR2Key.Should().BeNull("the scalar revoke persisted via Update");
        afterRevoke.ManualCoverLicense.Should().BeNull();
        afterRevoke.ManualCoverAttribution.Should().BeNull();
        afterRevoke.CoverAssignments.Should().ContainSingle("the Manual pin was cascade-removed; Wikidata survives");
        afterRevoke.CoverAssignments.Single().Source.Should().Be(CoverAssignmentSource.Wikidata);
    }

    /// <summary>
    /// #3615 — the crop invalidation must reach the DATABASE, not just the in-memory aggregate.
    ///
    /// The regeneration paths call <c>Update()</c>, which maps scalars only, so an invalidation
    /// applied to the child collection would be silently dropped: the domain would look right and
    /// the stale crop would keep being served. Only a round-trip through real Postgres proves it.
    /// </summary>
    [Fact]
    public async Task InvalidateGeneratedCrops_ClearsOnlyTheMatchingSource_OnPostgres()
    {
        var gameId = await SeedGameAsync();

        var g = await _repository.GetByIdAsync(gameId);
        var pdfPin = g!.AssignCover(CoverContext.Social, CoverAssignmentSource.Pdf, AdminId);
        pdfPin.SetGeneratedKey($"covers/crops/{gameId}/social.webp");
        var wikidataPin = g.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        wikidataPin.SetGeneratedKey($"covers/crops/{gameId}/card.webp");
        await _repository.ReconcileCoverAssignmentsAsync(g);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var cleared = await _repository.InvalidateGeneratedCropsAsync(gameId, CoverAssignmentSource.Pdf);

        // ExecuteUpdate runs server-side and needs no SaveChanges — assert on a fresh read.
        cleared.Should().Be(1);
        _dbContext.ChangeTracker.Clear();
        var after = await _repository.GetByIdAsync(gameId);
        after!.CoverAssignments.Single(a => a.Source == CoverAssignmentSource.Pdf)
            .GeneratedR2Key.Should().BeNull("the PDF base image was replaced");
        after.CoverAssignments.Single(a => a.Source == CoverAssignmentSource.Wikidata)
            .GeneratedR2Key.Should().Be($"covers/crops/{gameId}/card.webp",
                "a crop rendered from another source is still current");
    }

    [Fact]
    public async Task InvalidateGeneratedCrops_NoCropToClear_IsANoOp_OnPostgres()
    {
        // The overwhelmingly common case: a game with no rendered crop at all. The statement is
        // filtered on GeneratedR2Key != null so it touches zero rows rather than rewriting them
        // with a fresh UpdatedAt.
        var gameId = await SeedGameAsync();
        var g = await _repository.GetByIdAsync(gameId);
        g!.AssignCover(CoverContext.Social, CoverAssignmentSource.Pdf, AdminId);
        await _repository.ReconcileCoverAssignmentsAsync(g);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var cleared = await _repository.InvalidateGeneratedCropsAsync(gameId, CoverAssignmentSource.Pdf);

        cleared.Should().Be(0);
    }
}

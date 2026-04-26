using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="MechanicAnalysisRepository"/> against a real PostgreSQL
/// database (Testcontainers). Covers Issue #523 acceptance criteria that require EF/DB behavior:
/// partial unique index (AC-4), audit atomicity (AC-5), suppression query filter (AC-6),
/// review queue visibility (AC-7), optimistic concurrency (AC-9), cascade deletes (AC-11).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicAnalysisRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public MechanicAnalysisRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_mechanic_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new MechanicAnalysisRepository(_dbContext, mockEventCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // AC-4: Partial unique index — one Published+unsuppressed per SharedGame
    // ============================================================

    [Fact]
    public async Task AC4_PartialUniqueIndex_AllowsSecondPublishedAfterFirstIsSuppressed()
    {
        // Arrange: seed two analyses for the same SharedGame.
        // The partial unique index filters on `status=2 AND is_suppressed=false`,
        // so suppressing the first Published should free the slot for a second one.
        var sharedGameId = await SeedSharedGameAsync();

        var first = await PersistPublishedAnalysisAsync(sharedGameId);

        // Suppress the first → frees the unique slot.
        var actorId = Guid.NewGuid();
        first.Suppress(actorId, "Takedown", SuppressionRequestSource.Legal, DateTime.UtcNow, DateTime.UtcNow);
        _repository.Update(first);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act: publish a second analysis for the same SharedGame — should succeed.
        var second = await PersistPublishedAnalysisAsync(sharedGameId);

        // Assert: both rows coexist; only the unsuppressed one is visible via GetPublished.
        _dbContext.ChangeTracker.Clear();
        var total = await _dbContext.MechanicAnalyses
            .IgnoreQueryFilters()
            .CountAsync(a => a.SharedGameId == sharedGameId);
        total.Should().Be(2);

        var published = await _repository.GetPublishedForSharedGameAsync(sharedGameId);
        published.Should().NotBeNull();
        published!.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task AC4_PartialUniqueIndex_RejectsSecondPublishedWhenFirstIsStillActive()
    {
        // Arrange: two Published+unsuppressed analyses for the same SharedGame should violate
        // ux_mechanic_analyses_published_per_game.
        var sharedGameId = await SeedSharedGameAsync();
        await PersistPublishedAnalysisAsync(sharedGameId);

        // Act + Assert: second publish raises a unique violation.
        var act = async () => await PersistPublishedAnalysisAsync(sharedGameId);

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(ex => ex.InnerException != null
                && ex.InnerException.Message.Contains("ux_mechanic_analyses_published_per_game",
                    StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================
    // AC-5: Audit rows written atomically with the aggregate state change
    // ============================================================

    [Fact]
    public async Task AC5_StatusChange_WritesAuditRowInSameTransaction()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = BuildDraftAnalysisWithClaim(sharedGameId);
        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act: SubmitForReview raises MechanicAnalysisStatusChangedEvent → repo must synthesize
        // a mechanic_status_audit row.
        var loaded = await _repository.GetByIdWithClaimsAsync(analysis.Id);
        var actor = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        loaded!.SubmitForReview(actor, utcNow);
        _repository.Update(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: status updated AND audit row exists with correct from/to status.
        var entity = await _dbContext.MechanicAnalyses.AsNoTracking()
            .FirstAsync(a => a.Id == analysis.Id);
        entity.Status.Should().Be((int)MechanicAnalysisStatus.InReview);

        var audits = await _dbContext.MechanicStatusAudits.AsNoTracking()
            .Where(a => a.AnalysisId == analysis.Id)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();
        audits.Should().HaveCount(1);
        audits[0].FromStatus.Should().Be((int)MechanicAnalysisStatus.Draft);
        audits[0].ToStatus.Should().Be((int)MechanicAnalysisStatus.InReview);
        audits[0].ActorId.Should().Be(actor);
    }

    [Fact]
    public async Task AC5_Suppress_WritesSuppressionAuditRow()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var published = await PersistPublishedAnalysisAsync(sharedGameId);

        // Act: suppress → MechanicAnalysisSuppressedEvent → suppression audit row.
        _dbContext.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(published.Id);
        var actor = Guid.NewGuid();
        loaded!.Suppress(actor, "Copyright claim", SuppressionRequestSource.Legal, DateTime.UtcNow, DateTime.UtcNow);
        _repository.Update(loaded);
        await _dbContext.SaveChangesAsync();

        // Assert
        var audits = await _dbContext.MechanicSuppressionAudits.AsNoTracking()
            .Where(a => a.AnalysisId == published.Id)
            .ToListAsync();
        audits.Should().HaveCount(1);
        audits[0].IsSuppressed.Should().BeTrue();
        audits[0].ActorId.Should().Be(actor);
        audits[0].Reason.Should().Be("Copyright claim");
        audits[0].RequestSource.Should().Be((int)SuppressionRequestSource.Legal);
    }

    // ============================================================
    // AC-6: Global query filter hides suppressed analyses
    // ============================================================

    [Fact]
    public async Task AC6_SuppressedAnalysis_HiddenFromDefaultQueries()
    {
        // Arrange: publish, then suppress.
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = await PersistPublishedAnalysisAsync(sharedGameId);

        _dbContext.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(analysis.Id);
        loaded!.Suppress(Guid.NewGuid(), "T5 takedown", SuppressionRequestSource.Email, DateTime.UtcNow, DateTime.UtcNow);
        _repository.Update(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act + Assert
        // Player-facing query honors the filter → null.
        var afterFilter = await _repository.GetPublishedForSharedGameAsync(sharedGameId);
        afterFilter.Should().BeNull("global query filter hides IsSuppressed=true rows from player queries");

        var byId = await _repository.GetByIdAsync(analysis.Id);
        byId.Should().BeNull("GetByIdAsync honors the IsSuppressed filter per the interface contract");

        // Direct IgnoreQueryFilters inspection confirms row still exists.
        var rawExists = await _dbContext.MechanicAnalyses
            .IgnoreQueryFilters()
            .AnyAsync(a => a.Id == analysis.Id && a.IsSuppressed);
        rawExists.Should().BeTrue();
    }

    // ============================================================
    // AC-7: Review queue uses IgnoreQueryFilters and ordering
    // ============================================================

    [Fact]
    public async Task AC7_GetReviewQueue_IncludesSuppressedInReviewItems_OrderedByCreatedAt()
    {
        // Arrange: three analyses — one Draft (excluded), two InReview (one suppressed).
        var sharedGameId1 = await SeedSharedGameAsync();
        var sharedGameId2 = await SeedSharedGameAsync();
        var sharedGameId3 = await SeedSharedGameAsync();

        var draft = BuildDraftAnalysisWithClaim(sharedGameId1);
        await _repository.AddAsync(draft);
        await _dbContext.SaveChangesAsync();

        // InReview #1: oldest creation time
        var inReviewOld = BuildDraftAnalysisWithClaim(sharedGameId2, createdAt: DateTime.UtcNow.AddMinutes(-10));
        inReviewOld.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        await _repository.AddAsync(inReviewOld);

        // InReview #2: newer creation time, and suppressed — must still appear in queue.
        var inReviewSuppressed = BuildDraftAnalysisWithClaim(sharedGameId3, createdAt: DateTime.UtcNow.AddMinutes(-1));
        inReviewSuppressed.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        inReviewSuppressed.Suppress(Guid.NewGuid(), "mod hold", SuppressionRequestSource.Other, DateTime.UtcNow, DateTime.UtcNow);
        await _repository.AddAsync(inReviewSuppressed);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var queue = await _repository.GetReviewQueueAsync();

        // Assert: 2 InReview rows, oldest first, suppressed included.
        queue.Should().HaveCount(2);
        queue[0].Id.Should().Be(inReviewOld.Id);
        queue[1].Id.Should().Be(inReviewSuppressed.Id);
        queue[1].IsSuppressed.Should().BeTrue();
    }

    // ============================================================
    // AC-9: Optimistic concurrency via xmin RowVersion
    // ============================================================

    [Fact]
    public async Task AC9_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange: persist analysis, then load two copies in separate contexts.
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = BuildDraftAnalysisWithClaim(sharedGameId);
        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await using var ctxA = _fixture.CreateDbContext(_connectionString);
        await using var ctxB = _fixture.CreateDbContext(_connectionString);
        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector.Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        var repoA = new MechanicAnalysisRepository(ctxA, mockCollector.Object);
        var repoB = new MechanicAnalysisRepository(ctxB, mockCollector.Object);

        var copyA = await repoA.GetByIdWithClaimsAsync(analysis.Id);
        var copyB = await repoB.GetByIdWithClaimsAsync(analysis.Id);

        // Act: both copies mutate; first write wins.
        copyA!.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        repoA.Update(copyA);
        await ctxA.SaveChangesAsync();

        copyB!.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        repoB.Update(copyB);

        var act = async () => await ctxB.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ============================================================
    // AC-11: Cascade delete — removing analysis cascades to claims + citations
    // ============================================================

    [Fact]
    public async Task AC11_DeleteAnalysis_CascadesToClaimsAndCitations()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = BuildDraftAnalysisWithClaim(sharedGameId);
        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var claimsBefore = await _dbContext.MechanicClaims.CountAsync(c => c.AnalysisId == analysis.Id);
        var citationsBefore = await _dbContext.MechanicCitations.CountAsync();
        claimsBefore.Should().BeGreaterThan(0);
        citationsBefore.Should().BeGreaterThan(0);

        // Act: hard-delete the analysis row (simulates eventual purge after suppression).
        var entity = await _dbContext.MechanicAnalyses.IgnoreQueryFilters()
            .FirstAsync(a => a.Id == analysis.Id);
        _dbContext.MechanicAnalyses.Remove(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: all children removed via ON DELETE CASCADE.
        var claimsAfter = await _dbContext.MechanicClaims.CountAsync(c => c.AnalysisId == analysis.Id);
        var citationsAfter = await _dbContext.MechanicCitations.CountAsync();
        claimsAfter.Should().Be(0);
        citationsAfter.Should().Be(0);
    }

    // ============================================================
    // Regression: Update() with fresh (IsNew=true) claims minted via
    // MechanicClaim.Create(). Pre-fix, EF graph traversal in DbSet.Update()
    // marked these as Modified (because Guid.NewGuid() is non-default → IsKeySet=true),
    // emitted UPDATE against non-existent rows, and threw DbUpdateConcurrencyException.
    // Post-fix: repository inspects each claim's IsNew flag and explicitly sets
    // EntityState.Added for fresh claims, EntityState.Modified for rehydrated ones.
    // This mirrors the production executor flow:
    //   load Draft AsNoTracking → AddClaim x N (Create factory) → Update → SaveChanges.
    // ============================================================

    [Fact]
    public async Task Update_AggregateWithFreshClaimsCreatedViaFactory_PersistsThemAsAdded()
    {
        // Arrange: persist an empty Draft, then reload AsNoTracking (mirrors executor flow).
        var sharedGameId = await SeedSharedGameAsync();
        var emptyDraft = MechanicAnalysis.Create(
            sharedGameId: sharedGameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        await _repository.AddAsync(emptyDraft);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdWithClaimsAsync(emptyDraft.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Claims.Should().BeEmpty();

        // Act: add 3 fresh claims via the Create factory (each with Guid.NewGuid() Id and
        // a citation pre-bound to that Id), exactly as the executor does after parsing
        // an LLM section response.
        for (var i = 0; i < 3; i++)
        {
            var tempClaimId = Guid.NewGuid();
            var citation = MechanicCitation.Create(
                claimId: tempClaimId,
                pdfPage: i + 1,
                quote: $"Citation quote {i}.",
                chunkId: null,
                displayOrder: 0);

            var claim = MechanicClaim.Create(
                analysisId: reloaded.Id,
                section: MechanicSection.Mechanics,
                text: $"Fresh claim text {i}.",
                displayOrder: i,
                citations: new[] { citation });

            // Sanity: factory-minted claims must report IsNew=true.
            claim.IsNew.Should().BeTrue();
            claim.Citations[0].IsNew.Should().BeTrue();

            reloaded.AddClaim(claim);
        }

        _repository.Update(reloaded);

        // Assert: SaveChanges must NOT throw DbUpdateConcurrencyException.
        // Pre-fix, this line was the throw site.
        var act = async () => await _dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync<DbUpdateConcurrencyException>();

        // Verify the claims actually landed and their FKs match the parent analysis.
        _dbContext.ChangeTracker.Clear();
        var verify = await _repository.GetByIdWithClaimsAsync(emptyDraft.Id);
        verify.Should().NotBeNull();
        verify!.Claims.Should().HaveCount(3);
        verify.Claims.Should().OnlyContain(c => c.AnalysisId == emptyDraft.Id);
        verify.Claims.SelectMany(c => c.Citations).Should().HaveCount(3);

        // Rehydrated claims now report IsNew=false (the round-trip flips the flag).
        verify.Claims.Should().OnlyContain(c => !c.IsNew);
    }

    [Fact]
    public async Task Update_AggregateWithRehydratedClaims_AppliesUpdatesWithoutDuplicating()
    {
        // Arrange: persist an analysis with 1 claim, reload, mutate the claim's review state.
        // This is the IsNew=false path — must remain Modified, not Added (otherwise we'd get
        // a duplicate-PK insert).
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = BuildDraftAnalysisWithClaim(sharedGameId);
        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdWithClaimsAsync(analysis.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Claims.Should().HaveCount(1);
        reloaded.Claims[0].IsNew.Should().BeFalse();
        reloaded.Claims[0].Citations[0].IsNew.Should().BeFalse();

        // Act: move through the lifecycle that flips claim status (mutates an existing row).
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        reloaded.SubmitForReview(reviewer, now);
        reloaded.ApproveClaim(reloaded.Claims[0].Id, reviewer, now);

        _repository.Update(reloaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: still exactly one claim row (no Added duplicate), with the new status.
        var verify = await _repository.GetByIdWithClaimsAsync(analysis.Id);
        verify!.Claims.Should().HaveCount(1);
        verify.Claims[0].Status.Should().Be(MechanicClaimStatus.Approved);
        verify.Claims[0].Citations.Should().HaveCount(1);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<Guid> SeedSharedGameAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = $"Test Game {sharedGameId:N}".Substring(0, 25),
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }

    private static MechanicAnalysis BuildDraftAnalysisWithClaim(
        Guid sharedGameId,
        DateTime? createdAt = null)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: sharedGameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: createdAt ?? DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        // Pre-allocate the claim Id so the citation's ClaimId matches the FK at persistence.
        // MechanicClaim.Create() would generate a fresh Guid internally, breaking the FK link
        // between mechanic_citations.claim_id and mechanic_claims.id.
        var claimId = Guid.NewGuid();
        var citation = MechanicCitation.Create(
            claimId: claimId,
            pdfPage: 1,
            quote: "Each turn draw one card.",
            chunkId: null,
            displayOrder: 0);

        var claim = MechanicClaim.Reconstitute(
            id: claimId,
            analysisId: analysis.Id,
            section: MechanicSection.Mechanics,
            text: "On each turn, draw one card.",
            displayOrder: 0,
            status: MechanicClaimStatus.Pending,
            reviewedBy: null,
            reviewedAt: null,
            rejectionNote: null,
            citations: new[] { citation });

        analysis.AddClaim(claim);
        return analysis;
    }

    private async Task<MechanicAnalysis> PersistPublishedAnalysisAsync(Guid sharedGameId)
    {
        var analysis = BuildDraftAnalysisWithClaim(sharedGameId);
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        analysis.SubmitForReview(reviewer, now);
        analysis.ApproveClaim(analysis.Claims[0].Id, reviewer, now);
        analysis.Approve(reviewer, now);

        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Reload so XminVersion reflects the DB-assigned xmin (server-generated on INSERT).
        // Without this, subsequent repo.Update() on the returned aggregate would fail with a
        // spurious DbUpdateConcurrencyException (original xmin=0 vs actual xmin).
        var reloaded = await _repository.GetByIdWithClaimsAsync(analysis.Id);
        return reloaded!;
    }
}

using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
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
/// Integration tests (Testcontainers Postgres) for the #526/#2782 WRITE+READ mapper trap on
/// <see cref="MechanicClaim.Validations"/>. The Moq/handler tests read the in-memory domain object
/// and stay green even if <see cref="MechanicAnalysisRepository"/>'s write mapper drops the field —
/// ONLY this round-trip against a real jsonb column catches a dropped mapper copy (#2782 FU-1 M4).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisRepositoryValidationsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicAnalysisRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public MechanicAnalysisRepositoryValidationsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_mechanic_val_{Guid.NewGuid():N}";
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
    // #2782 FU-1 M4: claim validations round-trip across INSERT + reload (READ mapper)
    // AND survive a mutate-then-resave UPDATE (WRITE mapper — the #526 dropped-copy shape).
    // ============================================================

    [Fact]
    public async Task ClaimValidations_SurviveInsertReloadMutateAndResave()
    {
        // 1. Build an InReview analysis with one claim carrying real validations (mixed
        //    pass/fail/notRun + a T3b grounding score).
        var sharedGameId = await SeedSharedGameAsync();
        var analysis = BuildInReviewAnalysisWithValidatedClaim(sharedGameId);
        await _repository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // 2. Reload → validations survived the INSERT (write mapper) + reconstitute (read mapper).
        var afterInsert = await _repository.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id);
        afterInsert.Should().NotBeNull();
        var claim = afterInsert!.Claims.Single();
        claim.Validations.Should().HaveCount(4);
        claim.Validations.Single(v => v.Rule == "T1").Outcome.Should().Be(MechanicClaimValidationOutcomes.Pass);
        claim.Validations.Single(v => v.Rule == "T2").Outcome.Should().Be(MechanicClaimValidationOutcomes.Fail);
        claim.Validations.Single(v => v.Rule == "T4").Outcome.Should().Be(MechanicClaimValidationOutcomes.NotRun);
        claim.Validations.Single(v => v.Rule == "T3b").Score.Should().BeApproximately(0.83, 1e-9);

        // 3. MUTATE the claim graph (approve it) + Update — Update rebuilds a detached entity and
        //    force-writes EVERY column (EntityState.Modified), so this proves the WRITE mapper still
        //    copies validations after an unrelated mutation (the #526 dropped-mapper regression shape).
        var reviewer = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var claimId = claim.Id;
        afterInsert.ApproveClaim(claimId, reviewer, now);
        _repository.Update(afterInsert);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var afterUpdate = await _repository.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id);
        afterUpdate.Should().NotBeNull();
        var reloaded = afterUpdate!.Claims.Single();
        reloaded.Status.Should().Be(MechanicClaimStatus.Approved);
        reloaded.Validations.Should().HaveCount(4); // validations still there after UPDATE
        reloaded.Validations.Single(v => v.Rule == "T2").Message.Should().NotBeNull();
        reloaded.Validations.Single(v => v.Rule == "T3b").Score.Should().BeApproximately(0.83, 1e-9);
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

    /// <summary>
    /// Builds an <see cref="MechanicAnalysisStatus.InReview"/> analysis whose single claim carries
    /// three real validations (a passing T1, a failing T2 with a message, a notRun T4) plus a T3b
    /// grounding outcome with a cosine score. Validations are attached through the public
    /// <see cref="MechanicClaim.Reconstitute"/> <c>validations:</c> arg — the same path the READ
    /// mapper uses — so the seed exercises the real reconstitution shape.
    /// </summary>
    private static MechanicAnalysis BuildInReviewAnalysisWithValidatedClaim(Guid sharedGameId)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: sharedGameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        var claimId = Guid.NewGuid();
        var citation = MechanicCitation.Create(
            claimId: claimId,
            pdfPage: 1,
            quote: "Each turn draw one card.",
            chunkId: null,
            displayOrder: 0);

        var validations = new List<MechanicClaimValidation>
        {
            new(Rule: "T1", Outcome: MechanicClaimValidationOutcomes.Pass),
            new(Rule: "T2", Outcome: MechanicClaimValidationOutcomes.Fail, Message: "quote span mismatch"),
            new(Rule: "T3b", Outcome: MechanicClaimValidationOutcomes.Pass, Message: "grounded", Score: 0.83),
            new(Rule: "T4", Outcome: MechanicClaimValidationOutcomes.NotRun)
        };

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
            citations: new[] { citation },
            validations: validations);

        analysis.AddClaim(claim);
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        return analysis;
    }
}

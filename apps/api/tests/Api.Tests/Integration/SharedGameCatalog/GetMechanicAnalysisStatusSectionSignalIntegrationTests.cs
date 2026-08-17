using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// #2807 (ME-FU-1): the mechanic-analysis status query surfaces an "N/M sections produced claims"
/// signal (SectionsWithClaims / TotalSections) so a section that was silently dropped — e.g. failed
/// the well_formed check across all retries and never reached the review queue — is visible to the
/// reviewer as N &lt; M. Computed at query time from the persisted claims; no schema change.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetMechanicAnalysisStatusSectionSignalIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    public GetMechanicAnalysisStatusSectionSignalIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"status_section_signal_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Status query counts DISTINCT sections-with-claims out of 6 total")]
    public async Task Handle_ReportsDistinctSectionsWithClaims_OutOfSixTotal()
    {
        var analysisId = await SeedAnalysisAsync();
        // Claims across 3 DISTINCT sections (Summary=0, Mechanics=1, Resources=3); Mechanics twice
        // to prove we count distinct sections, not raw claims.
        AddClaim(analysisId, section: 0, order: 0);
        AddClaim(analysisId, section: 1, order: 1);
        AddClaim(analysisId, section: 1, order: 2);
        AddClaim(analysisId, section: 3, order: 3);
        await _dbContext.SaveChangesAsync();

        var handler = new GetMechanicAnalysisStatusQueryHandler(_dbContext);
        var dto = await handler.Handle(new GetMechanicAnalysisStatusQuery(analysisId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.ClaimsCount.Should().Be(4);
        dto.SectionsWithClaims.Should().Be(3); // sections 0,1,3 — Victory/Phases/Faq dropped
        dto.TotalSections.Should().Be(6);
    }

    [Fact(DisplayName = "Status query reports 0/6 when no section produced claims")]
    public async Task Handle_ReportsZeroSectionsWithClaims_WhenNoClaims()
    {
        var analysisId = await SeedAnalysisAsync();
        await _dbContext.SaveChangesAsync();

        var handler = new GetMechanicAnalysisStatusQueryHandler(_dbContext);
        var dto = await handler.Handle(new GetMechanicAnalysisStatusQuery(analysisId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.SectionsWithClaims.Should().Be(0);
        dto.TotalSections.Should().Be(6);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AddClaim(Guid analysisId, int section, int order) =>
        _dbContext.MechanicClaims.Add(new MechanicClaimEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Section = section,
            Text = $"claim s{section} o{order}",
            DisplayOrder = order,
            Status = 0
        });

    private async Task<Guid> SeedAnalysisAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Status Section Signal Test Game",
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = TestAdminId,
            CreatedAt = DateTime.UtcNow
        });

        var analysisId = Guid.NewGuid();
        _dbContext.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "mechanic-extractor-v1",
            Status = 0,
            CreatedBy = TestAdminId,
            CreatedAt = DateTime.UtcNow,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        await _dbContext.SaveChangesAsync();
        return analysisId;
    }
}

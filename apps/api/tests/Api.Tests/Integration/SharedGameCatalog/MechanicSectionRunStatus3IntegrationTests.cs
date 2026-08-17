using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration test proving that <c>mechanic_analysis_section_runs.status</c> accepts the value
/// <c>3</c> (RetainedWithGuardrailFlags) after the CHECK constraint
/// <c>ck_mechanic_section_runs_status_range</c> is widened from <c>0 AND 2</c> to <c>0 AND 3</c>
/// (ADR-051 / #2782, decision D9).
///
/// Before the widening migration, PostgreSQL rejects any insert with <c>status = 3</c>: the
/// pipeline (#2782 D3, commit b8c6f609f) now retains guardrail-failed sections with Status=3
/// instead of dropping them, so the persistence layer must allow the value.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicSectionRunStatus3IntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    public MechanicSectionRunStatus3IntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"section_run_status3_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Section-run with Status=3 (RetainedWithGuardrailFlags) is accepted by the widened CHECK")]
    public async Task SectionRun_WithStatus3_RetainedWithGuardrailFlags_IsAccepted()
    {
        var analysis = await SeedAnalysisAsync();

        var run = new MechanicAnalysisSectionRunEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysis,
            Section = 0,
            RunOrder = 0,
            Provider = "deepseek",
            ModelUsed = "deepseek-chat",
            PromptTokens = 1,
            CompletionTokens = 1,
            TotalTokens = 2,
            EstimatedCostUsd = 0.0001m,
            LatencyMs = 5,
            Status = 3,
            ErrorMessage = null,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _dbContext.MechanicAnalysisSectionRuns.Add(run);

        var act = async () => await _dbContext.SaveChangesAsync();

        await act.Should().NotThrowAsync(); // fails pre-D9: CHECK status BETWEEN 0 AND 2 rejects 3

        var persisted = await _dbContext.MechanicAnalysisSectionRuns
            .AsNoTracking()
            .SingleAsync(r => r.Id == run.Id);
        persisted.Status.Should().Be(3);
        persisted.ErrorMessage.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedAnalysisAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Section Run Status3 Test Game",
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

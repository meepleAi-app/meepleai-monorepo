using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration test proving that <c>mechanic_drafts</c> now raises
/// <see cref="DbUpdateConcurrencyException"/> on concurrent writes via the PostgreSQL
/// <c>xmin</c> system-column concurrency token.
///
/// Before Issue #2306 the table used a <c>byte[] RowVersion</c> (IsRowVersion) with no
/// trigger to populate it — the column was always an empty byte[], so EF's concurrency
/// check compared E'' == E'' on every update (always-success = last-write-wins).
/// After the xmin migration, EF emits <c>WHERE xmin = @original</c>; the second writer
/// holds a stale xmin and the update affects 0 rows → DbUpdateConcurrencyException.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicDraftRowVersionConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public MechanicDraftRowVersionConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"draft_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Concurrent updates throw DbUpdateConcurrencyException via xmin")]
    public async Task ConcurrentUpdates_ThrowDbUpdateConcurrencyException()
    {
        // ── Arrange: seed a parent SharedGame (FK requirement) ────────────────
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Xmin Concurrency Test Game",
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

        // ── Seed a minimal MechanicDraft row ──────────────────────────────────
        var draftId = Guid.NewGuid();
        _dbContext.MechanicDrafts.Add(new MechanicDraftEntity
        {
            Id = draftId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            GameTitle = "Xmin Concurrency Test Game",
            SummaryNotes = string.Empty,
            MechanicsNotes = string.Empty,
            VictoryNotes = string.Empty,
            ResourcesNotes = string.Empty,
            PhasesNotes = string.Empty,
            QuestionsNotes = string.Empty,
            SummaryDraft = string.Empty,
            MechanicsDraft = string.Empty,
            VictoryDraft = string.Empty,
            ResourcesDraft = string.Empty,
            PhasesDraft = string.Empty,
            QuestionsDraft = string.Empty,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            Status = 0,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m
        });
        await _dbContext.SaveChangesAsync();

        // ── Load the same row from two *independent* DbContext instances ────────
        // Each gets its own change tracker so each holds its own snapshot of Xmin.
        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);

        var draftA = await dbA.MechanicDrafts.FirstAsync(d => d.Id == draftId);
        var draftB = await dbB.MechanicDrafts.FirstAsync(d => d.Id == draftId);

        draftA.Should().NotBeSameAs(draftB);
        draftA.Xmin.Should().Be(draftB.Xmin, "both scopes loaded the same row — xmin must match");

        // ── Act: A wins the race and commits first ────────────────────────────
        draftA.SummaryNotes = "Updated by A";
        await dbA.SaveChangesAsync();

        // B holds a stale Xmin — its UPDATE will match 0 rows in Postgres.
        // EF Core must detect 0 affected rows and throw DbUpdateConcurrencyException.
        draftB.SummaryNotes = "Updated by B";
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ─────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "scope B holds a stale xmin after scope A committed — " +
            "EF optimistic concurrency must reject the second save");
    }
}

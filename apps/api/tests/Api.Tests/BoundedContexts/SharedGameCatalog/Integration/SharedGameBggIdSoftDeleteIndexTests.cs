using System.Data;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for the <c>ix_shared_games_bgg_id</c> partial unique index — Issue #3236.
/// <para>
/// The index was filtered only on <c>bgg_id IS NOT NULL</c>, missing <c>AND is_deleted = false</c>.
/// Because <c>shared_games</c> is soft-deleted (a global <c>!IsDeleted</c> query filter hides deleted
/// rows), a soft-deleted game permanently reserved its BGG id — no new active game could reuse it,
/// even though every app-layer read excludes the deleted row. The sibling <c>private_games</c> index
/// gets this right (<c>bgg_id IS NOT NULL AND is_deleted = false</c>).
/// </para>
/// Exercised against real Postgres via <see cref="SharedTestcontainersFixture"/> + the actual
/// migrations (<c>MigrateAsync</c>) so the index definition under test is the real one.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3236")]
public sealed class SharedGameBggIdSoftDeleteIndexTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public SharedGameBggIdSoftDeleteIndexTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"shared_games_bgg_softdel_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static SharedGameEntity MakeGame(int bggId) => new()
    {
        Id = Guid.NewGuid(),
        Title = $"Game {bggId}",
        BggId = bggId,
        YearPublished = 2024,
        Description = "desc",
        MinPlayers = 2,
        MaxPlayers = 4,
        PlayingTimeMinutes = 60,
        MinAge = 10,
        Status = 0,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    [Fact(DisplayName = "A soft-deleted game's bgg_id can be reused by a new active game")]
    public async Task SoftDeletedBggId_CanBeReusedByActiveGame()
    {
        const int bggId = 555001;

        var first = MakeGame(bggId);
        _dbContext.SharedGames.Add(first);
        await _dbContext.SaveChangesAsync(Ct);

        // Soft-delete the first game (IsDeleted only — SharedGameEntity has no DeletedAt).
        first.IsDeleted = true;
        await _dbContext.SaveChangesAsync(Ct);

        // Insert a brand-new active game reusing the same BGG id.
        var second = MakeGame(bggId);
        _dbContext.SharedGames.Add(second);
        var act = async () => await _dbContext.SaveChangesAsync(Ct);

        // Before the fix the unfiltered index still reserves the id → 23505; after the fix it succeeds.
        await act.Should().NotThrowAsync();

        // The active game (query filter excludes the soft-deleted one) is the reused row.
        var visible = await _dbContext.SharedGames.Where(g => g.BggId == bggId).ToListAsync(Ct);
        visible.Should().ContainSingle().Which.Id.Should().Be(second.Id);
    }

    [Fact(DisplayName = "Two active games with the same bgg_id still conflict (unique constraint preserved)")]
    public async Task TwoActiveGamesWithSameBggId_StillConflict()
    {
        const int bggId = 555002;

        _dbContext.SharedGames.Add(MakeGame(bggId));
        await _dbContext.SaveChangesAsync(Ct);

        _dbContext.SharedGames.Add(MakeGame(bggId));
        var act = async () => await _dbContext.SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "ix_shared_games_bgg_id is partial on is_deleted after the migration")]
    public async Task Ix_shared_games_bgg_id_FiltersOnSoftDelete()
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(Ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE tablename = 'shared_games'
              AND indexname = 'ix_shared_games_bgg_id';
            """;

        var indexDef = (string?)await cmd.ExecuteScalarAsync(Ct);

        indexDef.Should().NotBeNull("the bgg_id unique index must exist");
        // Postgres may render "is_deleted = false" as "(NOT is_deleted)"; assert the column
        // participates in the partial WHERE either way. The pre-fix index had no is_deleted at all.
        indexDef!.Should().Contain("is_deleted");
    }
}

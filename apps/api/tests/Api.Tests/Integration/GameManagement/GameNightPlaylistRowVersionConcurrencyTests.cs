using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration test proving that <c>game_night_playlists</c> now raises
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
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightPlaylistRowVersionConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightPlaylistRowVersionConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"playlist_xmin_{Guid.NewGuid():N}";
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
        // ── Arrange: seed a minimal playlist row ──────────────────────────────
        var playlistId = Guid.NewGuid();
        _dbContext.GameNightPlaylists.Add(new GameNightPlaylistEntity
        {
            Id = playlistId,
            Name = "Friday Night Games",
            CreatorUserId = Guid.NewGuid(),
            GamesJson = "[]",
            IsShared = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // ── Load the same row from two *independent* DbContext instances ────────
        // Each gets its own change tracker so each holds its own snapshot of Xmin.
        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);

        var playlistA = await dbA.GameNightPlaylists.FirstAsync(p => p.Id == playlistId);
        var playlistB = await dbB.GameNightPlaylists.FirstAsync(p => p.Id == playlistId);

        playlistA.Should().NotBeSameAs(playlistB);
        playlistA.Xmin.Should().Be(playlistB.Xmin, "both scopes loaded the same row — xmin must match");

        // ── Act: A wins the race and commits first ────────────────────────────
        playlistA.Name = "Friday Night Games — updated by A";
        await dbA.SaveChangesAsync();

        // B holds a stale Xmin — its UPDATE will match 0 rows in Postgres.
        // EF Core must detect 0 affected rows and throw DbUpdateConcurrencyException.
        playlistB.Name = "Friday Night Games — updated by B";
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ─────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "scope B holds a stale xmin after scope A committed — " +
            "EF optimistic concurrency must reject the second save");
    }
}

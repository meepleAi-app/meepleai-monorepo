using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure;

/// <summary>
/// Integration tests for <see cref="UserGamebookViewRepository"/> against a real
/// PostgreSQL provider (Testcontainers).
///
/// Issue #2850 / finding #M: <c>GET /api/v1/gamebooks</c> returned 500 with EF Core
/// <c>SelectExpression.ApplySetOperation</c>. Root cause: the repository composed the
/// shared-side and private-side projections with a DB-side <c>IQueryable.Concat</c>
/// (SQL UNION ALL). Both projections carry many correlated <c>let</c> subqueries
/// (Any/Max/Count), and EF Core cannot reconcile the two projection shapes across a
/// set operation, throwing at query-compilation time — <b>regardless of row count</b>.
///
/// These tests MUST run against Npgsql (not EF InMemory), because InMemory does not
/// translate set operations to SQL and therefore never reproduces the crash.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
[Collection("Integration-GroupC")]
public sealed class UserGamebookViewRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private readonly string _databaseName = $"test_gamebookview_{Guid.NewGuid():N}";

    public UserGamebookViewRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// Core regression: with no data at all, the query must still <b>compile and run</b>.
    /// Before the fix this threw the <c>ApplySetOperation</c> exception at execution time
    /// (the failure is in SQL generation of the <c>Concat</c>, independent of rows).
    /// </summary>
    [Fact]
    public async Task GetGamebookEntriesAsync_WithNoData_DoesNotThrowAndReturnsEmpty()
    {
        var repository = new UserGamebookViewRepository(_dbContext!);

        var act = async () => await repository.GetGamebookEntriesAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().BeEmpty();
    }

    /// <summary>
    /// Correctness: a private-game library entry (PrivateGameId set) is returned and
    /// mapped from the PrivateGame row. Also exercises the two-query composition end to end.
    /// </summary>
    [Fact]
    public async Task GetGamebookEntriesAsync_WithPrivateGameEntry_ReturnsMappedItem()
    {
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"gamebookview_{userId:N}@meepleai.test",
            DisplayName = "Gamebook View Test User",
            PasswordHash = "test-hash",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });

        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            OwnerId = userId,
            Title = "HP-TEST Private Gamebook",
            YearPublished = 2021,
            ImageUrl = "https://example.test/cover.webp",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
        });

        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PrivateGameId = privateGameId, // XOR: private side only (SharedGameId null)
            CurrentState = (int)GameStateType.Owned,
            StateChangedAt = DateTime.UtcNow,
            AddedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();

        var repository = new UserGamebookViewRepository(_dbContext);

        var result = await repository.GetGamebookEntriesAsync(userId, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        var item = result[0];
        item.GameId.Should().Be(privateGameId);
        item.Title.Should().Be("HP-TEST Private Gamebook");
        item.Year.Should().Be(2021);
        item.HasPrivatePdf.Should().BeTrue();
        item.HasActiveCampaign.Should().BeFalse();
    }
}

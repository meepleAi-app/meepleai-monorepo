using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for multi-table transaction scenarios.
/// Tests commit, rollback, optimistic locking, deadlock handling, and transaction scope.
/// Issue #2307: Week 3 - Transaction integrity integration testing (5 tests)
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "2307")]
public sealed class TransactionScenarioTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public TransactionScenarioTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_transactions_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Multi-Table Insert with Commit

    [Fact]
    public async Task MultiTableInsert_WithCommit_ShouldPersistBothTables()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "oauth@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        };

        var oauthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderUserId = "google_123",
            AccessTokenEncrypted = "encrypted_access_token_google_123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Begin explicit transaction
        await using var transaction = await _dbContext!.Database.BeginTransactionAsync(TestCancellationToken);

        _dbContext.Users.Add(user);
        _dbContext.OAuthAccounts.Add(oauthAccount);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        await transaction.CommitAsync(TestCancellationToken);

        // Assert - Verify both records persisted
        _dbContext.ChangeTracker.Clear();

        var persistedUser = await _dbContext.Users.FindAsync(new object[] { userId }, TestCancellationToken);
        var persistedOAuth = await _dbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.UserId == userId, TestCancellationToken);

        persistedUser.Should().NotBeNull();
        persistedUser!.Email.Should().Be("oauth@test.com");

        persistedOAuth.Should().NotBeNull();
        persistedOAuth!.Provider.Should().Be("Google");
        persistedOAuth.UserId.Should().Be(userId);
    }

    #endregion

    #region Multi-Table Insert with Rollback

    [Fact]
    public async Task MultiTableInsert_WithRollback_ShouldNotPersistAnyData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "rollback@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        };

        var oauthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "GitHub",
            ProviderUserId = "github_456",
            AccessTokenEncrypted = "encrypted_access_token_github_456",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Begin transaction, add data, then rollback
        await using var transaction = await _dbContext!.Database.BeginTransactionAsync(TestCancellationToken);

        _dbContext.Users.Add(user);
        _dbContext.OAuthAccounts.Add(oauthAccount);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Simulate validation error - rollback instead of commit
        await transaction.RollbackAsync(TestCancellationToken);

        // Assert - Verify NO records persisted
        _dbContext.ChangeTracker.Clear();

        var persistedUser = await _dbContext.Users.FindAsync(new object[] { userId }, TestCancellationToken);
        var persistedOAuth = await _dbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.UserId == userId, TestCancellationToken);

        persistedUser.Should().BeNull();
        persistedOAuth.Should().BeNull();
    }

    #endregion

    #region Optimistic Concurrency Conflict

    /// <summary>
    /// Issue #2709: This test requires RowVersion property on GameEntity for optimistic locking.
    /// GameEntity currently doesn't have [Timestamp] RowVersion, so EF Core cannot detect
    /// concurrent modifications. Skip until RowVersion is added in a future migration.
    /// </summary>
    [Fact(Skip = "Requires RowVersion concurrency token on GameEntity - see Issue #2709")]
    public async Task ConcurrentUpdate_OptimisticLocking_ShouldThrowDbUpdateConcurrencyException()
    {
        // Arrange - Create a game that will be updated concurrently
        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Original Title",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2020
        };

        _dbContext!.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Simulate concurrent updates (two separate contexts)
        var scope1 = _serviceProvider!.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var scope2 = _serviceProvider.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Context1 loads game
        var game1 = await context1.Games.FindAsync(new object[] { gameId }, TestCancellationToken);
        game1!.Name = "Updated by Context 1";

        // Context2 loads same game
        var game2 = await context2.Games.FindAsync(new object[] { gameId }, TestCancellationToken);
        game2!.Name = "Updated by Context 2";

        // Context1 saves first (succeeds)
        await context1.SaveChangesAsync(TestCancellationToken);

        // Context2 tries to save (should fail with concurrency exception)
        var act = async () => await context2.SaveChangesAsync(TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // Cleanup
        await context1.DisposeAsync();
        await context2.DisposeAsync();
        scope1.Dispose();
        scope2.Dispose();
    }

    #endregion

    #region Deadlock with Retry

    [Fact]
    public async Task ConcurrentTransactions_PotentialDeadlock_ShouldHandleGracefully()
    {
        // Arrange - Create two games for cross-updates
        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();

        _dbContext!.Games.AddRange(
            new GameEntity { Id = game1Id, Name = "Game 1", MinPlayers = 2, MaxPlayers = 4, YearPublished = 2020 },
            new GameEntity { Id = game2Id, Name = "Game 2", MinPlayers = 2, MaxPlayers = 6, YearPublished = 2021 }
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Simulate concurrent transactions with retry logic
        var successCount = 0;
        var tasks = new List<Task>();

        // Transaction 1: Update Game1 then Game2
        tasks.Add(Task.Run(async () =>
        {
            for (var retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var scope = _serviceProvider!.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

                    await using var transaction = await ctx.Database.BeginTransactionAsync(TestCancellationToken);

                    var g1 = await ctx.Games.FindAsync(new object[] { game1Id }, TestCancellationToken);
                    g1!.Name = "Updated Game 1 by Tx1";
                    await ctx.SaveChangesAsync(TestCancellationToken);

                    await Task.Delay(50, TestCancellationToken); // Introduce timing for potential deadlock

                    var g2 = await ctx.Games.FindAsync(new object[] { game2Id }, TestCancellationToken);
                    g2!.Name = "Updated Game 2 by Tx1";
                    await ctx.SaveChangesAsync(TestCancellationToken);

                    await transaction.CommitAsync(TestCancellationToken);
                    Interlocked.Increment(ref successCount);
                    break;
                }
                // Issue #2709: PostgreSQL deadlocks may throw NpgsqlException with SqlState 40P01
                // wrapped in DbUpdateException, InvalidOperationException, or directly
                catch (Exception ex) when (retry < 2 && IsDeadlockException(ex))
                {
                    await Task.Delay(100 * (retry + 1), TestCancellationToken);
                }
            }
        }));

        // Transaction 2: Update Game2 then Game1 (opposite order - potential deadlock)
        tasks.Add(Task.Run(async () =>
        {
            for (var retry = 0; retry < 3; retry++)
            {
                try
                {
                    using var scope = _serviceProvider!.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

                    await using var transaction = await ctx.Database.BeginTransactionAsync(TestCancellationToken);

                    var g2 = await ctx.Games.FindAsync(new object[] { game2Id }, TestCancellationToken);
                    g2!.Name = "Updated Game 2 by Tx2";
                    await ctx.SaveChangesAsync(TestCancellationToken);

                    await Task.Delay(50, TestCancellationToken); // Introduce timing for potential deadlock

                    var g1 = await ctx.Games.FindAsync(new object[] { game1Id }, TestCancellationToken);
                    g1!.Name = "Updated Game 1 by Tx2";
                    await ctx.SaveChangesAsync(TestCancellationToken);

                    await transaction.CommitAsync(TestCancellationToken);
                    Interlocked.Increment(ref successCount);
                    break;
                }
                // Issue #2709: PostgreSQL deadlocks may throw NpgsqlException with SqlState 40P01
                // wrapped in DbUpdateException, InvalidOperationException, or directly
                catch (Exception ex) when (retry < 2 && IsDeadlockException(ex))
                {
                    await Task.Delay(100 * (retry + 1), TestCancellationToken);
                }
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - Both transactions should eventually succeed with retry
        successCount.Should().Be(2, "Both transactions should succeed with retry logic");
    }

    #endregion

    #region Transaction Scope Across Repositories

    [Fact]
    public async Task TransactionScope_AcrossMultipleRepositories_ShouldMaintainConsistency()
    {
        // Arrange - Create user, game, and PDF in single transaction scope
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            Email = "scope@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        };

        var game = new GameEntity
        {
            Id = gameId,
            Name = "Scope Test Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2022
        };

        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "scope_test.pdf",
            FilePath = "/path/scope_test.pdf",
            FileSizeBytes = 1000000,
            UploadedByUserId = userId,
        };

        // Act - Single transaction across multiple entity types
        await using var transaction = await _dbContext!.Database.BeginTransactionAsync(TestCancellationToken);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        await transaction.CommitAsync(TestCancellationToken);

        // Assert - Verify all entities persisted with FK relationships intact
        _dbContext.ChangeTracker.Clear();

        var persistedUser = await _dbContext.Users.FindAsync(new object[] { userId }, TestCancellationToken);
        var persistedGame = await _dbContext.Games.FindAsync(new object[] { gameId }, TestCancellationToken);
        var persistedPdf = await _dbContext.PdfDocuments
            .Include(p => p.Game)
            .Include(p => p.UploadedBy)
            .FirstOrDefaultAsync(p => p.Id == pdfId, TestCancellationToken);

        persistedUser.Should().NotBeNull();
        persistedGame.Should().NotBeNull();
        persistedPdf.Should().NotBeNull();

        // Verify FK relationships
        persistedPdf!.GameId.Should().Be(gameId);
        persistedPdf.UploadedByUserId.Should().Be(userId);
        persistedPdf.Game.Should().NotBeNull();
        persistedPdf.Game.Name.Should().Be("Scope Test Game");
        persistedPdf.UploadedBy.Should().NotBeNull();
        persistedPdf.UploadedBy.Email.Should().Be("scope@test.com");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Issue #2709: Helper to detect PostgreSQL deadlock exceptions.
    /// Deadlock errors (40P01) may be wrapped in InvalidOperationException or DbUpdateException.
    /// </summary>
    private static bool IsDeadlockException(Exception ex)
    {
        // Check the exception and all inner exceptions for PostgreSQL deadlock code
        var current = ex;
        while (current != null)
        {
            if (current is NpgsqlException npgsqlEx && npgsqlEx.SqlState == "40P01")
            {
                return true;
            }

            if (current is DbUpdateException)
            {
                return true; // DbUpdateException from EF Core is always retryable
            }

            current = current.InnerException;
        }

        return false;
    }

    #endregion
}

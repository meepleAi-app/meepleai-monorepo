using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for ChunkedUploadSession FK constraints.
/// Tests DeleteBehavior.Cascade for User and Game FK.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupA")]
public class ChunkedUploadSessionForeignKeyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_chunked_fk_{Guid.NewGuid():N}";

    public ChunkedUploadSessionForeignKeyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task DeleteUser_WithChunkedUploadSessions_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "uploader@test.com", DisplayName = "Uploader", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Game" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionId = Guid.NewGuid();
        var session = new ChunkedUploadSessionEntity
        {
            Id = sessionId,
            GameId = gameId,
            UserId = userId,
            FileName = "rules.pdf",
            TotalFileSize = 5000000,
            TotalChunks = 10,
            ReceivedChunks = 5,
            TempDirectory = "/tmp/uploads",
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            ReceivedChunkIndices = "0,1,2,3,4"
        };
        _dbContext.ChunkedUploadSessions.Add(session);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete user (cascades to session)
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Session deleted via cascade
        var deletedSession = await _dbContext.ChunkedUploadSessions.FindAsync(sessionId);
        deletedSession.Should().BeNull();
    }

    [Fact]
    public async Task DeleteGame_WithChunkedUploadSessions_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Game" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var session = new ChunkedUploadSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = userId,
            FileName = "expansion.pdf",
            TotalFileSize = 3000000,
            TotalChunks = 5,
            ReceivedChunks = 2,
            TempDirectory = "/tmp/uploads",
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            ReceivedChunkIndices = "0,1"
        };
        _dbContext.ChunkedUploadSessions.Add(session);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game (cascades to session)
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - No orphaned sessions
        var orphanedSessions = await _dbContext.ChunkedUploadSessions.Where(s => s.GameId == gameId).ToListAsync(TestContext.Current.CancellationToken);
        orphanedSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteExpiredSessions_WithActiveUsers_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Game" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var expiredSession = new ChunkedUploadSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = userId,
            FileName = "expired.pdf",
            TotalFileSize = 1000000,
            TotalChunks = 2,
            ReceivedChunks = 0,
            TempDirectory = "/tmp/uploads",
            Status = "Expired",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            ReceivedChunkIndices = ""
        };
        _dbContext.ChunkedUploadSessions.Add(expiredSession);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete expired session
        _dbContext.ChunkedUploadSessions.Remove(expiredSession);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - User and game remain
        var remainingUser = await _dbContext.Users.FindAsync(userId);
        remainingUser.Should().NotBeNull();

        var remainingGame = await _dbContext.Games.FindAsync(gameId);
        remainingGame.Should().NotBeNull();
    }
}

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
/// Integration tests for DocumentCollection user FK restriction.
/// Tests DeleteBehavior.Restrict for CreatedBy user reference.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupA")]
public class DocumentCollectionUserRestrictionTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_doccol_user_{Guid.NewGuid():N}";

    public DocumentCollectionUserRestrictionTests(SharedTestcontainersFixture fixture)
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

    /// <summary>
    /// Issue #2708: EF Core may throw InvalidOperationException instead of DbUpdateException
    /// when tracked entities have FK constraint violations detected in the change tracker.
    /// </summary>
    [Fact]
    public async Task DeleteUser_WithCreatedDocumentCollections_ThrowsFKConstraintException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "creator@test.com", DisplayName = "Creator", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Gloomhaven" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Gloomhaven Campaign Rules",
            Description = "User-created collection",
            CreatedByUserId = userId,
            DocumentsJson = "[\"doc1.pdf\", \"doc2.pdf\"]"
        };
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents user deletion
        // Issue #2708: EF Core may throw either DbUpdateException (from database) or
        // InvalidOperationException (from change tracker at Remove() or SaveChangesAsync())
        Exception? exception = null;
        try
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.True(exception is DbUpdateException or InvalidOperationException,
            $"Expected DbUpdateException or InvalidOperationException but got {exception.GetType().Name}: {exception.Message}");
    }

    /// <summary>
    /// Issue #2708: EF Core may throw InvalidOperationException instead of DbUpdateException
    /// when tracked entities have FK constraint violations detected in the change tracker.
    /// </summary>
    [Fact]
    public async Task DeleteUser_WithMultipleCreatedCollections_ThrowsFKConstraintException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "prolific@test.com", DisplayName = "Prolific", Role = "User" };
        var game1Id = Guid.NewGuid();
        var game1 = new GameEntity { Id = game1Id, Name = "Terraforming Mars" };
        var game2Id = Guid.NewGuid();
        var game2 = new GameEntity { Id = game2Id, Name = "Spirit Island" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.AddRange(game1, game2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection1 = new DocumentCollectionEntity { Id = Guid.NewGuid(), GameId = game1Id, Name = "TM Collection 1", CreatedByUserId = userId, DocumentsJson = "[]" };
        var collection2 = new DocumentCollectionEntity { Id = Guid.NewGuid(), GameId = game2Id, Name = "SI Collection 1", CreatedByUserId = userId, DocumentsJson = "[]" };
        var collection3 = new DocumentCollectionEntity { Id = Guid.NewGuid(), GameId = game1Id, Name = "TM Collection 2", CreatedByUserId = userId, DocumentsJson = "[]" };

        _dbContext.DocumentCollections.AddRange(collection1, collection2, collection3);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents deletion even with multiple collections
        // Issue #2708: EF Core may throw either DbUpdateException (from database) or
        // InvalidOperationException (from change tracker at Remove() or SaveChangesAsync())
        Exception? exception = null;
        try
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        Assert.True(exception is DbUpdateException or InvalidOperationException,
            $"Expected DbUpdateException or InvalidOperationException but got {exception.GetType().Name}: {exception.Message}");
    }

    [Fact]
    public async Task DeleteUser_WithNoDocumentCollections_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "nocollections@test.com", DisplayName = "No Collections", Role = "User" };

        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete user with no collections
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var deletedUser = await _dbContext.Users.FindAsync(userId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDocumentCollection_ThenDeleteUser_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "cleanup@test.com", DisplayName = "Cleanup", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Pandemic" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Pandemic Rules",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete collection first, then user
        _dbContext.DocumentCollections.Remove(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Both deleted successfully
        var deletedCollection = await _dbContext.DocumentCollections.FindAsync(collectionId);
        deletedCollection.Should().BeNull();

        var deletedUser = await _dbContext.Users.FindAsync(userId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentCollections_ByCreatedByUserId_ReturnsUserCollections()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user1 = new UserEntity { Id = user1Id, Email = "user1@test.com", DisplayName = "User 1", Role = "User" };
        var user2Id = Guid.NewGuid();
        var user2 = new UserEntity { Id = user2Id, Email = "user2@test.com", DisplayName = "User 2", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Root" };

        _dbContext!.Users.AddRange(user1, user2);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var user1Collection = new DocumentCollectionEntity { Id = Guid.NewGuid(), GameId = gameId, Name = "User1 Collection", CreatedByUserId = user1Id, DocumentsJson = "[]" };
        var user2Collection = new DocumentCollectionEntity { Id = Guid.NewGuid(), GameId = gameId, Name = "User2 Collection", CreatedByUserId = user2Id, DocumentsJson = "[]" };

        _dbContext.DocumentCollections.AddRange(user1Collection, user2Collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var user1Collections = await _dbContext.DocumentCollections
            .Where(dc => dc.CreatedByUserId == user1Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        user1Collections.Should().HaveCount(1);
        user1Collections.First().Name.Should().Be("User1 Collection");
    }
}

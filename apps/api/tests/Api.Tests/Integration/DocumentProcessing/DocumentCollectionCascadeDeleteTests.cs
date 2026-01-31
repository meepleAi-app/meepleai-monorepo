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
/// Integration tests for DocumentCollection cascade delete behavior when Game is deleted.
/// Tests DeleteBehavior.Cascade for Game FK and junction table cleanup.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("SharedTestcontainers")]
public class DocumentCollectionCascadeDeleteTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_doccol_cascade_{Guid.NewGuid():N}";

    public DocumentCollectionCascadeDeleteTests(SharedTestcontainersFixture fixture)
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
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task DeleteGame_WithDocumentCollections_CascadesDeleteToCollections()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@test.com",
            DisplayName = "Test User",
            Role = "User"
        };

        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Catan"
        };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collection1Id = Guid.NewGuid();
        var collection1 = new DocumentCollectionEntity
        {
            Id = collection1Id,
            GameId = gameId,
            Name = "Catan Rules Collection",
            Description = "Official rules and expansions",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };

        var collection2Id = Guid.NewGuid();
        var collection2 = new DocumentCollectionEntity
        {
            Id = collection2Id,
            GameId = gameId,
            Name = "Catan FAQ Collection",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };

        _dbContext.DocumentCollections.AddRange(collection1, collection2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game (should cascade to collections)
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Collections are deleted via cascade
        var remainingCollections = await _dbContext.DocumentCollections
            .Where(dc => dc.GameId == gameId)
            .ToListAsync(TestContext.Current.CancellationToken);

        remainingCollections.Should().BeEmpty();

        var deletedGame = await _dbContext.Games.FindAsync(gameId);
        deletedGame.Should().BeNull();
    }

    [Fact]
    public async Task DeleteGame_WithDocumentCollectionsAndChatThreadJunctions_CascadesCleanup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@test.com",
            DisplayName = "User",
            Role = "User"
        };

        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Wingspan"
        };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Wingspan Rules",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create ChatThread
        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Wingspan Questions"
        };
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create ChatThreadCollection junction
        var junction = new ChatThreadCollectionEntity
        {
            ChatThreadId = threadId,
            CollectionId = collectionId
        };
        _dbContext.ChatThreadCollections.Add(junction);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game (cascades to collection, which cascades to junction)
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - No orphaned collections or junctions
        var remainingCollections = await _dbContext.DocumentCollections
            .Where(dc => dc.Id == collectionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        remainingCollections.Should().BeEmpty();

        var remainingJunctions = await _dbContext.ChatThreadCollections
            .Where(ctc => ctc.CollectionId == collectionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        remainingJunctions.Should().BeEmpty();

        // ChatThread should remain (not cascaded from game deletion)
        var remainingThread = await _dbContext.ChatThreads.FindAsync(threadId);
        remainingThread.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteDocumentCollection_LeavesGameIntact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "user@test.com",
            DisplayName = "User",
            Role = "User"
        };

        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Azul"
        };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Azul Collection",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete collection
        _dbContext.DocumentCollections.Remove(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Game remains
        var remainingGame = await _dbContext.Games.FindAsync(gameId);
        remainingGame.Should().NotBeNull();
        remainingGame!.Name.Should().Be("Azul");
    }

    [Fact]
    public async Task DeleteGame_WithNoCollections_Succeeds()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "7 Wonders"
        };

        _dbContext!.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game with no collections
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var deletedGame = await _dbContext.Games.FindAsync(gameId);
        deletedGame.Should().BeNull();
    }
}

using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for ChatThreadCollection junction table FK constraints.
/// Tests DeleteBehavior.Cascade for ChatThread and DocumentCollection FK.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupA")]
public class ChatThreadCollectionForeignKeyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_chatthread_col_fk_{Guid.NewGuid():N}";

    public ChatThreadCollectionForeignKeyTests(SharedTestcontainersFixture fixture)
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
    public async Task DeleteChatThread_WithCollectionJunction_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Gloomhaven" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Gloomhaven Questions",
            CreatedAt = DateTime.UtcNow
        };

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Gloomhaven Collection",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };

        _dbContext.ChatThreads.Add(thread);
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var junctionId = Guid.NewGuid();
        var junction = new ChatThreadCollectionEntity
        {
            Id = junctionId,
            ChatThreadId = threadId,
            CollectionId = collectionId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatThreadCollections.Add(junction);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete chat thread (cascades to junction)
        _dbContext.ChatThreads.Remove(thread);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Junction deleted, collection remains
        var deletedJunction = await _dbContext.ChatThreadCollections.FindAsync(junctionId);
        deletedJunction.Should().BeNull();

        var remainingCollection = await _dbContext.DocumentCollections.FindAsync(collectionId);
        remainingCollection.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteDocumentCollection_WithChatThreadJunction_CascadesDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Wingspan" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            Title = "Bird Questions",
            CreatedAt = DateTime.UtcNow
        };

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity
        {
            Id = collectionId,
            GameId = gameId,
            Name = "Wingspan Expansions",
            CreatedByUserId = userId,
            DocumentsJson = "[]"
        };

        _dbContext.ChatThreads.Add(thread);
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var junctionId = Guid.NewGuid();
        var junction = new ChatThreadCollectionEntity
        {
            Id = junctionId,
            ChatThreadId = threadId,
            CollectionId = collectionId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatThreadCollections.Add(junction);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete collection (cascades to junction)
        _dbContext.DocumentCollections.Remove(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Junction deleted, thread remains
        var deletedJunction = await _dbContext.ChatThreadCollections.FindAsync(junctionId);
        deletedJunction.Should().BeNull();

        var remainingThread = await _dbContext.ChatThreads.FindAsync(threadId);
        remainingThread.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteGame_WithCompleteChain_CascadesCorrectly()
    {
        // Arrange - Complex cascade: Game → Collection → Junction (ChatThread remains)
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "user@test.com", DisplayName = "User", Role = "User" };
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId, Name = "Azul" };

        _dbContext!.Users.Add(user);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var threadId = Guid.NewGuid();
        var thread = new ChatThreadEntity { Id = threadId, UserId = userId, GameId = gameId, Title = "Azul Thread", CreatedAt = DateTime.UtcNow };

        var collectionId = Guid.NewGuid();
        var collection = new DocumentCollectionEntity { Id = collectionId, GameId = gameId, Name = "Azul Collection", CreatedByUserId = userId, DocumentsJson = "[]" };

        _dbContext.ChatThreads.Add(thread);
        _dbContext.DocumentCollections.Add(collection);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var junction = new ChatThreadCollectionEntity { Id = Guid.NewGuid(), ChatThreadId = threadId, CollectionId = collectionId, CreatedAt = DateTime.UtcNow };
        _dbContext.ChatThreadCollections.Add(junction);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete game (Game → Collection → Junction cascade)
        _dbContext.Games.Remove(game);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Collection and junction deleted, thread remains
        var remainingCollections = await _dbContext.DocumentCollections.Where(c => c.GameId == gameId).ToListAsync(TestContext.Current.CancellationToken);
        remainingCollections.Should().BeEmpty();

        var remainingJunctions = await _dbContext.ChatThreadCollections.Where(j => j.CollectionId == collectionId).ToListAsync(TestContext.Current.CancellationToken);
        remainingJunctions.Should().BeEmpty();

        var remainingThread = await _dbContext.ChatThreads.FindAsync(threadId);
        remainingThread.Should().NotBeNull();
    }
}

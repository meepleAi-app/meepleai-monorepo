using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Integration tests for ConversationMemoryRepository with real PostgreSQL + pgvector.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred integration tests.
/// Issue #3985: Integration Tests for Context Engineering Repositories.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3493")]
[Trait("Issue", "3985")]
public sealed class ConversationMemoryRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IConversationMemoryRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ConversationMemoryRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_convmem_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations with retry
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

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new ConversationMemoryRepository(_dbContext, eventCollector);

        await SeedTestDataAsync();
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

    private async Task SeedTestDataAsync()
    {
        // Create test user
        var user = new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Role = "user",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Create test game
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidMemory_PersistsToDatabase()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var memory = new ConversationMemory(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: user.Id,
            gameId: null,
            content: "Test message content",
            messageType: "user",
            timestamp: DateTime.UtcNow);

        // Act
        await _repository!.AddAsync(memory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var entity = await _dbContext.ConversationMemories.FirstOrDefaultAsync(
            m => m.Id == memory.Id,
            TestCancellationToken);

        entity.Should().NotBeNull();
        entity!.Content.Should().Be("Test message content");
        entity.MessageType.Should().Be("user");
        entity.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task AddAsync_WithEmbedding_StoresVectorCorrectly()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var embedding = new float[1024];
        Array.Fill(embedding, 0.1f);

        var memory = new ConversationMemory(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: user.Id,
            gameId: null,
            content: "Response with embedding",
            messageType: "assistant",
            timestamp: DateTime.UtcNow);

        // Act
        await _repository!.AddAsync(memory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Manually set embedding (simulating embedding service)
        var entity = await _dbContext.ConversationMemories.FirstAsync(
            m => m.Id == memory.Id,
            TestCancellationToken);
        entity.Embedding = new Vector(embedding);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var reloaded = await _dbContext.ConversationMemories.FirstAsync(
            m => m.Id == memory.Id,
            TestCancellationToken);

        reloaded.Embedding.Should().NotBeNull();
        reloaded.Embedding!.ToArray().Should().HaveCount(1024);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetBySessionIdAsync_ReturnsMemoriesInTemporalOrder()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var sessionId = Guid.NewGuid();

        var memory1 = new ConversationMemory(
            Guid.NewGuid(), sessionId, user.Id, null, "First message", "user",
            DateTime.UtcNow.AddMinutes(-10));
        var memory2 = new ConversationMemory(
            Guid.NewGuid(), sessionId, user.Id, null, "Second message", "assistant",
            DateTime.UtcNow.AddMinutes(-5));
        var memory3 = new ConversationMemory(
            Guid.NewGuid(), sessionId, user.Id, null, "Third message", "user",
            DateTime.UtcNow);

        await _repository!.AddAsync(memory1, TestCancellationToken);
        await _repository.AddAsync(memory2, TestCancellationToken);
        await _repository.AddAsync(memory3, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetBySessionIdAsync(sessionId, limit: 10, TestCancellationToken);

        // Assert — repository returns memories in descending timestamp order (most recent first)
        results.Should().HaveCount(3);
        results[0].Content.Should().Be("Third message");
        results[1].Content.Should().Be("Second message");
        results[2].Content.Should().Be("First message");
    }

    [Fact]
    public async Task GetByUserIdAndGameIdAsync_FiltersCorrectly()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var game = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var memory1 = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, game.Id, "Game-specific", "user",
            DateTime.UtcNow);
        var memory2 = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null, "General", "user",
            DateTime.UtcNow);

        await _repository!.AddAsync(memory1, TestCancellationToken);
        await _repository.AddAsync(memory2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByUserAndGameAsync(
            user.Id,
            game.Id,
            limit: 10,
            TestCancellationToken);

        // Assert
        results.Should().HaveCount(1);
        results[0].Content.Should().Be("Game-specific");
        results[0].GameId.Should().Be(game.Id);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldMemories_PreservesRecent()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);

        var oldMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null, "Old message", "user",
            DateTime.UtcNow.AddDays(-100));
        var recentMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null, "Recent message", "user",
            DateTime.UtcNow.AddDays(-30));

        await _repository!.AddAsync(oldMemory, TestCancellationToken);
        await _repository.AddAsync(recentMemory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var cutoffDate = DateTime.UtcNow.AddDays(-90);

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate, TestCancellationToken);

        // Assert
        deletedCount.Should().Be(1);

        var remaining = await _dbContext.ConversationMemories.ToListAsync(TestCancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(recentMemory.Id);
        remaining[0].Content.Should().Be("Recent message");
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldMemories_ReturnsZero()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null, "Recent", "user",
            DateTime.UtcNow);

        await _repository!.AddAsync(memory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var cutoffDate = DateTime.UtcNow.AddDays(-90);

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate, TestCancellationToken);

        // Assert
        deletedCount.Should().Be(0);

        var all = await _dbContext!.ConversationMemories.ToListAsync(TestCancellationToken);
        all.Should().HaveCount(1);
    }

    #endregion

    #region Foreign Key and Cascade Tests

    [Fact]
    public async Task AddAsync_WithInvalidUserId_ThrowsDbUpdateException()
    {
        // Arrange
        var invalidUserId = Guid.NewGuid(); // User doesn't exist
        var memory = new ConversationMemory(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: invalidUserId,
            gameId: null,
            content: "Invalid user",
            messageType: "user",
            timestamp: DateTime.UtcNow);

        // Act & Assert
        await _repository!.AddAsync(memory, TestCancellationToken);
        Func<Task> act = () => _dbContext!.SaveChangesAsync(TestCancellationToken);
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeleteUser_CascadesDeleteToConversationMemories()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null, "Test", "user",
            DateTime.UtcNow);

        await _repository!.AddAsync(memory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Delete user (should cascade)
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var memories = await _dbContext.ConversationMemories.ToListAsync(TestCancellationToken);
        memories.Should().BeEmpty();
    }

    #endregion

    #region Vector Similarity Search Tests

    [Fact]
    public async Task VectorSimilaritySearch_ReturnsResultsOrderedByDistance()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);

        // Create 3 memories with distinct embeddings
        var embeddings = new[]
        {
            CreateNormalizedEmbedding(0.5f),  // "close" to query
            CreateNormalizedEmbedding(-0.5f), // "far" from query
            CreateNormalizedEmbedding(0.3f),  // "medium" from query
        };

        for (int i = 0; i < 3; i++)
        {
            var memory = new ConversationMemory(
                Guid.NewGuid(), Guid.NewGuid(), user.Id, null,
                $"Memory {i}", "user", DateTime.UtcNow.AddMinutes(-i));
            await _repository!.AddAsync(memory, TestCancellationToken);
            await _dbContext.SaveChangesAsync(TestCancellationToken);

            var entity = await _dbContext.ConversationMemories
                .FirstAsync(m => m.Id == memory.Id, TestCancellationToken);
            entity.Embedding = new Vector(embeddings[i]);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }

        // Query embedding close to embeddings[0]
        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - LINQ vector similarity search
        var results = await _dbContext.ConversationMemories
            .Where(m => m.Embedding != null)
            .OrderBy(m => m.Embedding!.CosineDistance(queryEmbedding))
            .Take(3)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().HaveCount(3);
        results[0].Content.Should().Be("Memory 0", "Closest embedding should be first");
    }

    [Fact]
    public async Task VectorSimilaritySearch_WithUserFilter_ReturnsOnlyUserMemories()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);

        // Create a second user
        var otherUser = new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "other@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(otherUser);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Add memories for both users with embeddings
        var embedding = CreateNormalizedEmbedding(0.5f);

        var userMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null,
            "User memory", "user", DateTime.UtcNow);
        var otherMemory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), otherUser.Id, null,
            "Other memory", "user", DateTime.UtcNow);

        await _repository!.AddAsync(userMemory, TestCancellationToken);
        await _repository.AddAsync(otherMemory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Set embeddings
        foreach (var id in new[] { userMemory.Id, otherMemory.Id })
        {
            var entity = await _dbContext.ConversationMemories
                .FirstAsync(m => m.Id == id, TestCancellationToken);
            entity.Embedding = new Vector(embedding);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - Filtered vector search
        var results = await _dbContext.ConversationMemories
            .Where(m => m.UserId == user.Id && m.Embedding != null)
            .OrderBy(m => m.Embedding!.CosineDistance(queryEmbedding))
            .Take(10)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().AllSatisfy(r => r.UserId.Should().Be(user.Id));
        results.Should().NotContain(r => r.Content == "Other memory");
    }

    [Fact]
    public async Task VectorSimilaritySearch_RawSql_ReturnsCorrectResults()
    {
        // Arrange
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);

        var embedding = CreateNormalizedEmbedding(0.7f);
        var memory = new ConversationMemory(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, null,
            "Embedded memory", "assistant", DateTime.UtcNow);

        await _repository!.AddAsync(memory, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var entity = await _dbContext.ConversationMemories
            .FirstAsync(m => m.Id == memory.Id, TestCancellationToken);
        entity.Embedding = new Vector(embedding);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.7f));

        // Act - Raw SQL vector similarity search
        var results = await _dbContext.ConversationMemories
            .FromSqlRaw(@"
                SELECT id, session_id, user_id, game_id, content, message_type, timestamp, embedding
                FROM conversation_memory
                ORDER BY embedding <=> {0}::vector
                LIMIT 10",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.Content == "Embedded memory");
    }

    private static float[] CreateNormalizedEmbedding(float baseValue)
    {
        var embedding = new float[1024];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = baseValue + (i % 10) * 0.01f;
        }

        // Normalize to unit vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => (double)x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    #endregion

    #region Index Verification Tests

    [Fact]
    public async Task Indexes_Exist_OnAllRequiredColumns()
    {
        // Arrange & Act - Query database for indexes
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'conversation_memory'
            ORDER BY indexname;
        ";

        var indexes = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestCancellationToken))
        {
            while (await reader.ReadAsync(TestCancellationToken))
            {
                indexes.Add(reader.GetString(0));
            }
        }

        // Assert — EF Core generates uppercase prefix (IX_/PK_), use case-insensitive comparison
        var lowerIndexes = indexes.Select(i => i.ToLowerInvariant()).ToList();
        lowerIndexes.Should().Contain("ix_conversation_memory_session_id");
        lowerIndexes.Should().Contain("ix_conversation_memory_user_id");
        lowerIndexes.Should().Contain("ix_conversation_memory_user_id_game_id");
        lowerIndexes.Should().Contain("ix_conversation_memory_timestamp");
        lowerIndexes.Should().Contain("pk_conversation_memory");
    }

    #endregion
}

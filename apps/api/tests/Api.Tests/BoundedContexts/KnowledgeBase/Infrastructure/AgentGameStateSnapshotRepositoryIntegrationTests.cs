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
/// Integration tests for AgentGameStateSnapshotRepository with real PostgreSQL + pgvector.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred integration tests.
/// Issue #3985: Integration Tests for Context Engineering Repositories.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3493")]
[Trait("Issue", "3985")]
public sealed class AgentGameStateSnapshotRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAgentGameStateSnapshotRepository? _repository;
    private IServiceProvider? _serviceProvider;
    private Guid _gameId;
    private Guid _agentSessionId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AgentGameStateSnapshotRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_snapshot_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

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

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new AgentGameStateSnapshotRepository(_dbContext, eventCollector);

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
        // Create minimal test data
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _gameId = game.Id;
        _dbContext!.Games.Add(game);

        // Create fake agent session ID (not actually creating AgentSession to simplify)
        _agentSessionId = Guid.NewGuid();

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidSnapshot_PersistsToDatabase()
    {
        // Arrange
        var boardState = new { turn = 1, players = new[] { "Alice", "Bob" } };
        var snapshot = new AgentGameStateSnapshot(
            id: Guid.NewGuid(),
            gameId: _gameId,
            agentSessionId: _agentSessionId,
            boardStateJson: System.Text.Json.JsonSerializer.Serialize(boardState),
            turnNumber: 1);

        // Act
        await _repository!.AddAsync(snapshot, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var entity = await _dbContext.AgentGameStateSnapshots.FirstOrDefaultAsync(
            s => s.Id == snapshot.Id,
            TestCancellationToken);

        entity.Should().NotBeNull();
        entity!.GameId.Should().Be(_gameId);
        entity.TurnNumber.Should().Be(1);
        entity.BoardStateJson.Should().Contain("Alice");
    }

    [Fact]
    public async Task AddAsync_WithJSONBoardState_QueryableViaJsonOperations()
    {
        // Arrange
        var boardState = """{"turn": 5, "activePlayer": "Alice"}""";
        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, boardState, 5);

        await _repository!.AddAsync(snapshot, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Query using JSONB operations
        var result = await _dbContext.AgentGameStateSnapshots
            .FirstOrDefaultAsync(
                s => EF.Functions.JsonContains(s.BoardStateJson, """{"activePlayer":"Alice"}"""),
                TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(snapshot.Id);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetByGameIdAsync_ReturnsSnapshotsInTemporalOrder()
    {
        // Arrange
        var snapshot1 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 1}""", 1);
        var snapshot2 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 2}""", 2);
        var snapshot3 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 3}""", 3);

        await _repository!.AddAsync(snapshot1, TestCancellationToken);
        await _repository.AddAsync(snapshot2, TestCancellationToken);
        await _repository.AddAsync(snapshot3, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByGameIdAsync(_gameId, limit: 10, TestCancellationToken);

        // Assert
        results.Should().HaveCount(3);
        // Ordered by turn number descending (latest first)
        results[0].TurnNumber.Should().Be(3);
        results[1].TurnNumber.Should().Be(2);
        results[2].TurnNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetLatestByGameIdAsync_ReturnsLatestSnapshot()
    {
        // Arrange
        var snapshot1 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 1}""", 1);
        var snapshot2 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 5}""", 5);

        await _repository!.AddAsync(snapshot1, TestCancellationToken);
        await _repository.AddAsync(snapshot2, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetLatestByGameIdAsync(_gameId, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.TurnNumber.Should().Be(5);
        result.BoardStateJson.Should().Contain("\"turn\": 5");
    }

    #endregion

    #region Vector Similarity Search Tests

    [Fact]
    public async Task VectorSimilaritySearch_ReturnsSnapshotsOrderedByDistance()
    {
        // Arrange - Create snapshots with distinct embeddings
        var embeddings = new[]
        {
            CreateNormalizedEmbedding(0.5f),
            CreateNormalizedEmbedding(-0.5f),
            CreateNormalizedEmbedding(0.3f),
        };

        for (int i = 0; i < 3; i++)
        {
            var snapshot = new AgentGameStateSnapshot(
                Guid.NewGuid(), _gameId, _agentSessionId,
                $"{{\"turn\": {i + 10}}}", i + 10);
            await _repository!.AddAsync(snapshot, TestCancellationToken);
            await _dbContext!.SaveChangesAsync(TestCancellationToken);

            var entity = await _dbContext.AgentGameStateSnapshots
                .FirstAsync(s => s.Id == snapshot.Id, TestCancellationToken);
            entity.Embedding = new Vector(embeddings[i]);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - LINQ CosineDistance
        var results = await _dbContext!.AgentGameStateSnapshots
            .Where(s => s.Embedding != null)
            .OrderBy(s => s.Embedding!.CosineDistance(queryEmbedding))
            .Take(3)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().HaveCount(3);
        results[0].TurnNumber.Should().Be(10, "Closest embedding should be first");
    }

    [Fact]
    public async Task VectorSimilaritySearch_FilteredByGame_ReturnsOnlyGameSnapshots()
    {
        // Arrange - Create a second game
        var otherGame = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Other Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Games.Add(otherGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var embedding = CreateNormalizedEmbedding(0.5f);

        // Add snapshot for each game
        var snap1 = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId, """{"turn": 1}""", 1);
        var snap2 = new AgentGameStateSnapshot(
            Guid.NewGuid(), otherGame.Id, _agentSessionId, """{"turn": 2}""", 2);

        await _repository!.AddAsync(snap1, TestCancellationToken);
        await _repository.AddAsync(snap2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        foreach (var id in new[] { snap1.Id, snap2.Id })
        {
            var entity = await _dbContext.AgentGameStateSnapshots
                .FirstAsync(s => s.Id == id, TestCancellationToken);
            entity.Embedding = new Vector(embedding);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - Game-filtered vector search
        var results = await _dbContext.AgentGameStateSnapshots
            .Where(s => s.GameId == _gameId && s.Embedding != null)
            .OrderBy(s => s.Embedding!.CosineDistance(queryEmbedding))
            .Take(5)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().AllSatisfy(s => s.GameId.Should().Be(_gameId));
        results.Should().NotContain(s => s.GameId == otherGame.Id);
    }

    [Fact]
    public async Task VectorSimilaritySearch_RawSql_ReturnsCorrectResults()
    {
        // Arrange
        var embedding = CreateNormalizedEmbedding(0.7f);
        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), _gameId, _agentSessionId,
            """{"turn": 99, "state": "embedded"}""", 99);

        await _repository!.AddAsync(snapshot, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var entity = await _dbContext.AgentGameStateSnapshots
            .FirstAsync(s => s.Id == snapshot.Id, TestCancellationToken);
        entity.Embedding = new Vector(embedding);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.7f));

        // Act - Raw SQL vector similarity
        var results = await _dbContext.AgentGameStateSnapshots
            .FromSqlRaw(@"
                SELECT id, game_id, agent_session_id, board_state_json, turn_number, created_at, embedding
                FROM agent_game_state_snapshots
                ORDER BY embedding <=> {0}::vector
                LIMIT 5",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(s => s.TurnNumber == 99);
    }

    private static float[] CreateNormalizedEmbedding(float baseValue)
    {
        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = baseValue + (i % 10) * 0.01f;
        }

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
        // Arrange & Act
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'agent_game_state_snapshots'
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

        // Assert
        indexes.Should().Contain("ix_agent_game_state_snapshots_game_id");
        indexes.Should().Contain("ix_agent_game_state_snapshots_agent_session_id");
        indexes.Should().Contain("ix_agent_game_state_snapshots_game_id_turn_number");
        indexes.Should().Contain("ix_agent_game_state_snapshots_created_at");
        indexes.Should().Contain("pk_agent_game_state_snapshots");
    }

    #endregion
}

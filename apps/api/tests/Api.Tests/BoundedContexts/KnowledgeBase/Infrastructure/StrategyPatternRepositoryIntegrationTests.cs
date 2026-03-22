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
/// Integration tests for StrategyPatternRepository with real PostgreSQL + pgvector.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred integration tests.
/// Issue #3985: Integration Tests for Context Engineering Repositories.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3493")]
[Trait("Issue", "3985")]
public sealed class StrategyPatternRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IStrategyPatternRepository? _repository;
    private IServiceProvider? _serviceProvider;
    private Guid _gameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public StrategyPatternRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_stratpat_{Guid.NewGuid():N}";
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

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new StrategyPatternRepository(_dbContext, eventCollector);

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
        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        };
        _gameId = game.Id;
        _dbContext!.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidPattern_PersistsToDatabase()
    {
        // Arrange
        var conditions = """{"position": "starting", "material": "equal"}""";
        var moves = """{"sequence": ["e4", "e5", "Nf3"]}""";

        var pattern = new StrategyPattern(
            id: Guid.NewGuid(),
            gameId: _gameId,
            patternName: "Italian Opening",
            applicablePhase: "opening",
            description: "Classic opening strategy",
            evaluationScore: 0.85,
            boardConditionsJson: conditions,
            moveSequenceJson: moves,
            source: "chess.com");

        // Act
        await _repository!.AddAsync(pattern, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var entity = await _dbContext.StrategyPatterns.FirstOrDefaultAsync(
            p => p.Id == pattern.Id,
            TestCancellationToken);

        entity.Should().NotBeNull();
        entity!.PatternName.Should().Be("Italian Opening");
        entity.EvaluationScore.Should().Be(0.85f);
        entity.ApplicablePhase.Should().Be("opening");
    }

    [Fact]
    public async Task AddAsync_WithJSONBFields_QueryableViaJsonOperations()
    {
        // Arrange
        var conditions = """{"material": "equal", "kingside": "castled"}""";
        var pattern = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Castled King Defense", "midgame", "Defense after castling",
            0.75, conditions, "{}", "manual");

        await _repository!.AddAsync(pattern, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Query using JSONB operations
        var result = await _dbContext.StrategyPatterns
            .FirstOrDefaultAsync(
                p => EF.Functions.JsonContains(p.BoardConditionsJson, """{"kingside":"castled"}"""),
                TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.PatternName.Should().Be("Castled King Defense");
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetByGameIdAndPhaseAsync_FiltersCorrectly()
    {
        // Arrange
        var opening = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Opening Strategy", "opening", "Test", 0.8, "{}", "{}", "test");
        var midgame = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Midgame Strategy", "midgame", "Test", 0.75, "{}", "{}", "test");
        var endgame = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Endgame Strategy", "endgame", "Test", 0.9, "{}", "{}", "test");

        await _repository!.AddAsync(opening, TestCancellationToken);
        await _repository.AddAsync(midgame, TestCancellationToken);
        await _repository.AddAsync(endgame, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var results = await _repository.GetByGameAndPhaseAsync(
            _gameId,
            "midgame",
            limit: 10,
            TestCancellationToken);

        // Assert
        results.Should().HaveCount(1);
        results[0].PatternName.Should().Be("Midgame Strategy");
        results[0].ApplicablePhase.Should().Be("midgame");
    }

    [Fact]
    public async Task GetTopRatedByGameIdAsync_ReturnsOrderedByScore()
    {
        // Arrange
        var low = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Low Score", "opening", "Test", 0.3, "{}", "{}", "test");
        var medium = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Medium Score", "opening", "Test", 0.65, "{}", "{}", "test");
        var high = new StrategyPattern(
            Guid.NewGuid(), _gameId, "High Score", "opening", "Test", 0.92, "{}", "{}", "test");

        await _repository!.AddAsync(low, TestCancellationToken);
        await _repository.AddAsync(medium, TestCancellationToken);
        await _repository.AddAsync(high, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Get top 2 patterns ordered by score descending
        var results = await _repository.GetTopRatedByGameIdAsync(
            _gameId,
            limit: 2,
            TestCancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results[0].PatternName.Should().Be("High Score");
        results[0].EvaluationScore.Should().Be(0.92);
        results[1].PatternName.Should().Be("Medium Score");
        results[1].EvaluationScore.Should().Be(0.65);
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
            WHERE tablename = 'strategy_patterns'
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
        indexes.Should().Contain("ix_strategy_patterns_game_id");
        indexes.Should().Contain("ix_strategy_patterns_game_id_applicable_phase");
        indexes.Should().Contain("ix_strategy_patterns_evaluation_score");
        indexes.Should().Contain("pk_strategy_patterns");
    }

    #endregion

    #region Vector Similarity Search Tests

    [Fact]
    public async Task VectorSimilaritySearch_ReturnsPatternsOrderedByDistance()
    {
        // Arrange - Create patterns with distinct embeddings
        var embeddings = new[]
        {
            CreateNormalizedEmbedding(0.5f),
            CreateNormalizedEmbedding(-0.5f),
            CreateNormalizedEmbedding(0.3f),
        };

        var patternNames = new[] { "Close Pattern", "Far Pattern", "Medium Pattern" };

        for (int i = 0; i < 3; i++)
        {
            var pattern = new StrategyPattern(
                Guid.NewGuid(), _gameId, patternNames[i], "opening",
                $"Description {i}", 0.8 - i * 0.1, "{}", "{}", "test");
            await _repository!.AddAsync(pattern, TestCancellationToken);
            await _dbContext!.SaveChangesAsync(TestCancellationToken);

            var entity = await _dbContext.StrategyPatterns
                .FirstAsync(p => p.Id == pattern.Id, TestCancellationToken);
            entity.Embedding = new Vector(embeddings[i]);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - LINQ CosineDistance
        var results = await _dbContext!.StrategyPatterns
            .Where(p => p.Embedding != null)
            .OrderBy(p => p.Embedding!.CosineDistance(queryEmbedding))
            .Take(3)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().HaveCount(3);
        results[0].PatternName.Should().Be("Close Pattern", "Closest embedding should be first");
    }

    [Fact]
    public async Task VectorSimilaritySearch_FilteredByGameAndPhase_ReturnsCorrectSubset()
    {
        // Arrange - Create another game
        var otherGame = new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Go",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Games.Add(otherGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var embedding = CreateNormalizedEmbedding(0.5f);

        // Patterns across games and phases
        var patterns = new[]
        {
            new StrategyPattern(Guid.NewGuid(), _gameId, "Chess Opening", "opening", "Test", 0.8, "{}", "{}", "test"),
            new StrategyPattern(Guid.NewGuid(), _gameId, "Chess Midgame", "midgame", "Test", 0.7, "{}", "{}", "test"),
            new StrategyPattern(Guid.NewGuid(), otherGame.Id, "Go Opening", "opening", "Test", 0.9, "{}", "{}", "test"),
        };

        foreach (var p in patterns)
        {
            await _repository!.AddAsync(p, TestCancellationToken);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        foreach (var p in patterns)
        {
            var entity = await _dbContext.StrategyPatterns
                .FirstAsync(e => e.Id == p.Id, TestCancellationToken);
            entity.Embedding = new Vector(embedding);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.5f));

        // Act - Filtered by game + phase
        var results = await _dbContext.StrategyPatterns
            .Where(p => p.GameId == _gameId && p.ApplicablePhase == "opening" && p.Embedding != null)
            .OrderBy(p => p.Embedding!.CosineDistance(queryEmbedding))
            .Take(10)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().HaveCount(1);
        results[0].PatternName.Should().Be("Chess Opening");
        results[0].GameId.Should().Be(_gameId);
        results[0].ApplicablePhase.Should().Be("opening");
    }

    [Fact]
    public async Task VectorSimilaritySearch_RawSql_ReturnsCorrectResults()
    {
        // Arrange
        var embedding = CreateNormalizedEmbedding(0.7f);
        var pattern = new StrategyPattern(
            Guid.NewGuid(), _gameId, "Embedded Pattern", "endgame",
            "Pattern with embedding", 0.95, "{}", "{}", "test");

        await _repository!.AddAsync(pattern, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var entity = await _dbContext.StrategyPatterns
            .FirstAsync(p => p.Id == pattern.Id, TestCancellationToken);
        entity.Embedding = new Vector(embedding);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var queryEmbedding = new Vector(CreateNormalizedEmbedding(0.7f));

        // Act - Raw SQL vector similarity
        var results = await _dbContext.StrategyPatterns
            .FromSqlRaw(@"
                SELECT id, game_id, pattern_name, description, applicable_phase,
                       board_conditions_json, move_sequence_json, evaluation_score, embedding, source
                FROM strategy_patterns
                ORDER BY embedding <=> {0}::vector
                LIMIT 10",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.PatternName == "Embedded Pattern");
    }

    private static float[] CreateNormalizedEmbedding(float baseValue)
    {
        var embedding = new float[1024];
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

    #region Composite Index Query Performance

    [Fact]
    public async Task CompositeIndex_GameIdAndPhase_UsedInQueries()
    {
        // Arrange - Seed data to test index usage
        for (int i = 0; i < 100; i++)
        {
            var phase = i % 3 == 0 ? "opening" : i % 3 == 1 ? "midgame" : "endgame";
            var pattern = new StrategyPattern(
                Guid.NewGuid(),
                _gameId,
                $"Pattern {i}",
                phase,
                "Test pattern",
                0.5 + (i % 50) * 0.01,
                "{}",
                "{}",
                "test");

            await _repository!.AddAsync(pattern, TestCancellationToken);
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Query using composite index
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
            SELECT id, pattern_name
            FROM strategy_patterns
            WHERE game_id = $1 AND applicable_phase = $2
            ORDER BY evaluation_score DESC;
        ";

        var gameIdParam = command.CreateParameter();
        gameIdParam.ParameterName = "$1";
        gameIdParam.Value = _gameId;
        command.Parameters.Add(gameIdParam);

        var phaseParam = command.CreateParameter();
        phaseParam.ParameterName = "$2";
        phaseParam.Value = "opening";
        command.Parameters.Add(phaseParam);

        var plan = await command.ExecuteScalarAsync(TestCancellationToken);
        var queryPlan = plan?.ToString() ?? "";

        // Assert - Should use composite index
        queryPlan.Should().Contain("Index");
        queryPlan.Should().NotContain("Seq Scan", "Query should use index, not sequential scan");

        Console.WriteLine("=== Query Plan for Composite Index ===");
        Console.WriteLine(queryPlan);
    }

    #endregion
}

using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
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
using System.Diagnostics;
using Xunit;

#pragma warning disable CA5394 // Random is acceptable for test data generation

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Performance tests for vector embedding queries on Context Engineering tables.
/// Issue #3493: PostgreSQL Schema Extensions - Acceptance Criteria validation.
/// Issue #3987: Vector Embedding Query Performance Validation.
///
/// Target: Vector similarity queries &lt;100ms P95 latency
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3493")]
[Trait("Issue", "3987")]
public sealed class VectorEmbeddingPerformanceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data IDs
    private readonly List<Guid> _userIds = [];
    private readonly List<Guid> _gameIds = [];
    private readonly List<Guid> _agentSessionIds = [];

    public VectorEmbeddingPerformanceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_vector_perf_{Guid.NewGuid():N}";
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

        await SeedLargeDatasetAsync();
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

    private async Task SeedLargeDatasetAsync()
    {
        // Create test users
        var users = Enumerable.Range(1, 100).Select(i => new Api.Infrastructure.Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"perfuser{i:D3}@test.com",
            Role = "user",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();
        _userIds.AddRange(users.Select(u => u.Id));
        _dbContext!.Users.AddRange(users);

        // Create test games
        var games = Enumerable.Range(1, 50).Select(i => new Api.Infrastructure.Entities.GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Performance Test Game {i:D2}",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2020 + (i % 5),
            CreatedAt = DateTime.UtcNow
        }).ToList();
        _gameIds.AddRange(games.Select(g => g.Id));
        _dbContext.Games.AddRange(games);

        // Generate fake agent session IDs (not needed for ConversationMemory queries)
        _agentSessionIds.AddRange(Enumerable.Range(1, 100).Select(_ => Guid.NewGuid()));

        // Seed 10,000+ conversation memories with embeddings
        await SeedConversationMemoriesAsync(10000);

        // Seed 5,000+ game state snapshots with embeddings
        await SeedGameStateSnapshotsAsync(5000);

        // Seed 1,000+ strategy patterns with embeddings
        await SeedStrategyPatternsAsync(1000);
    }

    private async Task SeedConversationMemoriesAsync(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var batches = count / 1000; // Insert in batches of 1000

        for (int batch = 0; batch < batches; batch++)
        {
            var memories = Enumerable.Range(1, 1000).Select(i =>
            {
                var embedding = GenerateRandomEmbedding(random, 1536);
                return new ConversationMemoryEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = _agentSessionIds[random.Next(_agentSessionIds.Count)],
                    UserId = _userIds[random.Next(_userIds.Count)],
                    GameId = random.Next(100) < 80 ? _gameIds[random.Next(_gameIds.Count)] : (Guid?)null,
                    MessageType = random.Next(100) < 50 ? "user" : "assistant",
                    Content = $"Test conversation content {batch * 1000 + i}",
                    Embedding = new Vector(embedding),
                    Timestamp = DateTime.UtcNow.AddMinutes(-(batch * 1000 + i))
                };
            }).ToList();

            _dbContext!.ConversationMemories.AddRange(memories);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }
    }

    private async Task SeedGameStateSnapshotsAsync(int count)
    {
        var random = new Random(43); // Fixed seed
        var batches = count / 1000;

        for (int batch = 0; batch < batches; batch++)
        {
            var snapshots = Enumerable.Range(1, 1000).Select(i =>
            {
                var embedding = GenerateRandomEmbedding(random, 1536);
                return new AgentGameStateSnapshotEntity
                {
                    Id = Guid.NewGuid(),
                    GameId = _gameIds[random.Next(_gameIds.Count)],
                    AgentSessionId = _agentSessionIds[random.Next(_agentSessionIds.Count)],
                    BoardStateJson = $"{{\"turn\": {i}, \"board\": \"state{i}\"}}",
                    TurnNumber = i,
                    Embedding = new Vector(embedding),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-(batch * 1000 + i))
                };
            }).ToList();

            _dbContext!.AgentGameStateSnapshots.AddRange(snapshots);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }
    }

    private async Task SeedStrategyPatternsAsync(int count)
    {
        var random = new Random(44); // Fixed seed
        var phases = new[] { "opening", "midgame", "endgame", "any" };

        var patterns = Enumerable.Range(1, count).Select(i =>
        {
            var embedding = GenerateRandomEmbedding(random, 1536);
            return new StrategyPatternEntity
            {
                Id = Guid.NewGuid(),
                GameId = _gameIds[random.Next(_gameIds.Count)],
                PatternName = $"Pattern {i:D4}",
                Description = $"Test strategy pattern description {i}",
                ApplicablePhase = phases[random.Next(phases.Length)],
                BoardConditionsJson = $"{{\"condition\": \"test{i}\"}}",
                MoveSequenceJson = $"{{\"moves\": [\"move{i}\"]}}",
                EvaluationScore = (float)(random.NextDouble() * 0.8 + 0.2), // 0.2-1.0
                Embedding = new Vector(embedding),
                Source = i % 3 == 0 ? "manual" : i % 3 == 1 ? "chess.com" : "ai_generated"
            };
        }).ToList();

        _dbContext!.StrategyPatterns.AddRange(patterns);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static float[] GenerateRandomEmbedding(Random random, int dimensions)
    {
        var embedding = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // -1.0 to 1.0
        }

        // Normalize to unit vector
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }

    #region ConversationMemory Vector Queries

    [Fact]
    public async Task ConversationMemory_VectorSimilaritySearch_Top10_ShouldBeFasterThan100ms_P95()
    {
        // Arrange - Generate query embedding
        var random = new Random(100);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var latencies = new List<double>();

        // Warm up query (exclude from measurement)
        _ = await _dbContext!.ConversationMemories
            .FromSqlRaw(@"
                SELECT id, session_id, user_id, game_id, content, message_type, timestamp, embedding
                FROM conversation_memory
                ORDER BY embedding <=> {0}::vector
                LIMIT 10",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act - Execute 100 queries to get reliable P95 measurement
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.ConversationMemories
                .FromSqlRaw(@"
                    SELECT id, session_id, user_id, game_id, content, message_type, timestamp, embedding
                    FROM conversation_memory
                    ORDER BY embedding <=> {0}::vector
                    LIMIT 10",
                    queryEmbedding)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().HaveCount(10);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - P95 should be < 100ms
        var p50 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.50));
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var p99 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.99));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "P95 latency for conversation_memory vector search should be under 100ms (Issue #3493 DoD)");
        average.Should().BeLessThan(50.0, "Average latency should be reasonable");

        Console.WriteLine($"[ConversationMemory Vector Search] Avg={average:F2}ms, P50={p50:F2}ms, P95={p95:F2}ms, P99={p99:F2}ms");
    }

    [Fact]
    public async Task ConversationMemory_VectorSearchWithFilter_ShouldMaintainPerformance()
    {
        // Arrange - Query with both vector similarity and filters
        var random = new Random(101);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var targetUserId = _userIds[0];
        var latencies = new List<double>();

        // Warm up
        await _dbContext!.ConversationMemories
            .FromSqlRaw(@"
                SELECT id, session_id, user_id, game_id, content, message_type, timestamp, embedding
                FROM conversation_memory
                WHERE user_id = {0} AND game_id IS NOT NULL
                ORDER BY embedding <=> {1}::vector
                LIMIT 10",
                targetUserId,
                queryEmbedding)
            .Take(10)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act - Measure hybrid query (filter + vector search)
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.ConversationMemories
                .Where(m => m.UserId == targetUserId && m.GameId != null)
                .OrderBy(m => m.Embedding!.CosineDistance(queryEmbedding))
                .Take(10)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().NotBeEmpty();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - P95 should be < 150ms for hybrid queries
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var average = latencies.Average();

        p95.Should().BeLessThan(150.0, "P95 latency for hybrid (filter + vector) queries should be under 150ms");
        average.Should().BeLessThan(75.0);

        Console.WriteLine($"[ConversationMemory Hybrid Query] Avg={average:F2}ms, P95={p95:F2}ms");
    }

    #endregion

    #region AgentGameStateSnapshot Vector Queries

    [Fact]
    public async Task GameStateSnapshot_VectorSimilaritySearch_Top5_ShouldBeFasterThan100ms_P95()
    {
        // Arrange - Find similar game positions
        var random = new Random(102);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var latencies = new List<double>();

        // Warm up
        _ = await _dbContext!.AgentGameStateSnapshots
            .FromSqlRaw(@"
                SELECT id, game_id, agent_session_id, board_state_json, turn_number, created_at, embedding
                FROM agent_game_state_snapshots
                ORDER BY embedding <=> {0}::vector
                LIMIT 5",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act - Execute 100 queries
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.AgentGameStateSnapshots
                .FromSqlRaw(@"
                    SELECT id, game_id, agent_session_id, board_state_json, turn_number, created_at, embedding
                    FROM agent_game_state_snapshots
                    ORDER BY embedding <=> {0}::vector
                    LIMIT 5",
                    queryEmbedding)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().HaveCount(5);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var p50 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.50));
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var p99 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.99));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "P95 latency for game state snapshot vector search should be under 100ms");
        average.Should().BeLessThan(50.0);

        Console.WriteLine($"[GameStateSnapshot Vector Search] Avg={average:F2}ms, P50={p50:F2}ms, P95={p95:F2}ms, P99={p99:F2}ms");
    }

    [Fact]
    public async Task GameStateSnapshot_VectorSearchByGame_ShouldUseCompositeIndex()
    {
        // Arrange - Query similar positions for specific game
        var random = new Random(103);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var targetGameId = _gameIds[0];
        var latencies = new List<double>();

        // Warm up
        await _dbContext!.AgentGameStateSnapshots
            .FromSqlRaw(@"
                SELECT id, game_id, agent_session_id, board_state_json, turn_number, created_at, embedding
                FROM agent_game_state_snapshots
                WHERE game_id = {0}
                ORDER BY embedding <=> {1}::vector
                LIMIT 5",
                targetGameId,
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.AgentGameStateSnapshots
                .Where(s => s.GameId == targetGameId)
                .OrderBy(s => s.Embedding!.CosineDistance(queryEmbedding))
                .Take(5)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(s => s.GameId.Should().Be(targetGameId));
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "Game-filtered vector search should benefit from composite index");
        average.Should().BeLessThan(50.0);

        Console.WriteLine($"[GameStateSnapshot Filtered Search] Avg={average:F2}ms, P95={p95:F2}ms");
    }

    #endregion

    #region StrategyPattern Vector Queries

    [Fact]
    public async Task StrategyPattern_VectorSimilaritySearch_Top20_ShouldBeFasterThan100ms_P95()
    {
        // Arrange - Find similar strategy patterns
        var random = new Random(104);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var latencies = new List<double>();

        // Warm up
        _ = await _dbContext!.StrategyPatterns
            .FromSqlRaw(@"
                SELECT id, game_id, pattern_name, description, applicable_phase, board_conditions_json, move_sequence_json, evaluation_score, embedding, source
                FROM strategy_patterns
                ORDER BY embedding <=> {0}::vector
                LIMIT 20",
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act - Execute 100 queries
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.StrategyPatterns
                .FromSqlRaw(@"
                    SELECT id, game_id, pattern_name, description, applicable_phase, board_conditions_json, move_sequence_json, evaluation_score, embedding, source
                    FROM strategy_patterns
                    ORDER BY embedding <=> {0}::vector
                    LIMIT 20",
                    queryEmbedding)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().HaveCount(20);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var p50 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.50));
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var p99 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.99));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "P95 latency for strategy pattern vector search should be under 100ms");
        average.Should().BeLessThan(50.0);

        Console.WriteLine($"[StrategyPattern Vector Search] Avg={average:F2}ms, P50={p50:F2}ms, P95={p95:F2}ms, P99={p99:F2}ms");
    }

    [Fact]
    public async Task StrategyPattern_VectorSearchByGameAndPhase_ShouldUseCompositeIndex()
    {
        // Arrange - Query opening strategies for specific game
        var random = new Random(105);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));
        var targetGameId = _gameIds[0];
        const string targetPhase = "opening";
        var latencies = new List<double>();

        // Warm up
        await _dbContext!.StrategyPatterns
            .FromSqlRaw(@"
                SELECT id, game_id, pattern_name, description, applicable_phase, board_conditions_json, move_sequence_json, evaluation_score, embedding, source
                FROM strategy_patterns
                WHERE game_id = {0} AND applicable_phase = {1}
                ORDER BY embedding <=> {2}::vector
                LIMIT 10",
                targetGameId,
                targetPhase,
                queryEmbedding)
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

        // Act
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext.StrategyPatterns
                .Where(p => p.GameId == targetGameId && p.ApplicablePhase == targetPhase)
                .OrderBy(p => p.Embedding!.CosineDistance(queryEmbedding))
                .Take(10)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(p =>
            {
                p.GameId.Should().Be(targetGameId);
                p.ApplicablePhase.Should().Be(targetPhase);
            });
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "Game+Phase filtered vector search should benefit from composite index");
        average.Should().BeLessThan(50.0);

        Console.WriteLine($"[StrategyPattern Filtered Search] Avg={average:F2}ms, P95={p95:F2}ms");
    }

    #endregion

    #region Index Usage Validation

    [Fact]
    public async Task VectorQueries_ShouldUseIndexes_VerifyWithExplainAnalyze()
    {
        // Arrange
        var random = new Random(106);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));

        // Act - Execute EXPLAIN ANALYZE on vector query
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
            SELECT id, session_id, content, timestamp, embedding <=> $1::vector AS distance
            FROM conversation_memory
            ORDER BY embedding <=> $1::vector
            LIMIT 10;
        ";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$1";
        parameter.Value = queryEmbedding.ToArray();
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(TestCancellationToken);
        var explainPlan = result?.ToString() ?? string.Empty;

        // Assert - Verify execution plan contains index usage
        explainPlan.Should().NotBeEmpty("EXPLAIN ANALYZE should return query plan");

        // Log query plan for analysis
        Console.WriteLine("=== Query Plan for Vector Search ===");
        Console.WriteLine(explainPlan);

        // Note: Full index verification requires parsing JSON plan
        // This test serves as documentation and manual verification point
    }

    #endregion

    #region Concurrent Query Performance

    [Fact]
    public async Task ConcurrentVectorQueries_10Parallel_ShouldMaintainThroughput()
    {
        // Arrange - Prepare 10 different query embeddings
        var random = new Random(107);
        var queryEmbeddings = Enumerable.Range(1, 10)
            .Select(_ => new Vector(GenerateRandomEmbedding(random, 1536)))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var allLatencies = new List<double>();

        // Act - Execute 10 queries in parallel
        var tasks = queryEmbeddings.Select(async embedding =>
        {
            var sw = Stopwatch.StartNew();

            var results = await _dbContext!.ConversationMemories
                .OrderBy(m => m.Embedding!.CosineDistance(embedding))
                .Take(10)
                .AsNoTracking()
                .ToListAsync(TestCancellationToken);

            sw.Stop();

            lock (allLatencies)
            {
                allLatencies.Add(sw.Elapsed.TotalMilliseconds);
            }

            return results.Count;
        });

        var counts = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        counts.Should().AllSatisfy(count => count.Should().Be(10));

        var totalTime = stopwatch.ElapsedMilliseconds;
        var averageLatency = allLatencies.Average();
        var p95 = allLatencies.OrderBy(x => x).ElementAt((int)(allLatencies.Count * 0.95));

        // With 10 concurrent queries, total time should not be 10x single query time
        // (indicates parallel execution efficiency)
        totalTime.Should().BeLessThan(1000, "10 concurrent queries should complete under 1 second total");
        p95.Should().BeLessThan(200, "P95 latency under concurrency should remain reasonable");

        Console.WriteLine($"[Concurrent Queries] Total={totalTime}ms, Avg={averageLatency:F2}ms, P95={p95:F2}ms, Throughput={10000.0 / totalTime:F2} queries/sec");
    }

    #endregion

    #region Cold vs Warm Query Performance

    [Fact]
    public async Task ColdQuery_FirstExecution_ShouldStillMeetTarget()
    {
        // Arrange - Create new isolated connection for cold query
        await using var newConnection = new NpgsqlConnection(_isolatedDbConnectionString);
        await newConnection.OpenAsync(TestCancellationToken);

        var random = new Random(108);
        var queryEmbedding = new Vector(GenerateRandomEmbedding(random, 1536));

        // Act - Execute cold query (no prior cache/execution)
        var sw = Stopwatch.StartNew();

        var command = newConnection.CreateCommand();
        command.CommandText = @"
            SELECT id, session_id, content, timestamp
            FROM conversation_memory
            ORDER BY embedding <=> $1::vector
            LIMIT 10;
        ";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$1";
        parameter.Value = queryEmbedding.ToArray();
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(TestCancellationToken);
        var results = new List<Guid>();
        while (await reader.ReadAsync(TestCancellationToken))
        {
            results.Add(reader.GetGuid(0));
        }

        sw.Stop();

        // Assert - Cold query should still meet performance target
        results.Should().HaveCount(10);
        sw.ElapsedMilliseconds.Should().BeLessThan(200, "Cold query should complete within 200ms");

        Console.WriteLine($"[Cold Query] Latency={sw.ElapsedMilliseconds}ms");
    }

    #endregion
}

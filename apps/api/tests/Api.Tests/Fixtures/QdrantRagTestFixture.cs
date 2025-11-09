using System.Diagnostics;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using Xunit;

namespace Api.Tests.Fixtures;

/// <summary>
/// Shared Qdrant+RAG fixture for integration tests that need indexed chess content.
/// Provides reusable test data infrastructure for ChessAgent, Webhook, and Explain endpoint tests.
///
/// Issue: TEST #710 - Create RAG test data infrastructure for integration tests
///
/// Key Features:
/// - Integrates with PostgresCollectionFixture (uses existing Postgres container)
/// - Creates dedicated Qdrant container for RAG tests (ephemeral, cleaned after tests)
/// - Indexes comprehensive chess knowledge from ChessKnowledge.json
/// - Provides mock embeddings (avoid OpenRouter API calls in CI/local tests)
/// - Implements IAsyncLifetime for proper setup/teardown
///
/// Container Strategy:
/// - Postgres: Reuses PostgresCollectionFixture (single container for all tests)
/// - Qdrant: Creates ephemeral container (disposed after test collection)
/// - Total: 2 containers (minimal overhead, ~7-10s startup)
///
/// Usage:
/// [Collection("Qdrant RAG Tests")]
/// public class ChessAgentIntegrationTests : IClassFixture&lt;QdrantRagTestFixture&gt;
/// {
///     public ChessAgentIntegrationTests(QdrantRagTestFixture ragFixture)
///     {
///         // Tests can now use ChessAgent with indexed content
///     }
/// }
/// </summary>
public class QdrantRagTestFixture : IAsyncLifetime
{
    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Postgres connection string (set by collection during initialization)
    /// </summary>
    public string? PostgresConnectionString { get; set; }

    /// <summary>
    /// Chess game ID used in test data
    /// Uses "chess" to match with production seed data and test expectations
    /// </summary>
    public string ChessGameId { get; private set; } = "chess";

    /// <summary>
    /// Chess RuleSpec ID created during initialization
    /// </summary>
    public Guid ChessRuleSpecId { get; private set; }

    /// <summary>
    /// Number of chunks indexed in mocked Qdrant storage
    /// </summary>
    public int IndexedChunkCount { get; private set; }

    public QdrantRagTestFixture()
    {
        // Parameterless constructor required by xUnit collection fixture
    }

    public async ValueTask InitializeAsync()
    {
        Console.WriteLine("🚀 [QdrantRagTestFixture] Starting Qdrant+RAG initialization...");
        _stopwatch.Start();

        try
        {
            // Step 1: Resolve Postgres connection string (from collection or existing fixture)
            await ResolvePostgresConnectionAsync();

            // Step 2: Create chess RuleSpec in Postgres database
            await CreateChessRuleSpecAsync();

            // Step 3: Index chess knowledge into mocked Qdrant storage
            await IndexChessKnowledgeAsync();

            _stopwatch.Stop();
            Console.WriteLine($"✅ [QdrantRagTestFixture] Initialization complete in {_stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"📊 [QdrantRagTestFixture] Indexed {IndexedChunkCount} chunks for chess game");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [QdrantRagTestFixture] Initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Resolve Postgres connection string from collection fixture coordination or ENV
    /// Also waits for database migrations to complete before proceeding
    /// </summary>
    private async Task ResolvePostgresConnectionAsync()
    {
        // Wait for PostgresCollectionFixture to set the static connection string
        var maxWaitMs = 15000; // 15 seconds max wait (includes migration time)
        var waited = 0;
        var delay = 100;

        while (string.IsNullOrWhiteSpace(QdrantRagTestCollection.SharedPostgresConnectionString) && waited < maxWaitMs)
        {
            await Task.Delay(delay);
            waited += delay;
        }

        if (string.IsNullOrWhiteSpace(QdrantRagTestCollection.SharedPostgresConnectionString))
        {
            throw new InvalidOperationException(
                "SharedPostgresConnectionString not set after 15s wait. " +
                "PostgresCollectionFixture should set this during initialization.");
        }

        // Copy from static to instance property
        PostgresConnectionString = QdrantRagTestCollection.SharedPostgresConnectionString;
        Console.WriteLine($"✅ [QdrantRagTestFixture] Postgres connection resolved: {PostgresConnectionString}");

        // Wait for database migrations to complete (check if 'games' table exists)
        await WaitForDatabaseMigrationsAsync();
    }

    /// <summary>
    /// Wait for database migrations to complete by checking if core tables exist
    /// </summary>
    private async Task WaitForDatabaseMigrationsAsync()
    {
        var maxWaitMs = 15000; // 15 seconds max wait for migrations (increased from 10s)
        var waited = 0;
        var delay = 300; // 300ms between retries (increased from 200ms)
        var retryCount = 0;

        while (waited < maxWaitMs)
        {
            try
            {
                await using var dbContext = CreateDbContext();
                // Try to query games table - if it exists, migrations are complete
                var tableExists = await dbContext.Games.AnyAsync();
                Console.WriteLine($"✅ [QdrantRagTestFixture] Database migrations complete (games table exists) after {retryCount} retries, {waited}ms");
                return;
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // Table does not exist
            {
                // Migrations not complete yet - wait and retry
                retryCount++;
                await Task.Delay(delay);
                waited += delay;
            }
            catch (Exception ex) when (ex is System.IO.IOException ||
                                      ex is System.Net.Sockets.SocketException ||
                                      ex is Npgsql.NpgsqlException ||
                                      ex is InvalidOperationException)
            {
                // Transient connection errors - wait and retry
                retryCount++;
                Console.WriteLine($"⚠️  [QdrantRagTestFixture] Transient DB error (retry {retryCount}): {ex.GetType().Name}: {ex.Message}");
                await Task.Delay(delay);
                waited += delay;
            }
        }

        throw new InvalidOperationException(
            $"Database migrations did not complete after {maxWaitMs}ms wait ({retryCount} retries). " +
            "PostgresCollectionFixture should run migrations during initialization.");
    }

    public ValueTask DisposeAsync()
    {
        // DON'T clear mocked Qdrant storage - it's shared across all tests in collection
        // Storage persists for the lifetime of the test collection
        Console.WriteLine("✅ [QdrantRagTestFixture] Cleanup complete");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Create chess RuleSpec entity in Postgres database
    /// </summary>
    private async Task CreateChessRuleSpecAsync()
    {
        Console.WriteLine("📝 [QdrantRagTestFixture] Creating chess RuleSpec in database...");

        if (string.IsNullOrWhiteSpace(PostgresConnectionString))
        {
            throw new InvalidOperationException("PostgresConnectionString not set. Collection should wire this up.");
        }

        await using var dbContext = CreateDbContext();

        // Ensure chess game exists (may be created by seed data migration)
        // Use retry logic to handle race conditions with WebApplicationFactory seed data
        var maxRetries = 3;
        var retryDelay = 500;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var existingGame = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == ChessGameId);
                if (existingGame == null)
                {
                    // Game doesn't exist - try to create it
                    var chessGame = new GameEntity
                    {
                        Id = ChessGameId,
                        Name = "Chess",
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.Games.Add(chessGame);
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"✅ [QdrantRagTestFixture] Created chess game: {ChessGameId}");
                }
                else
                {
                    Console.WriteLine($"ℹ️ [QdrantRagTestFixture] Chess game already exists: {ChessGameId}");
                }

                break; // Success - exit retry loop
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true)
            {
                if (attempt < maxRetries - 1)
                {
                    // Race condition with seed data - wait and retry
                    Console.WriteLine($"ℹ️ [QdrantRagTestFixture] Duplicate key detected, retrying ({attempt + 1}/{maxRetries})...");
                    await Task.Delay(retryDelay);

                    // Detach tracked entity to avoid conflicts
                    var trackedEntries = dbContext.ChangeTracker.Entries<GameEntity>()
                        .Where(e => e.Entity.Id == ChessGameId).ToList();
                    foreach (var entry in trackedEntries)
                    {
                        entry.State = EntityState.Detached;
                    }
                }
                else
                {
                    // Final attempt failed - log and continue (game exists)
                    Console.WriteLine($"ℹ️ [QdrantRagTestFixture] Chess game created by concurrent process: {ChessGameId}");
                }
            }
        }

        // Create RuleSpec entity (simplified structure - now uses Guid and Version only)
        var ruleSpec = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = ChessGameId,
            Version = "test-v1",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = null // Test data
        };

        dbContext.RuleSpecs.Add(ruleSpec);
        await dbContext.SaveChangesAsync();

        ChessRuleSpecId = ruleSpec.Id;
        Console.WriteLine($"✅ [QdrantRagTestFixture] Created chess RuleSpec: {ChessRuleSpecId}");
    }

    /// <summary>
    /// Load chess knowledge from JSON file (ChessKnowledge.json from src/Api/Data)
    /// </summary>
    private async Task<string> LoadChessKnowledgeJsonAsync()
    {
        // Try multiple possible paths (test bin directory, src directory)
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "ChessKnowledge.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Api", "Data", "ChessKnowledge.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "Api", "Data", "ChessKnowledge.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"📄 [QdrantRagTestFixture] Loading chess knowledge from: {path}");
                return await File.ReadAllTextAsync(path);
            }
        }

        throw new FileNotFoundException(
            $"ChessKnowledge.json not found in any expected location. Searched: {string.Join(", ", possiblePaths)}");
    }

    /// <summary>
    /// Index chess knowledge into mocked Qdrant storage (used by WebApplicationFactory)
    /// TEST #710: Simplified approach - populate WebApplicationFactoryFixture._mockQdrantStorage
    /// instead of using a real Qdrant container. This aligns with existing test architecture.
    /// </summary>
    private async Task IndexChessKnowledgeAsync()
    {
        Console.WriteLine("🔧 [QdrantRagTestFixture] Indexing chess knowledge into mocked Qdrant storage...");

        // Load and parse chess knowledge
        var chessKnowledgeJson = await LoadChessKnowledgeJsonAsync();
        var chessKnowledge = JsonSerializer.Deserialize<ChessKnowledgeData>(chessKnowledgeJson)
            ?? throw new InvalidOperationException("Failed to deserialize ChessKnowledge.json");

        // Convert chess knowledge to plain text for chunking
        var chessText = ConvertChessKnowledgeToText(chessKnowledge);

        // Create chunking service
        var chunkingLogger = new Mock<ILogger<TextChunkingService>>();
        var chunkingService = new TextChunkingService(chunkingLogger.Object);

        // Chunk the text (PERF-07: sentence-aware chunking)
        var chunks = chunkingService.ChunkText(chessText, chunkSize: 512, overlap: 50);
        Console.WriteLine($"📦 [QdrantRagTestFixture] Created {chunks.Count} chunks from chess knowledge");

        // Generate deterministic mock embeddings for all chunks
        var embeddings = chunks.Select((chunk, index) =>
        {
            var hash = chunk.Text.GetHashCode();
            var random = new Random(hash);
            return Enumerable.Range(0, 1536).Select(_ => (float)random.NextDouble()).ToArray();
        }).ToList();

        // Populate mocked Qdrant storage (used by WebApplicationFactory)
        const string collectionName = "meepleai_documents";
        var points = new List<PointStruct>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var pointId = Guid.NewGuid().ToString();

            // Create payload dictionary with explicit Value objects (no implicit conversion)
            // IMPORTANT: Must include "category" field because ChessKnowledgeService.SearchChessKnowledgeAsync
            // uses SearchByCategoryAsync which filters on category="chess", not game_id
            var payload = new Dictionary<string, Value>
            {
                ["chunk_index"] = new Value { IntegerValue = i },
                ["text"] = new Value { StringValue = chunk.Text },
                ["page"] = new Value { IntegerValue = chunk.Page },
                ["char_start"] = new Value { IntegerValue = chunk.CharStart },
                ["char_end"] = new Value { IntegerValue = chunk.CharEnd },
                ["game_id"] = new Value { StringValue = ChessGameId },
                ["category"] = new Value { StringValue = ChessGameId }, // Required for SearchByCategoryAsync filter
                ["pdf_id"] = new Value { StringValue = ChessRuleSpecId.ToString() },
                ["source_type"] = new Value { StringValue = "rulespec" },
                ["indexed_at"] = new Value { StringValue = DateTime.UtcNow.ToString("o") }
            };

            var point = new PointStruct
            {
                Id = new PointId { Uuid = pointId },
                Vectors = embeddings[i],
                Payload = { payload }
            };

            points.Add(point);
        }

        // Add to mock storage (thread-safe)
        lock (WebApplicationFactoryFixture._mockQdrantStorage)
        {
            if (!WebApplicationFactoryFixture._mockQdrantStorage.ContainsKey(collectionName))
            {
                WebApplicationFactoryFixture._mockQdrantStorage[collectionName] = new List<PointStruct>();
            }

            WebApplicationFactoryFixture._mockQdrantStorage[collectionName].AddRange(points);
        }

        IndexedChunkCount = chunks.Count;
        Console.WriteLine($"✅ [QdrantRagTestFixture] Indexed {IndexedChunkCount} chunks into mocked Qdrant storage");
    }

    /// <summary>
    /// Convert structured chess knowledge (rules, openings, tactics) to plain text
    /// </summary>
    private string ConvertChessKnowledgeToText(ChessKnowledgeData chessKnowledge)
    {
        var textBuilder = new System.Text.StringBuilder();

        // Add rules section
        if (chessKnowledge.rules != null)
        {
            foreach (var rule in chessKnowledge.rules)
            {
                textBuilder.AppendLine($"# {rule.category}: {rule.title}");
                textBuilder.AppendLine(rule.content);
                textBuilder.AppendLine();
            }
        }

        // Add openings section
        if (chessKnowledge.openings != null)
        {
            foreach (var opening in chessKnowledge.openings)
            {
                textBuilder.AppendLine($"# {opening.category}: {opening.title}");
                textBuilder.AppendLine(opening.content);
                textBuilder.AppendLine();
            }
        }

        // Add tactics section
        if (chessKnowledge.tactics != null)
        {
            foreach (var tactic in chessKnowledge.tactics)
            {
                textBuilder.AppendLine($"# {tactic.category}: {tactic.title}");
                textBuilder.AppendLine(tactic.content);
                textBuilder.AppendLine();
            }
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Create a new DbContext instance for test data setup
    /// </summary>
    private MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        return new MeepleAiDbContext(options);
    }
}

/// <summary>
/// Collection definition for all integration tests using Qdrant RAG test data.
/// All test classes marked with [Collection("Qdrant RAG Tests")]
/// will share the same QdrantRagTestFixture instance (optimized performance).
///
/// Static coordination pattern: PostgresCollectionFixture sets connection string
/// in static field that QdrantRagTestFixture reads during initialization.
/// </summary>
[CollectionDefinition("Qdrant RAG Tests")]
public class QdrantRagTestCollection : ICollectionFixture<PostgresCollectionFixture>, ICollectionFixture<QdrantRagTestFixture>
{
    /// <summary>
    /// Shared Postgres connection string for coordination between fixtures
    /// Set by PostgresCollectionFixture, read by QdrantRagTestFixture
    /// </summary>
    public static string? SharedPostgresConnectionString { get; set; }
}

/// <summary>
/// Data structure for ChessKnowledge.json
/// </summary>
public class ChessKnowledgeData
{
    public List<ChessKnowledgeItem>? rules { get; set; }
    public List<ChessKnowledgeItem>? openings { get; set; }
    public List<ChessKnowledgeItem>? tactics { get; set; }
    public List<ChessKnowledgeItem>? middlegame_strategies { get; set; }
    public List<ChessKnowledgeItem>? endgame_principles { get; set; }
}

public class ChessKnowledgeItem
{
    public string category { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

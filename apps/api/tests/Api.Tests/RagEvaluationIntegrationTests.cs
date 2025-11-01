using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Qdrant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;
using Testcontainers.Qdrant;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// AI-06: Integration tests for RAG evaluation with real Qdrant and database
/// Uses Testcontainers for isolated testing environment
/// </summary>
public class RagEvaluationIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly ITestOutputHelper _output;

    private PostgreSqlContainer? _postgresContainer;
    private QdrantContainer? _qdrantContainer;
    private MeepleAiDbContext? _dbContext;
    private IQdrantService? _qdrantService;
    private IEmbeddingService? _embeddingService;
    private IRagEvaluationService? _evaluationService;
    private string? _tempDatasetPath;

    public async Task InitializeAsync()
    {
        // Start Postgres container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        // Start Qdrant container
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.11.3")
            .Build();

        await _qdrantContainer.StartAsync();

        // Setup database context
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.MigrateAsync();

        // Seed test data
        await SeedTestDataAsync();

        // Setup services
        var qdrantRestPort = _qdrantContainer.GetMappedPublicPort(6333);
        var qdrantGrpcPort = _qdrantContainer.GetMappedPublicPort(6334);
        var qdrantUrl = $"http://{_qdrantContainer.Hostname}:{qdrantRestPort}";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QDRANT_URL"] = qdrantUrl,
                ["QDRANT_GRPC_PORT"] = qdrantGrpcPort.ToString(),
                ["EMBEDDING_PROVIDER"] = "ollama"
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var qdrantLogger = loggerFactory.CreateLogger<QdrantService>();
        var embeddingLogger = loggerFactory.CreateLogger<MockEmbeddingService>();
        var evaluationLogger = loggerFactory.CreateLogger<RagEvaluationService>();

        var qdrantAdapterLogger = loggerFactory.CreateLogger<QdrantClientAdapter>();
        var qdrantClientAdapter = new QdrantClientAdapter(config, qdrantAdapterLogger);

        var collectionManagerLogger = loggerFactory.CreateLogger<QdrantCollectionManager>();
        var collectionManager = new QdrantCollectionManager(qdrantClientAdapter, collectionManagerLogger);

        var vectorIndexerLogger = loggerFactory.CreateLogger<QdrantVectorIndexer>();
        var vectorIndexer = new QdrantVectorIndexer(qdrantClientAdapter, vectorIndexerLogger);

        var vectorSearcherLogger = loggerFactory.CreateLogger<QdrantVectorSearcher>();
        var vectorSearcher = new QdrantVectorSearcher(qdrantClientAdapter, vectorSearcherLogger);

        // Use mock embedding service for predictable tests
        _embeddingService = new MockEmbeddingService(embeddingLogger);

        _qdrantService = new QdrantService(
            collectionManager,
            vectorIndexer,
            vectorSearcher,
            _embeddingService,
            config,
            qdrantLogger);
        await _qdrantService.EnsureCollectionExistsAsync();

        _evaluationService = new RagEvaluationService(
            _qdrantService,
            _embeddingService,
            evaluationLogger);

        // Create temp dataset file
        _tempDatasetPath = Path.Combine(Path.GetTempPath(), $"integration-test-dataset-{Guid.NewGuid()}.json");
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_qdrantContainer != null)
        {
            await _qdrantContainer.DisposeAsync();
        }

        if (_tempDatasetPath != null && File.Exists(_tempDatasetPath))
        {
            File.Delete(_tempDatasetPath);
        }
    }

    public void Dispose()
    {
        // Synchronous disposal (calls async version)
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EndToEnd_LoadDatasetAndEvaluate_GeneratesReport()
    {
        // Arrange: Create and save test dataset
        var dataset = CreateIntegrationTestDataset();
        var json = JsonSerializer.Serialize(dataset);
        await File.WriteAllTextAsync(_tempDatasetPath!, json);

        // Index test documents in Qdrant
        await IndexTestDocumentsAsync();

        // Act: Load dataset
        var loadedDataset = await _evaluationService!.LoadDatasetAsync(_tempDatasetPath!);

        // Assert: Dataset loaded correctly
        loadedDataset.Should().NotBeNull();
        loadedDataset.Name.Should().Be(dataset.Name);
        loadedDataset.Queries.Count.Should().Be(dataset.Queries.Count);

        // Act: Evaluate
        var report = await _evaluationService.EvaluateAsync(loadedDataset, topK: 5);

        // Assert: Report generated with valid metrics
        report.Should().NotBeNull();
        report.TotalQueries.Should().Be(loadedDataset.Queries.Count);
        (report.SuccessfulQueries > 0).Should().BeTrue();
        (report.MeanReciprocalRank >= 0.0 && report.MeanReciprocalRank <= 1.0).Should().BeTrue();
        (report.AvgPrecisionAt5 >= 0.0 && report.AvgPrecisionAt5 <= 1.0).Should().BeTrue();
        (report.LatencyP95 > 0).Should().BeTrue(); // Some latency should be recorded
    }

    [Fact]
    public async Task EndToEnd_GenerateMarkdownReport_ValidFormat()
    {
        // Arrange
        var dataset = CreateIntegrationTestDataset();
        await IndexTestDocumentsAsync();

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 5);
        var markdown = _evaluationService.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("# RAG Evaluation Report");
        markdown.Should().Contain("Summary");
        markdown.Should().Contain("Information Retrieval Metrics");
        markdown.Should().Contain("Performance Metrics");
        markdown.Should().Contain(dataset.Name);
    }

    [Fact]
    public async Task EndToEnd_GenerateJsonReport_ValidJson()
    {
        // Arrange
        var dataset = CreateIntegrationTestDataset();
        await IndexTestDocumentsAsync();

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 5);
        var jsonReport = _evaluationService.GenerateJsonReport(report);

        // Assert
        jsonReport.Should().NotBeNull();

        // Deserialize to verify it's valid JSON
        var deserialized = JsonSerializer.Deserialize<RagEvaluationReport>(jsonReport, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        deserialized.Should().NotBeNull();
        deserialized.DatasetName.Should().Be(report.DatasetName);
        deserialized.TotalQueries.Should().Be(report.TotalQueries);
    }

    // Note: Mock embeddings may not produce sufficient vector similarity for retrieval - ensure Qdrant running
    [Fact]
    public async Task Evaluation_WithIndexedDocuments_RetrievesRelevantResults()
    {
        // Arrange: Create dataset matching indexed documents
        var dataset = new RagEvaluationDataset
        {
            Name = "Retrieval Test",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "ttt-test-001",
                    GameId = "tic-tac-toe",
                    Query = "How many players can play?", // Should match indexed doc
                    RelevantDocIds = new List<string> { "ttt-test-pdf-001" }
                }
            }
        };

        await IndexTestDocumentsAsync();

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 3);

        // Assert
        report.SuccessfulQueries.Should().Be(1);
        var result = report.QueryResults[0];
        result.Success.Should().BeTrue();
        (result.RetrievedCount > 0).Should().BeTrue("Should retrieve at least one document");

        // Since we're using mock embeddings, exact precision depends on mock implementation
        // But we can verify metrics are within valid ranges
        (result.PrecisionAt1 >= 0.0 && result.PrecisionAt1 <= 1.0).Should().BeTrue();
        (result.RecallAtK >= 0.0 && result.RecallAtK <= 1.0).Should().BeTrue();
    }

    [Fact]
    public async Task Evaluation_QualityGates_EnforcedCorrectly()
    {
        // Arrange
        var dataset = CreateIntegrationTestDataset();
        await IndexTestDocumentsAsync();

        // Set very strict thresholds that will likely fail
        var strictThresholds = new RagQualityThresholds
        {
            MinPrecisionAt5 = 0.99, // Nearly impossible to achieve
            MinMeanReciprocalRank = 0.99,
            MaxLatencyP95Ms = 1.0, // 1ms - very strict
            MinSuccessRate = 1.0 // 100% success required
        };

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 5, strictThresholds);

        // Assert: Should fail quality gates
        report.PassedQualityGates.Should().BeFalse();
        report.QualityGateFailures.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Evaluation_NoDocumentsIndexed_AllQueriesFail()
    {
        // Arrange: Don't index any documents
        var dataset = CreateIntegrationTestDataset();

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 5);

        // Assert: All queries should have zero results
        foreach (var result in report.QueryResults)
        {
            if (result.Success)
            {
                result.RetrievedCount.Should().Be(0);
                result.PrecisionAt5.Should().BeEquivalentTo(0.0);
                result.ReciprocalRank.Should().BeEquivalentTo(0.0);
            }
        }
    }

    [Fact]
    public async Task Evaluation_WithRealLatency_CalculatesPercentilesCorrectly()
    {
        // Arrange
        var dataset = CreateIntegrationTestDataset();
        await IndexTestDocumentsAsync();

        // Act
        var report = await _evaluationService!.EvaluateAsync(dataset, topK: 5);

        // Assert: Latency metrics should be calculated
        (report.AvgLatencyMs > 0).Should().BeTrue("Average latency should be positive");
        (report.LatencyP50 > 0).Should().BeTrue("p50 should be positive");
        (report.LatencyP95 >= report.LatencyP50).Should().BeTrue("p95 should be >= p50");
        (report.LatencyP99 >= report.LatencyP95).Should().BeTrue("p99 should be >= p95");
    }

    // Helper methods

    private async Task SeedTestDataAsync()
    {
        // Seed games for testing - only if they don't already exist
        // (seed migration may have already created them)
        var tictactoeExists = await _dbContext!.Games.AnyAsync(g => g.Id == "tic-tac-toe");
        var chessExists = await _dbContext.Games.AnyAsync(g => g.Id == "chess");

        if (!tictactoeExists)
        {
            _dbContext.Games.Add(new GameEntity { Id = "tic-tac-toe", Name = "Tic-Tac-Toe", CreatedAt = DateTime.UtcNow });
        }

        if (!chessExists)
        {
            _dbContext.Games.Add(new GameEntity { Id = "chess", Name = "Chess", CreatedAt = DateTime.UtcNow });
        }

        if (!tictactoeExists || !chessExists)
        {
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task IndexTestDocumentsAsync()
    {
        // Create mock document chunks for testing
        var chunks = new List<DocumentChunk>
        {
            new DocumentChunk
            {
                Text = "Tic-Tac-Toe is a game for two players who take turns marking spaces in a 3x3 grid.",
                Embedding = GenerateMockEmbedding("two players 3x3 grid"),
                Page = 1,
                CharStart = 0,
                CharEnd = 83
            },
            new DocumentChunk
            {
                Text = "The player who succeeds in placing three of their marks in a horizontal, vertical, or diagonal row wins.",
                Embedding = GenerateMockEmbedding("three marks row wins"),
                Page = 1,
                CharStart = 84,
                CharEnd = 189
            },
            new DocumentChunk
            {
                Text = "Chess is played by two players on a board with 64 squares arranged in an 8x8 grid.",
                Embedding = GenerateMockEmbedding("two players 64 squares 8x8"),
                Page = 1,
                CharStart = 0,
                CharEnd = 83
            }
        };

        // Index Tic-Tac-Toe documents
        await _qdrantService!.IndexDocumentChunksAsync(
            "tic-tac-toe",
            "ttt-test-pdf-001",
            chunks.Take(2).ToList());

        // Index Chess documents
        await _qdrantService.IndexDocumentChunksAsync(
            "chess",
            "chess-test-pdf-001",
            chunks.Skip(2).Take(1).ToList());
    }

    private float[] GenerateMockEmbedding(string text)
    {
        // Generate deterministic embedding based on text
        // This ensures consistent test behavior
        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new float[768];

        for (int i = 0; i < 768; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range: -1 to 1
        }

        return embedding;
    }

    private RagEvaluationDataset CreateIntegrationTestDataset()
    {
        return new RagEvaluationDataset
        {
            Name = "Integration Test Dataset",
            Version = "1.0",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "int-ttt-001",
                    GameId = "tic-tac-toe",
                    Query = "How many players can play Tic-Tac-Toe?",
                    RelevantDocIds = new List<string> { "ttt-test-pdf-001" },
                    Difficulty = "easy",
                    Category = "setup"
                },
                new RagEvaluationQuery
                {
                    Id = "int-ttt-002",
                    GameId = "tic-tac-toe",
                    Query = "How do you win in Tic-Tac-Toe?",
                    RelevantDocIds = new List<string> { "ttt-test-pdf-001" },
                    Difficulty = "easy",
                    Category = "winning"
                },
                new RagEvaluationQuery
                {
                    Id = "int-chess-001",
                    GameId = "chess",
                    Query = "How many squares are on a chess board?",
                    RelevantDocIds = new List<string> { "chess-test-pdf-001" },
                    Difficulty = "easy",
                    Category = "setup"
                }
            }
        };
    }
}

/// <summary>
/// Mock embedding service for predictable integration tests
/// </summary>
public class MockEmbeddingService : IEmbeddingService
{
    private readonly ILogger<MockEmbeddingService> _logger;

    public MockEmbeddingService(ILogger<MockEmbeddingService> logger)
    {
        _logger = logger;
    }

    public int GetEmbeddingDimensions() => 768;

    public string GetModelName() => "openai/text-embedding-3-small";

    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Generate deterministic embedding based on text hash
        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new float[768];

        for (int i = 0; i < 768; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }

        _logger.LogDebug("Generated mock embedding for text: {Text}", text.Substring(0, Math.Min(50, text.Length)));

        return Task.FromResult(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));
    }

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = texts.Select(text =>
        {
            var hash = text.GetHashCode();
            var random = new Random(hash);
            var embedding = new float[768];

            for (int i = 0; i < 768; i++)
            {
                embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            return embedding;
        }).ToList();

        return Task.FromResult(EmbeddingResult.CreateSuccess(embeddings));
    }

    // AI-09: Language-aware overloads
    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken cancellationToken = default)
    {
        // Delegate to base implementation (language is ignored in mock)
        return GenerateEmbeddingAsync(text, cancellationToken);
    }

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken cancellationToken = default)
    {
        // Delegate to base implementation (language is ignored in mock)
        return GenerateEmbeddingsAsync(texts, cancellationToken);
    }
}

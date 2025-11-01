using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// AI-06: Unit tests for RAG evaluation service
/// Tests metric calculations, dataset loading, report generation
/// </summary>
public class RagEvaluationServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<RagEvaluationService>> _mockLogger;
    private readonly RagEvaluationService _service;
    private readonly string _tempDatasetPath;

    public RagEvaluationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _qdrantServiceMock = new Mock<IQdrantService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<RagEvaluationService>>();
        _service = new RagEvaluationService(
            _qdrantServiceMock.Object,
            _embeddingServiceMock.Object,
            _mockLogger.Object);

        // Create temporary dataset file for testing
        _tempDatasetPath = Path.Combine(Path.GetTempPath(), $"test-dataset-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        // Clean up temp file
        if (File.Exists(_tempDatasetPath))
        {
            File.Delete(_tempDatasetPath);
        }
    }

    [Fact]
    public async Task LoadDatasetAsync_ValidJson_LoadsSuccessfully()
    {
        // Arrange
        var dataset = new RagEvaluationDataset
        {
            Name = "Test Dataset",
            Version = "1.0",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "test-001",
                    GameId = "test-game",
                    Query = "Test query",
                    RelevantDocIds = new List<string> { "doc-1" }
                }
            }
        };

        var json = JsonSerializer.Serialize(dataset);
        await File.WriteAllTextAsync(_tempDatasetPath, json);

        // Act
        var loaded = await _service.LoadDatasetAsync(_tempDatasetPath);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Name.Should().Be("Test Dataset");
        loaded.Version.Should().Be("1.0");
        loaded.Queries.Should().ContainSingle();
        loaded.Queries[0].Id.Should().Be("test-001");
    }

    [Fact]
    public async Task LoadDatasetAsync_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non-existent-file.json");

        // Act & Assert
        var act = async () => await _service.LoadDatasetAsync(nonExistentPath);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadDatasetAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempDatasetPath, "{ invalid json }");

        // Act & Assert
        var act = async () => await _service.LoadDatasetAsync(_tempDatasetPath);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LoadDatasetAsync_NullOrEmptyPath_ThrowsException()
    {
        // Act & Assert
        var act1 = async () => await _service.LoadDatasetAsync("");
        await act1.Should().ThrowAsync<ArgumentException>();
        var act2 = async () => await _service.LoadDatasetAsync(null!);
        await act2.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EvaluateAsync_PerfectResults_AllMetricsOne()
    {
        // Arrange
        var dataset = CreateSimpleDataset();

        // Mock perfect retrieval: all relevant docs retrieved in order
        SetupMockEmbedding();
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "doc-1", Score = 0.95f, Text = "Result 1", Page = 1 },
            new SearchResultItem { PdfId = "doc-2", Score = 0.90f, Text = "Result 2", Page = 2 }
        });

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        report.TotalQueries.Should().Be(1);
        report.SuccessfulQueries.Should().Be(1);
        report.FailedQueries.Should().Be(0);

        // Perfect precision at all K values
        report.AvgPrecisionAt1.Should().Be(1.0);
        report.AvgPrecisionAt3.Should().Be(1.0);
        report.AvgPrecisionAt5.Should().Be(1.0);

        // Perfect recall
        report.AvgRecallAtK.Should().Be(1.0);

        // Perfect MRR (first result is relevant)
        report.MeanReciprocalRank.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateAsync_NoRelevantResults_MetricsZero()
    {
        // Arrange
        var dataset = CreateSimpleDataset();

        // Mock retrieval with no relevant results
        SetupMockEmbedding();
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "irrelevant-1", Score = 0.50f, Text = "Irrelevant", Page = 1 },
            new SearchResultItem { PdfId = "irrelevant-2", Score = 0.45f, Text = "Irrelevant", Page = 2 }
        });

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        report.SuccessfulQueries.Should().Be(1);
        report.AvgPrecisionAt1.Should().Be(0.0);
        report.AvgPrecisionAt5.Should().Be(0.0);
        report.AvgRecallAtK.Should().Be(0.0);
        report.MeanReciprocalRank.Should().Be(0.0);
    }

    [Fact]
    public async Task EvaluateAsync_RelevantResultAtPositionThree_CorrectMRR()
    {
        // Arrange
        var dataset = CreateSimpleDataset();

        // Mock retrieval: first relevant result at position 3 (0-indexed: position 2)
        SetupMockEmbedding();
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "irrelevant-1", Score = 0.60f, Text = "Irrelevant", Page = 1 },
            new SearchResultItem { PdfId = "irrelevant-2", Score = 0.55f, Text = "Irrelevant", Page = 2 },
            new SearchResultItem { PdfId = "doc-1", Score = 0.50f, Text = "Relevant", Page = 3 } // Position 3
        });

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        // MRR = 1/3 = 0.3333...
        report.MeanReciprocalRank.Should().BeApproximately(1.0 / 3.0, 4);

        // Precision@1 = 0 (first result not relevant)
        report.AvgPrecisionAt1.Should().Be(0.0);

        // Precision@3 = 1/3 (1 relevant in top 3)
        report.AvgPrecisionAt3.Should().BeApproximately(1.0 / 3.0, 4);

        // Precision@5 = 1/3 (only 3 results total, 1 relevant)
        report.AvgPrecisionAt5.Should().BeApproximately(1.0 / 3.0, 4);
    }

    [Fact]
    public async Task EvaluateAsync_PartialRecall_CorrectCalculation()
    {
        // Arrange
        var dataset = new RagEvaluationDataset
        {
            Name = "Recall Test",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "recall-001",
                    GameId = "test-game",
                    Query = "Test query",
                    RelevantDocIds = new List<string> { "doc-1", "doc-2", "doc-3", "doc-4" } // 4 relevant docs
                }
            }
        };

        // Mock retrieval: only 2 out of 4 relevant docs retrieved
        SetupMockEmbedding();
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "doc-1", Score = 0.90f, Text = "Relevant 1", Page = 1 },
            new SearchResultItem { PdfId = "irrelevant", Score = 0.70f, Text = "Irrelevant", Page = 2 },
            new SearchResultItem { PdfId = "doc-2", Score = 0.60f, Text = "Relevant 2", Page = 3 }
        });

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        var result = report.QueryResults[0];
        result.RelevantCount.Should().Be(4); // 4 relevant docs in ground truth
        result.RelevantRetrievedCount.Should().Be(2); // 2 relevant docs retrieved

        // Recall = 2/4 = 0.5
        result.RecallAtK.Should().Be(0.5);
        report.AvgRecallAtK.Should().Be(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleQueries_AggregatesCorrectly()
    {
        // Arrange
        var dataset = new RagEvaluationDataset
        {
            Name = "Multiple Queries",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "q1",
                    GameId = "test-game",
                    Query = "Query 1",
                    RelevantDocIds = new List<string> { "doc-1" }
                },
                new RagEvaluationQuery
                {
                    Id = "q2",
                    GameId = "test-game",
                    Query = "Query 2",
                    RelevantDocIds = new List<string> { "doc-2" }
                }
            }
        };

        // Mock perfect results for both queries (doc-1 for query 1, doc-2 for query 2)
        SetupMockEmbedding();
        _qdrantServiceMock
            .SetupSequence(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { PdfId = "doc-1", Score = 0.95f, Text = "Result", Page = 1 }
            }))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { PdfId = "doc-2", Score = 0.95f, Text = "Result", Page = 1 }
            }));

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        report.TotalQueries.Should().Be(2);
        report.SuccessfulQueries.Should().Be(2);
        report.MeanReciprocalRank.Should().Be(1.0); // Both queries have perfect MRR
        report.AvgPrecisionAt1.Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateAsync_EmbeddingFailure_RecordsFailedQuery()
    {
        // Arrange
        var dataset = CreateSimpleDataset();

        // Mock embedding failure
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service unavailable"));

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        report.TotalQueries.Should().Be(1);
        report.SuccessfulQueries.Should().Be(0);
        report.FailedQueries.Should().Be(1);
        report.QueryResults[0].Success.Should().BeFalse();
        report.QueryResults[0].ErrorMessage.Should().Contain("Embedding generation failed");
    }

    [Fact]
    public async Task EvaluateAsync_SearchFailure_RecordsFailedQuery()
    {
        // Arrange
        var dataset = CreateSimpleDataset();

        SetupMockEmbedding();

        // Mock search failure
        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant connection failed"));

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        report.FailedQueries.Should().Be(1);
        report.QueryResults[0].Success.Should().BeFalse();
        report.QueryResults[0].ErrorMessage.Should().Contain("Search failed");
    }

    [Fact]
    public async Task EvaluateAsync_LatencyPercentiles_CalculatedCorrectly()
    {
        // Arrange: Create dataset with 100 queries to test percentile calculation
        var queries = Enumerable.Range(1, 100).Select(i => new RagEvaluationQuery
        {
            Id = $"q{i}",
            GameId = "test-game",
            Query = $"Query {i}",
            RelevantDocIds = new List<string> { "doc-1" }
        }).ToList();

        var dataset = new RagEvaluationDataset
        {
            Name = "Latency Test",
            Queries = queries
        };

        SetupMockEmbedding();

        // Mock search with deterministic latency simulation (using delays)
        var callIndex = 0;
        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string gameId, float[] embedding, int limit, CancellationToken ct) =>
            {
                // Simulate varying latency: 10ms to 200ms
                var latency = (callIndex % 100) * 2; // 0, 2, 4, ..., 198ms
                callIndex++;
                await Task.Delay(latency, ct);

                return SearchResult.CreateSuccess(new List<SearchResultItem>
                {
                    new SearchResultItem { PdfId = "doc-1", Score = 0.9f, Text = "Result", Page = 1 }
                });
            });

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10);

        // Assert
        (report.LatencyP50 > 0).Should().BeTrue();
        (report.LatencyP95 > report.LatencyP50).Should().BeTrue(); // p95 should be higher than median
        (report.LatencyP99 > report.LatencyP95).Should().BeTrue(); // p99 should be higher than p95
    }

    [Fact]
    public async Task EvaluateAsync_QualityGates_PassesWhenThresholdsMetAsync()
    {
        // Arrange
        var dataset = CreateSimpleDataset();
        SetupMockEmbedding();
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "doc-1", Score = 0.95f, Text = "Perfect match", Page = 1 }
        });

        var thresholds = new RagQualityThresholds
        {
            MinPrecisionAt5 = 0.70,
            MinMeanReciprocalRank = 0.60,
            MaxLatencyP95Ms = 2000.0,
            MinSuccessRate = 0.95
        };

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10, thresholds);

        // Assert
        report.PassedQualityGates.Should().BeTrue();
        report.QualityGateFailures.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_QualityGates_FailsWhenBelowThresholds()
    {
        // Arrange
        var dataset = CreateSimpleDataset();
        SetupMockEmbedding();

        // Mock poor results
        SetupMockSearch(new List<SearchResultItem>
        {
            new SearchResultItem { PdfId = "irrelevant", Score = 0.30f, Text = "Poor match", Page = 1 }
        });

        var thresholds = new RagQualityThresholds
        {
            MinPrecisionAt5 = 0.70, // Will fail: actual is 0.0
            MinMeanReciprocalRank = 0.60, // Will fail: actual is 0.0
            MaxLatencyP95Ms = 2000.0,
            MinSuccessRate = 0.95
        };

        // Act
        var report = await _service.EvaluateAsync(dataset, topK: 10, thresholds);

        // Assert
        report.PassedQualityGates.Should().BeFalse();
        report.QualityGateFailures.Should().NotBeEmpty();
        report.QualityGateFailures.Should().Contain(f => f.Contains("Precision@5"));
        report.QualityGateFailures.Should().Contain(f => f.Contains("MRR"));
    }

    [Fact]
    public async Task EvaluateAsync_InvalidArguments_ThrowsException()
    {
        // Act & Assert
        var act1 = async () => await _service.EvaluateAsync(null!);
        await act1.Should().ThrowAsync<ArgumentNullException>();

        var act2 = async () => await _service.EvaluateAsync(new RagEvaluationDataset { Name = "Empty", Queries = Array.Empty<RagEvaluationQuery>() });
        await act2.Should().ThrowAsync<ArgumentException>();

        var act3 = async () => await _service.EvaluateAsync(CreateSimpleDataset(), topK: 0);
        await act3.Should().ThrowAsync<ArgumentException>();
    }

    // Note: Markdown formatting test - needs investigation of exact format
    [Fact]
    public void GenerateMarkdownReport_ValidReport_GeneratesCorrectFormat()
    {
        // Arrange
        var report = new RagEvaluationReport
        {
            DatasetName = "Test Report",
            EvaluatedAt = new DateTime(2025, 10, 16, 12, 0, 0, DateTimeKind.Utc),
            TotalQueries = 10,
            SuccessfulQueries = 9,
            FailedQueries = 1,
            MeanReciprocalRank = 0.85,
            AvgPrecisionAt5 = 0.75,
            LatencyP95 = 150.5,
            PassedQualityGates = true,
            QueryResults = new List<RagEvaluationQueryResult>
            {
                new RagEvaluationQueryResult
                {
                    QueryId = "q1",
                    Query = "Test query",
                    PrecisionAt5 = 0.80,
                    LatencyMs = 120.0,
                    Success = true
                }
            }
        };

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("# RAG Evaluation Report");
        markdown.Should().Contain("Test Report");
        markdown.Should().Contain("✅ PASSED");
        markdown.Should().Contain("Mean Reciprocal Rank");
        markdown.Should().Contain("0.85"); // MRR value (flexible format matching)
        markdown.Should().Contain("Latency p95");
    }

    [Fact]
    public void GenerateMarkdownReport_FailedGates_ShowsFailures()
    {
        // Arrange
        var report = new RagEvaluationReport
        {
            DatasetName = "Failed Report",
            TotalQueries = 5,
            SuccessfulQueries = 5,
            PassedQualityGates = false,
            QualityGateFailures = new List<string>
            {
                "Precision@5 (0.6000) below threshold (0.7000)",
                "MRR (0.5000) below threshold (0.6000)"
            },
            QueryResults = Array.Empty<RagEvaluationQueryResult>()
        };

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("❌ FAILED");
        markdown.Should().Contain("Quality Gate Failures");
        markdown.Should().Contain("Precision@5 (0.6000) below threshold");
        markdown.Should().Contain("MRR (0.5000) below threshold");
    }

    [Fact]
    public void GenerateJsonReport_ValidReport_GeneratesValidJson()
    {
        // Arrange
        var report = new RagEvaluationReport
        {
            DatasetName = "JSON Test",
            TotalQueries = 5,
            SuccessfulQueries = 5,
            MeanReciprocalRank = 0.9,
            QueryResults = new List<RagEvaluationQueryResult>
            {
                new RagEvaluationQueryResult
                {
                    QueryId = "q1",
                    Query = "Test",
                    Success = true
                }
            }
        };

        // Act
        var json = _service.GenerateJsonReport(report);

        // Assert
        json.Should().NotBeNull();
        var deserialized = JsonSerializer.Deserialize<RagEvaluationReport>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        deserialized.Should().NotBeNull();
        deserialized.DatasetName.Should().Be("JSON Test");
        deserialized.MeanReciprocalRank.Should().Be(0.9);
    }

    [Fact]
    public void GenerateMarkdownReport_NullReport_ThrowsException()
    {
        // Act & Assert
        var act = () => _service.GenerateMarkdownReport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateJsonReport_NullReport_ThrowsException()
    {
        // Act & Assert
        var act = () => _service.GenerateJsonReport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // Helper methods

    private RagEvaluationDataset CreateSimpleDataset()
    {
        return new RagEvaluationDataset
        {
            Name = "Simple Test",
            Queries = new List<RagEvaluationQuery>
            {
                new RagEvaluationQuery
                {
                    Id = "test-001",
                    GameId = "test-game",
                    Query = "Test query",
                    RelevantDocIds = new List<string> { "doc-1", "doc-2" }
                }
            }
        };
    }

    private void SetupMockEmbedding()
    {
        var mockEmbedding = Enumerable.Repeat(0.1f, 768).ToArray(); // 768-dim embedding
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { mockEmbedding }));
    }

    private void SetupMockSearch(List<SearchResultItem> results)
    {
        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(results));
    }
}

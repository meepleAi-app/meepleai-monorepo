using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for PromptEvaluationService
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
public class PromptEvaluationServiceTests : IAsyncLifetime
{
    private readonly Mock<IRagService> _ragServiceMock;
    private readonly Mock<IPromptTemplateService> _promptServiceMock;
    private readonly Mock<ILogger<PromptEvaluationService>> _loggerMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly PromptEvaluationService _service;
    private readonly SqliteConnection _connection;
    private string _testDatasetPath = null!;

    public PromptEvaluationServiceTests()
    {
        // Setup in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup mocks
        _ragServiceMock = new Mock<IRagService>();
        _promptServiceMock = new Mock<IPromptTemplateService>();
        _loggerMock = new Mock<ILogger<PromptEvaluationService>>();

        // Create service instance with temp directory for testing
        _service = new PromptEvaluationService(
            _ragServiceMock.Object,
            _promptServiceMock.Object,
            _dbContext,
            _loggerMock.Object,
            allowedDatasetsDirectory: Path.GetTempPath());
    }

    public async Task InitializeAsync()
    {
        // Create test dataset file
        var testDataset = new PromptTestDataset
        {
            DatasetId = "test-dataset-v1",
            TemplateName = "qa-system-prompt",
            Version = "1.0.0",
            Description = "Test dataset for unit tests",
            TestCases = new List<PromptTestCase>
            {
                new()
                {
                    Id = "TC-001",
                    Category = "setup",
                    Difficulty = "easy",
                    Query = "How many players?",
                    GameId = Guid.Empty.ToString(),
                    RequiredKeywords = new List<string> { "2", "two" },
                    ForbiddenKeywords = new List<string> { "3", "three" },
                    ExpectedCitations = new List<string> { "1" },
                    MinConfidence = 0.70,
                    MaxLatencyMs = 3000
                },
                new()
                {
                    Id = "TC-002",
                    Category = "out-of-context",
                    Difficulty = "easy",
                    Query = "What is the capital of France?",
                    GameId = Guid.Empty.ToString(),
                    RequiredKeywords = new List<string> { "not specified" },
                    ForbiddenKeywords = new List<string> { "Paris" },
                    ExpectedCitations = new List<string>(),
                    MinConfidence = 0.00,
                    MaxLatencyMs = 2000
                }
            },
            Thresholds = new QualityThresholds
            {
                MinAccuracy = 0.80,
                MaxHallucinationRate = 0.10,
                MinAvgConfidence = 0.70,
                MinCitationCorrectness = 0.80,
                MaxAvgLatencyMs = 3000
            }
        };

        // Write to temp file
        _testDatasetPath = Path.Combine(Path.GetTempPath(), $"test-dataset-{Guid.NewGuid()}.json");
        var json = JsonSerializer.Serialize(testDataset, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_testDatasetPath, json);

        // Add test user first (required for navigation properties)
        var testUser = new UserEntity
        {
            Id = "test-user",
            Email = "test@example.com",
            Role = UserRole.Admin,
            PasswordHash = "test-password-hash",
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.Users.AddAsync(testUser);
        await _dbContext.SaveChangesAsync();

        // Add test prompt template
        var template = new PromptTemplateEntity
        {
            Id = "test-template-id",
            Name = "qa-system-prompt",
            Category = "qa",
            Description = "Test template",
            CreatedByUserId = "test-user",
            CreatedBy = testUser,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PromptTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        // Add test prompt version
        var version = new PromptVersionEntity
        {
            Id = "test-version-id",
            TemplateId = "test-template-id",
            Template = template,
            VersionNumber = 1,
            Content = "You are a helpful assistant. Answer questions accurately.",
            CreatedByUserId = "test-user",
            CreatedBy = testUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PromptVersions.AddAsync(version);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup test dataset file
        if (File.Exists(_testDatasetPath))
        {
            File.Delete(_testDatasetPath);
        }

        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    #region LoadDatasetAsync Tests

    [Fact]
    public async Task LoadDatasetAsync_ValidPath_LoadsCorrectly()
    {
        // Act
        var dataset = await _service.LoadDatasetAsync(_testDatasetPath);

        // Assert
        Assert.Equal("test-dataset-v1", dataset.DatasetId);
        Assert.Equal("qa-system-prompt", dataset.TemplateName);
        Assert.Equal(2, dataset.TestCases.Count);
        Assert.Equal("TC-001", dataset.TestCases[0].Id);
    }

    [Fact]
    public async Task LoadDatasetAsync_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidPath = "/nonexistent/dataset.json";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.LoadDatasetAsync(invalidPath));
    }

    [Fact]
    public async Task LoadDatasetAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        var malformedPath = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid()}.json");
        await File.WriteAllTextAsync(malformedPath, "{ invalid json }");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _service.LoadDatasetAsync(malformedPath));
        }
        finally
        {
            File.Delete(malformedPath);
        }
    }

    #endregion

    #region EvaluateAsync Tests

    [Fact]
    public async Task EvaluateAsync_AllQueriesAccurate_Returns100PercentAccuracy()
    {
        // Arrange: Mock RAG to return responses with all required keywords
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "How many players?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "The game requires 2 players (two players total). See Page 1 for details.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.85));

        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "What is the capital of France?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Not specified in the rulebook.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.0));

        // Act
        var result = await _service.EvaluateAsync(
            "test-template-id",
            "test-version-id",
            _testDatasetPath);

        // Assert
        Assert.Equal(100.0, result.Metrics.Accuracy);
        Assert.Equal(2, result.TotalQueries);
        Assert.True(result.QueryResults.All(q => q.IsAccurate));
    }

    [Fact]
    public async Task EvaluateAsync_WithHallucinations_CalculatesCorrectRate()
    {
        // Arrange: First query OK, second query has forbidden keyword "Paris"
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "How many players?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Two players are required.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.85));

        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "What is the capital of France?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "The capital of France is Paris.", // Hallucination!
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.0));

        // Act
        var result = await _service.EvaluateAsync(
            "test-template-id",
            "test-version-id",
            _testDatasetPath);

        // Assert
        Assert.Equal(50.0, result.Metrics.HallucinationRate); // 1 out of 2 queries hallucinated
        Assert.True(result.QueryResults[1].IsHallucinated);
    }

    [Fact]
    public async Task EvaluateAsync_BelowThresholds_ReturnsFailed()
    {
        // Arrange: Mock low accuracy responses (no required keywords)
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "I don't know.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.30));

        // Act
        var result = await _service.EvaluateAsync(
            "test-template-id",
            "test-version-id",
            _testDatasetPath);

        // Assert
        Assert.False(result.Passed);
        Assert.Contains("below threshold", result.Summary);
        Assert.True(result.Metrics.Accuracy < 80.0);
    }

    [Fact]
    public async Task EvaluateAsync_AboveThresholds_ReturnsPassed()
    {
        // Arrange: Mock good responses
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "How many players?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "The game requires 2 players (two players total). See Page 1 for details.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.85));

        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                "What is the capital of France?",
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Not specified in the rulebook.",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.0));

        // Act
        var result = await _service.EvaluateAsync(
            "test-template-id",
            "test-version-id",
            _testDatasetPath);

        // Assert
        Assert.True(result.Passed);
        Assert.Contains("PASSED", result.Summary);
        Assert.True(result.Metrics.Accuracy >= 80.0);
        Assert.True(result.Metrics.HallucinationRate <= 10.0);
    }

    [Fact]
    public async Task EvaluateAsync_CallsProgressCallback_WithCorrectCounts()
    {
        // Arrange
        var progressUpdates = new List<(int current, int total)>();
        Action<int, int> progressCallback = (current, total) => progressUpdates.Add((current, total));

        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Test response",
                snippets: new List<Snippet>().AsReadOnly(),
                confidence: 0.80));

        // Act
        await _service.EvaluateAsync(
            "test-template-id",
            "test-version-id",
            _testDatasetPath,
            progressCallback);

        // Assert
        Assert.Equal(2, progressUpdates.Count);
        Assert.Equal((1, 2), progressUpdates[0]);
        Assert.Equal((2, 2), progressUpdates[1]);
    }

    [Fact]
    public async Task EvaluateAsync_VersionNotFound_ThrowsArgumentException()
    {
        // Arrange: Non-existent version ID
        var nonExistentVersionId = "non-existent-version";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.EvaluateAsync(
                "test-template-id",
                nonExistentVersionId,
                _testDatasetPath));

        Assert.Contains("not found", exception.Message);
    }

    #endregion

    #region CompareVersionsAsync Tests

    [Fact]
    public async Task CompareVersionsAsync_CandidateBetter_ReturnsActivateRecommendation()
    {
        // Arrange: Get existing template and user
        var template = await _dbContext.PromptTemplates.FirstAsync();
        var user = await _dbContext.Users.FirstAsync();

        // Create candidate version
        var candidateVersion = new PromptVersionEntity
        {
            Id = "candidate-version-id",
            TemplateId = "test-template-id",
            Template = template,
            VersionNumber = 2,
            Content = "You are an excellent assistant with improved prompting.",
            CreatedByUserId = "test-user",
            CreatedBy = user,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PromptVersions.AddAsync(candidateVersion);
        await _dbContext.SaveChangesAsync();

        // Mock RAG responses: Create measurable improvement
        // Baseline: 100% accurate, confidence 0.72, hallucination 0%, latency 2000ms
        // Candidate: 100% accurate, confidence 0.88, hallucination 0%, latency 1500ms
        // Delta: confidence +0.16 (> 0.10 threshold) → should ACTIVATE

        var setupIndex = 0;
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                setupIndex++;
                // First 2 calls: baseline (100% accurate, lower confidence)
                if (setupIndex <= 2)
                {
                    return setupIndex == 1
                        ? new QaResponse("Two players required. See Page 1.", new List<Snippet>().AsReadOnly(), confidence: 0.72)
                        : new QaResponse("Not specified in the rulebook.", new List<Snippet>().AsReadOnly(), confidence: 0.72);
                }
                // Next 2 calls: candidate (100% accurate, much higher confidence)
                else
                {
                    return setupIndex == 3
                        ? new QaResponse("The game requires exactly 2 players (two players). See Page 1 for details.", new List<Snippet>().AsReadOnly(), confidence: 0.88)
                        : new QaResponse("This information is not specified in the game rulebook.", new List<Snippet>().AsReadOnly(), confidence: 0.88);
                }
            });

        // Act
        var comparison = await _service.CompareVersionsAsync(
            "test-template-id",
            "test-version-id", // baseline
            "candidate-version-id", // candidate
            _testDatasetPath);

        // Assert
        Assert.Equal(ComparisonRecommendation.Activate, comparison.Recommendation);
        Assert.True(comparison.Deltas.AvgConfidenceDelta >= 0.10); // Confidence improved by 0.16
        Assert.Contains("improvement", comparison.RecommendationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompareVersionsAsync_CandidateWorse_ReturnsRejectRecommendation()
    {
        // Arrange: Get existing template and user
        var template = await _dbContext.PromptTemplates.FirstAsync();
        var user = await _dbContext.Users.FirstAsync();

        // Create candidate version with poor prompt
        var candidateVersion = new PromptVersionEntity
        {
            Id = "poor-candidate-version",
            TemplateId = "test-template-id",
            Template = template,
            VersionNumber = 3,
            Content = "Answer questions.", // Much worse prompt
            CreatedByUserId = "test-user",
            CreatedBy = user,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PromptVersions.AddAsync(candidateVersion);
        await _dbContext.SaveChangesAsync();

        // Mock RAG: Baseline passes (90% accurate), Candidate passes but with regression
        // Baseline: 100% accurate, 0% hallucination
        // Candidate: 100% accurate, but lower confidence (regression)
        // For a REJECT based on regression, candidate needs significant accuracy drop

        var callCount = 0;
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First 2 calls: baseline (both accurate, 100%)
                if (callCount <= 2)
                {
                    return callCount == 1
                        ? new QaResponse("Two players required. Page 1.", new List<Snippet>().AsReadOnly(), confidence: 0.85)
                        : new QaResponse("Not specified in the rulebook.", new List<Snippet>().AsReadOnly(), confidence: 0.85);
                }
                // Next 2 calls: candidate (only 50% accurate = 1/2 accurate, significant regression)
                else
                {
                    return callCount == 3
                        ? new QaResponse("Two players needed. Page 1.", new List<Snippet>().AsReadOnly(), confidence: 0.90)
                        : new QaResponse("France is a country.", new List<Snippet>().AsReadOnly(), confidence: 0.60); // Wrong! Doesn't have "not specified"
                }
            });

        // Act
        var comparison = await _service.CompareVersionsAsync(
            "test-template-id",
            "test-version-id",
            "poor-candidate-version",
            _testDatasetPath);

        // Assert
        Assert.Equal(ComparisonRecommendation.Reject, comparison.Recommendation);
        Assert.True(comparison.Deltas.AccuracyDelta < 0); // 50% - 100% = -50%
        // With only 2 test cases, candidate fails 80% threshold (50% < 80%), so rejection is due to threshold failure
        Assert.Contains("failed quality threshold", comparison.RecommendationReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompareVersionsAsync_MarginalChanges_ReturnsManualReview()
    {
        // Arrange: Get existing template and user
        var template = await _dbContext.PromptTemplates.FirstAsync();
        var user = await _dbContext.Users.FirstAsync();

        // Create candidate version with slight improvement
        var candidateVersion = new PromptVersionEntity
        {
            Id = "marginal-candidate",
            TemplateId = "test-template-id",
            Template = template,
            VersionNumber = 4,
            Content = "You are a helpful assistant. Answer accurately and cite sources.",
            CreatedByUserId = "test-user",
            CreatedBy = user,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PromptVersions.AddAsync(candidateVersion);
        await _dbContext.SaveChangesAsync();

        // Mock RAG: Both versions perform well with slight difference (marginal improvement)
        // Baseline: 100% accurate, confidence 0.82
        // Candidate: 100% accurate, confidence 0.85 (only +0.03, below 0.10 threshold for ACTIVATE)
        // → Should trigger MANUAL_REVIEW due to marginal improvement
        var marginalCallCount = 0;
        _ragServiceMock
            .Setup(x => x.AskWithCustomPromptAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                marginalCallCount++;
                // First 2 calls: baseline (100% accurate, confidence 0.82)
                if (marginalCallCount <= 2)
                {
                    return marginalCallCount == 1
                        ? new QaResponse("Two players required. Page 1.", new List<Snippet>().AsReadOnly(), confidence: 0.82)
                        : new QaResponse("Not specified in the rulebook.", new List<Snippet>().AsReadOnly(), confidence: 0.82);
                }
                // Next 2 calls: candidate (100% accurate, confidence 0.85 - marginal improvement)
                else
                {
                    return marginalCallCount == 3
                        ? new QaResponse("Two players needed. Page 1.", new List<Snippet>().AsReadOnly(), confidence: 0.85)
                        : new QaResponse("Not specified in the game rules.", new List<Snippet>().AsReadOnly(), confidence: 0.85);
                }
            });

        // Act
        var comparison = await _service.CompareVersionsAsync(
            "test-template-id",
            "test-version-id",
            "marginal-candidate",
            _testDatasetPath);

        // Assert
        Assert.Equal(ComparisonRecommendation.ManualReview, comparison.Recommendation);
        Assert.Contains("manual review", comparison.RecommendationReason, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region GenerateReport Tests

    [Fact]
    public void GenerateReport_MarkdownFormat_ReturnsValidMarkdown()
    {
        // Arrange
        var evalResult = CreateSampleEvaluationResult();

        // Act
        var report = _service.GenerateReport(evalResult, ReportFormat.Markdown);

        // Assert
        Assert.Contains("# Prompt Evaluation Report", report);
        Assert.Contains("## Metrics Summary", report);
        Assert.Contains("| Metric | Value | Status |", report);
        Assert.Contains("Accuracy", report);
        Assert.Contains("Hallucination Rate", report);
        Assert.Contains("## Query Breakdown", report);
    }

    [Fact]
    public void GenerateReport_JsonFormat_ReturnsValidJson()
    {
        // Arrange
        var evalResult = CreateSampleEvaluationResult();

        // Act
        var report = _service.GenerateReport(evalResult, ReportFormat.Json);

        // Assert
        var deserialized = JsonSerializer.Deserialize<PromptEvaluationResult>(report);
        Assert.NotNull(deserialized);
        Assert.Equal(evalResult.EvaluationId, deserialized.EvaluationId);
        Assert.Equal(evalResult.Metrics.Accuracy, deserialized.Metrics.Accuracy);
    }

    #endregion

    #region Database Persistence Tests

    [Fact]
    public async Task StoreResultsAsync_ValidResult_SavesToDatabaseCorrectly()
    {
        // Arrange
        var evalResult = CreateSampleEvaluationResult();

        // Act
        await _service.StoreResultsAsync(evalResult);

        // Assert
        var stored = await _dbContext.PromptEvaluationResults
            .FirstOrDefaultAsync(e => e.Id == evalResult.EvaluationId);

        Assert.NotNull(stored);
        Assert.Equal(evalResult.TemplateId, stored.TemplateId);
        Assert.Equal(evalResult.VersionId, stored.VersionId);
        Assert.Equal(evalResult.Metrics.Accuracy, stored.Accuracy);
        Assert.Equal(evalResult.Metrics.HallucinationRate, stored.HallucinationRate);
        Assert.Equal(evalResult.Passed, stored.Passed);
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_MultipleResults_ReturnsOrderedByDate()
    {
        // Arrange: Insert 3 evaluation results with different dates
        var results = new[]
        {
            CreateEvaluationEntity("eval-1", DateTime.UtcNow.AddDays(-2)),
            CreateEvaluationEntity("eval-2", DateTime.UtcNow.AddDays(-1)),
            CreateEvaluationEntity("eval-3", DateTime.UtcNow)
        };

        await _dbContext.PromptEvaluationResults.AddRangeAsync(results);
        await _dbContext.SaveChangesAsync();

        // Act
        var historical = await _service.GetHistoricalResultsAsync("test-template-id", limit: 10);

        // Assert
        Assert.Equal(3, historical.Count);
        // Should be ordered by ExecutedAt DESC (newest first)
        Assert.Equal("eval-3", historical[0].EvaluationId);
        Assert.Equal("eval-2", historical[1].EvaluationId);
        Assert.Equal("eval-1", historical[2].EvaluationId);
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_WithLimit_ReturnsOnlyLimitedResults()
    {
        // Arrange: Insert 5 results
        var results = Enumerable.Range(1, 5)
            .Select(i => CreateEvaluationEntity($"eval-{i}", DateTime.UtcNow.AddDays(-i)))
            .ToArray();

        await _dbContext.PromptEvaluationResults.AddRangeAsync(results);
        await _dbContext.SaveChangesAsync();

        // Act
        var historical = await _service.GetHistoricalResultsAsync("test-template-id", limit: 2);

        // Assert
        Assert.Equal(2, historical.Count);
    }

    #endregion

    #region Helper Methods

    private PromptEvaluationResult CreateSampleEvaluationResult()
    {
        return new PromptEvaluationResult
        {
            EvaluationId = $"eval-{Guid.NewGuid():N}",
            TemplateId = "test-template-id",
            VersionId = "test-version-id",
            DatasetId = "test-dataset-v1",
            ExecutedAt = DateTime.UtcNow,
            TotalQueries = 10,
            Metrics = new EvaluationMetrics
            {
                Accuracy = 85.0,
                HallucinationRate = 5.0,
                AvgConfidence = 0.75,
                CitationCorrectness = 80.0,
                AvgLatencyMs = 2500
            },
            Passed = true,
            QueryResults = new List<QueryEvaluationResult>
            {
                new()
                {
                    TestCaseId = "TC-001",
                    Query = "Test query",
                    Response = "Test response",
                    Confidence = 0.85,
                    LatencyMs = 2000,
                    IsAccurate = true,
                    IsHallucinated = false,
                    AreCitationsCorrect = true
                }
            },
            Summary = "✅ PASSED: All quality metrics within acceptable thresholds"
        };
    }

    private PromptEvaluationResultEntity CreateEvaluationEntity(string id, DateTime executedAt)
    {
        return new PromptEvaluationResultEntity
        {
            Id = id,
            TemplateId = "test-template-id",
            VersionId = "test-version-id",
            DatasetId = "test-dataset-v1",
            ExecutedAt = executedAt,
            TotalQueries = 10,
            Accuracy = 85.0,
            HallucinationRate = 5.0,
            AvgConfidence = 0.75,
            CitationCorrectness = 80.0,
            AvgLatencyMs = 2500,
            Passed = true,
            Summary = "Test summary",
            QueryResultsJson = "[]",
            CreatedAt = executedAt
        };
    }

    #endregion
}

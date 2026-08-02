using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// HTTP-layer tests for the RAG evaluation admin endpoints (Issue #3433, Task 6):
///   POST /api/v1/admin/eval/retrieval — runs retrieval evaluation against a dataset file.
/// Verifies admin-gating (401/403) and that a happy-path admin call returns the
/// <see cref="Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services.EvaluationReportFormatter"/>
/// JSON report. <see cref="IRagService"/> is mocked so the test never depends on a live RAG
/// pipeline/LLM (REQ-AI-TEST-001).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3433")]
public sealed class AdminEvalEndpointsIntegrationTests : IAsyncLifetime
{
    private const string RetrievalEndpoint = "/api/v1/admin/eval/retrieval";
    private const string MergeLabelsEndpoint = "/api/v1/admin/eval/merge-labels";

    private static readonly Guid TestAdminId = Guid.NewGuid();

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;
    private string _userSessionToken = null!;
    private string _datasetPath = null!;
    private string _mergeOutputPath = null!;

    public AdminEvalEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"admin_eval_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var mockRagService = new Mock<IRagService>();
        mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Place your settlement on any intersection.",
                snippets: [new Snippet(text: "Setup rules...", source: "chunk-1", page: 3, line: 1, score: 0.9f)],
                confidence: 0.8));

        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IRagService>();
                    services.AddSingleton(mockRagService.Object);
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            (_, _adminSessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext, TestAdminId);
            (_, _userSessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        }

        _client = _factory.CreateClient();

        var dataset = EvaluationDataset.Create("AdminEvalEndpointsTest", "HTTP-layer smoke dataset");
        dataset.AddSample(new EvaluationSample
        {
            Id = "s1",
            Question = "Where do I place my first settlement?",
            ExpectedAnswer = "On any intersection",
            GameId = "catan",
            RelevantChunkIds = ["chunk-1"],
            Language = "en"
        });

        _datasetPath = Path.Combine(Path.GetTempPath(), $"eval-dataset-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(_datasetPath, dataset.ToJson());

        _mergeOutputPath = Path.Combine(Path.GetTempPath(), $"eval-merged-{Guid.NewGuid():N}.json");
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);

        if (File.Exists(_datasetPath))
        {
            File.Delete(_datasetPath);
        }

        if (File.Exists(_mergeOutputPath))
        {
            File.Delete(_mergeOutputPath);
        }
    }

    private static object BuildMergeReviewBody(string datasetPath, string? outputPath) => new
    {
        datasetPath,
        outputPath,
        review = new
        {
            items = new[]
            {
                new
                {
                    sampleId = "s1",
                    question = "Where do I place my first settlement?",
                    candidates = new[]
                    {
                        new { chunkId = "chunk-1", page = 3, score = 0.9f, snippet = "Setup rules...", relevant = true }
                    }
                }
            }
        }
    };

    [Fact]
    public async Task RunRetrievalEvaluation_WithAdminAuth_Returns200WithMetricsJson()
    {
        // Arrange
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            RetrievalEndpoint,
            _adminSessionToken,
            new { datasetPath = _datasetPath });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("dataset").GetString().Should().Be("AdminEvalEndpointsTest");
        root.GetProperty("metrics").GetProperty("recall_at_10").GetDouble().Should().Be(1.0,
            because: "the mocked RAG snippet source ('chunk-1') matches the sample's relevant_chunk_ids");
        root.GetProperty("coverage").GetProperty("labeled").GetInt32().Should().Be(1);
        root.GetProperty("coverage").GetProperty("unlabeled").GetInt32().Should().Be(0);
        root.GetProperty("by_language").GetProperty("en").GetProperty("recall_at_10").GetDouble().Should().Be(1.0);
    }

    [Fact]
    public async Task RunRetrievalEvaluation_WithNonExistentDatasetPath_Returns404()
    {
        // Arrange
        var missingDatasetPath = Path.Combine(Path.GetTempPath(), $"missing-dataset-{Guid.NewGuid():N}.json");
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            RetrievalEndpoint,
            _adminSessionToken,
            new { datasetPath = missingDatasetPath });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RunRetrievalEvaluation_WithRegularUserAuth_Returns403()
    {
        // Arrange
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            RetrievalEndpoint,
            _userSessionToken,
            new { datasetPath = _datasetPath });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RunRetrievalEvaluation_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, RetrievalEndpoint)
        {
            Content = JsonContent.Create(new { datasetPath = _datasetPath })
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MergeLabels_WithAdminAuth_PersistsLabeledDatasetAndReturns200()
    {
        // Arrange
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            MergeLabelsEndpoint,
            _adminSessionToken,
            BuildMergeReviewBody(_datasetPath, _mergeOutputPath));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(_mergeOutputPath).Should().BeTrue("the endpoint persists the merged dataset to the output path");
        var persisted = EvaluationDataset.FromJson(await File.ReadAllTextAsync(_mergeOutputPath));
        persisted.Samples.Should().ContainSingle()
            .Which.RelevantChunkIds.Should().BeEquivalentTo(new[] { "chunk-1" });
    }

    [Fact]
    public async Task MergeLabels_WithNonExistentDatasetPath_Returns404()
    {
        // Arrange
        var missingDatasetPath = Path.Combine(Path.GetTempPath(), $"missing-dataset-{Guid.NewGuid():N}.json");
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            MergeLabelsEndpoint,
            _adminSessionToken,
            BuildMergeReviewBody(missingDatasetPath, _mergeOutputPath));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MergeLabels_WithNullReviewItems_Returns400()
    {
        // Arrange — a body like {"review":{}} deserializes to a non-null Review with null Items; the endpoint
        // must reject it (400) rather than NRE (500) when the handler enumerates the items.
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            MergeLabelsEndpoint,
            _adminSessionToken,
            new { datasetPath = _datasetPath, outputPath = _mergeOutputPath, review = new { } });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MergeLabels_WithRegularUserAuth_Returns403()
    {
        // Arrange
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            MergeLabelsEndpoint,
            _userSessionToken,
            BuildMergeReviewBody(_datasetPath, _mergeOutputPath));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MergeLabels_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, MergeLabelsEndpoint)
        {
            Content = JsonContent.Create(BuildMergeReviewBody(_datasetPath, _mergeOutputPath))
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

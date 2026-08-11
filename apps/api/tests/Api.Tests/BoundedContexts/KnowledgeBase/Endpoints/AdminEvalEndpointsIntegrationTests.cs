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
using Microsoft.Extensions.Configuration;
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
    private string _datasetRoot = null!;

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
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: "Place your settlement on any intersection.",
                snippets: [new Snippet(text: "Setup rules...", source: "chunk-1", page: 3, line: 1, score: 0.9f)],
                confidence: 0.8));

        // #3438: the endpoints now resolve datasetPath against a configured root and refuse
        // anything outside it. The tests get their own throwaway root, so they exercise the
        // sandboxed behaviour instead of the absolute temp paths they used before.
        _datasetRoot = Path.Combine(Path.GetTempPath(), $"eval-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_datasetRoot);

        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Evaluation:DatasetRoot"] = _datasetRoot
                    }));

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

        // Names are now RELATIVE to the configured root — that is what the API accepts.
        _datasetPath = $"eval-dataset-{Guid.NewGuid():N}.json";
        await File.WriteAllTextAsync(Path.Combine(_datasetRoot, _datasetPath), dataset.ToJson());

        _mergeOutputPath = $"eval-merged-{Guid.NewGuid():N}.json";
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);

        if (Directory.Exists(_datasetRoot))
        {
            Directory.Delete(_datasetRoot, recursive: true);
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

        // Per-sample results (#3390 follow-up): the wire response carries one entry per sample so a
        // paired McNemar/Holm analysis can be run offline. Assert the array + a graded entry's fields.
        var samples = root.GetProperty("samples");
        samples.ValueKind.Should().Be(JsonValueKind.Array);
        samples.GetArrayLength().Should().Be(1);
        var sample = samples[0];
        sample.GetProperty("sample_id").GetString().Should().NotBeNullOrEmpty();
        sample.GetProperty("is_success").GetBoolean().Should().BeTrue();
        sample.TryGetProperty("citation_matched", out _).Should().BeTrue(
            because: "the per-sample McNemar binary must be present (JSON null when the sample is ungraded)");
    }

    [Fact]
    public async Task RunRetrievalEvaluation_WithNonExistentDatasetPath_Returns404()
    {
        // Arrange
        // Relativo e dentro la root: prova il 404 "non esiste", non il 400 "fuori dal sandbox".
        var missingDatasetPath = $"missing-dataset-{Guid.NewGuid():N}.json";
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
        var mergeOutputFullPath = Path.Combine(_datasetRoot, _mergeOutputPath);
        File.Exists(mergeOutputFullPath).Should().BeTrue("the endpoint persists the merged dataset to the output path");
        var persisted = EvaluationDataset.FromJson(await File.ReadAllTextAsync(mergeOutputFullPath));
        persisted.Samples.Should().ContainSingle()
            .Which.RelevantChunkIds.Should().BeEquivalentTo(new[] { "chunk-1" });
    }

    [Fact]
    public async Task MergeLabels_WithNonExistentDatasetPath_Returns404()
    {
        // Arrange
        // Relativo e dentro la root: prova il 404 "non esiste", non il 400 "fuori dal sandbox".
        var missingDatasetPath = $"missing-dataset-{Guid.NewGuid():N}.json";
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

    // ── #3438: the sandbox, proven from the attacker's side ──────────────────────────────
    //
    // The unit tests pin the resolver; these pin that the ENDPOINTS actually go through it. An
    // authenticated admin is the threat model here: admin-gating narrows who can try, it does not
    // make arbitrary file read/write acceptable.

    [Theory]
    [InlineData("../../../etc/passwd.json")]
    [InlineData("subdir/../../../../secrets.json")]
    public async Task RunRetrievalEvaluation_WithTraversalPath_Returns400AndNeverReads(string traversalPath)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            RetrievalEndpoint,
            _adminSessionToken,
            new { datasetPath = traversalPath });

        var response = await _client.SendAsync(request);

        // 400, not 404: a 404 would confirm whether the target exists, turning the endpoint into a
        // file-existence oracle for paths outside the root.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("passwd", "the error must not echo back the attacker's path");
    }

    [Fact]
    public async Task RunRetrievalEvaluation_WithAbsolutePath_Returns400()
    {
        // The dataset genuinely exists — but by absolute path, which the sandbox refuses. Proves
        // the refusal is about containment, not about the file being missing.
        var absolutePath = Path.Combine(_datasetRoot, _datasetPath);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            RetrievalEndpoint,
            _adminSessionToken,
            new { datasetPath = absolutePath });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MergeLabels_WithOutputPathOutsideRoot_Returns400AndWritesNothing()
    {
        // The write side is the more dangerous one: an unfiltered outputPath means dropping an
        // attacker-shaped JSON document anywhere the process can write.
        var escapeTarget = Path.Combine(Path.GetTempPath(), $"pwned-{Guid.NewGuid():N}.json");
        var relativeEscape = $"../{Path.GetFileName(escapeTarget)}";

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            MergeLabelsEndpoint,
            _adminSessionToken,
            BuildMergeReviewBody(_datasetPath, relativeEscape));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        File.Exists(escapeTarget).Should().BeFalse("nothing may be written outside the dataset root");
    }
}

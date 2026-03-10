using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Unit tests for ModelAvailabilityCheckJob.
/// Issue #5493: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelAvailabilityCheckJobTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IStrategyModelMappingRepository> _strategyMappingRepoMock;
    private readonly Mock<IModelCompatibilityRepository> _compatibilityRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IJobExecutionContext> _jobContextMock;
    private readonly IConfiguration _configuration;
    private readonly ModelAvailabilityCheckJob _job;

    public ModelAvailabilityCheckJobTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _strategyMappingRepoMock = new Mock<IStrategyModelMappingRepository>();
        _compatibilityRepoMock = new Mock<IModelCompatibilityRepository>();
        _mediatorMock = new Mock<IMediator>();
        _jobContextMock = new Mock<IJobExecutionContext>();

        _jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "OPENROUTER_API_KEY", "test-key" },
            })
            .Build();

        _job = new ModelAvailabilityCheckJob(
            _httpClientFactoryMock.Object,
            _strategyMappingRepoMock.Object,
            _compatibilityRepoMock.Object,
            _mediatorMock.Object,
            _configuration,
            Mock.Of<ILogger<ModelAvailabilityCheckJob>>());
    }

    private void SetupHttpClient(HttpStatusCode statusCode, object? responseBody = null)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseBody);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(client);
    }

    private void SetupMappings(params StrategyModelMappingEntry[] mappings)
    {
        _strategyMappingRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings.ToList().AsReadOnly());
    }

    // ── Successful check with all models available ──

    [Fact]
    public async Task Execute_AllModelsAvailable_UpdatesAvailabilityAndDoesNotPublishEvent()
    {
        // Arrange
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "meta-llama/llama-3.3-70b-instruct:free", name = "Llama 3.3 70B", context_length = 128000 },
                new { id = "qwen/qwen-2.5-72b:free", name = "Qwen 2.5 72B", context_length = 32000 },
            }
        };
        SetupHttpClient(HttpStatusCode.OK, modelsResponse);

        SetupMappings(
            new StrategyModelMappingEntry("FAST", "meta-llama/llama-3.3-70b-instruct:free",
                new[] { "qwen/qwen-2.5-72b:free" }, "openrouter", true, false));

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert — no deprecation events published
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<ModelDeprecatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Availability should be updated
        _compatibilityRepoMock.Verify(
            r => r.UpdateAvailabilityAsync(
                "meta-llama/llama-3.3-70b-instruct:free", true, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Model not found → publish ModelDeprecatedEvent ──

    [Fact]
    public async Task Execute_ModelNotAvailable_PublishesModelDeprecatedEvent()
    {
        // Arrange — OpenRouter returns models but NOT the one we use
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "qwen/qwen-2.5-72b:free", name = "Qwen 2.5 72B", context_length = 32000 },
            }
        };
        SetupHttpClient(HttpStatusCode.OK, modelsResponse);

        SetupMappings(
            new StrategyModelMappingEntry("BALANCED", "meta-llama/llama-3.3-70b-instruct:free",
                new[] { "qwen/qwen-2.5-72b:free" }, "openrouter", true, false));

        _compatibilityRepoMock
            .Setup(r => r.GetByModelIdAsync("meta-llama/llama-3.3-70b-instruct:free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelCompatibilityEntry(
                Guid.NewGuid(), "meta-llama/llama-3.3-70b-instruct:free", "Llama 3.3 70B", "openrouter",
                new[] { "qwen/qwen-2.5-72b:free" }, 128000, new[] { "reasoning" }, true, false, DateTime.UtcNow));

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert — deprecation event published
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<ModelDeprecatedEvent>(e =>
                    e.ModelId == "meta-llama/llama-3.3-70b-instruct:free" &&
                    e.AffectedStrategies.Contains("BALANCED") &&
                    e.SuggestedReplacement == "qwen/qwen-2.5-72b:free"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Change log should be recorded
        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(
                It.Is<ModelChangeLogEntry>(e =>
                    e.ModelId == "meta-llama/llama-3.3-70b-instruct:free" &&
                    e.ChangeType == "unavailable"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── OpenRouter API failure → graceful degradation ──

    [Fact]
    public async Task Execute_OpenRouterApiFailure_DoesNotThrowAndSetsFailureResult()
    {
        // Arrange
        SetupHttpClient(HttpStatusCode.ServiceUnavailable);
        SetupMappings();

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert — no events published, job should not throw
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<ModelDeprecatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Only OpenRouter models are checked (Ollama skipped) ──

    [Fact]
    public async Task Execute_OllamaMappings_AreSkipped()
    {
        // Arrange
        var modelsResponse = new { data = Array.Empty<object>() };
        SetupHttpClient(HttpStatusCode.OK, modelsResponse);

        SetupMappings(
            new StrategyModelMappingEntry("FAST", "llama3.2:latest",
                Array.Empty<string>(), "ollama", true, false));

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert — no events because Ollama models aren't checked against OpenRouter
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<ModelDeprecatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Fallback models are also checked ──

    [Fact]
    public async Task Execute_FallbackModelUnavailable_DoesNotPublishIfNotPrimary()
    {
        // Arrange — primary available, fallback not
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "meta-llama/llama-3.3-70b-instruct:free", name = "Llama 3.3", context_length = 128000 },
            }
        };
        SetupHttpClient(HttpStatusCode.OK, modelsResponse);

        SetupMappings(
            new StrategyModelMappingEntry("FAST", "meta-llama/llama-3.3-70b-instruct:free",
                new[] { "missing/fallback-model" }, "openrouter", true, false));

        _compatibilityRepoMock
            .Setup(r => r.GetByModelIdAsync("missing/fallback-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelCompatibilityEntry?)null);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert — change logged but no deprecation event (not a primary model)
        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(
                It.Is<ModelChangeLogEntry>(e => e.ModelId == "missing/fallback-model"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // No deprecation event since fallback is not a primary model for any strategy
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<ModelDeprecatedEvent>(e => e.AffectedStrategies.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── FetchOpenRouterModelsAsync unit test ──

    [Fact]
    public async Task FetchOpenRouterModelsAsync_ValidResponse_ReturnsModelList()
    {
        // Arrange
        var modelsResponse = new
        {
            data = new[]
            {
                new { id = "openai/gpt-4o-mini", name = "GPT-4o Mini", context_length = 128000 },
                new { id = "anthropic/claude-sonnet-4", name = "Claude Sonnet 4", context_length = 200000 },
            }
        };
        SetupHttpClient(HttpStatusCode.OK, modelsResponse);

        // Act
        var result = await _job.FetchOpenRouterModelsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Id.Should().Be("openai/gpt-4o-mini");
        result[1].Id.Should().Be("anthropic/claude-sonnet-4");
    }

    [Fact]
    public async Task FetchOpenRouterModelsAsync_ServerError_ReturnsNull()
    {
        // Arrange
        SetupHttpClient(HttpStatusCode.InternalServerError);

        // Act
        var result = await _job.FetchOpenRouterModelsAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ── Mock HTTP handler ──

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _responseBody;

        public MockHttpMessageHandler(HttpStatusCode statusCode, object? responseBody = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody != null
                ? JsonSerializer.Serialize(responseBody)
                : null;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody != null)
            {
                response.Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json");
            }
            return Task.FromResult(response);
        }
    }
}

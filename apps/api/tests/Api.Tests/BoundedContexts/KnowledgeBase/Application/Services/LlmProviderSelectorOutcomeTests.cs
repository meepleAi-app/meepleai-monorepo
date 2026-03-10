using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for LlmProviderSelector outcome recording methods (Issue #5492).
/// Verifies RecordSuccess, RecordFailure, and WarnIfApproachingLimitAsync.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5492")]
public sealed class LlmProviderSelectorOutcomeTests
{
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakerMock = new();
    private readonly Mock<IOpenRouterRateLimitTracker> _rateLimitTrackerMock = new();
    private readonly Mock<IFreeModelQuotaTracker> _quotaTrackerMock = new();
    private readonly Mock<ILlmRoutingStrategy> _routingStrategyMock = new();
    private readonly Mock<IAiModelConfigurationRepository> _modelConfigMock = new();
    private readonly ILogger<LlmProviderSelector> _logger;

    public LlmProviderSelectorOutcomeTests()
    {
        _logger = new LoggerFactory().CreateLogger<LlmProviderSelector>();
        _circuitBreakerMock.Setup(r => r.AllowsRequests(It.IsAny<string>())).Returns(true);
    }

    private LlmProviderSelector CreateSut(
        IOpenRouterRateLimitTracker? rateLimitTracker = null,
        IFreeModelQuotaTracker? freeModelQuotaTracker = null)
    {
        var mockClient = new Mock<ILlmClient>();
        mockClient.Setup(c => c.ProviderName).Returns("Ollama");

        var aiSettings = Options.Create(new AiProviderSettings());

        return new LlmProviderSelector(
            new[] { mockClient.Object },
            _routingStrategyMock.Object,
            _circuitBreakerMock.Object,
            aiSettings,
            _modelConfigMock.Object,
            _logger,
            freeModelQuotaTracker: freeModelQuotaTracker,
            rateLimitTracker: rateLimitTracker);
    }

    private static LlmCompletionResult CreateSuccessResult(
        int promptTokens = 100,
        int completionTokens = 50,
        string modelId = "test-model",
        string provider = "OpenRouter")
    {
        return LlmCompletionResult.CreateSuccess(
            "Test response",
            new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens),
            new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = modelId, Provider = provider });
    }

    private static LlmCompletionResult CreateFailureWithRateLimit(
        string modelId = "test-model",
        RateLimitErrorType errorType = RateLimitErrorType.Rpm)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rate_limit_type"] = errorType.ToString(),
            ["rate_limit_reset_ms"] = "60000",
            ["rate_limit_model"] = modelId
        };

        return new LlmCompletionResult
        {
            Success = false,
            ErrorMessage = "Rate limited",
            Metadata = metadata
        };
    }

    // --- RecordSuccess tests ---

    [Fact]
    public void RecordSuccess_RecordsCircuitBreakerSuccess()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult();

        sut.RecordSuccess("Ollama", "test-model", 100, result);

        _circuitBreakerMock.Verify(
            r => r.RecordSuccess("Ollama", 100L),
            Times.Once);
    }

    [Fact]
    public void RecordSuccess_WithRateLimitTracker_RecordsRequest()
    {
        var sut = CreateSut(rateLimitTracker: _rateLimitTrackerMock.Object);
        var result = CreateSuccessResult(promptTokens: 100, completionTokens: 50);

        sut.RecordSuccess("OpenRouter", "gpt-4o-mini", 150, result);

        _rateLimitTrackerMock.Verify(
            r => r.RecordRequestAsync("OpenRouter", "gpt-4o-mini", 150, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RecordSuccess_WithoutRateLimitTracker_DoesNotThrow()
    {
        var sut = CreateSut(rateLimitTracker: null);
        var result = CreateSuccessResult();

        // Should not throw
        sut.RecordSuccess("Ollama", "test-model", 100, result);

        _circuitBreakerMock.Verify(
            r => r.RecordSuccess("Ollama", 100L),
            Times.Once);
    }

    // --- RecordFailure tests ---

    [Fact]
    public void RecordFailure_RecordsCircuitBreakerFailure()
    {
        var sut = CreateSut();

        sut.RecordFailure("Ollama", "test-model", 200);

        _circuitBreakerMock.Verify(
            r => r.RecordFailure("Ollama", 200L),
            Times.Once);
    }

    [Fact]
    public void RecordFailure_OpenRouterRateLimit_RecordsQuotaError()
    {
        var sut = CreateSut(freeModelQuotaTracker: _quotaTrackerMock.Object);
        var result = CreateFailureWithRateLimit("gpt-4o-mini", RateLimitErrorType.Rpm);

        sut.RecordFailure("OpenRouter", "gpt-4o-mini", 100, result);

        _quotaTrackerMock.Verify(
            q => q.RecordRateLimitErrorAsync(
                "gpt-4o-mini",
                RateLimitErrorType.Rpm,
                60000L,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RecordFailure_NonOpenRouter_SkipsQuotaTracking()
    {
        var sut = CreateSut(freeModelQuotaTracker: _quotaTrackerMock.Object);
        var result = CreateFailureWithRateLimit();

        sut.RecordFailure("Ollama", "test-model", 100, result);

        _quotaTrackerMock.Verify(
            q => q.RecordRateLimitErrorAsync(
                It.IsAny<string>(),
                It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RecordFailure_InvalidRateLimitType_SkipsQuotaTracking()
    {
        var sut = CreateSut(freeModelQuotaTracker: _quotaTrackerMock.Object);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rate_limit_type"] = "unknown_invalid_type",
            ["rate_limit_reset_ms"] = "60000"
        };
        var result = new LlmCompletionResult
        {
            Success = false,
            ErrorMessage = "Rate limited",
            Metadata = metadata
        };

        sut.RecordFailure("OpenRouter", "test-model", 100, result);

        _quotaTrackerMock.Verify(
            q => q.RecordRateLimitErrorAsync(
                It.IsAny<string>(),
                It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RecordFailure_NullResult_SkipsQuotaTracking()
    {
        var sut = CreateSut(freeModelQuotaTracker: _quotaTrackerMock.Object);

        sut.RecordFailure("OpenRouter", "test-model", 100);

        _quotaTrackerMock.Verify(
            q => q.RecordRateLimitErrorAsync(
                It.IsAny<string>(),
                It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void RecordFailure_WithoutQuotaTracker_DoesNotThrow()
    {
        var sut = CreateSut(freeModelQuotaTracker: null);
        var result = CreateFailureWithRateLimit();

        // Should not throw
        sut.RecordFailure("OpenRouter", "test-model", 100, result);

        _circuitBreakerMock.Verify(
            r => r.RecordFailure("OpenRouter", 100L),
            Times.Once);
    }

    // --- WarnIfApproachingLimitAsync tests ---

    [Fact]
    public async Task WarnIfApproachingLimitAsync_WithTracker_ChecksLimit()
    {
        _rateLimitTrackerMock
            .Setup(r => r.IsApproachingLimitAsync("OpenRouter", 80, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = CreateSut(rateLimitTracker: _rateLimitTrackerMock.Object);

        await sut.WarnIfApproachingLimitAsync("OpenRouter");

        _rateLimitTrackerMock.Verify(
            r => r.IsApproachingLimitAsync("OpenRouter", 80, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WarnIfApproachingLimitAsync_WithoutTracker_DoesNotThrow()
    {
        var sut = CreateSut(rateLimitTracker: null);

        // Should not throw
        await sut.WarnIfApproachingLimitAsync("OpenRouter");
    }

    [Fact]
    public async Task WarnIfApproachingLimitAsync_ApproachingLimit_LogsWarning()
    {
        _rateLimitTrackerMock
            .Setup(r => r.IsApproachingLimitAsync("OpenRouter", 80, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut(rateLimitTracker: _rateLimitTrackerMock.Object);

        // Should not throw; warning is logged internally
        await sut.WarnIfApproachingLimitAsync("OpenRouter");

        _rateLimitTrackerMock.Verify(
            r => r.IsApproachingLimitAsync("OpenRouter", 80, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

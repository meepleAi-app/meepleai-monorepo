using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for LlmCostService (Issue #5489).
/// Verifies cost logging, budget tracking, usage stats, and ledger event publishing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5489")]
public sealed class LlmCostServiceTests
{
    private readonly Mock<ILlmCostLogRepository> _costLogRepoMock = new();
    private readonly Mock<IAiModelConfigurationRepository> _modelConfigRepoMock = new();
    private readonly Mock<IUserBudgetService> _budgetServiceMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<IOpenRouterFileLogger> _fileLoggerMock = new();
    private readonly Mock<IOpenRouterUsageService> _usageServiceMock = new();
    private readonly ILogger<LlmCostService> _logger;

    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public LlmCostServiceTests()
    {
        _logger = new LoggerFactory().CreateLogger<LlmCostService>();

        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ILlmCostLogRepository))).Returns(_costLogRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IAiModelConfigurationRepository))).Returns(_modelConfigRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserBudgetService))).Returns(_budgetServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IPublisher))).Returns(_publisherMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
    }

    private LlmCostService CreateSut()
    {
        return new LlmCostService(
            _scopeFactoryMock.Object,
            _logger,
            _fileLoggerMock.Object,
            _usageServiceMock.Object);
    }

    private static LlmCompletionResult CreateSuccessResult(
        decimal inputCost = 0.001m,
        decimal outputCost = 0.002m,
        int promptTokens = 100,
        int completionTokens = 50,
        string modelId = "gpt-4o-mini",
        string provider = "OpenRouter")
    {
        return LlmCompletionResult.CreateSuccess(
            "Test response",
            new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens),
            new LlmCost { InputCost = inputCost, OutputCost = outputCost, ModelId = modelId, Provider = provider });
    }

    private static User CreateUser()
    {
        var email = Email.Parse($"test-{Guid.NewGuid():N}@meepleai.dev");
        var password = PasswordHash.Create("TestPass123!");
        return new User(Guid.NewGuid(), email, "TestUser", password, Role.User);
    }

    // --- LogSuccessAsync tests ---

    [Fact]
    public async Task LogSuccessAsync_LogsCostToRepository()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult();

        await sut.LogSuccessAsync(result, user: null, latencyMs: 150, RequestSource.Manual);

        _costLogRepoMock.Verify(r => r.LogCostAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.Is<LlmCostCalculation>(c => c.ModelId == "gpt-4o-mini" && c.InputCost == 0.001m),
            "completion",
            true,
            null,
            150,
            null, null,
            It.IsAny<RequestSource>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_WithUser_RecordsBudgetUsage()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult();
        var user = CreateUser();

        await sut.LogSuccessAsync(result, user, latencyMs: 100, RequestSource.Manual);

        _budgetServiceMock.Verify(b => b.RecordUsageAsync(
            user.Id,
            result.Cost.TotalCost,
            result.Usage.PromptTokens + result.Usage.CompletionTokens,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_WithoutUser_SkipsBudgetRecording()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult();

        await sut.LogSuccessAsync(result, user: null, latencyMs: 100, RequestSource.Manual);

        _budgetServiceMock.Verify(b => b.RecordUsageAsync(
            It.IsAny<Guid>(),
            It.IsAny<decimal>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogSuccessAsync_WithCostAndUser_PublishesLedgerEvent()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult(inputCost: 0.01m, outputCost: 0.02m);
        var user = CreateUser();

        await sut.LogSuccessAsync(result, user, latencyMs: 100, RequestSource.Manual);

        _publisherMock.Verify(p => p.Publish(
            It.IsAny<TokenUsageLedgerEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_WithZeroCost_SkipsLedgerEvent()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult(inputCost: 0m, outputCost: 0m);
        var user = CreateUser();

        await sut.LogSuccessAsync(result, user, latencyMs: 100, RequestSource.Manual);

        _publisherMock.Verify(p => p.Publish(
            It.IsAny<TokenUsageLedgerEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogSuccessAsync_CallsFileLogger()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult();

        await sut.LogSuccessAsync(result, user: null, latencyMs: 200, RequestSource.Manual);

        _fileLoggerMock.Verify(f => f.LogRequest(
            It.IsAny<string>(),
            "gpt-4o-mini",
            "OpenRouter",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            100, 50,
            result.Cost.TotalCost,
            200L,
            true,
            It.IsAny<bool>(),
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_WithNonZeroCost_RecordsUsageService()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult(inputCost: 0.01m, outputCost: 0.02m);

        await sut.LogSuccessAsync(result, user: null, latencyMs: 100, RequestSource.Manual);

        _usageServiceMock.Verify(u => u.RecordRequestCostAsync(
            result.Cost.TotalCost,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_WithZeroCost_SkipsUsageService()
    {
        var sut = CreateSut();
        var result = CreateSuccessResult(inputCost: 0m, outputCost: 0m);

        await sut.LogSuccessAsync(result, user: null, latencyMs: 100, RequestSource.Manual);

        _usageServiceMock.Verify(u => u.RecordRequestCostAsync(
            It.IsAny<decimal>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogSuccessAsync_ExceptionInCostLog_DoesNotThrow()
    {
        _costLogRepoMock
            .Setup(r => r.LogCostAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<LlmCostCalculation>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var sut = CreateSut();
        var result = CreateSuccessResult();

        // Should not throw — error is caught and logged as warning
        await sut.LogSuccessAsync(result, user: null, latencyMs: 100, RequestSource.Manual);
    }

    // --- LogFailureAsync tests ---

    [Fact]
    public async Task LogFailureAsync_LogsEmptyCostToRepository()
    {
        var sut = CreateSut();

        await sut.LogFailureAsync("Provider timeout", user: null, latencyMs: 5000, RequestSource.Manual);

        _costLogRepoMock.Verify(r => r.LogCostAsync(
            It.IsAny<Guid?>(),
            "Anonymous",
            It.Is<LlmCostCalculation>(c => c.TotalCost == 0m),
            "completion",
            false,
            "Provider timeout",
            5000,
            null, null,
            It.IsAny<RequestSource>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogFailureAsync_CallsFileLoggerWithError()
    {
        var sut = CreateSut();

        await sut.LogFailureAsync("Some error", user: null, latencyMs: 1000, RequestSource.AgentTask);

        _fileLoggerMock.Verify(f => f.LogRequest(
            It.IsAny<string>(),
            string.Empty,
            string.Empty,
            "AgentTask",
            It.IsAny<Guid?>(),
            0, 0, 0m, 1000L,
            false, false, null,
            "Some error"), Times.Once);
    }

    [Fact]
    public async Task LogFailureAsync_ExceptionInCostLog_DoesNotThrow()
    {
        _costLogRepoMock
            .Setup(r => r.LogCostAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<LlmCostCalculation>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var sut = CreateSut();

        // Should not throw
        await sut.LogFailureAsync("error", user: null, latencyMs: 100, RequestSource.Manual);
    }
}

using Api.Infrastructure.Entities;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for LLM retry + tiered fallback logic in SendAgentMessageCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LlmFallbackTests : IDisposable
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<IChatThreadRepository> _mockThreadRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IModelConfigurationService> _mockModelConfigService;
    private readonly Mock<ILogger<SendAgentMessageCommandHandler>> _mockLogger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Guid _userId = Guid.NewGuid();

    public LlmFallbackTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockThreadRepo = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLlmService = new Mock<ILlmService>();
        _mockModelConfigService = new Mock<IModelConfigurationService>();
        _mockLogger = new Mock<ILogger<SendAgentMessageCommandHandler>>();

        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"LlmFallbackTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            dbOptions,
            Mock.Of<MediatR.IMediator>(),
            Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        // Default RAG pipeline mocks
        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[384] }));

        var mockBudget = new Mock<IUserBudgetService>();
        mockBudget
            .Setup(s => s.HasBudgetForQueryAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Store mocks for handler construction helper
        _embeddingMock = mockEmbedding;
        _budgetMock = mockBudget;
    }

    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IUserBudgetService> _budgetMock;

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 1: Free tier rate-limited → retries → falls back to Ollama
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FreeTierRateLimited_RetriesAndFallsBackToOllama()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var freeModel = AgentDefaults.DefaultModel;
        SeedAgent(agentId, freeModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Calls 1-2 fail (primary + retry), call 3 succeeds (Ollama fallback)
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("429 rate limited")
                    : LlmCompletionResult.CreateSuccess("Ollama response");
            });

        SetupFreeModelConfig(freeModel);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test question", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        callCount.Should().Be(3); // primary + retry + fallback
        events.Should().Contain(e => e.Type == StreamingEventType.Token);
        events.Should().Contain(e => e.Type == StreamingEventType.Complete);

        // Verify fallback model was Ollama
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            AgentDefaults.OllamaFallbackModel,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 2: Free tier fallback emits ModelDowngrade event
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FreeTierRateLimited_EmitsModelDowngradeEvent()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var freeModel = AgentDefaults.DefaultModel;
        SeedAgent(agentId, freeModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("429 rate limited")
                    : LlmCompletionResult.CreateSuccess("Fallback response");
            });

        SetupFreeModelConfig(freeModel);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        var downgradeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.ModelDowngrade);
        downgradeEvent.Should().NotBeNull();

        var downgrade = downgradeEvent.Data.Should().BeOfType<StreamingModelDowngrade>().Which;
        downgrade.OriginalModel.Should().Be(freeModel);
        downgrade.FallbackModel.Should().Be(AgentDefaults.OllamaFallbackModel);
        downgrade.IsLocalFallback.Should().BeTrue();
        downgrade.UpgradeMessage.Should().NotBeNull();
        downgrade.UpgradeMessage.Should().ContainEquivalentOf("Premium");
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 3: Premium model fails → falls back to same-tier model
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PremiumModelFails_FallsBackToSameTierModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var premiumModel = "anthropic/claude-3.5-haiku";
        var fallbackModel = "openai/gpt-4o-mini";
        SeedAgent(agentId, premiumModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("Service unavailable")
                    : LlmCompletionResult.CreateSuccess("GPT response");
            });

        // Setup Premium model config
        _mockModelConfigService
            .Setup(s => s.GetModelById(premiumModel))
            .Returns(ModelConfiguration.Create(
                id: premiumModel, name: "Claude 3.5 Haiku", provider: "anthropic",
                tier: ModelTier.Premium, costPer1kInput: 0.00025m, costPer1kOutput: 0.00125m,
                maxTokens: 8192));

        _mockModelConfigService
            .Setup(s => s.GetAllModels())
            .Returns(new List<ModelConfiguration>
            {
                ModelConfiguration.Create(
                    id: premiumModel, name: "Claude 3.5 Haiku", provider: "anthropic",
                    tier: ModelTier.Premium, costPer1kInput: 0.00025m, costPer1kOutput: 0.00125m,
                    maxTokens: 8192),
                ModelConfiguration.Create(
                    id: fallbackModel, name: "GPT-4o Mini", provider: "openai",
                    tier: ModelTier.Premium, costPer1kInput: 0.00015m, costPer1kOutput: 0.0006m,
                    maxTokens: 16384),
            });

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        events.Should().Contain(e => e.Type == StreamingEventType.Token);
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            fallbackModel,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 4: Premium fallback has no upgrade message
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PremiumFallback_NoUpgradeMessage()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var premiumModel = "anthropic/claude-3.5-haiku";
        SeedAgent(agentId, premiumModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("Error")
                    : LlmCompletionResult.CreateSuccess("Response");
            });

        _mockModelConfigService
            .Setup(s => s.GetModelById(premiumModel))
            .Returns(ModelConfiguration.Create(
                id: premiumModel, name: "Claude", provider: "anthropic",
                tier: ModelTier.Premium, costPer1kInput: 0.00025m, costPer1kOutput: 0.00125m,
                maxTokens: 8192));

        _mockModelConfigService
            .Setup(s => s.GetAllModels())
            .Returns(new List<ModelConfiguration>
            {
                ModelConfiguration.Create(
                    id: premiumModel, name: "Claude", provider: "anthropic",
                    tier: ModelTier.Premium, costPer1kInput: 0.00025m, costPer1kOutput: 0.00125m,
                    maxTokens: 8192),
                ModelConfiguration.Create(
                    id: "openai/gpt-4o-mini", name: "GPT-4o Mini", provider: "openai",
                    tier: ModelTier.Premium, costPer1kInput: 0.00015m, costPer1kOutput: 0.0006m,
                    maxTokens: 16384),
            });

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        var downgradeEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.ModelDowngrade);
        downgradeEvent.Should().NotBeNull();

        var downgrade = downgradeEvent.Data.Should().BeOfType<StreamingModelDowngrade>().Which;
        downgrade.UpgradeMessage.Should().BeNull();
        downgrade.IsLocalFallback.Should().BeFalse();
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 5: Retry succeeds → no fallback needed
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RetrySucceeds_NoFallbackNeeded()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var model = AgentDefaults.DefaultModel;
        SeedAgent(agentId, model);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call fails, retry succeeds
                return callCount == 1
                    ? LlmCompletionResult.CreateFailure("Temporary error")
                    : LlmCompletionResult.CreateSuccess("Success on retry");
            });

        SetupFreeModelConfig(model);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        callCount.Should().Be(2); // primary + retry (no fallback)
        events.Should().NotContain(e => e.Type == StreamingEventType.ModelDowngrade);
        events.Should().Contain(e => e.Type == StreamingEventType.Token);
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 6: All attempts fail → LLM_FAILED error
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AllAttemptsFail_ReturnsLlmFailedError()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var model = AgentDefaults.DefaultModel;
        SeedAgent(agentId, model);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("All providers down"));

        SetupFreeModelConfig(model);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        var events = await CollectEvents(handler, command);

        // Assert
        var errorEvent = events.LastOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().NotBeNull();

        var error = errorEvent.Data.Should().BeOfType<StreamingError>().Which;
        error.errorCode.Should().Be("LLM_FAILED");
        // Error message is now sanitized (no provider internals leaked)
        error.errorMessage.Should().ContainEquivalentOf("non è al momento disponibile");
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 7: GetFallbackModel — Free model returns Ollama
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFallbackModel_FreeModel_ReturnsOllama()
    {
        // Arrange: Use a free model and ensure all 3 attempts fail except the fallback
        var agentId = Guid.NewGuid();
        var freeModel = AgentDefaults.DefaultModel;
        SeedAgent(agentId, freeModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("Rate limited")
                    : LlmCompletionResult.CreateSuccess("Local response");
            });

        SetupFreeModelConfig(freeModel);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        await CollectEvents(handler, command);

        // Assert: Third call was to Ollama fallback
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            AgentDefaults.OllamaFallbackModel,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 8: GetFallbackModel — Premium prefers different provider
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFallbackModel_PremiumModel_ReturnsDifferentProviderSameTier()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var anthropicModel = "anthropic/claude-3.5-sonnet";
        var openaiModel = "openai/gpt-4o";
        SeedAgent(agentId, anthropicModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("Provider down")
                    : LlmCompletionResult.CreateSuccess("OpenAI response");
            });

        _mockModelConfigService
            .Setup(s => s.GetModelById(anthropicModel))
            .Returns(ModelConfiguration.Create(
                id: anthropicModel, name: "Sonnet", provider: "anthropic",
                tier: ModelTier.Premium, costPer1kInput: 0.003m, costPer1kOutput: 0.015m,
                maxTokens: 8192));

        _mockModelConfigService
            .Setup(s => s.GetAllModels())
            .Returns(new List<ModelConfiguration>
            {
                ModelConfiguration.Create(
                    id: anthropicModel, name: "Sonnet", provider: "anthropic",
                    tier: ModelTier.Premium, costPer1kInput: 0.003m, costPer1kOutput: 0.015m,
                    maxTokens: 8192),
                ModelConfiguration.Create(
                    id: openaiModel, name: "GPT-4o", provider: "openai",
                    tier: ModelTier.Premium, costPer1kInput: 0.005m, costPer1kOutput: 0.015m,
                    maxTokens: 128000),
            });

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        await CollectEvents(handler, command);

        // Assert: Fallback went to OpenAI (different provider)
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            openaiModel,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // Test 9: GetFallbackModel — Unknown model returns Ollama
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFallbackModel_UnknownModel_ReturnsOllama()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var unknownModel = "unknown/nonexistent-model";
        SeedAgent(agentId, unknownModel);

        var callCount = 0;
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2
                    ? LlmCompletionResult.CreateFailure("Model not found")
                    : LlmCompletionResult.CreateSuccess("Ollama fallback");
            });

        // Unknown model returns null from config
        _mockModelConfigService
            .Setup(s => s.GetModelById(unknownModel))
            .Returns((ModelConfiguration?)null);

        var handler = CreateHandler();
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        // Act
        await CollectEvents(handler, command);

        // Assert: Falls back to Ollama for unknown models
        _mockLlmService.Verify(s => s.GenerateCompletionWithModelAsync(
            AgentDefaults.OllamaFallbackModel,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ───────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────

    private SendAgentMessageCommandHandler CreateHandler()
    {
        var mockQueryRewriter = new Mock<IConversationQueryRewriter>();
        mockQueryRewriter
            .Setup(r => r.RewriteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((query, _, _) => Task.FromResult(query));

        // Issue #5513: Consent check — default true so existing tests pass
        var consentCheckMock = new Mock<IUserAiConsentCheckService>();
        consentCheckMock
            .Setup(s => s.IsAiProcessingAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new SendAgentMessageCommandHandler(
            _mockAgentRepo.Object,
            _mockThreadRepo.Object,
            _mockUnitOfWork.Object,
            _mockLlmService.Object,
            _embeddingMock.Object,
            _dbContext,
            _budgetMock.Object,
            Mock.Of<ILlmModelOverrideService>(),
            _mockModelConfigService.Object,
            new ChatContextDomainService(),
            mockQueryRewriter.Object,
            Mock.Of<IConversationSummarizer>(),
            consentCheckMock.Object,
            Mock.Of<IGameSessionOrchestratorService>(),
            Mock.Of<IHybridCacheService>(),
            CreatePermissiveRagAccessServiceMock(),
            _mockLogger.Object);
    }

    private void SeedAgent(Guid agentId, string llmModel)
    {
        var agent = new Agent(
            agentId, "TestAgent", AgentType.RagAgent,
            AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
            true);

        _mockAgentRepo
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var docId = Guid.NewGuid();
        _dbContext.AgentConfigurations.Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = llmModel,
            AgentMode = 0,
            SelectedDocumentIdsJson = $"[\"{docId}\"]",
            Temperature = 0.3m,
            MaxTokens = 2048,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        _dbContext.SaveChanges();
    }

    private void SetupFreeModelConfig(string freeModel)
    {
        _mockModelConfigService
            .Setup(s => s.GetModelById(freeModel))
            .Returns(ModelConfiguration.Create(
                id: freeModel, name: "Llama Free", provider: "meta-llama",
                tier: ModelTier.Free, costPer1kInput: 0m, costPer1kOutput: 0m,
                maxTokens: 8192));
    }

    private static async Task<List<RagStreamingEvent>> CollectEvents(
        SendAgentMessageCommandHandler handler,
        SendAgentMessageCommand command)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }
        return events;
    }
    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}

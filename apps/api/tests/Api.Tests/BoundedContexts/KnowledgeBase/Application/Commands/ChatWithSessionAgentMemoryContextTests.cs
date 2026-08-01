using System.Collections.Generic;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

// Disambiguate UserTier from two namespaces
using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Issue #2750 gap E5 — proving test for Path 2 (session-agent streaming chat) house-rule injection.
/// The existing session-agent handler tests stub <see cref="IAgentMemoryContextBuilder"/> to return null;
/// this class proves the OTHER branch: when the builder returns a non-null house-rule context, the handler
/// appends it under a "## Agent Memory Context" heading and forwards the enriched system prompt to the LLM.
/// Mirrors the Path-1 proof pattern in AskQuestionQueryHandlerPhase2Tests (Handle_WhenHouseRuleMatched_...).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ChatWithSessionAgentMemoryContextTests
{
    [Fact]
    public async Task Chat_WhenAgentMemoryContextReturned_InjectsHouseRulesIntoSystemPrompt()
    {
        // Arrange — builder yields a non-null house-rule context (Path 2 non-null branch)
        const string memoryContext = "House Rules for this game:\n- \"No trading on turn 1\" (from player)";

        string? capturedSystemPrompt = null;
        var handler = BuildHandler(
            memoryContext: memoryContext,
            captureSystemPrompt: sys => capturedSystemPrompt = sys);

        var command = BuildCommand();

        // Act — drain the stream to completion so the LLM call is issued
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — enriched system prompt reached the LLM
        capturedSystemPrompt.Should().NotBeNull("the LLM must be invoked with a system prompt");
        capturedSystemPrompt.Should().Contain("## Agent Memory Context",
            "the handler wraps the injected memory under this heading");
        capturedSystemPrompt.Should().Contain("No trading on turn 1",
            "the house-rule text must be present in the enriched system prompt");
        capturedSystemPrompt.Should().Contain("(from player)");
    }

    [Fact]
    public async Task Chat_WhenNoAgentMemoryContext_DoesNotInjectMemoryHeading()
    {
        // Arrange — builder yields null (nothing to inject); baseline / negative control
        string? capturedSystemPrompt = null;
        var handler = BuildHandler(
            memoryContext: null,
            captureSystemPrompt: sys => capturedSystemPrompt = sys);

        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert
        capturedSystemPrompt.Should().NotBeNull();
        capturedSystemPrompt.Should().NotContain("## Agent Memory Context",
            "no memory context means the heading must not be appended");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ChatWithSessionAgentCommand BuildCommand() =>
        new ChatWithSessionAgentCommand(
            AgentSessionId: Guid.NewGuid(),
            UserQuestion: "What are the rules?",
            UserId: Guid.NewGuid());

    /// <summary>
    /// Constructs a fully-wired handler with minimal happy-path mocks, overriding the agent-memory
    /// builder to return <paramref name="memoryContext"/> and capturing the system prompt forwarded
    /// to the LLM. Harness mirrors ChatSessionAgentBroadcastTests.BuildHandler.
    /// </summary>
    private static ChatWithSessionAgentCommandHandler BuildHandler(
        string? memoryContext,
        Action<string> captureSystemPrompt)
    {
        // --- AgentSession repo ---
        var playerId = Guid.NewGuid();
        var initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: playerId,
            playerScores: new Dictionary<Guid, decimal> { [playerId] = 0 },
            gamePhase: "Setup",
            lastAction: "Game started");

        var agentSession = new AgentSession(
            id: Guid.NewGuid(),
            agentDefinitionId: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            initialState: initialState);

        var sessionRepo = new Mock<IAgentSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentSession);

        // --- AgentDefinition repo ---
        var definition = AgentDefinition.Create(
            name: "tutor",
            description: "Test agent",
            type: AgentType.Custom("tutor", "Tutor"),
            config: AgentDefinitionConfig.Default());

        var definitionRepo = new Mock<IAgentDefinitionRepository>();
        definitionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        // --- Game core data ---
        var gameCoreData = new Mock<IGameCoreDataProvider>();
        gameCoreData
            .Setup(g => g.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameCoreData?)null);  // null → "Unknown Game" title

        // --- ChatThread repo: return null to trigger auto-create path ---
        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);
        chatThreadRepo
            .Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatThreadRepo
            .Setup(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // --- LiveSession repo: null → no live-session context, groupId == null ---
        var liveSessionRepo = new Mock<ILiveSessionRepository>();
        liveSessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        // --- UoW ---
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // --- RAG prompt assembly ---
        var assembled = new AssembledPrompt(
            SystemPrompt: "You are a helpful assistant.",
            UserPrompt: "What are the rules?",
            Citations: new List<ChunkCitation>(),
            EstimatedTokens: 100);

        var ragPromptService = new Mock<IRagPromptAssemblyService>();
        ragPromptService
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()))
            .ReturnsAsync(assembled);

        // --- Copyright tier resolver → returns citations as-is (empty) ---
        var tierResolver = new Mock<ICopyrightTierResolver>();
        tierResolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkCitation>());

        // --- Agent memory context builder → returns the context under test ---
        var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
        memoryBuilder
            .Setup(b => b.BuildContextAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(memoryContext);

        // --- LLM service: capture the forwarded system prompt, emit a token + final chunk ---
        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((sys, _, _, _) => captureSystemPrompt(sys))
            .Returns(CreateLlmStream("Hello world"));

        // --- Budget service ---
        var budgetService = new Mock<IUserBudgetService>();
        budgetService
            .Setup(b => b.HasBudgetForQueryAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // --- Circuit breaker ---
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string, string)>());

        // --- ServiceScopeFactory (fire-and-forget chunk usage increment only) ---
        var scopeFactory = new Mock<IServiceScopeFactory>();

        // --- Copyright leak guard (no leak) ---
        var noLeak = new CopyrightLeakResult(HasLeak: false, Matches: Array.Empty<LeakMatch>());
        var leakGuard = new Mock<ICopyrightLeakGuard>();
        leakGuard
            .Setup(g => g.ScanAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(noLeak);

        var fallbackProvider = new Mock<ICopyrightFallbackMessageProvider>();

        // --- Live session stream gateway (unused: no live session) ---
        var gateway = new Mock<ILiveSessionStreamGateway>();

        return new ChatWithSessionAgentCommandHandler(
            sessionRepository: sessionRepo.Object,
            definitionRepository: definitionRepo.Object,
            chatThreadRepository: chatThreadRepo.Object,
            gameCoreData: gameCoreData.Object,
            liveSessionRepository: liveSessionRepo.Object,
            unitOfWork: unitOfWork.Object,
            ragPromptService: ragPromptService.Object,
            copyrightTierResolver: tierResolver.Object,
            agentMemoryContextBuilder: memoryBuilder.Object,
            llmService: llmService.Object,
            userBudgetService: budgetService.Object,
            circuitBreakerRegistry: registry.Object,
            scopeFactory: scopeFactory.Object,
            logger: NullLogger<ChatWithSessionAgentCommandHandler>.Instance,
            copyrightLeakGuard: leakGuard.Object,
            fallbackMessageProvider: fallbackProvider.Object,
            copyrightOptions: Options.Create(new CopyrightLeakGuardOptions()),
            liveSessionStreamGateway: gateway.Object);
    }

    private static async IAsyncEnumerable<StreamChunk> CreateLlmStream(string content)
    {
        await Task.Yield();
        yield return new StreamChunk(Content: content);
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(10, 5, 15),
            IsFinal: true);
    }
}

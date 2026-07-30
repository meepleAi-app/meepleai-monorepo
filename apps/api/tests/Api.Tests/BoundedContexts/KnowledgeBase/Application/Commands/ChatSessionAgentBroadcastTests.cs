using System.Collections.Generic;
using System.Text.Json;
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
/// SP2 T8 — verifies that ChatWithSessionAgentCommandHandler fires ONE session:chat
/// broadcast (with tier-aware citations[]) via ILiveSessionStreamGateway when a RAG
/// chat completes. AC-CHAT-1 (Full tier), AC-CHAT-2 (Protected tier, no verbatim),
/// AC-CHAT-3 (non-grounded, empty citations), non-live guard, broadcast-failure swallow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ChatSessionAgentBroadcastTests
{
    // -----------------------------------------------------------------------
    // AC-CHAT-1: Full tier → broadcast with non-empty snippetPreview
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WithLiveSession_FullTierCitation_BroadcastsSessionChatWithSnippetPreview()
    {
        // Arrange
        var liveSessionId = Guid.NewGuid();
        var liveSession = BuildLiveSession(liveSessionId);

        var citation = new ChunkCitation(
            DocumentId: "doc-1",
            PageNumber: 3,
            RelevanceScore: 0.95f,
            SnippetPreview: "verbatim rule text",
            CopyrightTier: CopyrightTier.Full,
            IsPublic: true);

        var gateway = new Mock<ILiveSessionStreamGateway>();

        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation> { citation },
            gateway: gateway.Object);

        var command = BuildCommand();

        // Act — drain the stream to completion
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — exactly one session:chat broadcast
        gateway.Verify(
            g => g.BroadcastAsync(
                liveSessionId,
                It.Is<LiveSessionStreamEvent>(e =>
                    e.Type == "session:chat" &&
                    HasNonEmptySnippetPreview(e.Data)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should broadcast session:chat with snippetPreview for Full-tier citation");
    }

    // -----------------------------------------------------------------------
    // AC-CHAT-2: Protected tier → broadcast with SnippetPreview == null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WithLiveSession_ProtectedTierCitation_BroadcastsWithNullSnippetPreview()
    {
        // Arrange
        var liveSessionId = Guid.NewGuid();
        var liveSession = BuildLiveSession(liveSessionId);

        var citation = new ChunkCitation(
            DocumentId: "doc-2",
            PageNumber: 1,
            RelevanceScore: 0.80f,
            SnippetPreview: "this should be suppressed",   // present in raw citation
            CopyrightTier: CopyrightTier.Protected);

        var gateway = new Mock<ILiveSessionStreamGateway>();

        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation> { citation },
            gateway: gateway.Object);

        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — broadcast must not carry verbatim snippet
        gateway.Verify(
            g => g.BroadcastAsync(
                liveSessionId,
                It.Is<LiveSessionStreamEvent>(e =>
                    e.Type == "session:chat" &&
                    AllSnippetPreviewsAreNull(e.Data)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Protected tier: SnippetPreview must be null on the broadcast payload (no verbatim leak)");
    }

    // -----------------------------------------------------------------------
    // SP-C (#3407): region overlay is Full-gated (same as SnippetPreview)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WithLiveSession_FullTierCitation_BroadcastsRegions()
    {
        // Arrange — Full-tier citation carrying a bounding box.
        var liveSessionId = Guid.NewGuid();
        var liveSession = BuildLiveSession(liveSessionId);

        var citation = new ChunkCitation(
            DocumentId: "doc-1",
            PageNumber: 2,
            RelevanceScore: 0.95f,
            SnippetPreview: "verbatim rule text",
            CopyrightTier: CopyrightTier.Full,
            IsPublic: true)
        {
            BoundingBoxesJson = "[{\"page\":2,\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4}]",
            CharStart = 100,
            CharEnd = 250,
        };

        var gateway = new Mock<ILiveSessionStreamGateway>();
        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation> { citation },
            gateway: gateway.Object);
        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — the region overlay is surfaced for Full-tier content.
        gateway.Verify(
            g => g.BroadcastAsync(
                liveSessionId,
                It.Is<LiveSessionStreamEvent>(e =>
                    e.Type == "session:chat" &&
                    HasRegions(e.Data)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Full tier: broadcast citation must carry parsed regions[]");
    }

    [Fact]
    public async Task Chat_WithLiveSession_ProtectedTierCitation_BroadcastsWithNullRegions()
    {
        // Arrange — same bounding box, but Protected tier → regions MUST be withheld (DA-4).
        var liveSessionId = Guid.NewGuid();
        var liveSession = BuildLiveSession(liveSessionId);

        var citation = new ChunkCitation(
            DocumentId: "doc-2",
            PageNumber: 1,
            RelevanceScore: 0.80f,
            SnippetPreview: "this should be suppressed",
            CopyrightTier: CopyrightTier.Protected)
        {
            BoundingBoxesJson = "[{\"page\":1,\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4}]",
            CharStart = 100,
            CharEnd = 250,
        };

        var gateway = new Mock<ILiveSessionStreamGateway>();
        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation> { citation },
            gateway: gateway.Object);
        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — no verbatim region highlight for Protected content.
        gateway.Verify(
            g => g.BroadcastAsync(
                liveSessionId,
                It.Is<LiveSessionStreamEvent>(e =>
                    e.Type == "session:chat" &&
                    NoRegions(e.Data)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Protected tier: regions must be null on the broadcast payload (no verbatim highlight leak)");
    }

    // -----------------------------------------------------------------------
    // AC-CHAT-3: Non-grounded / no citations → still broadcasts with citations == []
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WithLiveSession_NoCitations_BroadcastsSessionChatWithEmptyCitations()
    {
        // Arrange
        var liveSessionId = Guid.NewGuid();
        var liveSession = BuildLiveSession(liveSessionId);

        var gateway = new Mock<ILiveSessionStreamGateway>();

        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation>(),   // no citations
            gateway: gateway.Object);

        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert
        gateway.Verify(
            g => g.BroadcastAsync(
                liveSessionId,
                It.Is<LiveSessionStreamEvent>(e =>
                    e.Type == "session:chat" &&
                    HasEmptyCitations(e.Data)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "non-grounded chat should still broadcast session:chat with empty citations list");
    }

    // -----------------------------------------------------------------------
    // Non-live guard: liveSession == null → BroadcastAsync NEVER called
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WithoutLiveSession_NeverCallsBroadcast()
    {
        // Arrange — liveSession repo returns null
        var gateway = new Mock<ILiveSessionStreamGateway>();

        var handler = BuildHandler(
            liveSession: null,
            resolvedCitations: new List<ChunkCitation>(),
            gateway: gateway.Object);

        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert
        gateway.Verify(
            g => g.BroadcastAsync(
                It.IsAny<Guid>(),
                It.IsAny<LiveSessionStreamEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "no live session → gateway must never be called");
    }

    // -----------------------------------------------------------------------
    // Broadcast failure → stream still completes (try/catch swallow)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Chat_WhenBroadcastThrows_StreamingCompleteStillYielded()
    {
        // Arrange
        var liveSession = BuildLiveSession(Guid.NewGuid());

        var gateway = new Mock<ILiveSessionStreamGateway>();
        gateway
            .Setup(g => g.BroadcastAsync(
                It.IsAny<Guid>(),
                It.IsAny<LiveSessionStreamEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis is down"));

        var handler = BuildHandler(
            liveSession: liveSession,
            resolvedCitations: new List<ChunkCitation>(),
            gateway: gateway.Object);

        var command = BuildCommand();

        // Act
        var events = new List<RagStreamingEvent>();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var evt in handler.Handle(command, CancellationToken.None))
            {
                events.Add(evt);
            }
        });

        // Assert — no exception escapes the handler
        exception.Should().BeNull("broadcast failure must be swallowed");

        // StreamingComplete must still be yielded
        events.Should().Contain(e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded even when broadcast throws");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static LiveGameSession BuildLiveSession(Guid id) =>
        LiveGameSession.Create(
            id: id,
            createdByUserId: Guid.NewGuid(),
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

    private static ChatWithSessionAgentCommand BuildCommand() =>
        new ChatWithSessionAgentCommand(
            AgentSessionId: Guid.NewGuid(),
            UserQuestion: "What are the rules?",
            UserId: Guid.NewGuid());

    /// <summary>
    /// Constructs a fully-wired handler with minimal happy-path mocks,
    /// overriding only liveSession, resolvedCitations and gateway for per-test control.
    /// </summary>
    private static ChatWithSessionAgentCommandHandler BuildHandler(
        LiveGameSession? liveSession,
        IReadOnlyList<ChunkCitation> resolvedCitations,
        ILiveSessionStreamGateway gateway)
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

        // --- LiveSession repo ---
        var liveSessionRepo = new Mock<ILiveSessionRepository>();
        liveSessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSession);

        // --- UoW ---
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // --- RAG prompt assembly → returns citations that will be resolved ---
        var assembled = new AssembledPrompt(
            SystemPrompt: "You are a helpful assistant.",
            UserPrompt: "What are the rules?",
            Citations: resolvedCitations.ToList(),
            EstimatedTokens: 100);

        var ragPromptService = new Mock<IRagPromptAssemblyService>();
        ragPromptService
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>()))
            .ReturnsAsync(assembled);

        // --- Copyright tier resolver → returns resolvedCitations as-is ---
        var tierResolver = new Mock<ICopyrightTierResolver>();
        tierResolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedCitations);

        // --- Agent memory context builder ---
        var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
        memoryBuilder
            .Setup(b => b.BuildContextAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // --- LLM service: emit one content token + final chunk ---
        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
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

        // --- ServiceScopeFactory (for chunk usage increment fire-and-forget only) ---
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
            liveSessionStreamGateway: gateway);
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

    // -----------------------------------------------------------------------
    // Payload inspection helpers (Data is an anonymous object — use JSON round-trip)
    // -----------------------------------------------------------------------

    private static bool HasNonEmptySnippetPreview(object data)
    {
        var doc = ToJsonDocument(data);
        if (!doc.RootElement.TryGetProperty("citations", out var citations)) return false;
        if (citations.GetArrayLength() == 0) return false;
        var first = citations[0];
        return first.TryGetProperty("snippetPreview", out var sp) &&
               sp.ValueKind == JsonValueKind.String &&
               !string.IsNullOrEmpty(sp.GetString());
    }

    private static bool AllSnippetPreviewsAreNull(object data)
    {
        var doc = ToJsonDocument(data);
        if (!doc.RootElement.TryGetProperty("citations", out var citations)) return true;
        foreach (var item in citations.EnumerateArray())
        {
            if (item.TryGetProperty("snippetPreview", out var sp) &&
                sp.ValueKind != JsonValueKind.Null)
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasEmptyCitations(object data)
    {
        var doc = ToJsonDocument(data);
        if (!doc.RootElement.TryGetProperty("citations", out var citations)) return true;
        return citations.GetArrayLength() == 0;
    }

    private static bool HasRegions(object data)
    {
        var doc = ToJsonDocument(data);
        if (!doc.RootElement.TryGetProperty("citations", out var citations)) return false;
        if (citations.GetArrayLength() == 0) return false;
        var first = citations[0];
        return first.TryGetProperty("regions", out var regions) &&
               regions.ValueKind == JsonValueKind.Array &&
               regions.GetArrayLength() > 0;
    }

    private static bool NoRegions(object data)
    {
        var doc = ToJsonDocument(data);
        if (!doc.RootElement.TryGetProperty("citations", out var citations)) return true;
        foreach (var item in citations.EnumerateArray())
        {
            if (item.TryGetProperty("regions", out var regions) &&
                regions.ValueKind == JsonValueKind.Array &&
                regions.GetArrayLength() > 0)
            {
                return false;
            }
        }
        return true;
    }

    private static JsonDocument ToJsonDocument(object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return JsonDocument.Parse(json);
    }
}

using Api.Infrastructure.Entities;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
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

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for SendAgentMessageCommandHandler
/// Issue #4126: API Integration for Agent Chat
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SendAgentMessageCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepository;
    private readonly Mock<IChatThreadRepository> _mockChatThreadRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IUserBudgetService> _mockBudgetService;
    private readonly Mock<ILogger<SendAgentMessageCommandHandler>> _mockLogger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly SendAgentMessageCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public SendAgentMessageCommandHandlerTests()
    {
        _mockAgentRepository = new Mock<IAgentRepository>();
        _mockChatThreadRepository = new Mock<IChatThreadRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLlmService = new Mock<ILlmService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockBudgetService = new Mock<IUserBudgetService>();
        _mockLogger = new Mock<ILogger<SendAgentMessageCommandHandler>>();
        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"SendAgentTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<MediatR.IMediator>(), Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        // Default RAG pipeline mocks: embedding → vector search → budget check
        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[384] }));

        _mockBudgetService
            .Setup(s => s.HasBudgetForQueryAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Query rewriter stub: returns the original query unchanged
        var mockQueryRewriter = new Mock<IConversationQueryRewriter>();
        mockQueryRewriter
            .Setup(r => r.RewriteQueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((query, _, _) => Task.FromResult(query));

        // Issue #5513: Consent check — default true so existing tests pass
        var consentCheckMock = new Mock<IUserAiConsentCheckService>();
        consentCheckMock
            .Setup(s => s.IsAiProcessingAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new SendAgentMessageCommandHandler(
            _mockAgentRepository.Object,
            _mockChatThreadRepository.Object,
            _mockUnitOfWork.Object,
            _mockLlmService.Object,
            _mockEmbeddingService.Object,
            _dbContext,
            _mockBudgetService.Object,
            Mock.Of<ILlmModelOverrideService>(),
            Mock.Of<IModelConfigurationService>(),
            new ChatContextDomainService(),
            mockQueryRewriter.Object,
            Mock.Of<IConversationSummarizer>(),
            consentCheckMock.Object,
            Mock.Of<IGameSessionOrchestratorService>(),
            Mock.Of<IHybridCacheService>(),
            CreatePermissiveRagAccessServiceMock(),
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Should_Stream_Tokens_When_Agent_Exists()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "What is Catan?", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Catan is a board game."));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Type == StreamingEventType.StateUpdate);
        Assert.Contains(events, e => e.Type == StreamingEventType.Token);
        Assert.Contains(events, e => e.Type == StreamingEventType.Complete);

        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Single(tokenEvents); // Non-streaming: single token with full response
    }

    [Fact]
    public async Task Should_Return_Error_When_Agent_Not_Found()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var command = new SendAgentMessageCommand(agentId, "What is Catan?", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.Single(events);
        var errorEvent = events[0];
        Assert.Equal(StreamingEventType.Error, errorEvent.Type);

        var error = Assert.IsType<StreamingError>(errorEvent.Data);
        Assert.Contains(agentId.ToString(), error.errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("AGENT_NOT_FOUND", error.errorCode);
    }

    [Fact]
    public async Task Should_Include_StateUpdate_Before_Tokens()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Chess Master", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Teach me chess", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Chess is..."));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var stateUpdateEvent = events.First(e => e.Type == StreamingEventType.StateUpdate);
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(stateUpdateEvent.Data);
        Assert.Contains("Chess Master", stateUpdate.message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_Include_Complete_Event_At_End()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Test question", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Response"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var completeEvent = events.Last();
        Assert.Equal(StreamingEventType.Complete, completeEvent.Type);

        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        // No vector results returned → retrievalConfidence is null
        Assert.Null(complete.confidence);
    }

    [Fact]
    public async Task Should_Skip_Empty_Token_Chunks()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Test", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // Empty LLM response should not emit a token event
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(""));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - handler emits single token (non-streaming), but skips persistence for empty response
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        Assert.Single(tokenEvents);
        // Only user message update (no assistant persistence for empty response)
        _mockChatThreadRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Throw_When_Command_Is_Null()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _handler.Handle(null!, CancellationToken.None))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task Should_AutoCreate_Thread_When_ChatThreadId_Is_Null()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "What is Catan?", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - thread was auto-created and persisted
        _mockChatThreadRepository.Verify(
            r => r.AddAsync(It.Is<ChatThread>(t => t.UserId == _userId), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Should_Reuse_Existing_Thread_When_ChatThreadId_Provided()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var existingThread = new ChatThread(threadId, _userId, agentId: agentId, title: "Existing thread");
        var command = new SendAgentMessageCommand(agentId, "Follow-up question", _userId, threadId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockChatThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingThread);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Response"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - no new thread created, existing one updated
        _mockChatThreadRepository.Verify(
            r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockChatThreadRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // user msg + assistant msg
    }

    [Fact]
    public async Task Should_Return_Error_When_ChatThreadId_Not_Found()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Question", _userId, threadId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockChatThreadRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        Assert.Single(events);
        var error = Assert.IsType<StreamingError>(events[0].Data);
        Assert.Equal("THREAD_NOT_FOUND", error.errorCode);
    }

    [Fact]
    public async Task Should_Include_ChatThreadId_In_Complete_Event()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Question", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var completeEvent = events.Last();
        var complete = Assert.IsType<StreamingComplete>(completeEvent.Data);
        Assert.NotNull(complete.chatThreadId);
        Assert.NotEqual(Guid.Empty, complete.chatThreadId!.Value);
    }

    [Fact]
    public async Task Should_Include_ChatThreadId_In_StateUpdate_Event()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "Question", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert
        var stateEvent = events.First(e => e.Type == StreamingEventType.StateUpdate);
        var stateUpdate = Assert.IsType<StreamingStateUpdate>(stateEvent.Data);
        Assert.NotNull(stateUpdate.chatThreadId);
    }

    [Fact]
    public async Task Should_Persist_User_And_Assistant_Messages()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent, AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)), true);
        SeedAgentConfiguration(agentId);
        var command = new SendAgentMessageCommand(agentId, "What is Catan?", _userId);

        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Catan is great"));

        // Act
        var events = new List<RagStreamingEvent>();
        await foreach (var @event in _handler.Handle(command, CancellationToken.None))
        {
            events.Add(@event);
        }

        // Assert - update called twice: once for user message, once for assistant message
        _mockChatThreadRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        // SaveChanges: 1 for thread creation, 1 for user msg, 1 for assistant msg
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    private void SeedAgentConfiguration(Guid agentId)
    {
        _dbContext.AgentConfigurations.Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "gpt-4",
            AgentMode = 0,
            SelectedDocumentIdsJson = $"[\"{Guid.NewGuid()}\"]",
            Temperature = 0.7m,
            MaxTokens = 2048,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        _dbContext.SaveChanges();
    }

    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}

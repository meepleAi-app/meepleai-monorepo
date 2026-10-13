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
/// Issue #4242, the session agent. The request may name a thread: the handler writes the question
/// and the answer into it and puts its history into the prompt. It checked only that the thread
/// existed, so a caller who named someone else's thread read that conversation through the answer
/// and wrote into it.
/// </summary>
/// <remarks>
/// Both directions are asserted. A guard that refused every named thread would pass the refusal
/// tests and silently start a new conversation at every question.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4242")]
public sealed class ChatWithSessionAgentThreadOwnershipTests
{
    private const string Question = "What are the rules?";
    private const string OwnersMessage = "We play with the long variant.";
    private static readonly Guid Asker = Guid.NewGuid();
    private static readonly Guid SessionGame = Guid.NewGuid();

    [Fact]
    public async Task TheAskersOwnThread_IsUsedAndReceivesTheQuestion()
    {
        // The threads the session agent creates have no game: this is the one the client sends back.
        var thread = new ChatThread(Guid.NewGuid(), Asker);
        var bench = Bench.With(thread);

        var events = await bench.AskAsync(thread.Id);

        events.Should().NotContain(e => e.Type == StreamingEventType.Error);
        bench.ThePromptWasBuiltOn(thread);
        thread.Messages.Should().Contain(m => m.Role == "user" && m.Content == Question);
    }

    [Fact]
    public async Task TheAskersOwnThread_OnTheGameOfTheSession_IsUsed()
    {
        var thread = new ChatThread(Guid.NewGuid(), Asker, SessionGame);
        var bench = Bench.With(thread);

        var events = await bench.AskAsync(thread.Id);

        events.Should().NotContain(e => e.Type == StreamingEventType.Error);
        bench.ThePromptWasBuiltOn(thread);
    }

    [Fact]
    public async Task SomeoneElsesThread_IsRefusedAndLeftUntouched()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());
        thread.AddUserMessage(OwnersMessage);
        var bench = Bench.With(thread);

        var events = await bench.AskAsync(thread.Id);

        ErrorOf(events).errorCode.Should().Be("THREAD_NOT_FOUND");
        bench.NothingWasDoneWithAThread();
        thread.Messages.Should().ContainSingle().Which.Content.Should().Be(OwnersMessage);
    }

    /// <summary>
    /// The answer does not say whether the thread exists: same single event, same code, same text
    /// as for an id that names nothing.
    /// </summary>
    [Fact]
    public async Task SomeoneElsesThread_IsAnsweredExactlyLikeAThreadThatDoesNotExist()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid());

        var forAStranger = await Bench.With(thread).AskAsync(thread.Id);
        var forNothing = await Bench.With(thread: null).AskAsync(thread.Id);

        ErrorOf(forAStranger).Should().Be(ErrorOf(forNothing));
    }

    /// <summary>
    /// The same rule as the other answer paths: a thread is used for its owner and for its game.
    /// Here the thread is also written to, so it is refused rather than answered without history.
    /// </summary>
    [Fact]
    public async Task TheAskersOwnThread_OnAnotherGame_IsRefusedAndLeftUntouched()
    {
        var thread = new ChatThread(Guid.NewGuid(), Asker, Guid.NewGuid());
        var bench = Bench.With(thread);

        var events = await bench.AskAsync(thread.Id);

        ErrorOf(events).errorCode.Should().Be("THREAD_NOT_FOUND");
        bench.NothingWasDoneWithAThread();
        thread.Messages.Should().BeEmpty();
    }

    /// <summary>A refusal is one event and nothing else: no state update, no token.</summary>
    private static StreamingError ErrorOf(IReadOnlyList<RagStreamingEvent> events)
    {
        var only = events.Should().ContainSingle().Subject;
        only.Type.Should().Be(StreamingEventType.Error);
        return only.Data.Should().BeOfType<StreamingError>().Subject;
    }

    // -----------------------------------------------------------------------
    // Bench — mirrors ChatWithSessionAgentMemoryContextTests.BuildHandler, and keeps the mocks whose
    // calls these tests assert.
    // -----------------------------------------------------------------------

    private sealed class Bench
    {
        private readonly ChatWithSessionAgentCommandHandler _handler;
        private readonly Mock<IChatThreadRepository> _chatThreadRepo;
        private readonly Mock<IUnitOfWork> _unitOfWork;
        private readonly Mock<IRagPromptAssemblyService> _ragPromptService;
        private readonly Mock<ILlmService> _llmService;

        private Bench(
            ChatWithSessionAgentCommandHandler handler,
            Mock<IChatThreadRepository> chatThreadRepo,
            Mock<IUnitOfWork> unitOfWork,
            Mock<IRagPromptAssemblyService> ragPromptService,
            Mock<ILlmService> llmService)
        {
            _handler = handler;
            _chatThreadRepo = chatThreadRepo;
            _unitOfWork = unitOfWork;
            _ragPromptService = ragPromptService;
            _llmService = llmService;
        }

        public async Task<IReadOnlyList<RagStreamingEvent>> AskAsync(Guid chatThreadId)
        {
            var command = new ChatWithSessionAgentCommand(
                AgentSessionId: Guid.NewGuid(),
                UserQuestion: Question,
                UserId: Asker,
                ChatThreadId: chatThreadId);

            var events = new List<RagStreamingEvent>();
            await foreach (var e in _handler.Handle(command, CancellationToken.None))
            {
                events.Add(e);
            }

            return events;
        }

        public void ThePromptWasBuiltOn(ChatThread thread) =>
            _ragPromptService.Verify(AssemblePrompt(t => ReferenceEquals(t, thread)), Times.Once);

        /// <summary>Not read into a prompt, not sent to the model, and nothing saved.</summary>
        public void NothingWasDoneWithAThread()
        {
            _ragPromptService.Verify(AssemblePrompt(_ => true), Times.Never);
            _llmService.Verify(
                l => l.GenerateCompletionStreamAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _chatThreadRepo.Verify(
                r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
            _chatThreadRepo.Verify(
                r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        private static System.Linq.Expressions.Expression<Func<IRagPromptAssemblyService, Task<AssembledPrompt>>>
            AssemblePrompt(System.Linq.Expressions.Expression<Func<ChatThread?, bool>> thread) =>
            r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.Is(thread),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>());

        /// <param name="thread">What the repository returns for any id; null for a thread that does not exist.</param>
        public static Bench With(ChatThread? thread)
        {
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
                userId: Asker,
                gameId: SessionGame,
                initialState: initialState);

            var sessionRepo = new Mock<IAgentSessionRepository>();
            sessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentSession);

            var definitionRepo = new Mock<IAgentDefinitionRepository>();
            definitionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AgentDefinition.Create(
                    name: "tutor",
                    description: "Test agent",
                    config: AgentDefinitionConfig.Default()));

            var gameCoreData = new Mock<IGameCoreDataProvider>();
            gameCoreData
                .Setup(g => g.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GameCoreData?)null);

            var chatThreadRepo = new Mock<IChatThreadRepository>();
            chatThreadRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(thread);
            chatThreadRepo
                .Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            chatThreadRepo
                .Setup(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var liveSessionRepo = new Mock<ILiveSessionRepository>();
            liveSessionRepo
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LiveGameSession?)null);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var ragPromptService = new Mock<IRagPromptAssemblyService>();
            ragPromptService
                .Setup(AssemblePrompt(_ => true))
                .ReturnsAsync(new AssembledPrompt(
                    SystemPrompt: "You are a helpful assistant.",
                    UserPrompt: Question,
                    Citations: new List<ChunkCitation>(),
                    EstimatedTokens: 100));

            var tierResolver = new Mock<ICopyrightTierResolver>();
            tierResolver
                .Setup(r => r.ResolveAsync(
                    It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChunkCitation>());

            var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
            memoryBuilder
                .Setup(b => b.BuildContextAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var llmService = new Mock<ILlmService>();
            llmService
                .Setup(l => l.GenerateCompletionStreamAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
                .Returns(CreateLlmStream("Hello world"));

            var registry = new Mock<ICircuitBreakerRegistry>();
            registry
                .Setup(r => r.GetMonitoringStatus())
                .Returns(new Dictionary<string, (string, string)>());

            var leakGuard = new Mock<ICopyrightLeakGuard>();
            leakGuard
                .Setup(g => g.ScanAsync(
                    It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CopyrightLeakResult(HasLeak: false, Matches: Array.Empty<LeakMatch>()));

            var handler = new ChatWithSessionAgentCommandHandler(
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
                circuitBreakerRegistry: registry.Object,
                scopeFactory: new Mock<IServiceScopeFactory>().Object,
                logger: NullLogger<ChatWithSessionAgentCommandHandler>.Instance,
                groundedAnswerService: new GroundedAnswerService(
                    leakGuard.Object, new Mock<ICopyrightFallbackMessageProvider>().Object,
                    Options.Create(new CopyrightLeakGuardOptions()),
                    NullLogger<GroundedAnswerService>.Instance),
                liveSessionStreamGateway: new Mock<ILiveSessionStreamGateway>().Object);

            return new Bench(handler, chatThreadRepo, unitOfWork, ragPromptService, llmService);
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
}

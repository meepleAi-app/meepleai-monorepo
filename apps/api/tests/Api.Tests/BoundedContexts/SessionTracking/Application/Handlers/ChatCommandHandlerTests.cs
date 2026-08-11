using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.Services.ImageProcessing;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Unit tests for chat command handlers.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SendSessionChatMessageCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly SendSessionChatMessageCommandHandler _handler;

    public SendSessionChatMessageCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _mockMediator = new Mock<IMediator>();
        _handler = new SendSessionChatMessageCommandHandler(
            _mockSessionRepo.Object,
            _mockChatRepo.Object,
            _mockMediator.Object);
    }

    private static Session CreateSessionWithParticipant(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participant = new Participant { Id = participantId, SessionId = sessionId };
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(participant);

        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesMessageAndPublishesEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockChatRepo.Setup(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new SendSessionChatMessageCommand(sessionId, senderId, "Hello team!", 3, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MessageId.Should().NotBe(Guid.Empty);
        result.SequenceNumber.Should().Be(1);
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new SendSessionChatMessageCommand(sessionId, Guid.NewGuid(), "msg", null, null);

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SenderNotInSession_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var differentParticipant = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, differentParticipant);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new SendSessionChatMessageCommand(sessionId, senderId, "msg", null, null);

        var act2 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<NotFoundException>();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SendSystemEventCommandHandlerTests
{
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly SendSystemEventCommandHandler _handler;

    public SendSystemEventCommandHandlerTests()
    {
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _handler = new SendSystemEventCommandHandler(_mockChatRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesSystemEventMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockChatRepo.Setup(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var command = new SendSystemEventCommand(sessionId, "Alice joined", 2);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MessageId.Should().NotBe(Guid.Empty);
        result.SequenceNumber.Should().Be(5);
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Collection("AgentGroundingMetrics")]
public class AskSessionAgentCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly AskSessionAgentCommandHandler _handler;

    public AskSessionAgentCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLlmService = new Mock<ILlmService>();

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Test agent response"));

        _handler = new AskSessionAgentCommandHandler(
            _mockSessionRepo.Object,
            _mockChatRepo.Object,
            _mockMediator.Object,
            _mockLlmService.Object,
            new Mock<IImagePreprocessor>().Object,
            new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object);
    }

    private static Session CreateSessionWithParticipant(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participant = new Participant { Id = participantId, SessionId = sessionId };
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(participant);

        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesBetweenUserAndAgentMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var callOrder = 0;
        _mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ReturnsAsync(2);

        // Track SaveChangesAsync calls to verify it's called between the two AddAsync calls
        var addCount = 0;
        var saveCount = 0;
        var saveCalledAfterFirstAdd = false;

        _mockChatRepo.Setup(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => addCount++);

        _mockChatRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                saveCount++;
                if (addCount == 1 && saveCount == 1)
                    saveCalledAfterFirstAdd = true;
            });

        var command = new AskSessionAgentCommand(sessionId, senderId, "What are the rules?", 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MessageId.Should().NotBe(Guid.Empty);
        result.Confidence.Should().BeNull(); // #3388: no fabricated confidence on the REST path
        result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded); // #3388: REST path never has citations
        result.AgentType.Should().Be("tutor");
        saveCalledAfterFirstAdd.Should().BeTrue("SaveChangesAsync should be called after the first AddAsync (user message) to prevent sequence number race condition");
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockChatRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AskSessionAgentCommand(sessionId, Guid.NewGuid(), "Question", 1);

        var act3 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act3.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SenderNotInSession_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var differentParticipant = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, differentParticipant);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AskSessionAgentCommand(sessionId, Guid.NewGuid(), "Question", 1);

        var act4 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act4.Should().ThrowAsync<NotFoundException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #3390 Slice 1: structured grounding observability (image / text-only path)
    // ──────────────────────────────────────────────────────────────────────────

    private const string AgentGroundingName = "meepleai.agent.response.grounding";

    [Fact]
    public async Task Handle_TextOnlyPath_EmitsGroundingMetricTextUngroundedNone()
    {
        // Arrange — the text-only branch (no images) of the multimodal handler: no retrieval.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);

        var captures = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = BuildCounterListener(AgentGroundingName, captures);

        var command = new AskSessionAgentCommand(sessionId, senderId, "What are the rules?", 1);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        captures.Should().ContainSingle("grounding must be recorded exactly once per response");
        var (value, tags) = captures[0];
        value.Should().Be(1L);
        tags["path"].Should().Be("text", "no images attached -> text branch");
        tags["grounding_status"].Should().Be("ungrounded", "this handler never retrieves, so it is always ungrounded (#3388)");
        tags["retrieval_profile"].Should().Be("none", "no retrieval on this path");
    }

    [Fact]
    public async Task Handle_ImagePath_EmitsGroundingMetricImageUngroundedNone()
    {
        // Arrange — the vision branch: images attached, multimodal LLM, still no retrieval.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        var mockMediator = new Mock<IMediator>();

        var mockImagePreprocessor = new Mock<IImagePreprocessor>();
        mockImagePreprocessor
            .Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<ImageProcessingOptions?>()))
            .ReturnsAsync(new ProcessedImage(new byte[] { 1, 2, 3 }, "image/jpeg", 10, 10, 3));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(s => s.GenerateMultimodalCompletionAsync(
                It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("I see a Catan board mid-game."));

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object,
            mockChatRepo.Object,
            mockMediator.Object,
            mockLlm.Object,
            mockImagePreprocessor.Object,
            new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object);

        var captures = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = BuildCounterListener(AgentGroundingName, captures);

        var command = new AskSessionAgentCommand(
            sessionId, senderId, "What should I do?", 1,
            new List<ChatImageAttachment> { new(new byte[] { 1, 2, 3 }, "image/jpeg", "board.jpg") });

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        captures.Should().ContainSingle();
        var (value, tags) = captures[0];
        value.Should().Be(1L);
        tags["path"].Should().Be("image", "images attached -> vision branch");
        tags["grounding_status"].Should().Be("ungrounded");
        tags["retrieval_profile"].Should().Be("none");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #3390 Slice 2: grounded retrieval on the image/text path (feature-gated, with fallback)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FlagOn_GroundedRetrieval_ReturnsGroundedWithLiveSessionProfile()
    {
        // Arrange — flag ON, non-empty text, KnowledgeBase returns a grounded answer.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        var mockMediator = new Mock<IMediator>();
        var groundedDto = new GroundedSessionAnswerDto(
            "You take turns placing settlements. [Page 2]",
            new List<Api.Models.CitationDto> { new("doc-1", 2, 0.9f, "settlements", "full") },
            GroundingStatus.Grounded,
            0.82);
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(groundedDto);
        var mockLlm = new Mock<ILlmService>();

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>()))
            .ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            new Mock<IImagePreprocessor>().Object, new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        var captures = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = BuildCounterListener(AgentGroundingName, captures);

        // Act — text turn (no images).
        var result = await handler.Handle(
            new AskSessionAgentCommand(sessionId, senderId, "How do I start my turn?", 1),
            TestContext.Current.CancellationToken);

        // Assert — grounded result, citations persisted, LLM never called directly (retrieval owned by KB).
        result.GroundingStatus.Should().Be(GroundingStatus.Grounded);
        result.CitationsJson.Should().NotBeNullOrEmpty("grounded answer carries serialized citations");
        mockLlm.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
        captures.Should().ContainSingle();
        captures[0].Tags["path"].Should().Be("text");
        captures[0].Tags["grounding_status"].Should().Be("grounded");
        captures[0].Tags["retrieval_profile"].Should().Be("live_session");
    }

    [Fact]
    public async Task Handle_FlagOn_RetrievalThrows_FallsBackToMultimodalUngrounded()
    {
        // Arrange — flag ON, but the grounded query fails (timeout/error). Must degrade fail-open.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GroundedSessionGenerationException("kb down"));
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Multimodal fallback answer"));

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>()))
            .ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            new Mock<IImagePreprocessor>().Object, new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        var captures = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = BuildCounterListener(AgentGroundingName, captures);

        // Act
        var result = await handler.Handle(
            new AskSessionAgentCommand(sessionId, senderId, "How do I start?", 1),
            TestContext.Current.CancellationToken);

        // Assert — degraded to multimodal-only: ungrounded, no citations, LLM WAS called (fallback).
        result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded);
        result.CitationsJson.Should().BeNull();
        mockLlm.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
        captures.Should().ContainSingle();
        captures[0].Tags["grounding_status"].Should().Be("ungrounded");
        captures[0].Tags["retrieval_profile"].Should().Be("none", "fallback path uses no retrieval profile");
    }

    [Fact]
    public async Task Handle_FlagOn_ImageTurn_GroundedEmpty_FallsBackToMultimodal()
    {
        // Arrange — image attached, flag ON, but grounded retrieval finds 0 citations. The image
        // must still be analyzed (multimodal fallback), not silently dropped for a text-only answer.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedSessionAnswerDto("Not in the rulebook.", new List<Api.Models.CitationDto>(), GroundingStatus.Ungrounded, null));
        var mockImagePreprocessor = new Mock<IImagePreprocessor>();
        mockImagePreprocessor
            .Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<ImageProcessingOptions?>()))
            .ReturnsAsync(new ProcessedImage(new byte[] { 1 }, "image/jpeg", 1, 1, 1));
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(l => l.GenerateMultimodalCompletionAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("I see a board with red tokens."));

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>()))
            .ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            mockImagePreprocessor.Object, new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        var captures = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = BuildCounterListener(AgentGroundingName, captures);

        var command = new AskSessionAgentCommand(
            sessionId, senderId, "What should I do?", 1,
            new List<ChatImageAttachment> { new(new byte[] { 1 }, "image/jpeg", "board.jpg") });

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — multimodal fallback ran (image analyzed), ungrounded, retrieval_profile=none.
        mockLlm.Verify(l => l.GenerateMultimodalCompletionAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
        result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded);
        captures.Should().ContainSingle();
        captures[0].Tags["path"].Should().Be("image");
        captures[0].Tags["retrieval_profile"].Should().Be("none");
    }

    [Fact]
    public async Task Handle_FlagOn_ClientCancels_PropagatesWithoutFallback()
    {
        // Arrange — the client aborts the request. Cancellation must propagate; the handler must
        // NOT fail-open into a wasted second (multimodal) LLM call.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var mockLlm = new Mock<ILlmService>();

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>()))
            .ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            new Mock<IImagePreprocessor>().Object, new Mock<IGameStateExtractor>().Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        // Act — client-cancelled token.
        var act = () => handler.Handle(new AskSessionAgentCommand(sessionId, senderId, "Question", 1), cts.Token);

        // Assert — cancellation propagates; no multimodal/text fallback LLM call.
        await act.Should().ThrowAsync<OperationCanceledException>();
        mockLlm.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
        mockLlm.Verify(l => l.GenerateMultimodalCompletionAsync(It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #3390 Slice 3: vision-derived retrieval query for empty-text turns
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_VisionExpansionOn_EmptyText_DerivesQueryFromVisionState()
    {
        // Arrange — empty turn text, vision-expansion + image-retrieval flags ON, a vision snapshot exists.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        SessionChatMessage? capturedUserMessage = null;
        mockChatRepo.Setup(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SessionChatMessage, CancellationToken>((m, _) => capturedUserMessage ??= m)
            .Returns(Task.CompletedTask);

        var mockExtractor = new Mock<IGameStateExtractor>();
        mockExtractor
            .Setup(e => e.ExtractIfNeededAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"observations\":\"Red player has 3 settlements\",\"game_phase\":\"main\",\"visible_elements\":{\"resources\":[\"wood\",\"brick\"]}}");

        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedSessionAnswerDto("You can build. [Page 3]", new List<Api.Models.CitationDto> { new("d", 3, 0.9f, "s", "full") }, GroundingStatus.Grounded, 0.8));
        var mockLlm = new Mock<ILlmService>();

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveVisionQueryExpansionKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>())).ReturnsAsync(true);
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>())).ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            new Mock<IImagePreprocessor>().Object, mockExtractor.Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        // Act — empty turn text.
        await handler.Handle(new AskSessionAgentCommand(sessionId, senderId, "", 1), TestContext.Current.CancellationToken);

        // Assert — retrieval query derived from vision terms, and the user message shows the same query.
        mockMediator.Verify(m => m.Send(
            It.Is<AskGroundedSessionQuery>(q => q.Question.Contains("settlements") && q.Question.Contains("board state")),
            It.IsAny<CancellationToken>()), Times.Once);
        capturedUserMessage.Should().NotBeNull();
        capturedUserMessage!.Content.Should().Contain("settlements", "the user message shows the vision-derived query (transparency)");
    }

    [Fact]
    public async Task Handle_VisionExpansionOn_EmptyText_NoVision_UsesFallbackQuery()
    {
        // Arrange — empty turn text, flag ON, but no vision snapshot → deterministic fallback query.
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, senderId);

        var mockSessionRepo = new Mock<ISessionRepository>();
        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var mockChatRepo = new Mock<ISessionChatRepository>();
        mockChatRepo.SetupSequence(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1).ReturnsAsync(2);
        var mockExtractor = new Mock<IGameStateExtractor>(); // ExtractIfNeededAsync → null (no snapshot)

        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedSessionAnswerDto("No match.", new List<Api.Models.CitationDto>(), GroundingStatus.Ungrounded, null));
        var mockLlm = new Mock<ILlmService>();

        var mockFlags = new Mock<IFeatureFlagService>();
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveVisionQueryExpansionKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>())).ReturnsAsync(true);
        mockFlags.Setup(f => f.IsEnabledAsync(FeatureFlagConstants.LiveImageRetrievalKey, It.IsAny<Api.Infrastructure.Entities.UserRole?>())).ReturnsAsync(true);

        var handler = new AskSessionAgentCommandHandler(
            mockSessionRepo.Object, mockChatRepo.Object, mockMediator.Object, mockLlm.Object,
            new Mock<IImagePreprocessor>().Object, mockExtractor.Object,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object,
            mockFlags.Object);

        // Act — whitespace text.
        await handler.Handle(new AskSessionAgentCommand(sessionId, senderId, "   ", 1), TestContext.Current.CancellationToken);

        // Assert — deterministic fallback retrieval query.
        mockMediator.Verify(m => m.Send(
            It.Is<AskGroundedSessionQuery>(q => q.Question == "What should I do now based on the current board state?"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MeterListener BuildCounterListener(
        string metricName,
        List<(long Value, Dictionary<string, object?> Tags)> captures)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == metricName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in tags)
            {
                dict[kv.Key] = kv.Value;
            }
            captures.Add((value, dict));
        });
        listener.Start();
        return listener;
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DeleteChatMessageCommandHandlerTests
{
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly DeleteChatMessageCommandHandler _handler;

    public DeleteChatMessageCommandHandlerTests()
    {
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
        _handler = new DeleteChatMessageCommandHandler(_mockChatRepo.Object, _mockSessionRepo.Object);
    }

    private static Session CreateSessionWithParticipant(Guid sessionId, Guid participantId, Guid? ownerUserId)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participant = new Participant { Id = participantId, SessionId = sessionId, UserId = ownerUserId };
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(participant);

        return session;
    }

    [Fact]
    public async Task Handle_SenderDeletes_SoftDeletesTextMessage()
    {
        // Arrange — the authenticated user owns the participant that sent the message.
        var sessionId = Guid.NewGuid();
        var senderParticipantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var message = SessionChatMessage.CreateTextMessage(sessionId, senderParticipantId, "Hello", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionWithParticipant(sessionId, senderParticipantId, ownerUserId));

        var command = new DeleteChatMessageCommand(message.Id, ownerUserId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        message.IsDeleted.Should().BeTrue();
        _mockChatRepo.Verify(r => r.UpdateAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MessageNotFound_ThrowsNotFoundException()
    {
        var messageId = Guid.NewGuid();
        _mockChatRepo.Setup(r => r.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionChatMessage?)null);

        var command = new DeleteChatMessageCommand(messageId, Guid.NewGuid());

        var act5 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act5.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DifferentUser_ThrowsForbiddenException()
    {
        // Arrange — #2655 IDOR fix: a user who does NOT own the sending participant
        // cannot delete the message, even by forging a requester id (now resolved
        // server-side from the authenticated caller).
        var sessionId = Guid.NewGuid();
        var senderParticipantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var message = SessionChatMessage.CreateTextMessage(sessionId, senderParticipantId, "Hello", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSessionWithParticipant(sessionId, senderParticipantId, ownerUserId));

        var command = new DeleteChatMessageCommand(message.Id, attackerUserId);

        var act6 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act6.Should().ThrowAsync<ForbiddenException>();

        _mockChatRepo.Verify(r => r.UpdateAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SystemEventMessage_ThrowsConflictException()
    {
        // Non-text messages are rejected by the type check, which runs before the
        // ownership resolution — so no session lookup is needed here. Deleting a
        // non-text message is a conflicting request on the resource state, so it must
        // surface as 409 (ConflictException), not a 500 (InvalidOperationException). (#3173)
        var message = SessionChatMessage.CreateSystemEvent(Guid.NewGuid(), "event", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var command = new DeleteChatMessageCommand(message.Id, Guid.NewGuid());

        var act7 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act7.Should().ThrowAsync<ConflictException>()
            .WithMessage("*text messages*");
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class MediaQueryHandlerTests
{
    private readonly Mock<ISessionMediaRepository> _mockMediaRepo;
    private readonly Mock<ISessionRepository> _mockSessionRepo;

    public MediaQueryHandlerTests()
    {
        _mockMediaRepo = new Mock<ISessionMediaRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
    }

    [Fact]
    public async Task GetSessionMedia_WhenOwner_ReturnsMediaDtos()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);
        var sessionId = session.Id;
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var media1 = SessionMedia.Create(
            sessionId, Guid.NewGuid(), "f1", "a.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        var media2 = SessionMedia.Create(
            sessionId, Guid.NewGuid(), "f2", "b.png", "image/png", 200, SessionMediaType.Screenshot);

        _mockMediaRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionMedia> { media1, media2 });

        var handler = new GetSessionMediaQueryHandler(_mockMediaRepo.Object, _mockSessionRepo.Object);
        var query = new GetSessionMediaQuery(sessionId, ownerId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(2);
        result[0].FileName.Should().Be("a.jpg");
        result[1].FileName.Should().Be("b.png");
    }

    [Fact]
    public async Task GetSessionMedia_WhenNotOwnerOrParticipant_ThrowsForbidden()
    {
        // #3119 IDOR guard: a non-owner/non-participant must not read another user's session media.
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionMediaQueryHandler(_mockMediaRepo.Object, _mockSessionRepo.Object);
        var query = new GetSessionMediaQuery(session.Id, Guid.NewGuid());

        var act = () => handler.Handle(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        _mockMediaRepo.Verify(
            r => r.GetBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMediaBySnapshot_ReturnsFilteredMedia()
    {
        // Arrange
        var snapshotId = Guid.NewGuid();
        var media = SessionMedia.Create(
            Guid.NewGuid(), Guid.NewGuid(), "f", "snap.jpg", "image/jpeg", 100, SessionMediaType.Photo,
            snapshotId: snapshotId);

        _mockMediaRepo.Setup(r => r.GetBySnapshotIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionMedia> { media });

        var handler = new GetMediaBySnapshotQueryHandler(_mockMediaRepo.Object);
        var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetMediaBySnapshotQuery(snapshotId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].SnapshotId.Should().Be(snapshotId);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class ChatQueryHandlerTests
{
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly Mock<ISessionRepository> _mockSessionRepo;

    public ChatQueryHandlerTests()
    {
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
    }

    [Fact]
    public async Task GetSessionChat_WhenOwner_ReturnsMessagesAndTotalCount()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);
        var sessionId = session.Id;
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var msg1 = SessionChatMessage.CreateTextMessage(sessionId, Guid.NewGuid(), "Hi", 1);
        var msg2 = SessionChatMessage.CreateSystemEvent(sessionId, "Game started", 2);

        _mockChatRepo.Setup(r => r.GetBySessionIdAsync(sessionId, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionChatMessage> { msg1, msg2 });
        _mockChatRepo.Setup(r => r.GetCountBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = new GetSessionChatQueryHandler(_mockChatRepo.Object, _mockSessionRepo.Object);
        var query = new GetSessionChatQuery(sessionId, 50, 0, ownerId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Messages.Count.Should().Be(2);
        result.TotalCount.Should().Be(2);
        result.Messages[0].MessageType.Should().Be("Text");
        result.Messages[1].MessageType.Should().Be("SystemEvent");
    }

    [Fact]
    public async Task GetSessionChat_WhenNotOwnerOrParticipant_ThrowsForbidden()
    {
        // #3119 IDOR guard: a non-owner/non-participant must not read another user's session chat.
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionChatQueryHandler(_mockChatRepo.Object, _mockSessionRepo.Object);
        var query = new GetSessionChatQuery(session.Id, 50, 0, Guid.NewGuid());

        var act = () => handler.Handle(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        _mockChatRepo.Verify(
            r => r.GetBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

/// <summary>
/// #3388: guards the wire casing of <see cref="AskSessionAgentResult.GroundingStatus"/>.
/// The FE image-path disclaimer keys off the exact serialized value
/// <c>"groundingStatus":"Ungrounded"</c> (camelCase property name, PascalCase enum
/// value) produced by the global JSON options configured in
/// <c>Program.cs</c> (<c>ConfigureHttpJsonOptions</c>: camelCase naming policy +
/// <see cref="JsonStringEnumConverter"/>). This test mirrors those two settings so a
/// regression to numeric enum serialization or snake/Pascal-case property naming
/// fails fast here instead of silently breaking the FE contract.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AskSessionAgentResultSerializationTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Serialize_UngroundedResult_ProducesCamelCasePropertyWithPascalCaseEnumValue()
    {
        // Arrange
        var result = new AskSessionAgentResult(
            MessageId: Guid.NewGuid(),
            Answer: "The rulebook does not cover this.",
            AgentType: "qa",
            Confidence: null,
            CitationsJson: null,
            GroundingStatus: GroundingStatus.Ungrounded);

        // Act
        var json = JsonSerializer.Serialize(result, WireOptions);

        // Assert
        json.Should().Contain("\"groundingStatus\":\"Ungrounded\"");
    }
}

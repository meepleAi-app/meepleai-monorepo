using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Confidence.Should().Be(0.85f); // LLM success returns 0.85 confidence
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
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DeleteChatMessageCommandHandlerTests
{
    private readonly Mock<ISessionChatRepository> _mockChatRepo;
    private readonly DeleteChatMessageCommandHandler _handler;

    public DeleteChatMessageCommandHandlerTests()
    {
        _mockChatRepo = new Mock<ISessionChatRepository>();
        _handler = new DeleteChatMessageCommandHandler(_mockChatRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesTextMessage()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var message = SessionChatMessage.CreateTextMessage(Guid.NewGuid(), senderId, "Hello", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var command = new DeleteChatMessageCommand(message.Id, senderId);

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
    public async Task Handle_DifferentSender_ThrowsForbiddenException()
    {
        var senderId = Guid.NewGuid();
        var message = SessionChatMessage.CreateTextMessage(Guid.NewGuid(), senderId, "Hello", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var command = new DeleteChatMessageCommand(message.Id, Guid.NewGuid());

        var act6 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act6.Should().ThrowAsync<ForbiddenException>();

        _mockChatRepo.Verify(r => r.UpdateAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SystemEventMessage_ThrowsInvalidOperationException()
    {
        var message = SessionChatMessage.CreateSystemEvent(Guid.NewGuid(), "event", 1);

        _mockChatRepo.Setup(r => r.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        // System messages have no sender, so requesterId won't match but the type check comes first
        var command = new DeleteChatMessageCommand(message.Id, Guid.NewGuid());

        // SenderId is null for system events, so ForbiddenException won't fire (null != requesterId is false with HasValue check)
        // Instead, it reaches the MessageType check
        var act7 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act7.Should().ThrowAsync<InvalidOperationException>();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class MediaQueryHandlerTests
{
    private readonly Mock<ISessionMediaRepository> _mockMediaRepo;

    public MediaQueryHandlerTests()
    {
        _mockMediaRepo = new Mock<ISessionMediaRepository>();
    }

    [Fact]
    public async Task GetSessionMedia_ReturnsMediaDtos()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var media1 = SessionMedia.Create(
            sessionId, Guid.NewGuid(), "f1", "a.jpg", "image/jpeg", 100, SessionMediaType.Photo);
        var media2 = SessionMedia.Create(
            sessionId, Guid.NewGuid(), "f2", "b.png", "image/png", 200, SessionMediaType.Screenshot);

        _mockMediaRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionMedia> { media1, media2 });

        var handler = new GetSessionMediaQueryHandler(_mockMediaRepo.Object);
        var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetSessionMediaQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(2);
        result[0].FileName.Should().Be("a.jpg");
        result[1].FileName.Should().Be("b.png");
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

    public ChatQueryHandlerTests()
    {
        _mockChatRepo = new Mock<ISessionChatRepository>();
    }

    [Fact]
    public async Task GetSessionChat_ReturnsMessagesAndTotalCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var msg1 = SessionChatMessage.CreateTextMessage(sessionId, Guid.NewGuid(), "Hi", 1);
        var msg2 = SessionChatMessage.CreateSystemEvent(sessionId, "Game started", 2);

        _mockChatRepo.Setup(r => r.GetBySessionIdAsync(sessionId, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionChatMessage> { msg1, msg2 });
        _mockChatRepo.Setup(r => r.GetCountBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = new GetSessionChatQueryHandler(_mockChatRepo.Object);
        var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetSessionChatQuery(sessionId, 50, 0);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Messages.Count.Should().Be(2);
        result.TotalCount.Should().Be(2);
        result.Messages[0].MessageType.Should().Be("Text");
        result.Messages[1].MessageType.Should().Be("SystemEvent");
    }
}

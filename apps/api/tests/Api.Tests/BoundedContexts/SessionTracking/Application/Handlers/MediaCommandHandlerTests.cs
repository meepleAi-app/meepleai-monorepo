using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Unit tests for media command handlers.
/// Issue #4760
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UploadSessionMediaCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<ISessionMediaRepository> _mockMediaRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly UploadSessionMediaCommandHandler _handler;

    public UploadSessionMediaCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockMediaRepo = new Mock<ISessionMediaRepository>();
        _mockMediator = new Mock<IMediator>();
        _handler = new UploadSessionMediaCommandHandler(
            _mockSessionRepo.Object,
            _mockMediaRepo.Object,
            _mockMediator.Object);
    }

    private static Session CreateSessionWithParticipant(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        // Use reflection to set the session ID and add participant
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participant = new Participant { Id = participantId, SessionId = sessionId };
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(participant);

        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_UploadsMediaAndReturnsResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, participantId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UploadSessionMediaCommand(
            sessionId, participantId, "file-123", "photo.jpg", "image/jpeg", 2048, "Photo", "Caption", null, 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MediaId.Should().NotBe(Guid.Empty);
        _mockMediaRepo.Verify(r => r.AddAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMediaRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new UploadSessionMediaCommand(
            sessionId, Guid.NewGuid(), "f", "f.jpg", "image/jpeg", 100, "Photo", null, null, null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _mockMediaRepo.Verify(r => r.AddAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ParticipantNotInSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var differentParticipant = Guid.NewGuid();
        var session = CreateSessionWithParticipant(sessionId, differentParticipant);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UploadSessionMediaCommand(
            sessionId, participantId, "f", "f.jpg", "image/jpeg", 100, "Photo", null, null, null);

        // Act & Assert
        var act2 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<NotFoundException>();

        _mockMediaRepo.Verify(r => r.AddAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdateMediaCaptionCommandHandlerTests
{
    private readonly Mock<ISessionMediaRepository> _mockMediaRepo;
    private readonly UpdateMediaCaptionCommandHandler _handler;

    public UpdateMediaCaptionCommandHandlerTests()
    {
        _mockMediaRepo = new Mock<ISessionMediaRepository>();
        _handler = new UpdateMediaCaptionCommandHandler(_mockMediaRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesCaptionAndSaves()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var media = SessionMedia.Create(
            Guid.NewGuid(), participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo, "old");

        _mockMediaRepo.Setup(r => r.GetByIdAsync(media.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        var command = new UpdateMediaCaptionCommand(media.Id, participantId, "new caption");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        media.Caption.Should().Be("new caption");
        _mockMediaRepo.Verify(r => r.UpdateAsync(media, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediaRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MediaNotFound_ThrowsNotFoundException()
    {
        var mediaId = Guid.NewGuid();
        _mockMediaRepo.Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionMedia?)null);

        var command = new UpdateMediaCaptionCommand(mediaId, Guid.NewGuid(), "cap");

        var act3 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act3.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DifferentParticipant_ThrowsForbiddenException()
    {
        var participantId = Guid.NewGuid();
        var media = SessionMedia.Create(
            Guid.NewGuid(), participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);

        _mockMediaRepo.Setup(r => r.GetByIdAsync(media.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        var command = new UpdateMediaCaptionCommand(media.Id, Guid.NewGuid(), "hacked caption");

        var act4 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act4.Should().ThrowAsync<ForbiddenException>();

        _mockMediaRepo.Verify(r => r.UpdateAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DeleteSessionMediaCommandHandlerTests
{
    private readonly Mock<ISessionMediaRepository> _mockMediaRepo;
    private readonly Mock<IMediator> _mockMediator;
    private readonly DeleteSessionMediaCommandHandler _handler;

    public DeleteSessionMediaCommandHandlerTests()
    {
        _mockMediaRepo = new Mock<ISessionMediaRepository>();
        _mockMediator = new Mock<IMediator>();
        _handler = new DeleteSessionMediaCommandHandler(_mockMediaRepo.Object, _mockMediator.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesAndPublishesEvent()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var media = SessionMedia.Create(
            Guid.NewGuid(), participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);

        _mockMediaRepo.Setup(r => r.GetByIdAsync(media.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        var command = new DeleteSessionMediaCommand(media.Id, participantId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        media.IsDeleted.Should().BeTrue();
        _mockMediaRepo.Verify(r => r.UpdateAsync(media, It.IsAny<CancellationToken>()), Times.Once);
        _mockMediaRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MediaNotFound_ThrowsNotFoundException()
    {
        var mediaId = Guid.NewGuid();
        _mockMediaRepo.Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionMedia?)null);

        var command = new DeleteSessionMediaCommand(mediaId, Guid.NewGuid());

        var act5 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act5.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DifferentParticipant_ThrowsForbiddenException()
    {
        var participantId = Guid.NewGuid();
        var media = SessionMedia.Create(
            Guid.NewGuid(), participantId, "f", "f.jpg", "image/jpeg", 100, SessionMediaType.Photo);

        _mockMediaRepo.Setup(r => r.GetByIdAsync(media.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        var command = new DeleteSessionMediaCommand(media.Id, Guid.NewGuid());

        var act6 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act6.Should().ThrowAsync<ForbiddenException>();

        _mockMediaRepo.Verify(r => r.UpdateAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

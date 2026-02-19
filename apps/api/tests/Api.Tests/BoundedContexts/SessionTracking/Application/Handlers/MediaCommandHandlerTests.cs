using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Handlers;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, result.MediaId);
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

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
        Assert.Equal("new caption", media.Caption);
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

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
        Assert.True(media.IsDeleted);
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockMediaRepo.Verify(r => r.UpdateAsync(It.IsAny<SessionMedia>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

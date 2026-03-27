using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class UploadSessionAttachmentCommandHandlerTests : IDisposable
{
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ISessionAttachmentRepository> _attachmentRepoMock;
    private readonly Mock<ISessionAttachmentService> _attachmentServiceMock;
    private readonly Mock<ILogger<UploadSessionAttachmentCommandHandler>> _loggerMock;
    private readonly UploadSessionAttachmentCommandHandler _sut;
    private readonly MemoryStream _dummyStream;

    public UploadSessionAttachmentCommandHandlerTests()
    {
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _attachmentRepoMock = new Mock<ISessionAttachmentRepository>();
        _attachmentServiceMock = new Mock<ISessionAttachmentService>();
        _loggerMock = new Mock<ILogger<UploadSessionAttachmentCommandHandler>>();

        _sut = new UploadSessionAttachmentCommandHandler(
            _sessionRepoMock.Object,
            _attachmentRepoMock.Object,
            _attachmentServiceMock.Object,
            _loggerMock.Object);

        _dummyStream = new MemoryStream(new byte[2048]);
    }

    public void Dispose()
    {
        _dummyStream.Dispose();
    }

    #region Happy Path

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsAttachmentDto()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(0);
        SetupUploadSuccess();

        var command = CreateCommand(sessionId, playerId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.SessionId.Should().Be(sessionId);
        result.PlayerId.Should().Be(playerId);
        result.AttachmentType.Should().Be(AttachmentType.BoardState);
        result.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Handle_WithValidCommand_PersistsAttachment()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(0);
        SetupUploadSuccess();

        var command = CreateCommand(sessionId, playerId);
        await _sut.Handle(command, CancellationToken.None);

        _attachmentRepoMock.Verify(
            x => x.AddAsync(It.IsAny<SessionAttachment>(), It.IsAny<CancellationToken>()), Times.Once);
        _attachmentRepoMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Setup)]
    [InlineData(LiveSessionStatus.InProgress)]
    [InlineData(LiveSessionStatus.Paused)]
    public async Task Handle_WithAllowedSessionStatus_Succeeds(LiveSessionStatus status)
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInStatus(sessionId, status);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(0);
        SetupUploadSuccess();

        var command = CreateCommand(sessionId, playerId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
    }

    #endregion

    #region Session Validation

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFoundException()
    {
        _sessionRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var command = CreateCommand(Guid.NewGuid(), Guid.NewGuid());

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSessionCompleted_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateCompletedSession(sessionId);

        SetupSessionRepo(sessionId, session);

        var command = CreateCommand(sessionId, session.Players[0].Id);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenSessionCreated_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = LiveGameSession.Create(sessionId, Guid.NewGuid(), "Test", TimeProvider.System);
        // Status is Created (no players added yet)

        SetupSessionRepo(sessionId, session);

        var command = CreateCommand(sessionId, Guid.NewGuid());

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    #endregion

    #region Player Validation

    [Fact]
    public async Task Handle_WhenPlayerNotInSession_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var unknownPlayerId = Guid.NewGuid();

        SetupSessionRepo(sessionId, session);

        var command = CreateCommand(sessionId, unknownPlayerId);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Attachment Count Limit

    [Fact]
    public async Task Handle_WhenAtMaxAttachments_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(5); // Max is 5

        var command = CreateCommand(sessionId, playerId);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenBelowMaxAttachments_Succeeds()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(4); // Below max of 5
        SetupUploadSuccess();

        var command = CreateCommand(sessionId, playerId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
    }

    #endregion

    #region Upload Failure

    [Fact]
    public async Task Handle_WhenUploadFails_ThrowsInvalidOperationException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;

        SetupSessionRepo(sessionId, session);
        SetupAttachmentCount(0);
        _attachmentServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<AttachmentType>(), It.IsAny<string?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionAttachmentUploadResult(false, null, null, 0, "S3 down"));

        var command = CreateCommand(sessionId, playerId);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("S3 down");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSessionRepo_ThrowsArgumentNullException()
    {
        var act = () =>
            new UploadSessionAttachmentCommandHandler(
                null!, _attachmentRepoMock.Object, _attachmentServiceMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAttachmentRepo_ThrowsArgumentNullException()
    {
        var act = () =>
            new UploadSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, null!, _attachmentServiceMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAttachmentService_ThrowsArgumentNullException()
    {
        var act = () =>
            new UploadSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, _attachmentRepoMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () =>
            new UploadSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, _attachmentRepoMock.Object, _attachmentServiceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helpers

    private UploadSessionAttachmentCommand CreateCommand(Guid sessionId, Guid playerId)
    {
        return new UploadSessionAttachmentCommand(
            sessionId, playerId, _dummyStream,
            "photo.jpg", "image/jpeg", 2048,
            AttachmentType.BoardState, null, null);
    }

    private void SetupSessionRepo(Guid sessionId, LiveGameSession? session)
    {
        _sessionRepoMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupAttachmentCount(int count)
    {
        _attachmentRepoMock
            .Setup(x => x.CountByPlayerAndSnapshotAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
    }

    private void SetupUploadSuccess()
    {
        _attachmentServiceMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<AttachmentType>(), It.IsAny<string?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionAttachmentUploadResult(
                true, "path/to/photo.jpg", "path/to/thumb.jpg", 2048));
    }

    private static LiveGameSession CreateSessionInProgress(Guid? sessionId = null)
    {
        var session = LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(), Guid.NewGuid(), "Test Game", TimeProvider.System);
        session.AddPlayer(null, "Player One", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        return session;
    }

    private static LiveGameSession CreateSessionInStatus(Guid sessionId, LiveSessionStatus status)
    {
        var session = LiveGameSession.Create(sessionId, Guid.NewGuid(), "Test Game", TimeProvider.System);
        session.AddPlayer(null, "Player One", PlayerColor.Red, TimeProvider.System);

        if (status is LiveSessionStatus.Setup or LiveSessionStatus.InProgress or LiveSessionStatus.Paused)
        {
            session.MoveToSetup(TimeProvider.System);
        }

        if (status is LiveSessionStatus.InProgress or LiveSessionStatus.Paused)
        {
            session.Start(TimeProvider.System);
        }

        if (status == LiveSessionStatus.Paused)
        {
            session.Pause(TimeProvider.System);
        }

        return session;
    }

    private static LiveGameSession CreateCompletedSession(Guid sessionId)
    {
        var session = CreateSessionInProgress(sessionId);
        session.Complete(TimeProvider.System);
        return session;
    }

    #endregion
}

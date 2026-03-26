using Api.BoundedContexts.GameManagement.Application.Commands;
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
public sealed class DeleteSessionAttachmentCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionAttachmentRepository> _attachmentRepoMock = new();
    private readonly Mock<ISessionAttachmentService> _attachmentServiceMock = new();
    private readonly Mock<ILogger<DeleteSessionAttachmentCommandHandler>> _loggerMock = new();
    private readonly DeleteSessionAttachmentCommandHandler _sut;

    public DeleteSessionAttachmentCommandHandlerTests()
    {
        _sut = new DeleteSessionAttachmentCommandHandler(
            _sessionRepoMock.Object,
            _attachmentRepoMock.Object,
            _attachmentServiceMock.Object,
            _loggerMock.Object);
    }

    #region Happy Path

    [Fact]
    public async Task Handle_OwnerDeletes_SoftDeletesAndCleansUpBlobs()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var playerId = session.Players[0].Id;
        var attachment = CreateAttachment(sessionId, playerId);

        SetupMocks(sessionId, session, attachment);

        var command = new DeleteSessionAttachmentCommand(sessionId, attachment.Id, playerId);
        await _sut.Handle(command, CancellationToken.None);

        _attachmentRepoMock.Verify(
            x => x.SoftDeleteAsync(attachment.Id, It.IsAny<CancellationToken>()), Times.Once);
        _attachmentRepoMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HostDeletes_SoftDeletesSuccessfully()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var hostId = session.Players[0].Id; // First player is host
        var otherPlayerId = Guid.NewGuid();
        var attachment = CreateAttachment(sessionId, otherPlayerId);

        SetupMocks(sessionId, session, attachment);

        // Host deletes another player's attachment
        var command = new DeleteSessionAttachmentCommand(sessionId, attachment.Id, hostId);
        await _sut.Handle(command, CancellationToken.None);

        _attachmentRepoMock.Verify(
            x => x.SoftDeleteAsync(attachment.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Session Not Found

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        _sessionRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var command = new DeleteSessionAttachmentCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Attachment Not Found

    [Fact]
    public async Task Handle_AttachmentNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);

        _sessionRepoMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _attachmentRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionAttachment?)null);

        var command = new DeleteSessionAttachmentCommand(sessionId, Guid.NewGuid(), session.Players[0].Id);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AttachmentBelongsToDifferentSession_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var attachment = CreateAttachment(Guid.NewGuid(), session.Players[0].Id); // Different session

        _sessionRepoMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _attachmentRepoMock.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        var command = new DeleteSessionAttachmentCommand(sessionId, attachment.Id, session.Players[0].Id);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Authorization

    [Fact]
    public async Task Handle_NonOwnerNonHost_ThrowsForbiddenException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSession(sessionId);
        var ownerId = Guid.NewGuid();
        var attachment = CreateAttachment(sessionId, ownerId);

        // Add a non-host player
        var nonHostPlayer = session.AddPlayer(null, "NonHost", PlayerColor.Blue, TimeProvider.System);

        SetupMocks(sessionId, session, attachment);

        var command = new DeleteSessionAttachmentCommand(sessionId, attachment.Id, nonHostPlayer.Id);

        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region Constructor Null Checks

    [Fact]
    public void Constructor_NullSessionRepo_Throws()
    {
        var act = () =>
            new DeleteSessionAttachmentCommandHandler(
                null!, _attachmentRepoMock.Object, _attachmentServiceMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAttachmentRepo_Throws()
    {
        var act = () =>
            new DeleteSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, null!, _attachmentServiceMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAttachmentService_Throws()
    {
        var act = () =>
            new DeleteSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, _attachmentRepoMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () =>
            new DeleteSessionAttachmentCommandHandler(
                _sessionRepoMock.Object, _attachmentRepoMock.Object, _attachmentServiceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Null Command

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act =
            () => _sut.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Helpers

    private static LiveGameSession CreateSession(Guid sessionId)
    {
        var session = LiveGameSession.Create(sessionId, Guid.NewGuid(), "Test Game", TimeProvider.System);
        session.AddPlayer(null, "Host Player", PlayerColor.Red, TimeProvider.System);
        return session;
    }

    private static SessionAttachment CreateAttachment(Guid sessionId, Guid playerId)
    {
        return SessionAttachment.Create(
            sessionId,
            playerId,
            AttachmentType.BoardState,
            "https://storage.example.com/photo.jpg",
            "image/jpeg",
            5000,
            "https://storage.example.com/thumb.jpg",
            "Test caption");
    }

    private void SetupMocks(Guid sessionId, LiveGameSession session, SessionAttachment attachment)
    {
        _sessionRepoMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _attachmentRepoMock.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _attachmentRepoMock.Setup(x => x.SoftDeleteAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #endregion
}

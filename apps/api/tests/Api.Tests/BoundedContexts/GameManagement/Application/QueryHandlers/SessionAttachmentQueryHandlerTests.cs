using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.QueryHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.QueryHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionAttachmentQueryHandlerTests
{
    private readonly Mock<ISessionAttachmentRepository> _repoMock = new();
    private readonly Mock<ISessionAttachmentService> _serviceMock = new();
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock = new();

    #region GetSessionAttachmentsQuery

    [Fact]
    public async Task GetSessionAttachments_ReturnsAllForSession()
    {
        var sessionId = Guid.NewGuid();
        var attachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId),
            CreateAttachment(sessionId),
        };
        _repoMock.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        var handler = new GetSessionAttachmentsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentsQuery(sessionId), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSessionAttachments_FiltersByPlayerId()
    {
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var attachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId, playerId: playerId),
            CreateAttachment(sessionId, playerId: Guid.NewGuid()),
        };
        _repoMock.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        var handler = new GetSessionAttachmentsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentsQuery(sessionId, PlayerId: playerId), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(playerId, result[0].PlayerId);
    }

    [Fact]
    public async Task GetSessionAttachments_FiltersByType()
    {
        var sessionId = Guid.NewGuid();
        var attachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId, type: AttachmentType.BoardState),
            CreateAttachment(sessionId, type: AttachmentType.CharacterSheet),
        };
        _repoMock.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        var handler = new GetSessionAttachmentsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentsQuery(sessionId, Type: AttachmentType.BoardState), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(AttachmentType.BoardState, result[0].AttachmentType);
    }

    [Fact]
    public async Task GetSessionAttachments_FiltersBySnapshot()
    {
        var sessionId = Guid.NewGuid();
        var snapshotAttachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId, snapshotIndex: 1),
        };
        _repoMock.Setup(x => x.GetBySnapshotAsync(sessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshotAttachments);

        var handler = new GetSessionAttachmentsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentsQuery(sessionId, SnapshotIndex: 1), CancellationToken.None);

        Assert.Single(result);
        _repoMock.Verify(x => x.GetBySnapshotAsync(sessionId, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSessionAttachments_WhenEmpty_ReturnsEmptyList()
    {
        var sessionId = Guid.NewGuid();
        _repoMock.Setup(x => x.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());

        var handler = new GetSessionAttachmentsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentsQuery(sessionId), CancellationToken.None);

        Assert.Empty(result);
    }

    #endregion

    #region GetSessionAttachmentByIdQuery

    [Fact]
    public async Task GetAttachmentById_ReturnsDetailWithDownloadUrl()
    {
        var sessionId = Guid.NewGuid();
        // Create session and get the auto-generated player ID
        var session = LiveGameSession.Create(sessionId, Guid.NewGuid(), "Test Game", TimeProvider.System);
        var player = session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        var playerId = player.Id;

        var attachment = CreateAttachment(sessionId, playerId: playerId);

        _repoMock.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _serviceMock.Setup(x => x.GetDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s3.example.com/download");
        _sessionRepoMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionAttachmentByIdQueryHandler(
            _repoMock.Object, _serviceMock.Object, _sessionRepoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentByIdQuery(sessionId, attachment.Id), CancellationToken.None);

        Assert.Equal("https://s3.example.com/download", result.DownloadUrl);
        Assert.Equal("Alice", result.PlayerDisplayName);
        Assert.Equal(attachment.Id, result.Id);
    }

    [Fact]
    public async Task GetAttachmentById_WhenNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionAttachment?)null);

        var handler = new GetSessionAttachmentByIdQueryHandler(
            _repoMock.Object, _serviceMock.Object, _sessionRepoMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new GetSessionAttachmentByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetAttachmentById_WhenSessionMismatch_ThrowsNotFoundException()
    {
        var attachment = CreateAttachment(Guid.NewGuid()); // Different session ID

        _repoMock.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        var handler = new GetSessionAttachmentByIdQueryHandler(
            _repoMock.Object, _serviceMock.Object, _sessionRepoMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new GetSessionAttachmentByIdQuery(Guid.NewGuid(), attachment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetAttachmentById_WhenSessionNotFound_ReturnsUnknownPlayer()
    {
        var sessionId = Guid.NewGuid();
        var attachment = CreateAttachment(sessionId);

        _repoMock.Setup(x => x.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _serviceMock.Setup(x => x.GetDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("url");
        _sessionRepoMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new GetSessionAttachmentByIdQueryHandler(
            _repoMock.Object, _serviceMock.Object, _sessionRepoMock.Object);
        var result = await handler.Handle(
            new GetSessionAttachmentByIdQuery(sessionId, attachment.Id), CancellationToken.None);

        Assert.Equal("Unknown Player", result.PlayerDisplayName);
    }

    #endregion

    #region GetSnapshotPhotosQuery

    [Fact]
    public async Task GetSnapshotPhotos_ReturnsPhotosForSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var attachments = new List<SessionAttachment>
        {
            CreateAttachment(sessionId, snapshotIndex: 2),
            CreateAttachment(sessionId, snapshotIndex: 2),
        };
        _repoMock.Setup(x => x.GetBySnapshotAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachments);

        var handler = new GetSnapshotPhotosQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSnapshotPhotosQuery(sessionId, 2), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSnapshotPhotos_WhenNoPhotos_ReturnsEmpty()
    {
        var sessionId = Guid.NewGuid();
        _repoMock.Setup(x => x.GetBySnapshotAsync(sessionId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());

        var handler = new GetSnapshotPhotosQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetSnapshotPhotosQuery(sessionId, 5), CancellationToken.None);

        Assert.Empty(result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void GetSessionAttachmentsHandler_NullRepo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSessionAttachmentsQueryHandler(null!));
    }

    [Fact]
    public void GetAttachmentByIdHandler_NullRepo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSessionAttachmentByIdQueryHandler(null!, _serviceMock.Object, _sessionRepoMock.Object));
    }

    [Fact]
    public void GetAttachmentByIdHandler_NullService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSessionAttachmentByIdQueryHandler(_repoMock.Object, null!, _sessionRepoMock.Object));
    }

    [Fact]
    public void GetSnapshotPhotosHandler_NullRepo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetSnapshotPhotosQueryHandler(null!));
    }

    #endregion

    #region Helpers

    private static SessionAttachment CreateAttachment(
        Guid sessionId,
        Guid? playerId = null,
        AttachmentType type = AttachmentType.BoardState,
        int? snapshotIndex = null)
    {
        return SessionAttachment.Create(
            sessionId,
            playerId ?? Guid.NewGuid(),
            type,
            "https://storage.example.com/photo.jpg",
            "image/jpeg",
            5000,
            "https://storage.example.com/thumb.jpg",
            "Test caption",
            snapshotIndex);
    }

    #endregion
}

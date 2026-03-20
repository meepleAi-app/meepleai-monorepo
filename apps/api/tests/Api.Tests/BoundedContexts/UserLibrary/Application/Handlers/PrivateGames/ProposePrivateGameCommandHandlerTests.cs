using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Unit tests for ProposePrivateGameCommandHandler.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class ProposePrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepositoryMock;
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ProposePrivateGameCommandHandler>> _loggerMock;
    private readonly ProposePrivateGameCommandHandler _handler;

    public ProposePrivateGameCommandHandlerTests()
    {
        _privateGameRepositoryMock = new Mock<IPrivateGameRepository>();
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ProposePrivateGameCommandHandler>>();

        _handler = new ProposePrivateGameCommandHandler(
            _privateGameRepositoryMock.Object,
            _shareRequestRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidPrivateGame_CreatesProposal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();
        var privateGame = PrivateGame.CreateManual(
            userId,
            "My Indie Game",
            2,
            4,
            2024,
            "A great indie game",
            60,
            12,
            3.5m,
            "https://example.com/image.jpg");

        var command = new ProposePrivateGameCommand(
            userId,
            privateGameId,
            "Please add this game to the catalog");

        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _shareRequestRepositoryMock
            .Setup(r => r.GetPendingProposalForPrivateGameAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ShareRequestId.Should().NotBeEmpty();
        result.Status.Should().Be(ShareRequestStatus.Pending);
        result.ContributionType.Should().Be(ContributionType.NewGameProposal);

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.Is<ShareRequest>(sr =>
                sr.SourcePrivateGameId == privateGameId &&
                sr.UserId == userId &&
                sr.ContributionType == ContributionType.NewGameProposal),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PrivateGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        var command = new ProposePrivateGameCommand(
            userId,
            privateGameId,
            "Please add this game");

        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserDoesNotOwnGame_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        var privateGame = PrivateGame.CreateManual(
            otherUserId, // Different owner
            "My Indie Game",
            2,
            4);

        var command = new ProposePrivateGameCommand(
            userId,
            privateGameId,
            "Please add this game");

        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        // Act & Assert
        var act2 = () =>
            _handler.Handle(command, CancellationToken.None);
        await act2.Should().ThrowAsync<UnauthorizedAccessException>();

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicatePendingProposal_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        var privateGame = PrivateGame.CreateManual(
            userId,
            "My Indie Game",
            2,
            4);

        var existingProposal = ShareRequest.CreateGameProposal(
            userId,
            privateGameId,
            "Previous proposal");

        var command = new ProposePrivateGameCommand(
            userId,
            privateGameId,
            "Another proposal");

        _privateGameRepositoryMock
            .Setup(r => r.GetByIdAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _shareRequestRepositoryMock
            .Setup(r => r.GetPendingProposalForPrivateGameAsync(privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProposal);

        // Act & Assert
        var act3 = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act3.Should().ThrowAsync<ConflictException>()).Which;

        exception.Message.Should().Contain("pending proposal already exists");

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

}

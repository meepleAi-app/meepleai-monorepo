using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Tests for RevokeLibraryShareLinkCommandHandler.
/// Tests share link revocation with ownership verification.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RevokeLibraryShareLinkCommandHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RevokeLibraryShareLinkCommandHandler _handler;

    public RevokeLibraryShareLinkCommandHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _handler = new RevokeLibraryShareLinkCommandHandler(
            _mockShareLinkRepository.Object,
            _mockUnitOfWork.Object,
            NullLogger<RevokeLibraryShareLinkCommandHandler>.Instance);
    }

    #region Successful Revocation Tests

    [Fact]
    public async Task Handle_WithValidCommand_RevokesShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new RevokeLibraryShareLinkCommand(userId, shareToken);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        shareLink.RevokedAt.Should().NotBeNull();
        shareLink.IsValid.Should().BeFalse();

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(shareLink, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPublicLink_RevokesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new RevokeLibraryShareLinkCommand(userId, shareToken);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        shareLink.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithUnlistedLink_RevokesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Unlisted,
            false,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new RevokeLibraryShareLinkCommand(userId, shareToken);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        shareLink.RevokedAt.Should().NotBeNull();
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentToken_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareToken = "nonexistenttoken12345678901234";
        var command = new RevokeLibraryShareLinkCommand(userId, shareToken);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain(shareToken);

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Ownership Verification Tests

    [Fact]
    public async Task Handle_WithDifferentOwner_ThrowsNotFoundException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var shareLink = LibraryShareLink.Create(
            ownerId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new RevokeLibraryShareLinkCommand(requesterId, shareToken);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        // Should throw NotFoundException to hide existence from non-owners
        exception.Message.Should().Contain(shareToken);

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion
}

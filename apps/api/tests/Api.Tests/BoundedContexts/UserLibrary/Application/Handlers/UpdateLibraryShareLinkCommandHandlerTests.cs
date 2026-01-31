using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Tests for UpdateLibraryShareLinkCommandHandler.
/// Tests share link updates (privacy level, include notes, expiration).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateLibraryShareLinkCommandHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly UpdateLibraryShareLinkCommandHandler _handler;

    public UpdateLibraryShareLinkCommandHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns("https://meepleai.com");

        _handler = new UpdateLibraryShareLinkCommandHandler(
            _mockShareLinkRepository.Object,
            _mockUnitOfWork.Object,
            _mockConfiguration.Object,
            NullLogger<UpdateLibraryShareLinkCommandHandler>.Instance);
    }

    #region Successful Update Tests

    [Fact]
    public async Task Handle_WithPrivacyLevelUpdate_UpdatesPrivacyLevel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "unlisted",
            null,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("unlisted");

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(shareLink, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithIncludeNotesUpdate_UpdatesIncludeNotes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            null,
            false,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IncludeNotes.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithExpirationUpdate_UpdatesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;
        var newExpiration = DateTime.UtcNow.AddDays(30);

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            null,
            null,
            newExpiration);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ExpiresAt.Should().BeCloseTo(newExpiration, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_WithAllFieldsUpdated_UpdatesAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;
        var newExpiration = DateTime.UtcNow.AddDays(7);

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "unlisted",
            false,
            newExpiration);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("unlisted");
        result.IncludeNotes.Should().BeFalse();
        result.ExpiresAt.Should().BeCloseTo(newExpiration, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("PUBLIC")]
    [InlineData("Public")]
    [InlineData("public")]
    public async Task Handle_WithDifferentCasing_ParsesPrivacyLevelCorrectly(string privacyLevel)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Unlisted,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            privacyLevel,
            null,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("public");
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentToken_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareToken = "nonexistenttoken12345678901234";
        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "public",
            null,
            null);

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

        var command = new UpdateLibraryShareLinkCommand(
            requesterId,
            shareToken,
            "unlisted",
            null,
            null);

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

    #region Invalid State Tests

    [Fact]
    public async Task Handle_WithRevokedLink_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        shareLink.Revoke(); // Revoke the link
        var shareToken = shareLink.ShareToken;

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "unlisted",
            null,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain("revoked");

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidPrivacyLevel_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "invalid_privacy_level",
            null,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain("Invalid privacy level");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Handle_WithMissingBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns((string?)null);

        var command = new UpdateLibraryShareLinkCommand(
            userId,
            shareToken,
            "unlisted",
            null,
            null);

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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

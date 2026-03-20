using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Tests for CreateLibraryShareLinkCommandHandler.
/// Tests share link creation, rate limiting, and existing link revocation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateLibraryShareLinkCommandHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly CreateLibraryShareLinkCommandHandler _handler;

    public CreateLibraryShareLinkCommandHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns("https://meepleai.com");

        _handler = new CreateLibraryShareLinkCommandHandler(
            _mockShareLinkRepository.Object,
            _mockUnitOfWork.Object,
            _mockConfiguration.Object,
            NullLogger<CreateLibraryShareLinkCommandHandler>.Instance);
    }

    #region Successful Creation Tests

    [Fact]
    public async Task Handle_WithValidCommand_CreatesShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "public",
            true,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.PrivacyLevel.Should().Be("public");
        result.IncludeNotes.Should().BeTrue();
        result.ShareToken.Should().NotBeNullOrEmpty();
        result.ShareToken.Should().HaveLength(32);
        result.ShareUrl.Should().StartWith("https://meepleai.com/library/shared/");
        result.IsActive.Should().BeTrue();

        _mockShareLinkRepository.Verify(
            r => r.AddAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnlistedPrivacy_CreatesUnlistedShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "unlisted",
            false,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("unlisted");
        result.IncludeNotes.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithExpiration_CreatesShareLinkWithExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "public",
            true,
            expiresAt);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("PUBLIC")]
    [InlineData("Public")]
    [InlineData("public")]
    public async Task Handle_WithDifferentCasing_ParsesPrivacyLevelCorrectly(string privacyLevel)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            privacyLevel,
            true,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("public");
    }

    [Fact]
    public async Task Handle_WithInvalidPrivacyLevel_DefaultsToUnlisted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "invalid_value",
            true,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("unlisted");
    }

    #endregion

    #region Existing Link Revocation Tests

    [Fact]
    public async Task Handle_WithExistingActiveLink_RevokesOldAndCreatesNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);

        var command = new CreateLibraryShareLinkCommand(
            userId,
            "unlisted",
            false,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PrivacyLevel.Should().Be("unlisted");

        _mockShareLinkRepository.Verify(
            r => r.UpdateAsync(It.Is<LibraryShareLink>(l => l.RevokedAt != null), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockShareLinkRepository.Verify(
            r => r.AddAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Rate Limiting Tests

    [Theory]
    [InlineData(9)]
    [InlineData(5)]
    [InlineData(0)]
    public async Task Handle_WithinRateLimit_CreatesShareLink(int recentCount)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "public",
            true,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentCount);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(100)]
    public async Task Handle_ExceedsRateLimit_ThrowsDomainException(int recentCount)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "public",
            true,
            null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentCount);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        exception.Message.Should().Contain("10");
        exception.Message.Should().Contain("share links per day");

        _mockShareLinkRepository.Verify(
            r => r.AddAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Handle_WithMissingBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateLibraryShareLinkCommand(
            userId,
            "public",
            true,
            null);

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns((string?)null);

        _mockShareLinkRepository
            .Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

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

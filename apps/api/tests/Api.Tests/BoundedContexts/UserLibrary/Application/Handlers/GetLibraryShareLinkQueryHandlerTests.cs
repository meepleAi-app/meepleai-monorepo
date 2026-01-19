using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Tests for GetLibraryShareLinkQueryHandler.
/// Tests retrieving user's active share link.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetLibraryShareLinkQueryHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly GetLibraryShareLinkQueryHandler _handler;

    public GetLibraryShareLinkQueryHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns("https://meepleai.com");

        _handler = new GetLibraryShareLinkQueryHandler(
            _mockShareLinkRepository.Object,
            _mockConfiguration.Object);
    }

    #region Successful Retrieval Tests

    [Fact]
    public async Task Handle_WithActiveShareLink_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var query = new GetLibraryShareLinkQuery(userId);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.PrivacyLevel.Should().Be("public");
        result.IncludeNotes.Should().BeTrue();
        result.ShareToken.Should().NotBeNullOrEmpty();
        result.ShareToken.Should().HaveLength(32);
        result.ShareUrl.Should().Be($"https://meepleai.com/library/shared/{shareLink.ShareToken}");
        result.IsActive.Should().BeTrue();
        result.ViewCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithUnlistedLink_ReturnsCorrectPrivacyLevel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Unlisted,
            false,
            null);
        var query = new GetLibraryShareLinkQuery(userId);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.PrivacyLevel.Should().Be("unlisted");
        result.IncludeNotes.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithExpiration_ReturnsExpirationDate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            expiresAt);
        var query = new GetLibraryShareLinkQuery(userId);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region No Active Link Tests

    [Fact]
    public async Task Handle_WithNoActiveLink_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLibraryShareLinkQuery(userId);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
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
        var query = new GetLibraryShareLinkQuery(userId);

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns((string?)null);

        _mockShareLinkRepository
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion
}

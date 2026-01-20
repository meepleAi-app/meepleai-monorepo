using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Tests for GetSharedLibraryQuery validation and share link behavior.
/// Note: Full handler tests require integration testing due to DbContext dependencies.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetSharedLibraryQueryHandlerTests
{
    private readonly Mock<ILibraryShareLinkRepository> _mockShareLinkRepository;

    public GetSharedLibraryQueryHandlerTests()
    {
        _mockShareLinkRepository = new Mock<ILibraryShareLinkRepository>();
    }

    #region Query Validation Tests

    [Fact]
    public void Query_WithValidToken_CreatesSuccessfully()
    {
        // Arrange & Act
        var query = new GetSharedLibraryQuery("12345678901234567890123456789012");

        // Assert
        query.Should().NotBeNull();
        query.ShareToken.Should().Be("12345678901234567890123456789012");
    }

    #endregion

    #region Share Link Validity Tests

    [Fact]
    public void ShareLink_WhenRevoked_IsInvalid()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            null);

        // Act
        shareLink.Revoke();

        // Assert
        shareLink.IsValid.Should().BeFalse();
        shareLink.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void ShareLink_WhenExpired_IsInvalid()
    {
        // Arrange - Create a valid link first, then test the IsExpired property directly
        // Note: Domain prevents creating expired links at creation time (correct behavior)
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            DateTime.UtcNow.AddMilliseconds(1)); // Expires in 1ms

        // Wait for expiration
        Thread.Sleep(10);

        // Assert
        shareLink.IsExpired.Should().BeTrue();
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ShareLink_WhenActive_IsValid()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            DateTime.UtcNow.AddDays(7)); // Expires in 7 days

        // Assert
        shareLink.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ShareLink_RecordAccess_IncrementsViewCount()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var initialCount = shareLink.ViewCount;

        // Act
        shareLink.RecordAccess();

        // Assert
        shareLink.ViewCount.Should().Be(initialCount + 1);
        shareLink.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public void ShareLink_RecordAccess_UpdatesLastAccessedAt()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            true,
            null);

        // Act
        shareLink.RecordAccess();

        // Assert
        shareLink.LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Repository Behavior Tests

    [Fact]
    public async Task Repository_GetByShareToken_ReturnsNullForNonExistent()
    {
        // Arrange
        var shareToken = "nonexistenttoken12345678901234";

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LibraryShareLink?)null);

        // Act
        var result = await _mockShareLinkRepository.Object.GetByShareTokenAsync(
            shareToken, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_GetByShareToken_ReturnsShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = LibraryShareLink.Create(
            userId,
            LibrarySharePrivacyLevel.Public,
            true,
            null);
        var shareToken = shareLink.ShareToken;

        _mockShareLinkRepository
            .Setup(r => r.GetByShareTokenAsync(shareToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _mockShareLinkRepository.Object.GetByShareTokenAsync(
            shareToken, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ShareToken.Should().Be(shareToken);
        result.UserId.Should().Be(userId);
    }

    #endregion
}

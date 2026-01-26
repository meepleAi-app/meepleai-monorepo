using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the LibraryShareLink aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class LibraryShareLinkTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_CreatesInstance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privacyLevel = LibrarySharePrivacyLevel.Public;
        var includeNotes = true;

        // Act
        var shareLink = LibraryShareLink.Create(userId, privacyLevel, includeNotes);

        // Assert
        shareLink.Id.Should().NotBeEmpty();
        shareLink.UserId.Should().Be(userId);
        shareLink.PrivacyLevel.Should().Be(privacyLevel);
        shareLink.IncludeNotes.Should().BeTrue();
        shareLink.ShareToken.Should().NotBeNullOrEmpty();
        shareLink.ShareToken.Should().HaveLength(32); // 16 bytes = 32 hex chars
        shareLink.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        shareLink.ExpiresAt.Should().BeNull();
        shareLink.RevokedAt.Should().BeNull();
        shareLink.ViewCount.Should().Be(0);
        shareLink.LastAccessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var shareLink = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Unlisted, false, expiresAt);

        // Assert
        shareLink.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var action = () => LibraryShareLink.Create(Guid.Empty, LibrarySharePrivacyLevel.Public, false);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("User ID cannot be empty*");
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var action = () => LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false, pastDate);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Expiration must be in the future*");
    }

    [Fact]
    public void Create_GeneratesUniqueTokens()
    {
        // Arrange & Act
        var link1 = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        var link2 = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Assert
        link1.ShareToken.Should().NotBe(link2.ShareToken);
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_WhenNotRevoked_SetsRevokedAt()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Act
        shareLink.Revoke();

        // Assert
        shareLink.IsRevoked.Should().BeTrue();
        shareLink.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        shareLink.Revoke();

        // Act
        var action = () => shareLink.Revoke();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Share link is already revoked");
    }

    #endregion

    #region RecordAccess Tests

    [Fact]
    public void RecordAccess_IncrementsViewCount()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Act
        shareLink.RecordAccess();

        // Assert
        shareLink.ViewCount.Should().Be(1);
    }

    [Fact]
    public void RecordAccess_UpdatesLastAccessedAt()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Act
        shareLink.RecordAccess();

        // Assert
        shareLink.LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordAccess_CalledMultipleTimes_AccumulatesCount()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Act
        shareLink.RecordAccess();
        shareLink.RecordAccess();
        shareLink.RecordAccess();

        // Assert
        shareLink.ViewCount.Should().Be(3);
    }

    #endregion

    #region UpdatePrivacyLevel Tests

    [Fact]
    public void UpdatePrivacyLevel_WhenNotRevoked_UpdatesLevel()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Act
        shareLink.UpdatePrivacyLevel(LibrarySharePrivacyLevel.Unlisted);

        // Assert
        shareLink.PrivacyLevel.Should().Be(LibrarySharePrivacyLevel.Unlisted);
    }

    [Fact]
    public void UpdatePrivacyLevel_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        shareLink.Revoke();

        // Act
        var action = () => shareLink.UpdatePrivacyLevel(LibrarySharePrivacyLevel.Unlisted);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update privacy level for revoked share link");
    }

    #endregion

    #region UpdateIncludeNotes Tests

    [Fact]
    public void UpdateIncludeNotes_WhenNotRevoked_UpdatesSetting()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Act
        shareLink.UpdateIncludeNotes(true);

        // Assert
        shareLink.IncludeNotes.Should().BeTrue();
    }

    [Fact]
    public void UpdateIncludeNotes_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        shareLink.Revoke();

        // Act
        var action = () => shareLink.UpdateIncludeNotes(true);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update settings for revoked share link");
    }

    #endregion

    #region UpdateExpiration Tests

    [Fact]
    public void UpdateExpiration_WhenNotRevoked_UpdatesExpiration()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        var newExpiration = DateTime.UtcNow.AddDays(30);

        // Act
        shareLink.UpdateExpiration(newExpiration);

        // Assert
        shareLink.ExpiresAt.Should().BeCloseTo(newExpiration, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateExpiration_WithNull_RemovesExpiration()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            false,
            DateTime.UtcNow.AddDays(7));

        // Act
        shareLink.UpdateExpiration(null);

        // Assert
        shareLink.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void UpdateExpiration_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        shareLink.Revoke();

        // Act
        var action = () => shareLink.UpdateExpiration(DateTime.UtcNow.AddDays(7));

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update expiration for revoked share link");
    }

    [Fact]
    public void UpdateExpiration_WithPastDate_ThrowsArgumentException()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Act
        var action = () => shareLink.UpdateExpiration(DateTime.UtcNow.AddDays(-1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("New expiration must be in the future*");
    }

    #endregion

    #region IsValid Property Tests

    [Fact]
    public void IsValid_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Assert
        shareLink.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var shareLink = CreateTestShareLink();
        shareLink.Revoke();

        // Assert
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange - Create with expiration in 1 millisecond
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            false,
            DateTime.UtcNow.AddMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(10);

        // Assert
        shareLink.IsExpired.Should().BeTrue();
        shareLink.IsValid.Should().BeFalse();
    }

    #endregion

    #region TimeUntilExpiration Tests

    [Fact]
    public void TimeUntilExpiration_WhenNoExpiration_ReturnsNull()
    {
        // Arrange
        var shareLink = CreateTestShareLink();

        // Assert
        shareLink.TimeUntilExpiration.Should().BeNull();
    }

    [Fact]
    public void TimeUntilExpiration_WhenNotExpired_ReturnsRemainingTime()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            false,
            expiresAt);

        // Assert
        shareLink.TimeUntilExpiration.Should().NotBeNull();
        shareLink.TimeUntilExpiration!.Value.TotalMinutes.Should().BeApproximately(60, 1);
    }

    [Fact]
    public void TimeUntilExpiration_WhenExpired_ReturnsNull()
    {
        // Arrange
        var shareLink = LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            false,
            DateTime.UtcNow.AddMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(10);

        // Assert
        shareLink.TimeUntilExpiration.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static LibraryShareLink CreateTestShareLink()
    {
        return LibraryShareLink.Create(
            Guid.NewGuid(),
            LibrarySharePrivacyLevel.Public,
            includeNotes: false);
    }

    #endregion
}

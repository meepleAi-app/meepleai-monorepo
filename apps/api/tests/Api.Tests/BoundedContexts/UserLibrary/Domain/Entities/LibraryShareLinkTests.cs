using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the LibraryShareLink entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 8
/// </summary>
[Trait("Category", "Unit")]
public sealed class LibraryShareLinkTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var privacyLevel = LibrarySharePrivacyLevel.Public;

        // Act
        var link = LibraryShareLink.Create(userId, privacyLevel, includeNotes: true);

        // Assert
        link.Should().NotBeNull();
        link.Id.Should().NotBe(Guid.Empty);
        link.UserId.Should().Be(userId);
        link.PrivacyLevel.Should().Be(privacyLevel);
        link.IncludeNotes.Should().BeTrue();
        link.ShareToken.Should().NotBeNullOrEmpty();
        link.ShareToken.Should().HaveLength(32); // 16 bytes = 32 hex chars
        link.ViewCount.Should().Be(0);
        link.RevokedAt.Should().BeNull();
        link.LastAccessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        // Act
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Unlisted, false);
        var after = DateTime.UtcNow;

        // Assert
        link.CreatedAt.Should().BeOnOrAfter(before);
        link.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false, expiresAt);

        // Assert
        link.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithoutExpiration_ExpiresAtIsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false, expiresAt: null);

        // Assert
        link.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var link1 = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false);
        var link2 = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false);

        // Assert
        link1.ShareToken.Should().NotBe(link2.ShareToken);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var action = () => LibraryShareLink.Create(Guid.Empty, LibrarySharePrivacyLevel.Public, false);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*User ID cannot be empty*");
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pastExpiration = DateTime.UtcNow.AddDays(-1);

        // Act
        var action = () => LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, false, pastExpiration);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Expiration must be in the future*");
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_SetsRevokedAt()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        var before = DateTime.UtcNow;

        // Act
        link.Revoke();
        var after = DateTime.UtcNow;

        // Assert
        link.RevokedAt.Should().NotBeNull();
        link.RevokedAt.Should().BeOnOrAfter(before);
        link.RevokedAt.Should().BeOnOrBefore(after);
        link.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        link.Revoke();

        // Act
        var action = () => link.Revoke();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Share link is already revoked*");
    }

    #endregion

    #region RecordAccess Tests

    [Fact]
    public void RecordAccess_IncrementsViewCount()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Act
        link.RecordAccess();

        // Assert
        link.ViewCount.Should().Be(1);
    }

    [Fact]
    public void RecordAccess_UpdatesLastAccessedAt()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        var before = DateTime.UtcNow;

        // Act
        link.RecordAccess();
        var after = DateTime.UtcNow;

        // Assert
        link.LastAccessedAt.Should().NotBeNull();
        link.LastAccessedAt.Should().BeOnOrAfter(before);
        link.LastAccessedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void RecordAccess_MultipleAccesses_AccumulatesViewCount()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Act
        link.RecordAccess();
        link.RecordAccess();
        link.RecordAccess();

        // Assert
        link.ViewCount.Should().Be(3);
    }

    #endregion

    #region UpdatePrivacyLevel Tests

    [Fact]
    public void UpdatePrivacyLevel_ChangesLevel()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Act
        link.UpdatePrivacyLevel(LibrarySharePrivacyLevel.Unlisted);

        // Assert
        link.PrivacyLevel.Should().Be(LibrarySharePrivacyLevel.Unlisted);
    }

    [Fact]
    public void UpdatePrivacyLevel_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        link.Revoke();

        // Act
        var action = () => link.UpdatePrivacyLevel(LibrarySharePrivacyLevel.Unlisted);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot update privacy level for revoked share link*");
    }

    #endregion

    #region UpdateIncludeNotes Tests

    [Fact]
    public void UpdateIncludeNotes_ChangesValue()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, includeNotes: false);

        // Act
        link.UpdateIncludeNotes(true);

        // Assert
        link.IncludeNotes.Should().BeTrue();
    }

    [Fact]
    public void UpdateIncludeNotes_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        link.Revoke();

        // Act
        var action = () => link.UpdateIncludeNotes(true);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot update settings for revoked share link*");
    }

    #endregion

    #region UpdateExpiration Tests

    [Fact]
    public void UpdateExpiration_ChangesExpiresAt()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        var newExpiration = DateTime.UtcNow.AddDays(14);

        // Act
        link.UpdateExpiration(newExpiration);

        // Assert
        link.ExpiresAt.Should().Be(newExpiration);
    }

    [Fact]
    public void UpdateExpiration_ToNull_RemovesExpiration()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false, DateTime.UtcNow.AddDays(7));

        // Act
        link.UpdateExpiration(null);

        // Assert
        link.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void UpdateExpiration_ToPast_ThrowsArgumentException()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        var pastDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var action = () => link.UpdateExpiration(pastDate);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*New expiration must be in the future*");
    }

    [Fact]
    public void UpdateExpiration_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        link.Revoke();

        // Act
        var action = () => link.UpdateExpiration(DateTime.UtcNow.AddDays(7));

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot update expiration for revoked share link*");
    }

    #endregion

    #region Status Property Tests

    [Fact]
    public void IsValid_WhenActiveAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Assert
        link.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);
        link.Revoke();

        // Assert
        link.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_WhenNotRevoked_ReturnsFalse()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false);

        // Assert
        link.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenNoExpiration_ReturnsFalse()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false, expiresAt: null);

        // Assert
        link.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpirationInFuture_ReturnsFalse()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false, DateTime.UtcNow.AddDays(7));

        // Assert
        link.IsExpired.Should().BeFalse();
    }

    #endregion

    #region TimeUntilExpiration Tests

    [Fact]
    public void TimeUntilExpiration_WhenNoExpiration_ReturnsNull()
    {
        // Arrange
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false, expiresAt: null);

        // Assert
        link.TimeUntilExpiration.Should().BeNull();
    }

    [Fact]
    public void TimeUntilExpiration_WhenExpirationInFuture_ReturnsPositiveTimeSpan()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var link = LibraryShareLink.Create(Guid.NewGuid(), LibrarySharePrivacyLevel.Public, false, expiresAt);

        // Assert
        link.TimeUntilExpiration.Should().NotBeNull();
        link.TimeUntilExpiration!.Value.TotalDays.Should().BeApproximately(7, 0.01);
    }

    #endregion
}

using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive tests for ShareLink domain entity.
/// Tests factory method, revocation, access tracking, and expiration logic.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ShareLinkTests
{
    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";

    [Fact]
    public void Create_WithValidParameters_ReturnsShareLink()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var shareLink = ShareLink.Create(
            threadId: threadId,
            creatorId: creatorId,
            role: ShareLinkRole.View,
            expiresAt: expiresAt,
            label: "Test share link"
        );

        // Assert
        shareLink.Should().NotBeNull();
        shareLink.Id.Should().NotBe(Guid.Empty);
        shareLink.ThreadId.Should().Be(threadId);
        shareLink.CreatorId.Should().Be(creatorId);
        shareLink.Role.Should().Be(ShareLinkRole.View);
        shareLink.ExpiresAt.Should().Be(expiresAt);
        shareLink.Label.Should().Be("Test share link");
        shareLink.IsRevoked.Should().BeFalse();
        shareLink.IsExpired.Should().BeFalse();
        shareLink.IsValid.Should().BeTrue();
        shareLink.AccessCount.Should().Be(0);
        shareLink.LastAccessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithCommentRole_SetsCorrectRole()
    {
        // Arrange & Act
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.Comment,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Assert
        shareLink.Role.Should().Be(ShareLinkRole.Comment);
    }

    [Fact]
    public void Create_WithNullLabel_SetsNullLabel()
    {
        // Arrange & Act
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7),
            label: null
        );

        // Assert
        shareLink.Label.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyThreadId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act = () =>
            ShareLink.Create(
                threadId: Guid.Empty,
                creatorId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddDays(7)
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("Thread ID");
    }

    [Fact]
    public void Create_WithEmptyCreatorId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act = () =>
            ShareLink.Create(
                threadId: Guid.NewGuid(),
                creatorId: Guid.Empty,
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddDays(7)
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("Creator ID");
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act = () =>
            ShareLink.Create(
                threadId: Guid.NewGuid(),
                creatorId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddSeconds(-1) // Past
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("future");
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        var after = DateTime.UtcNow;

        // Assert
        shareLink.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Revoke_SetsRevokedAtTimestamp()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        var before = DateTime.UtcNow;

        // Act
        shareLink.Revoke();

        var after = DateTime.UtcNow;

        // Assert
        shareLink.IsRevoked.Should().BeTrue();
        shareLink.RevokedAt.Should().NotBeNull();
        shareLink.RevokedAt.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        shareLink.Revoke();

        // Act & Assert
        var act = () =>
            shareLink.Revoke();
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().Contain("already revoked");
    }

    [Fact]
    public void RecordAccess_IncrementsAccessCount()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Act
        shareLink.RecordAccess();
        shareLink.RecordAccess();
        shareLink.RecordAccess();

        // Assert
        shareLink.AccessCount.Should().Be(3);
    }

    [Fact]
    public void RecordAccess_SetsLastAccessedAt()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        var before = DateTime.UtcNow;

        // Act
        shareLink.RecordAccess();

        var after = DateTime.UtcNow;

        // Assert
        shareLink.LastAccessedAt.Should().NotBeNull();
        shareLink.LastAccessedAt.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateExpiration_WithFutureDate_UpdatesExpiration()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        var newExpiration = DateTime.UtcNow.AddDays(30);

        // Act
        shareLink.UpdateExpiration(newExpiration);

        // Assert
        shareLink.ExpiresAt.Should().Be(newExpiration);
    }

    [Fact]
    public void UpdateExpiration_WithPastDate_ThrowsArgumentException()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Act & Assert
        var act = () =>
            shareLink.UpdateExpiration(DateTime.UtcNow.AddSeconds(-1));
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("future");
    }

    [Fact]
    public void UpdateExpiration_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        shareLink.Revoke();

        // Act & Assert
        var act = () =>
            shareLink.UpdateExpiration(DateTime.UtcNow.AddDays(30));
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().Contain("revoked");
    }

    [Fact]
    public void IsExpired_WhenPastExpiration_ReturnsTrue()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Assert - no waiting needed, deterministically expired
        shareLink.IsExpired.Should().BeTrue();
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Assert
        shareLink.IsValid.Should().BeTrue();
        shareLink.IsRevoked.Should().BeFalse();
        shareLink.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        shareLink.Revoke();

        // Assert
        shareLink.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TimeUntilExpiration_WhenNotExpired_ReturnsPositiveTimeSpan()
    {
        // Arrange
        var expiresIn = TimeSpan.FromDays(7);
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.Add(expiresIn)
        );

        // Assert
        shareLink.TimeUntilExpiration.Should().NotBeNull();
        // Allow for some execution time variance
        shareLink.TimeUntilExpiration.Value.TotalDays.Should().BeInRange(6.9, 7.1);
    }

    [Fact]
    public void TimeUntilExpiration_WhenExpired_ReturnsNull()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Assert - no waiting needed, deterministically expired
        shareLink.TimeUntilExpiration.Should().BeNull();
    }

    [Fact]
    public void GenerateToken_WithValidShareLink_ReturnsToken()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        // Act
        var token = shareLink.GenerateToken(TestSecretKey);

        // Assert
        token.Should().NotBeNull();
        token.Value.Should().NotBeEmpty();
        token.ShareLinkId.Should().Be(shareLink.Id);
        token.ThreadId.Should().Be(shareLink.ThreadId);
        token.CreatorId.Should().Be(shareLink.CreatorId);
        token.Role.Should().Be(shareLink.Role);
    }

    [Fact]
    public void GenerateToken_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareLink = ShareLink.Create(
            threadId: Guid.NewGuid(),
            creatorId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            expiresAt: DateTime.UtcNow.AddDays(7)
        );

        shareLink.Revoke();

        // Act & Assert
        var act = () =>
            shareLink.GenerateToken(TestSecretKey);
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().Contain("revoked");
    }

    [Fact]
    public void GenerateToken_WhenExpired_ThrowsInvalidOperationException()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Act & Assert - no waiting needed, deterministically expired
        var act = () =>
            shareLink.GenerateToken(TestSecretKey);
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().Contain("expired");
    }
}

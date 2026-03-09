using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Xunit;

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
        Assert.NotNull(shareLink);
        Assert.NotEqual(Guid.Empty, shareLink.Id);
        Assert.Equal(threadId, shareLink.ThreadId);
        Assert.Equal(creatorId, shareLink.CreatorId);
        Assert.Equal(ShareLinkRole.View, shareLink.Role);
        Assert.Equal(expiresAt, shareLink.ExpiresAt);
        Assert.Equal("Test share link", shareLink.Label);
        Assert.False(shareLink.IsRevoked);
        Assert.False(shareLink.IsExpired);
        Assert.True(shareLink.IsValid);
        Assert.Equal(0, shareLink.AccessCount);
        Assert.Null(shareLink.LastAccessedAt);
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
        Assert.Equal(ShareLinkRole.Comment, shareLink.Role);
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
        Assert.Null(shareLink.Label);
    }

    [Fact]
    public void Create_WithEmptyThreadId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLink.Create(
                threadId: Guid.Empty,
                creatorId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddDays(7)
            )
        );

        Assert.Contains("Thread ID", exception.Message);
    }

    [Fact]
    public void Create_WithEmptyCreatorId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLink.Create(
                threadId: Guid.NewGuid(),
                creatorId: Guid.Empty,
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddDays(7)
            )
        );

        Assert.Contains("Creator ID", exception.Message);
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLink.Create(
                threadId: Guid.NewGuid(),
                creatorId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                expiresAt: DateTime.UtcNow.AddSeconds(-1) // Past
            )
        );

        Assert.Contains("future", exception.Message);
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
        Assert.InRange(shareLink.CreatedAt, before, after);
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
        Assert.True(shareLink.IsRevoked);
        Assert.NotNull(shareLink.RevokedAt);
        Assert.InRange(shareLink.RevokedAt.Value, before, after);
        Assert.False(shareLink.IsValid);
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
        var exception = Assert.Throws<InvalidOperationException>(() =>
            shareLink.Revoke()
        );

        Assert.Contains("already revoked", exception.Message);
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
        Assert.Equal(3, shareLink.AccessCount);
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
        Assert.NotNull(shareLink.LastAccessedAt);
        Assert.InRange(shareLink.LastAccessedAt.Value, before, after);
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
        Assert.Equal(newExpiration, shareLink.ExpiresAt);
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
        var exception = Assert.Throws<ArgumentException>(() =>
            shareLink.UpdateExpiration(DateTime.UtcNow.AddSeconds(-1))
        );

        Assert.Contains("future", exception.Message);
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
        var exception = Assert.Throws<InvalidOperationException>(() =>
            shareLink.UpdateExpiration(DateTime.UtcNow.AddDays(30))
        );

        Assert.Contains("revoked", exception.Message);
    }

    [Fact]
    public void IsExpired_WhenPastExpiration_ReturnsTrue()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Assert - no waiting needed, deterministically expired
        Assert.True(shareLink.IsExpired);
        Assert.False(shareLink.IsValid);
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
        Assert.True(shareLink.IsValid);
        Assert.False(shareLink.IsRevoked);
        Assert.False(shareLink.IsExpired);
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
        Assert.False(shareLink.IsValid);
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
        Assert.NotNull(shareLink.TimeUntilExpiration);
        // Allow for some execution time variance
        Assert.InRange(shareLink.TimeUntilExpiration.Value.TotalDays, 6.9, 7.1);
    }

    [Fact]
    public void TimeUntilExpiration_WhenExpired_ReturnsNull()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Assert - no waiting needed, deterministically expired
        Assert.Null(shareLink.TimeUntilExpiration);
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
        Assert.NotNull(token);
        Assert.NotEmpty(token.Value);
        Assert.Equal(shareLink.Id, token.ShareLinkId);
        Assert.Equal(shareLink.ThreadId, token.ThreadId);
        Assert.Equal(shareLink.CreatorId, token.CreatorId);
        Assert.Equal(shareLink.Role, token.Role);
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
        var exception = Assert.Throws<InvalidOperationException>(() =>
            shareLink.GenerateToken(TestSecretKey)
        );

        Assert.Contains("revoked", exception.Message);
    }

    [Fact]
    public void GenerateToken_WhenExpired_ThrowsInvalidOperationException()
    {
        // Arrange - use deterministic expired share link via reflection
        var shareLink = ShareLinkBuilder.CreateExpired();

        // Act & Assert - no waiting needed, deterministically expired
        var exception = Assert.Throws<InvalidOperationException>(() =>
            shareLink.GenerateToken(TestSecretKey)
        );

        Assert.Contains("expired", exception.Message);
    }
}

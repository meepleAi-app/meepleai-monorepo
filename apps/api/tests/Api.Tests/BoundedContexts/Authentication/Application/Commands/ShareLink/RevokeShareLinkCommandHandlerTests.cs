using Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using System.Globalization;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.ShareLink;

/// <summary>
/// Comprehensive tests for RevokeShareLinkCommandHandler.
/// Tests revocation, Redis blacklist entry creation, and error cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RevokeShareLinkCommandHandlerTests
{
    private readonly Mock<IShareLinkRepository> _shareLinkRepositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly RevokeShareLinkCommandHandler _handler;

    public RevokeShareLinkCommandHandlerTests()
    {
        _shareLinkRepositoryMock = new Mock<IShareLinkRepository>();
        _cacheMock = new Mock<IDistributedCache>();

        _handler = new RevokeShareLinkCommandHandler(
            _shareLinkRepositoryMock.Object,
            _cacheMock.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidRequest_RevokesShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(shareLink.IsRevoked);

        _shareLinkRepositoryMock.Verify(
            r => r.UpdateAsync(shareLink, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidRequest_AddsToRedisBlacklist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        string? capturedKey = null;
        string? capturedValue = null;
        DistributedCacheEntryOptions? capturedOptions = null;

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, value, options, _) =>
            {
                capturedKey = key;
                capturedValue = System.Text.Encoding.UTF8.GetString(value);
                capturedOptions = options;
            });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedKey);
        Assert.Equal($"revoked_share_link:{shareLink.Id}", capturedKey);
        Assert.NotNull(capturedValue);
        Assert.NotEmpty(capturedValue);
        // Verify blacklist entry contains ISO 8601 timestamp (RevokedAt value)
        Assert.True(DateTimeOffset.TryParse(capturedValue, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDateOffset),
            $"Blacklist value should be a valid ISO 8601 timestamp, got: {capturedValue}");
        // Convert to UTC for consistent comparison
        var parsedUtc = parsedDateOffset.UtcDateTime;
        var now = DateTime.UtcNow;
        // RevokedAt timestamp should be within the last 30 seconds (reasonable test execution window)
        Assert.True(parsedUtc >= now.AddSeconds(-30) && parsedUtc <= now.AddSeconds(5),
            $"RevokedAt should be recent. Got: {parsedUtc:O}, Now: {now:O}");
        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions.AbsoluteExpiration);
    }

    [Fact]
    public async Task Handle_ShareLinkNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: nonExistentId,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.Authentication.Domain.Entities.ShareLink?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        _shareLinkRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserIsNotCreator_ReturnsFalse()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(creatorId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: differentUserId // Different user
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(shareLink.IsRevoked); // Should not be revoked

        _shareLinkRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyExpiredShareLink_StillRevokesButNoBlacklistEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Create a share link that will expire soon
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromMilliseconds(1)) // Very short expiry
            .Build();

        // Wait for it to expire
        await Task.Delay(TestConstants.Timing.TinyDelay);

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(shareLink.IsRevoked);

        _shareLinkRepositoryMock.Verify(
            r => r.UpdateAsync(shareLink, It.IsAny<CancellationToken>()),
            Times.Once);

        // No blacklist entry needed for already expired links
        _cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _handler.Handle(command, token);

        // Assert
        _shareLinkRepositoryMock.Verify(
            r => r.GetByIdAsync(shareLink.Id, token),
            Times.Once);

        _shareLinkRepositoryMock.Verify(
            r => r.UpdateAsync(shareLink, token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BlacklistKeyFormat_IsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        string? capturedKey = null;

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, _, _, _) =>
            {
                capturedKey = key;
            });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal($"revoked_share_link:{shareLink.Id}", capturedKey);
    }

    [Fact]
    public async Task Handle_BlacklistTTL_MatchesExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .WithExpiresAt(expiresAt)
            .Build();

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        DistributedCacheEntryOptions? capturedOptions = null;

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, options, _) =>
            {
                capturedOptions = options;
            });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedOptions?.AbsoluteExpiration);
        // The blacklist expiration should match the share link expiration
        Assert.Equal(expiresAt, capturedOptions.AbsoluteExpiration.Value.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_SetsRevokedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shareLink = new ShareLinkBuilder()
            .WithCreatorId(userId)
            .ExpiresIn(TimeSpan.FromDays(7))
            .Build();

        var beforeRevoke = DateTime.UtcNow;

        var command = new RevokeShareLinkCommand(
            ShareLinkId: shareLink.Id,
            UserId: userId
        );

        _shareLinkRepositoryMock
            .Setup(r => r.GetByIdAsync(shareLink.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareLink);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        var afterRevoke = DateTime.UtcNow;

        // Assert
        Assert.NotNull(shareLink.RevokedAt);
        Assert.InRange(shareLink.RevokedAt.Value, beforeRevoke, afterRevoke);
    }
}

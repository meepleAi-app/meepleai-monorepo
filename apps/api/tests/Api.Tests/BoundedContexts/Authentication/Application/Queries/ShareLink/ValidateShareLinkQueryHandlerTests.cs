using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries.ShareLink;

/// <summary>
/// Comprehensive tests for ValidateShareLinkQueryHandler.
/// Tests JWT token validation, expiry checking, and revocation detection via Redis blacklist.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ValidateShareLinkQueryHandlerTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly ValidateShareLinkQueryHandler _handler;

    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";

    public ValidateShareLinkQueryHandlerTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup default configuration
        _configurationMock.Setup(c => c["Jwt:ShareLinks:SecretKey"]).Returns(TestSecretKey);

        _handler = new ValidateShareLinkQueryHandler(
            _cacheMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidToken_ReturnsValidResult()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        // Not revoked
        _cacheMock
            .Setup(c => c.GetAsync($"revoked_share_link:{shareLinkId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(shareLinkId, result.ShareLinkId);
        Assert.Equal(threadId, result.ThreadId);
        Assert.Equal(ShareLinkRole.View, result.Role);
        Assert.Equal(creatorId, result.CreatorId);
    }

    [Fact]
    public async Task Handle_WithCommentRole_ReturnsCorrectRole()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.Comment,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        _cacheMock
            .Setup(c => c.GetAsync($"revoked_share_link:{shareLinkId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ShareLinkRole.Comment, result.Role);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ReturnsInvalidResult()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        // Token is revoked - blacklisted in Redis
        var revokedAt = DateTime.UtcNow.ToString("O");
        _cacheMock
            .Setup(c => c.GetAsync($"revoked_share_link:{shareLinkId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(revokedAt));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal(shareLinkId, result.ShareLinkId);
        Assert.Equal(threadId, result.ThreadId);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsNull()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddSeconds(1); // Very short expiry

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Wait for token to expire
        await Task.Delay(1500);

        var query = new ValidateShareLinkQuery(token.Value);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - expired tokens return null (token validation fails)
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithMalformedToken_ReturnsNull()
    {
        // Arrange
        var query = new ValidateShareLinkQuery("malformed-not-a-jwt-token");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithEmptyToken_ReturnsNull()
    {
        // Arrange
        var query = new ValidateShareLinkQuery("");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullToken_ReturnsNull()
    {
        // Arrange
        var query = new ValidateShareLinkQuery(null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithWrongSecretKey_ReturnsNull()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Generate token with different secret key
        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: "different-secret-key-that-is-at-least-32-characters-long"
        );

        var query = new ValidateShareLinkQuery(token.Value);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - signature validation fails
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_MissingJwtSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(c => c["Jwt:ShareLinks:SecretKey"]).Returns((string?)null);

        var handler = new ValidateShareLinkQueryHandler(
            _cacheMock.Object,
            _configurationMock.Object
        );

        var query = new ValidateShareLinkQuery("any-token");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(query, CancellationToken.None)
        );

        Assert.Contains("JWT secret key not configured", exception.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToCache()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _cacheMock
            .Setup(c => c.GetAsync($"revoked_share_link:{shareLinkId}", cancellationToken))
            .ReturnsAsync((byte[]?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _cacheMock.Verify(
            c => c.GetAsync($"revoked_share_link:{shareLinkId}", cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ChecksCorrectBlacklistKey()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        string? capturedKey = null;

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKey = key)
            .ReturnsAsync((byte[]?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal($"revoked_share_link:{shareLinkId}", capturedKey);
    }

    [Fact]
    public async Task Handle_ReturnsAllClaimsFromToken()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.Comment,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var query = new ValidateShareLinkQuery(token.Value);

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shareLinkId, result.ShareLinkId);
        Assert.Equal(threadId, result.ThreadId);
        Assert.Equal(creatorId, result.CreatorId);
        Assert.Equal(ShareLinkRole.Comment, result.Role);
        // Expiration should be close to the token's expiration
        Assert.InRange(result.ExpiresAt, expiresAt.AddSeconds(-1), expiresAt.AddSeconds(1));
    }

    [Fact]
    public async Task Handle_TamperedToken_ReturnsNull()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Tamper with token by modifying a character
        var tamperedToken = token.Value.Substring(0, token.Value.Length - 5) + "XXXXX";

        var query = new ValidateShareLinkQuery(tamperedToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - tampered token should fail validation
        Assert.Null(result);
    }
}

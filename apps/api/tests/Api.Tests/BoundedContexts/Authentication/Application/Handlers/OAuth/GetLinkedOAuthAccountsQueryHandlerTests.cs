using Api.BoundedContexts.Authentication.Application.Queries.OAuth;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.OAuth;

/// <summary>
/// Tests for GetLinkedOAuthAccountsQueryHandler.
/// Validates retrieval and mapping of OAuth accounts to DTOs.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetLinkedOAuthAccountsQueryHandlerTests
{
    private readonly Mock<IOAuthAccountRepository> _oauthAccountRepositoryMock;
    private readonly Mock<ILogger<GetLinkedOAuthAccountsQueryHandler>> _loggerMock;
    private readonly GetLinkedOAuthAccountsQueryHandler _handler;

    public GetLinkedOAuthAccountsQueryHandlerTests()
    {
        _oauthAccountRepositoryMock = new Mock<IOAuthAccountRepository>();
        _loggerMock = new Mock<ILogger<GetLinkedOAuthAccountsQueryHandler>>();

        _handler = new GetLinkedOAuthAccountsQueryHandler(
            _oauthAccountRepositoryMock.Object,
            _loggerMock.Object);
    }
    [Fact]
    public async Task Handle_NoLinkedAccounts_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthAccount>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Accounts);
        Assert.Empty(result.Accounts);
    }

    [Fact]
    public async Task Handle_MultipleLinkedAccounts_ReturnsAllAccounts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        var accounts = new List<OAuthAccount>
        {
            CreateTestOAuthAccount(userId, "google"),
            CreateTestOAuthAccount(userId, "discord"),
            CreateTestOAuthAccount(userId, "github")
        };

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Accounts);
        Assert.Equal(3, result.Accounts.Count);
        Assert.Contains(result.Accounts, a => a.Provider == "google");
        Assert.Contains(result.Accounts, a => a.Provider == "discord");
        Assert.Contains(result.Accounts, a => a.Provider == "github");
    }

    [Fact]
    public async Task Handle_LinkedAccounts_MapsToDtosCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };
        var accountId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);

        var account = new OAuthAccount(
            id: accountId,
            userId: userId,
            provider: "google",
            providerUserId: "google_user_123",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token",
            tokenExpiresAt: DateTime.UtcNow.AddHours(1)
        );

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthAccount> { account });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Accounts);

        var dto = result.Accounts.First();
        Assert.Equal(accountId, dto.Id);
        Assert.Equal("google", dto.Provider);
        Assert.Equal("google_user_123", dto.ProviderUserId);
        Assert.Equal(account.CreatedAt, dto.CreatedAt);
    }

    [Fact]
    public async Task Handle_LinkedAccounts_IncludesTokenExpirationStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        var expiredAccount = new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: "google",
            providerUserId: "google_user_123",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token",
            tokenExpiresAt: DateTime.UtcNow.AddHours(-1) // Expired
        );

        var validAccount = new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: "discord",
            providerUserId: "discord_user_456",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token",
            tokenExpiresAt: DateTime.UtcNow.AddHours(1) // Valid
        );

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthAccount> { expiredAccount, validAccount });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Accounts.Count);

        var expiredDto = result.Accounts.First(a => a.Provider == "google");
        Assert.True(expiredDto.IsTokenExpired);

        var validDto = result.Accounts.First(a => a.Provider == "discord");
        Assert.False(validDto.IsTokenExpired);
    }

    [Fact]
    public async Task Handle_LinkedAccounts_IncludesRefreshSupportStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        var googleAccount = new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: "google",
            providerUserId: "google_user_123",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token", // Supports refresh
            tokenExpiresAt: DateTime.UtcNow.AddHours(1)
        );

        var githubAccount = new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: "github",
            providerUserId: "github_user_456",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: null, // No refresh token
            tokenExpiresAt: null
        );

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthAccount> { googleAccount, githubAccount });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Accounts.Count);

        var googleDto = result.Accounts.First(a => a.Provider == "google");
        Assert.True(googleDto.SupportsRefresh);

        var githubDto = result.Accounts.First(a => a.Provider == "github");
        Assert.False(githubDto.SupportsRefresh);
    }
    [Fact]
    public async Task Handle_ExceptionThrown_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Accounts);
        Assert.Empty(result.Accounts);
    }

    [Fact]
    public async Task Handle_ExceptionThrown_LogsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetLinkedOAuthAccountsQuery { UserId = userId };

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    private static OAuthAccount CreateTestOAuthAccount(Guid userId, string provider)
    {
        return new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: provider,
            providerUserId: $"{provider}_user_123",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token",
            tokenExpiresAt: DateTime.UtcNow.AddHours(1)
        );
    }
}

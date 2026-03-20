using Api.BoundedContexts.Authentication.Application.Queries.OAuth;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
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
        result.Should().NotBeNull();
        result.Accounts.Should().NotBeNull();
        result.Accounts.Should().BeEmpty();
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
        result.Should().NotBeNull();
        result.Accounts.Should().NotBeNull();
        result.Accounts.Count.Should().Be(3);
        result.Accounts.Should().Contain(a => a.Provider == "google");
        result.Accounts.Should().Contain(a => a.Provider == "discord");
        result.Accounts.Should().Contain(a => a.Provider == "github");
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
        result.Should().NotBeNull();
        result.Accounts.Should().ContainSingle();

        var dto = result.Accounts[0];
        dto.Id.Should().Be(accountId);
        dto.Provider.Should().Be("google");
        dto.ProviderUserId.Should().Be("google_user_123");
        dto.CreatedAt.Should().Be(account.CreatedAt);
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
        result.Accounts.Count.Should().Be(2);

        var expiredDto = result.Accounts.First(a => a.Provider == "google");
        expiredDto.IsTokenExpired.Should().BeTrue();

        var validDto = result.Accounts.First(a => a.Provider == "discord");
        validDto.IsTokenExpired.Should().BeFalse();
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
        result.Accounts.Count.Should().Be(2);

        var googleDto = result.Accounts.First(a => a.Provider == "google");
        googleDto.SupportsRefresh.Should().BeTrue();

        var githubDto = result.Accounts.First(a => a.Provider == "github");
        githubDto.SupportsRefresh.Should().BeFalse();
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
        result.Should().NotBeNull();
        result.Accounts.Should().NotBeNull();
        result.Accounts.Should().BeEmpty();
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
using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests;

public class OAuthServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IEncryptionService> _mockEncryption;
    private readonly Mock<ILogger<OAuthService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly OAuthConfiguration _config;
    private readonly OAuthService _service;
    private readonly TestTimeProvider _timeProvider;

    public OAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeepleAiDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _mockEncryption = new Mock<IEncryptionService>();
        _mockLogger = new Mock<ILogger<OAuthService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2024-10-26T00:00:00Z"));

        _config = new OAuthConfiguration
        {
            CallbackBaseUrl = "http://localhost:8080",
            Providers = new Dictionary<string, OAuthProviderConfig>
            {
                ["Google"] = new OAuthProviderConfig
                {
                    ClientId = "google-client-id",
                    ClientSecret = "google-client-secret",
                    AuthorizationUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenUrl = "https://oauth2.googleapis.com/token",
                    UserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo",
                    Scope = "openid profile email"
                },
                ["Discord"] = new OAuthProviderConfig
                {
                    ClientId = "discord-client-id",
                    ClientSecret = "discord-client-secret",
                    AuthorizationUrl = "https://discord.com/api/oauth2/authorize",
                    TokenUrl = "https://discord.com/api/oauth2/token",
                    UserInfoUrl = "https://discord.com/api/users/@me",
                    Scope = "identify email"
                },
                ["GitHub"] = new OAuthProviderConfig
                {
                    ClientId = "github-client-id",
                    ClientSecret = "github-client-secret",
                    AuthorizationUrl = "https://github.com/login/oauth/authorize",
                    TokenUrl = "https://github.com/login/oauth/access_token",
                    UserInfoUrl = "https://api.github.com/user",
                    Scope = "read:user user:email"
                }
            }
        };

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _mockEncryption.Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string plaintext, string purpose) => $"encrypted_{plaintext}");
        _mockEncryption.Setup(e => e.DecryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string ciphertext, string purpose) => ciphertext.Replace("encrypted_", ""));

        _service = new OAuthService(
            _db,
            _mockEncryption.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object,
            Options.Create(_config),
            _timeProvider);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task GetAuthorizationUrlAsync_ValidProvider_GeneratesCorrectUrl(string provider)
    {
        // Arrange
        var state = "test-state-123";

        // Act
        var url = await _service.GetAuthorizationUrlAsync(provider, state);

        // Assert
        Assert.Contains($"state={Uri.EscapeDataString(state)}", url);
        Assert.Contains($"client_id=", url);
        Assert.Contains($"redirect_uri=", url);
        Assert.Contains($"scope=", url);
        Assert.Contains($"response_type=code", url);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_UnsupportedProvider_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetAuthorizationUrlAsync("unsupported", "state"));
    }

    [Fact]
    public async Task StoreStateAsync_ValidState_StoresSuccessfully()
    {
        // Arrange
        var state = "secure-state-token";

        // Act
        await _service.StoreStateAsync(state);
        var isValid = await _service.ValidateStateAsync(state);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateStateAsync_InvalidState_ReturnsFalse()
    {
        // Act
        var isValid = await _service.ValidateStateAsync("nonexistent-state");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateStateAsync_ExpiredState_ReturnsFalse()
    {
        // Arrange
        var state = "expired-state";
        await _service.StoreStateAsync(state);

        // Advance time by 11 minutes (state lifetime is 10 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(11));

        // Act
        var isValid = await _service.ValidateStateAsync(state);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateStateAsync_SingleUse_StateRemovedAfterValidation()
    {
        // Arrange
        var state = "single-use-state";
        await _service.StoreStateAsync(state);

        // Act
        var firstValidation = await _service.ValidateStateAsync(state);
        var secondValidation = await _service.ValidateStateAsync(state);

        // Assert
        Assert.True(firstValidation);
        Assert.False(secondValidation); // State should be removed after first use
    }

    [Fact]
    public async Task HandleCallbackAsync_NewUser_CreatesUserAndOAuthAccount()
    {
        // Arrange
        var provider = "google";
        var code = "auth-code-123";
        var state = "csrf-state";
        await _service.StoreStateAsync(state);

        MockSuccessfulTokenExchange();
        MockSuccessfulUserInfoRetrieval("google", "12345", "test@example.com", "Test User");

        // Act
        var result = await _service.HandleCallbackAsync(provider, code, state);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsNewUser);
        Assert.Equal("test@example.com", result.User.Email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);

        var oauthAccount = await _db.OAuthAccounts.FirstOrDefaultAsync(oa => oa.UserId == user.Id);
        Assert.NotNull(oauthAccount);
        Assert.Equal(provider, oauthAccount.Provider);
    }

    [Fact]
    public async Task HandleCallbackAsync_ExistingOAuthAccount_UpdatesToken()
    {
        // Arrange
        var user = CreateTestUser();
        var existingAccount = CreateTestOAuthAccount(user.Id, "google", "12345");

        var provider = "google";
        var code = "auth-code-123";
        var state = "csrf-state";
        await _service.StoreStateAsync(state);

        MockSuccessfulTokenExchange("new-access-token");
        MockSuccessfulUserInfoRetrieval("google", "12345", user.Email, "Test User");

        // Act
        var result = await _service.HandleCallbackAsync(provider, code, state);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsNewUser);
        Assert.Equal(user.Email, result.User.Email);

        var updatedAccount = await _db.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == existingAccount.Id);
        Assert.NotNull(updatedAccount);
        Assert.Contains("new-access-token", updatedAccount.AccessTokenEncrypted);
    }

    [Fact]
    public async Task HandleCallbackAsync_ExistingUserEmail_LinksOAuthAccount()
    {
        // Arrange
        var existingUser = CreateTestUser("existing@example.com");

        var provider = "github";
        var code = "auth-code";
        var state = "state";
        await _service.StoreStateAsync(state);

        MockSuccessfulTokenExchange();
        MockSuccessfulUserInfoRetrieval("github", "gh-123", "existing@example.com", "GitHub User");

        // Act
        var result = await _service.HandleCallbackAsync(provider, code, state);

        // Assert
        Assert.False(result.IsNewUser);
        Assert.Equal(existingUser.Id, result.User.Id);

        var oauthAccounts = await _db.OAuthAccounts.Where(oa => oa.UserId == existingUser.Id).ToListAsync();
        Assert.Single(oauthAccounts);
        Assert.Equal("github", oauthAccounts[0].Provider);
    }

    [Fact]
    public async Task HandleCallbackAsync_InvalidState_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var provider = "google";
        var code = "code";
        var state = "invalid-state";

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.HandleCallbackAsync(provider, code, state));
    }

    [Fact]
    public async Task UnlinkOAuthAccountAsync_ExistingAccount_RemovesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, "discord", "discord-123");

        // Act
        await _service.UnlinkOAuthAccountAsync(user.Id, "discord");

        // Assert
        var removed = await _db.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.Null(removed);
    }

    [Fact]
    public async Task UnlinkOAuthAccountAsync_NonexistentAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UnlinkOAuthAccountAsync(user.Id, "google"));
    }

    [Fact]
    public async Task GetLinkedAccountsAsync_MultipleAccounts_ReturnsAll()
    {
        // Arrange
        var user = CreateTestUser();
        CreateTestOAuthAccount(user.Id, "google", "g-123");
        CreateTestOAuthAccount(user.Id, "discord", "d-456");

        // Act
        var accounts = await _service.GetLinkedAccountsAsync(user.Id);

        // Assert
        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.Provider == "google");
        Assert.Contains(accounts, a => a.Provider == "discord");
    }

    [Fact]
    public async Task GetLinkedAccountsAsync_NoAccounts_ReturnsEmpty()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var accounts = await _service.GetLinkedAccountsAsync(user.Id);

        // Assert
        Assert.Empty(accounts);
    }

    // AUTH-06-P2: Token Refresh Tests

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    public async Task RefreshTokenAsync_WithValidRefreshToken_ReturnsNewToken(string provider)
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, provider, "prov-123");
        account.RefreshTokenEncrypted = "encrypted_refresh123";
        await _db.SaveChangesAsync();

        MockSuccessfulRefreshTokenExchange("new-access-token");

        // Act
        var result = await _service.RefreshTokenAsync(user.Id, provider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new-access-token", result.AccessToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_GitHub_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        CreateTestOAuthAccount(user.Id, "github", "gh-123");

        // Act
        var result = await _service.RefreshTokenAsync(user.Id, "github");

        // Assert
        Assert.Null(result); // GitHub doesn't support refresh
    }

    [Fact]
    public async Task RefreshTokenAsync_NoRefreshToken_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, "google", "g-123");
        account.RefreshTokenEncrypted = null; // No refresh token
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.RefreshTokenAsync(user.Id, "google");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidRefreshToken_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, "discord", "d-123");
        account.RefreshTokenEncrypted = "encrypted_invalidtoken";
        await _db.SaveChangesAsync();

        MockFailedRefreshTokenExchange(HttpStatusCode.Unauthorized);

        // Act
        var result = await _service.RefreshTokenAsync(user.Id, "discord");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_ProviderError_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, "google", "g-123");
        account.RefreshTokenEncrypted = "encrypted_token";
        await _db.SaveChangesAsync();

        MockFailedRefreshTokenExchange(HttpStatusCode.InternalServerError);

        // Act
        var result = await _service.RefreshTokenAsync(user.Id, "google");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_UpdatesDatabaseToken()
    {
        // Arrange
        var user = CreateTestUser();
        var account = CreateTestOAuthAccount(user.Id, "google", "g-123");
        account.RefreshTokenEncrypted = "encrypted_oldtoken";
        var oldAccessToken = account.AccessTokenEncrypted;
        await _db.SaveChangesAsync();

        MockSuccessfulRefreshTokenExchange("refreshed-access-token");

        // Act
        await _service.RefreshTokenAsync(user.Id, "google");

        // Assert
        var updated = await _db.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.NotNull(updated);
        Assert.NotEqual(oldAccessToken, updated.AccessTokenEncrypted);
        Assert.Contains("refreshed-access-token", updated.AccessTokenEncrypted);
    }

    // Helper methods
    private UserEntity CreateTestUser(string? email = null)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = email ?? "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private OAuthAccountEntity CreateTestOAuthAccount(string userId, string provider, string providerUserId)
    {
        var account = new OAuthAccountEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Provider = provider,
            ProviderUserId = providerUserId,
            AccessTokenEncrypted = "encrypted_token",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            User = null!
        };
        _db.OAuthAccounts.Add(account);
        _db.SaveChanges();
        return account;
    }

    private void MockSuccessfulTokenExchange(string accessToken = "access-token-123")
    {
        var tokenResponse = new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });
    }

    private void MockSuccessfulUserInfoRetrieval(string provider, string id, string email, string? name)
    {
        object userInfo = provider.ToLower() switch
        {
            "google" => new { sub = id, email, name },
            "discord" => new { id, email, username = name },
            "github" => new { id = long.Parse(id), email, name },
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("userinfo") ||
                    req.RequestUri!.ToString().Contains("users/@me") ||
                    req.RequestUri!.ToString().Contains("api.github.com/user")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(userInfo))
            });
    }

    private void MockSuccessfulRefreshTokenExchange(string accessToken = "refreshed-access-token")
    {
        var tokenResponse = new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = "new-refresh-token" // Optional: provider may return new refresh token
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("token") &&
                    req.Content != null),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse))
            });
    }

    private void MockFailedRefreshTokenExchange(HttpStatusCode statusCode)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("token") &&
                    req.Content != null),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("{\"error\": \"invalid_grant\"}")
            });
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public TestTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan timeSpan)
        {
            _now = _now.Add(timeSpan);
        }
    }
}

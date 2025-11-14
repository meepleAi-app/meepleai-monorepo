using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Example tests demonstrating usage of test helper builders.
/// These examples show best practices for efficient test writing.
/// </summary>
public class BuilderUsageExamples
{
    [Fact]
    public void Example_UserBuilder_CreateDefaultUser()
    {
        // Simplest way: Create default user
        var user = UserBuilder.CreateDefault();

        Assert.NotNull(user);
        Assert.Equal("test@example.com", user.Email.Value);
        Assert.Equal(Role.User, user.Role);
    }

    [Fact]
    public void Example_UserBuilder_FluentAPI()
    {
        // Arrange: Use fluent API to customize user
        var user = new UserBuilder()
            .WithEmail("admin@meepleai.dev")
            .WithDisplayName("System Admin")
            .WithPassword("SecureAdminPass123!")
            .AsAdmin()
            .With2FA()
            .Build();

        // Assert
        Assert.Equal("admin@meepleai.dev", user.Email.Value);
        Assert.Equal("System Admin", user.DisplayName);
        Assert.True(user.Role.IsAdmin());
        Assert.True(user.IsTwoFactorEnabled);
        Assert.True(user.VerifyPassword("SecureAdminPass123!"));
    }

    [Fact]
    public void Example_SessionBuilder_CreateForUser()
    {
        // Arrange
        var user = UserBuilder.CreateDefault();
        var session = new SessionBuilder()
            .ForUser(user)
            .WithIpAddress("192.168.1.1")
            .WithUserAgent("Mozilla/5.0")
            .ExpiresInDays(30)
            .Build();

        // Assert
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal("192.168.1.1", session.IpAddress);
        Assert.False(session.IsRevoked());
    }

    [Fact]
    public void Example_ApiKeyBuilder_WithScopes()
    {
        // Arrange
        var user = UserBuilder.CreateDefault();
        var (apiKey, plaintextKey) = new ApiKeyBuilder()
            .ForUser(user)
            .WithName("Production API Key")
            .WithScopes("read", "write", "admin")
            .ExpiresInDays(365)
            .Build();

        // Assert
        Assert.Equal("Production API Key", apiKey.KeyName);
        Assert.True(apiKey.HasScope("read"));
        Assert.True(apiKey.HasScope("write"));
        Assert.True(apiKey.HasScope("admin"));
        Assert.True(apiKey.VerifyKey(plaintextKey));
    }

    [Fact]
    public void Example_OAuthAccountBuilder_Google()
    {
        // Arrange
        var user = UserBuilder.CreateDefault();
        var oauthAccount = new OAuthAccountBuilder()
            .ForUser(user)
            .AsGoogle()
            .WithProviderUserId("google_user_123456")
            .ExpiresInHours(1)
            .Build();

        // Assert
        Assert.Equal("google", oauthAccount.Provider);
        Assert.Equal(user.Id, oauthAccount.UserId);
        Assert.True(oauthAccount.SupportsRefresh());
    }

    [Fact]
    public void Example_CombinedScenario_AdminWithSessionAndApiKey()
    {
        // Arrange: Create an admin user with active session and API key
        var admin = new UserBuilder()
            .WithEmail("admin@meepleai.dev")
            .AsAdmin()
            .With2FA()
            .Build();

        var session = SessionBuilder.CreateForUser(admin);
        var (apiKey, plaintextKey) = ApiKeyBuilder.CreateForUser(admin);

        // Act & Assert
        Assert.True(admin.Role.IsAdmin());
        Assert.True(admin.IsTwoFactorEnabled);
        Assert.Equal(admin.Id, session.UserId);
        Assert.Equal(admin.Id, apiKey.UserId);
        Assert.True(apiKey.VerifyKey(plaintextKey));
    }

    [Fact]
    public void Example_TestingExpiration_RevokedSession()
    {
        // Arrange: Create expired and revoked sessions for testing
        var expiredSession = SessionBuilder.CreateExpired();
        var revokedSession = new SessionBuilder()
            .ExpiresInDays(30)
            .Revoked()
            .Build();

        // Assert
        Assert.True(expiredSession.IsExpired(TimeProvider.System));
        Assert.True(revokedSession.IsRevoked());
        Assert.False(revokedSession.IsValid(TimeProvider.System));
    }
}

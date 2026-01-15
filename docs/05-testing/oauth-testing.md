# OAuth Authentication Testing Guide

**Last Updated**: 2026-01-15
**Issue**: [#2461](https://github.com/anthropics/meepleai-monorepo-dev/issues/2461)

## Overview

This document describes the integration testing strategy for OAuth authentication flows in MeepleAI. The test suite validates OAuth logic without requiring real browsers or external OAuth provider interaction through comprehensive HTTP mocking.

## Test Architecture

### Test Infrastructure

**Base Class**: `OAuthIntegrationTestBase`
**Location**: `tests/Api.Tests/BoundedContexts/Authentication/TestHelpers/`
**Mocking Framework**: Moq + Moq.Contrib.HttpClient

The base class provides:
- Mock OAuth service for provider communication
- Mock HTTP handlers for simulating provider responses
- Test data factories for Google, Discord, GitHub
- Helper methods for common OAuth scenarios
- Database context management (EF Core InMemory)

### Supported Providers

1. **Google OAuth 2.0**
   - Authorization endpoint: `accounts.google.com`
   - Scopes: `openid email profile`
   - Refresh token support: Yes

2. **Discord OAuth 2.0**
   - Authorization endpoint: `discord.com/api/oauth2/authorize`
   - Scopes: `identify email`
   - Refresh token support: Yes

3. **GitHub OAuth 2.0**
   - Authorization endpoint: `github.com/login/oauth/authorize`
   - Scopes: `user:email`
   - Refresh token support: No

## Test Suites

### 1. Authorization Tests (`OAuthAuthorizationTests`)

**Purpose**: Verify OAuth authorization URL generation for all providers.

**Coverage**:
- ✅ Valid URL generation for Google, Discord, GitHub
- ✅ State parameter generation and validation
- ✅ Redirect URL inclusion in authorization URL
- ✅ Provider validation (supported vs unsupported)
- ✅ Error handling (empty provider, configuration missing)
- ✅ State security (entropy, uniqueness, Base64 encoding)

**Key Tests**:
```csharp
[Theory]
[InlineData("google")]
[InlineData("discord")]
[InlineData("github")]
public async Task InitiateOAuthLogin_WithValidProvider_ReturnsAuthorizationUrl(string provider)

[Fact]
public async Task InitiateOAuthLogin_StateGeneration_IsSecure()

[Fact]
public async Task InitiateOAuthLogin_WithUnsupportedProvider_ReturnsError()
```

### 2. Callback Integration Tests (`OAuthCallbackIntegrationTests`)

**Purpose**: Validate complete OAuth callback flow with mocked provider responses.

**Coverage**:
- ✅ New user creation with OAuth account linking
- ✅ Existing user OAuth account linking
- ✅ OAuth account token updates
- ✅ Token expiration handling
- ✅ Refresh token storage (Google/Discord)
- ✅ No refresh token storage (GitHub)
- ✅ Duplicate account prevention
- ✅ Session creation after OAuth success
- ✅ Rollback on session creation failure

**Key Tests**:
```csharp
[Theory]
[InlineData("google")]
[InlineData("discord")]
[InlineData("github")]
public async Task HandleCallback_NewUser_CreatesUserAndLinksOAuth(string provider)

[Fact]
public async Task HandleCallback_ExistingUser_LinksOAuthAccount()

[Fact]
public async Task HandleCallback_ExistingOAuthAccount_UpdatesTokens()

[Fact]
public async Task HandleCallback_SessionCreationFailure_RollsBackChanges()
```

### 3. Error Scenario Tests (`OAuthErrorTests`)

**Purpose**: Ensure robust error handling for all failure modes.

**Coverage**:
- ✅ Invalid/expired state parameter
- ✅ Invalid authorization code
- ✅ Provider HTTP errors (400, 500, 503)
- ✅ Network timeouts
- ✅ Invalid access token
- ✅ User denies authorization
- ✅ Malformed token/user info responses
- ✅ Missing email in user info
- ✅ Empty provider/code validation
- ✅ Encryption service failures
- ✅ Database connection failures

**Key Tests**:
```csharp
[Fact]
public async Task HandleCallback_InvalidState_ReturnsError()

[Theory]
[InlineData("google")]
[InlineData("discord")]
[InlineData("github")]
public async Task HandleCallback_InvalidAuthorizationCode_ReturnsError(string provider)

[Theory]
[InlineData("google", HttpStatusCode.Unauthorized)]
[InlineData("discord", HttpStatusCode.Forbidden)]
[InlineData("github", HttpStatusCode.TooManyRequests)]
public async Task HandleCallback_ProviderHttpError_ReturnsSpecificError(...)
```

### 4. Security Tests (`OAuthSecurityTests`)

**Purpose**: Validate CSRF protection, replay attack prevention, and token security.

**Coverage**:
- ✅ CSRF protection via state parameter
- ✅ State uniqueness and entropy validation
- ✅ State modification detection
- ✅ Replay attack prevention (single-use state)
- ✅ Expired state blocking
- ✅ Duplicate authorization code detection
- ✅ Redirect URL validation
- ✅ Open redirect prevention
- ✅ Access token encryption before storage
- ✅ Refresh token encryption before storage
- ✅ Session token not exposed in logs
- ✅ Session fixation prevention

**Key Tests**:
```csharp
[Fact]
public async Task HandleCallback_WithoutValidState_PreventsCsrf()

[Fact]
public async Task InitiateOAuthLogin_GeneratesSecureRandomState()

[Fact]
public async Task HandleCallback_StateUsedOnce_PreventsReplay()

[Fact]
public async Task HandleCallback_EncryptsAccessToken_BeforeStorage()

[Fact]
public async Task HandleCallback_PreventsSessionFixation()
```

## Mocking Strategy

### OAuth Service Mocking

```csharp
// Setup successful state validation
MockValidState("test_state");

// Setup token exchange
var tokenResponse = CreateTokenResponse(
    accessToken: "google_access_token",
    refreshToken: "google_refresh_token",
    expiresIn: 3600);
MockSuccessfulTokenExchange("google", "auth_code", tokenResponse);

// Setup user info retrieval
var userInfo = CreateGoogleUserInfo(
    sub: "google_user_123",
    email: "test@gmail.com",
    name: "Test User");
MockSuccessfulUserInfo("google", "google_access_token", userInfo);

// Setup failure scenarios
MockInvalidState("invalid_state");
MockFailedTokenExchange("google", "invalid_code");
MockFailedUserInfo("google", "invalid_token");
```

### HTTP Endpoint Mocking

```csharp
// Mock Google token endpoint
HttpHandlerMock
    .SetupRequest("https://oauth2.googleapis.com/token")
    .ReturnsJsonResponse(tokenResponse);

// Mock Google user info endpoint
HttpHandlerMock
    .SetupRequest("https://www.googleapis.com/oauth2/v2/userinfo")
    .ReturnsJsonResponse(userInfo);

// Mock HTTP error
HttpHandlerMock
    .SetupRequest("https://provider.com/token")
    .ReturnsResponse(HttpStatusCode.InternalServerError, "Server Error");

// Mock timeout
HttpHandlerMock
    .SetupRequest("https://provider.com/token")
    .ThrowsAsync(new TaskCanceledException("Request timeout"));
```

## Test Data Factories

### Token Responses

```csharp
// Default token response
var tokenResponse = CreateTokenResponse();

// Custom token response
var tokenResponse = CreateTokenResponse(
    accessToken: "custom_access_token",
    refreshToken: "custom_refresh_token",
    expiresIn: 7200);

// Token without refresh token (GitHub)
var tokenResponse = CreateTokenResponse(
    accessToken: "github_token",
    refreshToken: null,
    expiresIn: 0);
```

### User Info Responses

```csharp
// Google user info
var userInfo = CreateGoogleUserInfo(
    sub: "google_user_123",
    email: "test@gmail.com",
    name: "Test User");

// Discord user info
var userInfo = CreateDiscordUserInfo(
    id: "discord_user_456",
    email: "test@discord.com",
    username: "testuser");

// GitHub user info
var userInfo = CreateGitHubUserInfo(
    id: "github_user_789",
    email: "test@github.com",
    login: "testuser");
```

## Running Tests

### Run All OAuth Tests

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~OAuth"
```

### Run Specific Test Suite

```bash
# Authorization tests only
dotnet test --filter "FullyQualifiedName~OAuthAuthorizationTests"

# Callback integration tests
dotnet test --filter "FullyQualifiedName~OAuthCallbackIntegrationTests"

# Error scenario tests
dotnet test --filter "FullyQualifiedName~OAuthErrorTests"

# Security tests
dotnet test --filter "FullyQualifiedName~OAuthSecurityTests"
```

### Run with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover --filter "FullyQualifiedName~OAuth"
```

## Test Coverage Metrics

### Target Coverage

- **Overall OAuth Handler Coverage**: >90%
- **Authorization Flow**: 100% (critical security path)
- **Callback Flow**: >95% (main authentication path)
- **Error Handling**: >90% (all error scenarios)
- **Security Features**: 100% (CSRF, replay, token encryption)

### Coverage Report

```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=html

# View report
# Open: coverage/index.html
```

## Best Practices

### Test Naming Conventions

```csharp
// Pattern: MethodName_Scenario_ExpectedBehavior
[Fact]
public async Task HandleCallback_WithValidState_CreatesUserAndSession()

[Fact]
public async Task HandleCallback_WithExpiredState_ReturnsError()

[Theory]
[InlineData("google")]
[InlineData("discord")]
public async Task InitiateOAuthLogin_WithValidProvider_ReturnsAuthorizationUrl(string provider)
```

### Test Organization

1. **Arrange**: Setup mocks, create test data
2. **Act**: Execute the command/handler
3. **Assert**: Verify results, verify mock calls, check database state

```csharp
[Fact]
public async Task HandleCallback_NewUser_CreatesUserAndLinksOAuth()
{
    // Arrange
    var command = CreateTestCallbackCommand("google");
    var tokenResponse = CreateTokenResponse();
    var userInfo = CreateGoogleUserInfo();

    MockValidState(command.State);
    MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
    MockSuccessfulUserInfo("google", tokenResponse.AccessToken, userInfo);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.True(result.Success);
    Assert.NotNull(result.UserId);

    // Verify database state
    var user = await DbContext.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email);
    Assert.NotNull(user);

    // Verify mock calls
    OAuthServiceMock.Verify(s => s.ExchangeCodeForTokenAsync("google", command.Code), Times.Once);
}
```

### Assertion Guidelines

- **Use FluentAssertions** for complex assertions
- **Verify mock calls** to ensure correct service interaction
- **Check database state** for persistence operations
- **Assert error messages** contain relevant context
- **Verify no side effects** (e.g., no user created on error)

### Test Data Management

- **Use builders** for complex entity creation
- **Avoid magic values**: Use constants or named parameters
- **Isolate test data**: Each test creates its own data
- **Clean up**: DbContext disposed automatically by base class

## CI/CD Integration

### GitHub Actions Workflow

```yaml
- name: Run OAuth Integration Tests
  run: dotnet test --filter "FullyQualifiedName~OAuth" --logger "trx;LogFileName=oauth-tests.trx"

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: oauth-test-results
    path: '**/oauth-tests.trx'

- name: Check Coverage Threshold
  run: dotnet test /p:CollectCoverage=true /p:Threshold=90 /p:ThresholdType=line --filter "FullyQualifiedName~OAuth"
```

## Troubleshooting

### Common Issues

**Issue**: Tests fail with "DbContext disposed"
**Solution**: Ensure base class `Dispose()` is called correctly

**Issue**: Mock setup not working
**Solution**: Verify mock setup occurs before `Act` phase

**Issue**: Tests fail intermittently
**Solution**: Check for timing issues, use `DateTimeOffset.UtcNow` consistently

**Issue**: Coverage not reaching >90%
**Solution**: Add tests for edge cases and error scenarios

### Debug Tips

1. **Enable test logging**: Use `ITestOutputHelper` in test constructor
2. **Verify mock calls**: Use `Moq.Verify()` to check service interactions
3. **Check database state**: Query `DbContext` after operation
4. **Inspect error messages**: Assert error messages contain expected text
5. **Run single test**: Isolate failing test for easier debugging

## Future Enhancements

### Planned Improvements

1. **Multi-factor OAuth**: Test OAuth with 2FA enabled
2. **Token refresh flows**: Test automatic token refresh
3. **Account unlinking**: Test OAuth account removal
4. **Multiple provider linking**: Test same user with Google + GitHub
5. **Performance tests**: Measure OAuth flow latency

### Test Coverage Gaps

- [ ] OAuth token refresh scenarios
- [ ] Concurrent OAuth attempts for same user
- [ ] OAuth state expiration edge cases (exactly 10 minutes)
- [ ] Large-scale user info responses (>10KB)

## References

- **OAuth 2.0 Spec**: [RFC 6749](https://tools.ietf.org/html/rfc6749)
- **CSRF Protection**: [OWASP CSRF Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- **Moq Documentation**: [Moq GitHub](https://github.com/moq/moq)
- **Moq.Contrib.HttpClient**: [GitHub](https://github.com/maxkagamine/Moq.Contrib.HttpClient)

---

**Maintained by**: MeepleAI Development Team
**Related Issues**: #2456 (E2E Browser Tests), #2457 (JWT Secret Management)

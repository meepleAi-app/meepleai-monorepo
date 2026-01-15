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
**Related Issues**: #2456 (E2E OAuth Flows), #2457 (JWT Secret Management)

## E2E OAuth Flow Tests (Playwright)

**Last Updated**: 2026-01-16
**Issue**: [#2456](https://github.com/anthropics/meepleai-monorepo-dev/issues/2456)
**Test Location**: `apps/web/e2e/oauth-flows.spec.ts`

### Overview

Complete end-to-end tests for OAuth authentication flows using Playwright browser automation. These tests validate the full OAuth journey from authorization URL generation through callback handling and session establishment.

### Test Architecture

**Base Helper**: `AuthHelper` (centralized OAuth mocking utilities)
**Framework**: Playwright + Chromatic fixtures
**Mocking Strategy**: `page.route()` for OAuth endpoint simulation
**Providers**: Google, Discord, GitHub

### Test Suites

#### 1. Google OAuth Flow

**Coverage**:
- ✅ Authorization URL generation with correct Google OAuth parameters
- ✅ Redirect to Google login page (302 response)
- ✅ Successful authentication callback handling
- ✅ User profile creation from Google data
- ✅ Session creation after OAuth success
- ✅ Existing user login (new=false parameter)

**Key Tests**:
```typescript
test('generates correct authorization URL for Google')
test('redirects to Google login page')
test('handles successful Google authentication callback')
test('creates user profile from Google data')
test('creates session after Google OAuth success')
test('handles existing Google user login')
```

#### 2. Discord OAuth Flow

**Coverage**:
- ✅ Authorization URL generation with Discord OAuth parameters
- ✅ Redirect to Discord authorization page
- ✅ Successful authentication callback handling
- ✅ User profile creation from Discord data

**Key Tests**:
```typescript
test('generates correct authorization URL for Discord')
test('redirects to Discord authorization page')
test('handles successful Discord authentication callback')
test('creates user profile from Discord data')
```

#### 3. GitHub OAuth Flow

**Coverage**:
- ✅ Authorization URL generation with GitHub OAuth parameters
- ✅ Redirect to GitHub authorization page
- ✅ Successful authentication callback handling
- ✅ User profile creation from GitHub data

**Key Tests**:
```typescript
test('generates correct authorization URL for GitHub')
test('redirects to GitHub authorization page')
test('handles successful GitHub authentication callback')
test('creates user profile from GitHub data')
```

#### 4. Error Scenarios

**Coverage**:
- ✅ Invalid client ID error handling
- ✅ Invalid client secret error handling (oauth_failed)
- ✅ Invalid state parameter (CSRF protection)
- ✅ User cancels OAuth flow (access_denied)
- ✅ OAuth provider failure (oauth_failed)

**Key Tests**:
```typescript
test('handles invalid client ID error')
test('handles invalid client secret error')
test('handles invalid state parameter (CSRF protection)')
test('handles user cancels OAuth flow')
test('handles OAuth provider failure')
```

#### 5. Session Management

**Coverage**:
- ✅ Session persistence after page reload
- ✅ Session persistence across page navigation
- ✅ Session contains correct user role
- ✅ Session cookie verification

**Key Tests**:
```typescript
test('session persists after page reload (Google)')
test('session persists across page navigation (Discord)')
test('session contains correct user role (GitHub)')
```

#### 6. Cross-Provider Scenarios

**Coverage**:
- ✅ All three providers authenticate successfully
- ✅ Error handling consistent across all providers

**Key Tests**:
```typescript
test('all three providers can authenticate successfully')
test('error handling consistent across all providers')
```

### Mocking Strategy

#### OAuth Login Mock

```typescript
// Mock OAuth initiation (302 redirect to provider)
await authHelper.mockOAuthLogin('google');

// Mocks: GET /api/v1/auth/oauth/google/login
// Returns: 302 redirect to https://accounts.google.com/o/oauth2/v2/auth?...
```

#### OAuth Callback Mock

```typescript
// Mock authenticated session after OAuth callback
const newUser = {
  id: 'oauth-google-new-user-1',
  email: 'newuser.google@gmail.com',
  displayName: 'Google New User',
  role: 'User',
};

await authHelper.mockAuthenticatedSession(newUser);

// Simulate callback page
await page.goto('/auth/callback?success=true&new=true');
```

#### Session Verification

```typescript
// Verify session established
const isAuthenticated = await authHelper.verifyAuthenticated();
expect(isAuthenticated).toBe(true);

// Verify session cookies
const cookies = await authHelper.getSessionCookies();
const sessionCookie = cookies.find(c => c.name === 'meepleai_session');
expect(sessionCookie).toBeDefined();
```

### Running E2E OAuth Tests

#### Run All OAuth E2E Tests

```bash
cd apps/web
pnpm test:e2e oauth-flows.spec.ts
```

#### Run Specific Test Suite

```bash
# Google OAuth tests only
pnpm test:e2e oauth-flows.spec.ts -g "Google OAuth Flow"

# Error scenario tests
pnpm test:e2e oauth-flows.spec.ts -g "OAuth Error Handling"

# Session management tests
pnpm test:e2e oauth-flows.spec.ts -g "OAuth Session Management"
```

#### Run in UI Mode (Interactive Debugging)

```bash
pnpm test:e2e:ui oauth-flows.spec.ts
```

### Test Coverage Metrics

**Target Coverage**:
- ✅ Authorization URL generation: 100% (all 3 providers)
- ✅ Callback handling: 100% (success + error scenarios)
- ✅ User profile creation: 100% (all 3 providers)
- ✅ Session creation: 100% (persistence + verification)
- ✅ Error scenarios: 100% (all 5 error types)

**Actual Coverage**: 100% (34 tests, all passing)

### Test Data Fixtures

```typescript
const OAUTH_USER_FIXTURES = {
  google: {
    new: {
      id: 'oauth-google-new-user-1',
      email: 'newuser.google@gmail.com',
      displayName: 'Google New User',
      role: 'User',
    },
    existing: {
      id: 'oauth-google-existing-user-1',
      email: 'existinguser.google@gmail.com',
      displayName: 'Google Existing User',
      role: 'User',
    },
  },
  // ... Discord, GitHub fixtures
};
```

### Callback URL Parameters

```typescript
const CALLBACK_PARAMS = {
  SUCCESS_NEW_USER: 'success=true&new=true',
  SUCCESS_EXISTING_USER: 'success=true&new=false',
  ERROR_ACCESS_DENIED: 'error=access_denied',
  ERROR_INVALID_STATE: 'error=invalid_state',
  ERROR_OAUTH_FAILED: 'error=oauth_failed',
  ERROR_INVALID_CLIENT: 'error=invalid_client',
};
```

### CI/CD Integration

#### GitHub Actions Workflow

```yaml
- name: Run OAuth E2E Tests
  run: |
    cd apps/web
    pnpm test:e2e oauth-flows.spec.ts
  env:
    NEXT_PUBLIC_API_BASE: http://localhost:8080

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: oauth-e2e-test-results
    path: apps/web/test-results/
```

### Best Practices

#### 1. Use AuthHelper for All OAuth Mocks

```typescript
// ✅ CORRECT - Centralized mocking
await authHelper.mockOAuthLogin('google');
await authHelper.mockAuthenticatedSession(user);

// ❌ WRONG - Inline page.route() duplication
await page.route(`${API_BASE}/api/v1/auth/oauth/google/login`, ...);
```

#### 2. Test All Three Providers

```typescript
// ✅ CORRECT - Test all supported providers
test.describe('Google OAuth Flow', () => { ... });
test.describe('Discord OAuth Flow', () => { ... });
test.describe('GitHub OAuth Flow', () => { ... });

// ❌ WRONG - Only test one provider
test.describe('OAuth Flow', () => { /* only Google */ });
```

#### 3. Verify Session Persistence

```typescript
// ✅ CORRECT - Verify session persists after reload
await page.reload();
const isAuthenticated = await authHelper.verifyAuthenticated();
expect(isAuthenticated).toBe(true);

// ❌ WRONG - Only verify initial session
const isAuthenticated = await authHelper.verifyAuthenticated();
```

#### 4. Test Error Scenarios

```typescript
// ✅ CORRECT - Test all error types
test('handles invalid client ID error');
test('handles invalid state parameter');
test('handles user cancels OAuth flow');

// ❌ WRONG - Only test success path
test('handles successful authentication');
```

### Troubleshooting

#### Common Issues

**Issue**: Tests fail with "Timeout waiting for /dashboard"
**Solution**: Verify AuthHelper mock setup is called before navigation

**Issue**: Session cookie not found
**Solution**: Ensure `authHelper.mockAuthenticatedSession()` is called

**Issue**: OAuth redirect not working
**Solution**: Check `authHelper.mockOAuthLogin()` is set up correctly

**Issue**: Tests fail intermittently
**Solution**: Use `reducedMotion: 'reduce'` in beforeEach, increase timeouts if needed

### Future Enhancements

1. **Visual Regression Tests**: Screenshot comparisons for OAuth buttons
2. **Performance Tests**: Measure OAuth flow latency
3. **Accessibility Tests**: WCAG compliance for OAuth UI
4. **Mobile Testing**: OAuth flows on mobile viewports
5. **Network Failure Tests**: Simulate network issues during OAuth

---

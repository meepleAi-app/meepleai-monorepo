# OAuth 2.0 Security Documentation

**Feature**: AUTH-06 OAuth Providers (Google, Discord, GitHub)
**Status**: Production
**Last Updated**: 2025-12-13T10:59:23.970Z
**Security Classification**: CRITICAL

---

## Executive Summary

This document details the security architecture and implementation of OAuth 2.0 authentication for MeepleAI, covering token encryption, CSRF protection, account linking security, and operational security procedures.

**Threat Model**: OAuth implementation protects against:
- CSRF attacks via state parameter manipulation
- Token theft via encryption at rest
- Account takeover via provider compromise
- OAuth flow abuse via rate limiting
- Session hijacking via secure cookie handling

---

## Security Architecture

### 1. Token Encryption (CRITICAL)

**Implementation**: ASP.NET Core Data Protection API

**Encryption Spec**:
- **Algorithm**: AES-256-CBC with HMAC-SHA256 authentication
- **Key Management**: Managed by Data Protection API (automatic key rotation)
- **Purpose Strings**: `"OAuthTokens"` for OAuth token encryption
- **Storage**: Encrypted tokens stored in `oauth_accounts.access_token_encrypted` and `refresh_token_encrypted`

**Code Reference**:
```csharp
// apps/api/src/Api/Services/EncryptionService.cs:22-42
private const string EncryptionPurpose = "OAuthTokens";

var accessTokenEncrypted = await _encryption.EncryptAsync(
    tokenResponse.AccessToken,
    EncryptionPurpose);
```

**Key Rotation**:
- Data Protection API automatically rotates keys every 90 days
- Old keys retained for 90 days to decrypt existing tokens
- No manual intervention required for key rotation
- **Production Requirement**: Configure persistent key storage (Azure Key Vault, Redis, or file system)

**Security Validation**:
- ✅ Tokens never logged in plaintext
- ✅ Purpose-based encryption prevents cross-context decryption
- ✅ Automatic key rotation with backward compatibility
- ⚠️ **Action Required**: Configure production key storage location

---

### 2. CSRF Protection (CRITICAL)

**Implementation**: OAuth State Parameter

**CSRF Flow**:
1. **State Generation**: 32-byte cryptographically secure random value
   ```csharp
   // apps/api/src/Api/Program.cs:1209
   var state = Convert.ToBase64String(
       System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
   ```

2. **State Storage**: In-memory dictionary with 10-minute expiration
   ```csharp
   // apps/api/src/Api/Services/OAuthService.cs:25-26
   private static readonly Dictionary<string, DateTime> _stateStore = new();
   private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
   ```

3. **Single-Use Enforcement**: State removed immediately after validation
   ```csharp
   // apps/api/src/Api/Services/OAuthService.cs:180-183
   if (now <= expiresAt)
   {
       _stateStore.Remove(state); // Single-use enforcement
       return Task.FromResult(true);
   }
   ```

4. **Validation**: State validated on OAuth callback before token exchange

**Security Properties**:
- ✅ 2^256 possible state values (cryptographically secure)
- ✅ Single-use enforcement prevents replay attacks
- ✅ 10-minute expiration limits attack window
- ✅ Automatic cleanup of expired states
- ⚠️ **Production Limitation**: In-memory storage lost on app restart

**Production Recommendation**:
```csharp
// Migrate to Redis for production
// apps/api/src/Api/Services/OAuthService.cs (future enhancement)
// Use IDistributedCache instead of in-memory dictionary
await _cache.SetStringAsync($"oauth:state:{state}", expiresAt.ToString(),
    new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt });
```

---

### 3. Rate Limiting

**Implementation**: Token bucket algorithm via `IRateLimitService`

**Limits**:
- **OAuth Login**: 10 requests per minute per IP address
- **OAuth Callback**: 10 requests per minute per IP address
- **Rate Limit Key**: `oauth:login:{ipAddress}` or `oauth:callback:{ipAddress}`

**Configuration**:
```json
// appsettings.json
{
  "RateLimit": {
    "OAuth": {
      "MaxTokens": 10,
      "RefillRate": 0.16667  // 10 tokens per minute
    }
  }
}
```

**Code Reference**:
```csharp
// apps/api/src/Api/Program.cs:1192-1206
var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
    $"oauth:login:{ipAddress}",
    oauthRateLimit.MaxTokens,
    oauthRateLimit.RefillRate);

if (!rateLimitResult.Allowed)
{
    context.Response.Headers["Retry-After"] = "60";
    return Results.StatusCode(429); // Too Many Requests
}
```

**Protection Against**:
- ✅ OAuth flow abuse (excessive authorization requests)
- ✅ CSRF brute force attempts (state guessing)
- ✅ Token exchange flooding
- ✅ DoS attacks on OAuth endpoints

---

### 4. Account Linking Security

**Auto-Linking Strategy**: Trust OAuth provider's email verification (MVP)

**Flow**:
1. User authenticates via OAuth provider (Google/Discord/GitHub)
2. System retrieves verified email from provider
3. If user exists with same email → link OAuth account
4. If user doesn't exist → create new user + OAuth account

**Security Assumptions**:
- OAuth provider (Google/Discord/GitHub) has verified the email address
- Provider's OAuth implementation is secure
- Email is unique identifier across users

**Code Reference**:
```csharp
// apps/api/src/Api/Services/OAuthService.cs:98-106
// Check if user exists with same email (auto-link for MVP)
user = await _db.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email.ToLowerInvariant());

if (user == null)
{
    // Create new user
    user = new UserEntity { /* ... */ };
}
else
{
    // Link to existing user
}
```

**Risk Assessment**:
- **Risk**: If OAuth provider is compromised, attacker could link to existing accounts
- **Mitigation**: Trust reputable providers (Google, Discord, GitHub)
- **Future Enhancement**: Optional email verification before linking (AUTH-06-P6)

**Account Takeover Scenarios**:
| Scenario | Risk | Mitigation |
|----------|------|------------|
| Provider compromised | HIGH | Multi-factor auth on provider side |
| Email spoofing | LOW | Providers validate email ownership |
| Account enumeration | LOW | Generic error messages on login |

---

### 5. Session Security

**Session Creation**: After successful OAuth callback

**Flow**:
1. OAuth provider validates user
2. System validates CSRF state
3. System creates session via `AuthService.CreateSessionForUserAsync()`
4. Session cookie set with `HttpOnly`, `Secure`, `SameSite=Strict`

**Code Reference**:
```csharp
// apps/api/src/Api/Program.cs:1247-1258
var authResult = await authService.CreateSessionForUserAsync(
    result.User.Id, sessionIpAddress, userAgent, ct);

// Set session cookie
WriteSessionCookie(context, authResult.SessionToken, authResult.ExpiresAt);
```

**Session Properties**:
- ✅ HttpOnly: JavaScript cannot access session token
- ✅ Secure: Only transmitted over HTTPS
- ✅ SameSite=Strict: CSRF protection
- ✅ IP address tracking: Session hijacking detection
- ✅ User agent tracking: Device fingerprinting

---

### 6. Error Handling

**Principle**: Never expose sensitive information to frontend

**Implementation**:
- **Frontend**: Generic error messages (`error=oauth_failed`, `error=rate_limit`)
- **Backend Logs**: Detailed error messages with stack traces
- **No Token Exposure**: Access/refresh tokens never logged

**Code Reference**:
```csharp
// apps/api/src/Api/Program.cs:1265-1272
catch (Exception ex)
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "OAuth callback failed for provider: {Provider}", provider);

    // Generic error to frontend
    var redirectUrl = $"{frontendUrl}/auth/callback?error=oauth_failed";
    return Results.Redirect(redirectUrl);
}
```

**Error Types**:
| Error | Frontend Message | Backend Log |
|-------|------------------|-------------|
| Invalid state | `error=oauth_failed` | "Invalid OAuth state parameter for provider: {Provider}" |
| Rate limit | `error=rate_limit` | - |
| Token exchange failed | `error=oauth_failed` | "Failed to exchange OAuth code for token. Provider: {Provider}" (HttpRequestException) |
| User info failed | `error=oauth_failed` | "Failed to get user info from OAuth provider. Provider: {Provider}" (HttpRequestException) |

---

### 7. HTTPS Enforcement

**Development**: Localhost exemption (HTTP allowed)

**Production Requirements**:
- ✅ `CallbackBaseUrl` must use HTTPS
- ✅ OAuth provider callback URLs must use HTTPS
- ✅ ASP.NET Core HTTPS redirection middleware enabled

**Configuration**:
```json
// appsettings.Production.json
{
  "Authentication": {
    "OAuth": {
      "CallbackBaseUrl": "https://app.meepleai.com"  // HTTPS required
    }
  }
}
```

**⚠️ Security Gap**: No explicit validation of HTTPS in code

**Recommendation**:
```csharp
// apps/api/src/Api/Services/OAuthService.cs (future enhancement)
private string GetCallbackUrl(string provider)
{
    var baseUrl = _config.CallbackBaseUrl.TrimEnd('/');

    // Production HTTPS enforcement
    if (!_env.IsDevelopment() && !baseUrl.StartsWith("https://"))
    {
        throw new InvalidOperationException(
            "OAuth CallbackBaseUrl must use HTTPS in production");
    }

    return $"{baseUrl}/api/v1/auth/oauth/{provider.ToLowerInvariant()}/callback";
}
```

---

### 8. Audit Logging

**Logged Events**:
- ✅ OAuth login attempts (all providers)
- ✅ OAuth callback success/failure
- ✅ Account creation via OAuth
- ✅ Account linking via OAuth
- ✅ OAuth account unlinking
- ✅ Token refresh attempts
- ✅ Rate limit violations

**Log Level Mapping**:
```csharp
// apps/api/src/Api/Services/OAuthService.cs
_logger.LogDebug("Generated OAuth authorization URL");        // Line 59
_logger.LogWarning("Invalid OAuth state parameter");          // Line 74
_logger.LogInformation("OAuth login for existing account");   // Line 96
_logger.LogInformation("Created new user via OAuth");         // Line 115
_logger.LogInformation("Linking OAuth to existing user");     // Line 119
_logger.LogError(ex, "Failed to exchange OAuth code");        // Line 296
```

**Audit Trail**:
- All logs include timestamp, provider, user ID (if applicable)
- Errors include exception details and stack traces
- IP addresses logged for rate limit enforcement
- User agents logged for session tracking

---

## Security Checklist (Production Deployment)

### Pre-Deployment

- [ ] **Key Storage**: Configure Data Protection key persistence (Azure Key Vault, Redis, or file system)
- [ ] **HTTPS Enforcement**: Verify `CallbackBaseUrl` uses HTTPS
- [ ] **OAuth Apps**: Register OAuth apps with production callback URLs
- [ ] **Environment Variables**: Set `GOOGLE_OAUTH_CLIENT_SECRET`, `DISCORD_OAUTH_CLIENT_SECRET`, `GITHUB_OAUTH_CLIENT_SECRET`
- [ ] **Rate Limiting**: Verify rate limits configured in `appsettings.Production.json`
- [ ] **State Storage**: Consider migrating from in-memory to Redis for multi-instance deployments

### Post-Deployment

- [ ] **Token Encryption**: Verify tokens encrypted in database (`AccessTokenEncrypted` column)
- [ ] **CSRF Protection**: Test invalid state parameter returns error
- [ ] **Rate Limiting**: Test 11th request returns 429 status
- [ ] **Error Handling**: Verify no sensitive data in frontend error messages
- [ ] **Session Creation**: Verify session cookie set with `HttpOnly`, `Secure`, `SameSite=Strict`
- [ ] **Audit Logs**: Verify OAuth events logged in Seq/Jaeger
- [ ] **Token Refresh**: Test refresh token mechanism for Google/Discord

---

## Incident Response Procedures

### Compromised OAuth Provider

**Scenario**: OAuth provider (Google/Discord/GitHub) reports security breach

**Actions**:
1. **Immediate**: Revoke all OAuth tokens via provider's admin console
2. **Within 1 hour**: Force re-authentication for all users with linked accounts
3. **Within 24 hours**: Audit all account linking events during breach window
4. **Communication**: Notify affected users via email

**Code**:
```sql
-- Force re-auth by deleting OAuth accounts for compromised provider
DELETE FROM oauth_accounts WHERE provider = 'google';  -- Example: Google breach
```

### Token Leakage

**Scenario**: Encrypted tokens exposed (database dump, backup leak)

**Actions**:
1. **Immediate**: Rotate Data Protection keys
   ```bash
   # Force key rotation (generates new key, retires old ones)
   dotnet run --rotate-keys
   ```
2. **Within 1 hour**: Decrypt and re-encrypt all tokens with new key
3. **Within 24 hours**: Revoke all OAuth tokens via provider APIs
4. **Communication**: Security advisory to users

### CSRF Attack Detected

**Scenario**: Large volume of invalid state parameters in logs

**Actions**:
1. **Immediate**: Review rate limit configuration
2. **Within 1 hour**: Increase rate limits if legitimate traffic
3. **Within 24 hours**: Analyze attack patterns (IP addresses, timing)
4. **Mitigation**: Add IP-based blocking for repeat offenders

---

## Security Testing

### Automated Tests

**Test Coverage** (23 tests total):
- ✅ `EncryptionServiceTests.cs`: 10 tests (encrypt, decrypt, purposes, error handling)
- ✅ `OAuthServiceTests.cs`: 13 tests (URL generation, callback, state validation, account linking)

**Security Test Cases**:
```csharp
// apps/api/tests/Api.Tests/OAuthServiceTests.cs
[Fact] public async Task ValidateStateAsync_InvalidState_ReturnsFalse()
[Fact] public async Task ValidateStateAsync_ExpiredState_ReturnsFalse()
[Fact] public async Task ValidateStateAsync_ValidState_RemovesStateAfterValidation()
[Fact] public async Task HandleCallbackAsync_InvalidState_ThrowsUnauthorizedAccessException()
```

### Manual Security Testing

**Penetration Testing Checklist**:
- [ ] **CSRF**: Attempt state parameter manipulation
- [ ] **Token Theft**: Verify tokens encrypted in database
- [ ] **Rate Limit Bypass**: Send >10 requests per minute
- [ ] **Account Takeover**: Attempt linking with unverified email
- [ ] **Session Hijacking**: Attempt cookie theft/replay
- [ ] **Error Enumeration**: Verify generic error messages

---

## Known Limitations

### 1. State Storage (In-Memory)

**Limitation**: State dictionary lost on app restart, doesn't scale across instances

**Impact**:
- Users mid-OAuth flow will get "Invalid state" error after app restart
- Multi-instance deployments will fail (state stored on different server)

**Mitigation**: Migrate to Redis for production (AUTH-06-P7)

### 2. Auto-Linking Without Verification

**Limitation**: Trusts OAuth provider's email verification

**Impact**: If provider compromised, attacker could link to existing accounts

**Mitigation**:
- Use reputable providers (Google, Discord, GitHub)
- Consider optional email verification (AUTH-06-P6)

### 3. No Token Expiry Validation

**Limitation**: Tokens not validated before use (relies on provider's TTL)

**Impact**: Expired tokens may cause API calls to fail

**Mitigation**: Implement proactive token refresh before expiry (AUTH-06-P8)

---

## Security Contacts

**Security Issues**: Report to security@meepleai.com
**OAuth Provider Incidents**:
- Google: https://security.google.com/settings/security/securitycheckup
- Discord: https://support.discord.com/hc/en-us/requests/new
- GitHub: https://support.github.com/contact/security

---

## References

- [OAuth 2.0 RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749)
- [OAuth 2.0 Security Best Practices](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)
- [ASP.NET Core Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/)
- [OWASP OAuth 2.0 Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/OAuth2_Cheat_Sheet.html)

---

**Document Status**: ✅ COMPLETE
**Security Review**: ✅ PASSED (2025-10-27)
**Next Review**: 2026-01-27 (Quarterly)


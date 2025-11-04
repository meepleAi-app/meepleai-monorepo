# [SECURITY] Security Improvements: CORS Configuration & JSON Deserialization

## Priority: P3 (Low)
## Severity: INFO
## Category: Security Hardening
## CWE: CWE-942 (CORS), CWE-502 (Deserialization), CWE-532 (Logging)
## OWASP: A01:2021, A08:2021, A09:2021

---

## Executive Summary

This issue tracks low-priority security improvements to further harden the MeepleAI codebase:
1. **CORS Configuration:** Explicitly define allowed headers instead of `AllowAnyHeader()`
2. **JSON Deserialization:** Add validation after deserializing external API responses
3. **Logging:** Reduce token hash length in production logs

These are **not critical vulnerabilities** but represent security best practices for defense-in-depth.

---

## 1. CORS Configuration Improvements

### Current Implementation

**File:** `apps/api/src/Api/Program.cs:180`

```csharp
policy.AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

### Issue

While origins are properly restricted (`WithOrigins()`), allowing **all headers** could theoretically expose the API to custom header-based attacks. However, this risk is mitigated by:
- ✅ Origins are restricted to `localhost:3000` (dev) or configured domains (prod)
- ✅ Authorization is properly enforced at the endpoint level
- ✅ No known exploits targeting this configuration

### Risk Level
- **Likelihood:** Very Low
- **Impact:** Low
- **Overall Risk:** LOW (informational)

### Recommended Fix

Be explicit about allowed headers:

```csharp
// apps/api/src/Api/Program.cs:173-183

var allowedOrigins = isDevelopment
    ? new[] { "http://localhost:3000" }
    : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithHeaders(
                  "Content-Type",
                  "Authorization",
                  "X-API-Key",
                  "X-Correlation-Id",
                  "Accept",
                  "Origin"
              )
              .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
              .AllowCredentials()
              .SetIsOriginAllowedToAllowWildcardSubdomains(); // If needed for *.example.com
    });
});
```

### Alternative: Keep Current Config (Also Acceptable)

The current `AllowAnyHeader()` is acceptable if:
- Origins remain restricted (✅ already implemented)
- Authorization is enforced at endpoints (✅ already implemented)
- No custom security headers are used by API

**Decision:** Document the trade-off and choose based on security posture preference.

---

## 2. JSON Deserialization Validation

### Current Implementation

Multiple services deserialize JSON from external APIs without explicit validation:

**Examples:**
- `LlmService.cs:160, 343, 400` - OpenRouter API responses
- `OAuthService.cs:286, 328, 398, 572` - OAuth provider responses
- `N8nTemplateService.cs:78, 148, 256, 428` - n8n API responses
- `PromptEvaluationService.cs:91, 849` - Evaluation results

```csharp
// Current pattern:
var response = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody);
// Missing: null check, structure validation
```

### Issue

Deserialization without validation can lead to:
- **NullReferenceException** if API returns unexpected format
- **Logic errors** if required fields are missing
- **Security issues** if data structure is manipulated (low risk with typed deserialization)

### Risk Level
- **Likelihood:** Low (external APIs are trusted)
- **Impact:** Medium (application crash, incorrect behavior)
- **Overall Risk:** LOW (informational)

### Recommended Improvements

#### Pattern 1: Post-Deserialization Validation

```csharp
// LlmService.cs example:
var chatResponse = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody);

if (chatResponse == null)
{
    throw new ApplicationException("OpenRouter API returned null response");
}

if (string.IsNullOrEmpty(chatResponse.Id) || chatResponse.Choices == null || chatResponse.Choices.Count == 0)
{
    _logger.LogError("Invalid OpenRouter response structure: {Response}", responseBody);
    throw new ApplicationException("OpenRouter API returned invalid response structure");
}

// Continue processing...
```

#### Pattern 2: Try-Catch with Specific Handling

```csharp
// OAuthService.cs example:
try
{
    var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(jsonResponse);

    if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
    {
        throw new InvalidOperationException("OAuth provider returned invalid token response");
    }

    return tokenResponse;
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Failed to deserialize OAuth token response from {Provider}: {Response}",
        provider, jsonResponse);
    throw new ApplicationException($"Invalid response from {provider} OAuth provider", ex);
}
```

#### Pattern 3: FluentValidation (Optional)

For complex validation logic:

```csharp
// Create validator:
public class OpenRouterChatResponseValidator : AbstractValidator<OpenRouterChatResponse>
{
    public OpenRouterChatResponseValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Response ID is required");
        RuleFor(x => x.Choices).NotNull().NotEmpty().WithMessage("Response must contain at least one choice");
        RuleFor(x => x.Choices[0].Message).NotNull().WithMessage("Message content is required");
    }
}

// Usage:
var response = JsonSerializer.Deserialize<OpenRouterChatResponse>(responseBody);
var validator = new OpenRouterChatResponseValidator();
var validationResult = validator.Validate(response);

if (!validationResult.IsValid)
{
    _logger.LogError("Invalid OpenRouter response: {Errors}", validationResult.Errors);
    throw new ApplicationException("Invalid response from OpenRouter API");
}
```

---

## 3. Logging Sensitive Data (Token Hashes)

### Current Implementation

**File:** `apps/api/src/Api/Services/SessionCacheService.cs`
**Lines:** Multiple (39, 43, 49, 54, 59, 64, 86, 95, 100, 104, 108, 122, 127, 131, 135)

```csharp
_logger.LogDebug("Session cache miss for hash: {TokenHash}", tokenHash.Substring(0, 8));
_logger.LogDebug("Session cache hit for hash: {TokenHash}", tokenHash.Substring(0, 8));
```

### Issue

The service logs the first **8 characters** of SHA-256 token hashes. While truncated, this could theoretically:
- Reduce brute-force search space (8 hex chars = 32 bits)
- Leak partial token information if debug logs are exposed

However, the risk is **very low** because:
- ✅ Original 32-byte token is never logged
- ✅ Only 8 out of 64 hex characters are logged
- ✅ Debug logs should not be exposed in production
- ✅ Tokens are already hashed (SHA-256)

### Risk Level
- **Likelihood:** Very Low
- **Impact:** Very Low
- **Overall Risk:** VERY LOW (best practice improvement)

### Recommended Fix

Reduce to 4-6 characters in production:

```csharp
// SessionCacheService.cs - Use conditional logging length

private int GetLogHashLength()
{
    // 8 chars in development, 4 chars in production
    return _environment.IsDevelopment() ? 8 : 4;
}

// Update all logging calls:
_logger.LogDebug("Session cache miss for hash: {TokenHash}",
    tokenHash.Substring(0, GetLogHashLength()));
```

**Alternative:** Use LoggerMessage source generators for performance:

```csharp
public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Session cache miss for hash: {TokenHash}")]
    public static partial void LogSessionCacheMiss(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] string tokenHash);
}

// Usage:
_logger.LogSessionCacheMiss(tokenHash.Substring(0, 4));
```

---

## Implementation Priority

### Phase 1: CORS Headers (2 hours) - Optional
1. Update `Program.cs` CORS configuration
2. Test with frontend (`pnpm dev`)
3. Verify preflight OPTIONS requests
4. Document allowed headers in API documentation

### Phase 2: JSON Deserialization Validation (4-6 hours) - Recommended
5. Identify high-risk deserialization points (external APIs)
6. Add null checks and structure validation
7. Add try-catch with specific error messages
8. Add unit tests for validation logic
9. Update error handling in calling code

### Phase 3: Logging Improvements (1 hour) - Optional
10. Reduce token hash log length to 4-6 characters
11. Add environment-based conditional logging
12. Review other sensitive data logging patterns
13. Update logging documentation

---

## Testing Strategy

### CORS Testing

```bash
# Test 1: Verify allowed headers
curl -X OPTIONS http://localhost:8080/api/v1/games \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: Content-Type,X-Custom-Header" \
  --include

# Expected: Access-Control-Allow-Headers should list specific headers

# Test 2: Verify credentials support
curl -X GET http://localhost:8080/api/v1/users/me \
  -H "Origin: http://localhost:3000" \
  -H "Cookie: meeple_session=..." \
  --include

# Expected: Access-Control-Allow-Credentials: true
```

### Deserialization Testing

```csharp
// Unit test example:
[Fact]
public async Task LlmService_Should_Throw_On_Invalid_Response()
{
    // Arrange
    var invalidJson = "{}"; // Missing required fields
    var mockHttpClient = CreateMockHttpClient(invalidJson);

    // Act & Assert
    await Assert.ThrowsAsync<ApplicationException>(async () =>
    {
        await _llmService.SendChatRequestAsync("test prompt");
    });
}

[Fact]
public async Task OAuthService_Should_Validate_Token_Response()
{
    // Arrange
    var invalidJson = "{\"access_token\": \"\"}"; // Empty access token
    var mockHttpClient = CreateMockHttpClient(invalidJson);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _oAuthService.HandleCallbackAsync("google", "code", "state");
    });
}
```

### Logging Testing

```bash
# Run application with debug logging
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_LOGGING__LOGLEVEL__DEFAULT=Debug
dotnet run

# Verify log output:
grep "Session cache" logs.txt
# Expected: Token hashes truncated to 4-8 characters
```

---

## Impact Assessment

### Security Impact
- ✅ Further hardens CORS configuration (defense-in-depth)
- ✅ Improves error handling and application resilience
- ✅ Reduces information leakage in logs
- ⚠️ No critical vulnerabilities addressed (all LOW risk)

### Operational Impact
- ✅ Better error messages for deserialization failures
- ✅ Easier debugging with validated responses
- ⚠️ Slightly more verbose code (validation logic)

### Performance Impact
- ⚠️ Negligible (<1ms per request for validation)
- ✅ Improves reliability (fail-fast on invalid data)

---

## Definition of Done

### CORS Improvements
- [ ] CORS configuration updated with explicit headers (if chosen)
- [ ] Frontend tested with new CORS policy
- [ ] API documentation updated
- [ ] Preflight OPTIONS requests verified

### JSON Deserialization
- [ ] High-risk deserialization points identified (10-15 locations)
- [ ] Validation added to external API responses
- [ ] Try-catch with specific error handling
- [ ] Unit tests added for validation logic (90% coverage)
- [ ] Error messages documented

### Logging Improvements
- [ ] Token hash log length reduced to 4-6 characters
- [ ] Environment-based conditional logging implemented
- [ ] Sensitive data logging patterns reviewed
- [ ] Logging documentation updated

### Overall
- [ ] Code review approved
- [ ] Security scan re-run (Semgrep confirms improvements)
- [ ] Deployed to staging and tested
- [ ] Documentation updated

---

## Effort Estimate
- **CORS Headers:** 2 hours (optional)
- **JSON Deserialization:** 4-6 hours (recommended)
- **Logging Improvements:** 1 hour (optional)
- **Total:** 7-9 hours (or 4-6 hours for deserialization only)

---

## References

- **CORS Best Practices:** https://cheatsheetseries.owasp.org/cheatsheets/Cross-Origin_Resource_Sharing_(CORS)_Cheat_Sheet.html
- **CWE-942 (CORS):** https://cwe.mitre.org/data/definitions/942.html
- **CWE-502 (Deserialization):** https://cwe.mitre.org/data/definitions/502.html
- **CWE-532 (Logging):** https://cwe.mitre.org/data/definitions/532.html
- **ASP.NET Core CORS:** https://learn.microsoft.com/en-us/aspnet/core/security/cors
- **System.Text.Json Security:** https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/security-considerations

---

## Related Issues
- #264 - SEC-04: Security Audit Implementation
- #307 - SEC-03: Security Scanning Pipeline
- SECURITY-01 - XSS Vulnerability in Editor (P1)
- SECURITY-02 - Hardcoded Credentials (P2)

---

**Detected by:** Semgrep Security Analysis
**Report Date:** 2025-11-04
**Assignee:** TBD
**Labels:** `security`, `hardening`, `p3-low`, `backend`, `cors`, `deserialization`, `logging`

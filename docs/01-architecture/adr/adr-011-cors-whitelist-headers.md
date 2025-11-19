# ADR-011: CORS Header Whitelist Strategy

**Status**: Proposed
**Date**: 2025-01-19
**Deciders**: Engineering Lead, Security Team
**Context**: Code Review - Backend-Frontend Interactions Security Hardening

---

## Context

During the code review of backend-frontend interactions (2025-01-19), a security vulnerability was identified in the CORS configuration:

**Current Implementation** (`Program.cs:214` and `WebApplicationExtensions.cs:130`):
```csharp
policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
```

**Problem**: `AllowAnyHeader()` permits **any custom HTTP header**, creating a potential attack vector:
1. Malicious headers can bypass security controls
2. Custom headers can leak sensitive information
3. Broader attack surface for header injection attacks
4. Non-compliance with principle of least privilege

**Industry Best Practice**:
- OWASP: Explicitly whitelist allowed headers
- Mozilla Web Security: "Never use AllowAnyHeader with credentials"
- CORS Security Cheat Sheet: "Minimize allowed headers"

**Current Risk Level**: **Medium-High**
- No active exploits observed
- But preventable attack surface
- Security audit red flag

---

## Decision

Replace `AllowAnyHeader()` with **explicit header whitelist** for all CORS policies.

### Allowed Headers

```csharp
policy
    .WithHeaders(
        "Content-Type",        // Required for JSON requests
        "Authorization",       // Required for auth (cookie fallback)
        "X-Correlation-ID",    // Required for distributed tracing
        "X-API-Key"            // Required for API key authentication
    )
    .AllowAnyMethod()
    .AllowCredentials();
```

### Justification for Each Header

| Header | Purpose | Required By | Risk if Blocked |
|--------|---------|-------------|-----------------|
| `Content-Type` | JSON/form data encoding | All endpoints | ❌ All requests fail |
| `Authorization` | Bearer tokens, Basic auth | Auth endpoints | ❌ Auth broken |
| `X-Correlation-ID` | Request tracing, debugging | Observability | ⚠️ Logs incomplete |
| `X-API-Key` | Programmatic API access | API key auth | ❌ CLI/scripts fail |

**Non-Standard Headers** (explicitly blocked):
- `X-Custom-*`: No custom headers allowed
- `X-Debug-*`: Security risk (information leakage)
- `X-Admin-*`: Privilege escalation risk

---

## Architecture

### Before (Vulnerable)
```
┌─────────────────────────────────────────┐
│ Frontend Request                        │
│ Headers: Content-Type, X-Evil-Header    │  ← Any header allowed
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ CORS Middleware                         │
│ AllowAnyHeader() → PASS ✅              │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ Backend Endpoint                        │
│ Receives X-Evil-Header (attack vector)  │
└─────────────────────────────────────────┘
```

### After (Secure)
```
┌─────────────────────────────────────────┐
│ Frontend Request                        │
│ Headers: Content-Type, X-Evil-Header    │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│ CORS Middleware                         │
│ WithHeaders([whitelist]) → REJECT ❌    │  ← X-Evil-Header blocked
└──────────────┬──────────────────────────┘
               │
               ▼ (only whitelisted headers pass)
┌─────────────────────────────────────────┐
│ Backend Endpoint                        │
│ Receives only: Content-Type             │
└─────────────────────────────────────────┘
```

---

## Implementation

### Location
- `apps/api/src/Api/Program.cs` (line 214)
- `apps/api/src/Api/Extensions/WebApplicationExtensions.cs` (line 130)

### Code Changes

**Before**:
```csharp
options.AddPolicy("web", policy =>
{
    policy.WithOrigins(configuredOrigins)
          .AllowAnyHeader()        // ← Remove this
          .AllowAnyMethod()
          .AllowCredentials();
});
```

**After**:
```csharp
options.AddPolicy("web", policy =>
{
    policy.WithOrigins(configuredOrigins)
          .WithHeaders(            // ← Explicit whitelist
              "Content-Type",
              "Authorization",
              "X-Correlation-ID",
              "X-API-Key"
          )
          .AllowAnyMethod()
          .AllowCredentials();
});
```

### Configuration Support

**appsettings.json**:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://meepleai.dev"
    ],
    "AllowedHeaders": [
      "Content-Type",
      "Authorization",
      "X-Correlation-ID",
      "X-API-Key"
    ],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
  }
}
```

**Dynamic Loading**:
```csharp
var allowedHeaders = configuration
    .GetSection("Cors:AllowedHeaders")
    .Get<string[]>() ?? new[] {
        "Content-Type",
        "Authorization",
        "X-Correlation-ID",
        "X-API-Key"
    };

policy.WithHeaders(allowedHeaders)
      .AllowAnyMethod()
      .AllowCredentials();
```

---

## Consequences

### Positive

✅ **Security Hardening**:
- Reduced attack surface (no arbitrary headers)
- Compliance with OWASP CORS guidelines
- Explicit documentation of allowed headers

✅ **Auditability**:
- Clear inventory of permitted headers
- Easy to review in code/config
- Security scan improvements

✅ **Zero Performance Impact**:
- Same CORS preflight handling
- No additional overhead

✅ **Maintainability**:
- Explicit is better than implicit
- Self-documenting code
- Easier to review in PRs

### Negative

⚠️ **Breaking Changes (if any)**:
- If frontend uses non-standard headers → requests fail
- **Mitigation**: Test all endpoints before merge

⚠️ **Future Header Additions**:
- Need code change to add new headers
- **Mitigation**: Configuration-based (appsettings.json)

### Risks

🟡 **Frontend Breakage**:
- **Risk**: Unknown custom headers used by frontend
- **Mitigation**: Comprehensive testing + grep for custom headers
- **Testing**: `grep -r "X-" apps/web/src/` to find custom headers

🟢 **CORS Preflight Complexity**:
- **Risk**: Preflight requests fail for new headers
- **Mitigation**: Test OPTIONS requests explicitly

---

## Validation Plan

### Pre-Merge Testing

1. **Grep for Custom Headers**:
```bash
# Search frontend for custom headers
grep -r "X-" apps/web/src/lib/api/
grep -r "headers\[" apps/web/src/
```

2. **Test All Endpoints**:
```bash
# Test with allowed headers
curl -H "Content-Type: application/json" \
     -H "X-Correlation-ID: test-123" \
     http://localhost:5080/api/v1/games

# Test with non-allowed header (should fail preflight)
curl -H "X-Custom-Header: evil" \
     http://localhost:5080/api/v1/games
```

3. **CORS Preflight Tests**:
```bash
# OPTIONS request should include allowed headers
curl -X OPTIONS \
     -H "Origin: http://localhost:3000" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: Content-Type,X-Correlation-ID" \
     http://localhost:5080/api/v1/auth/login
```

### Integration Tests

```csharp
[Fact]
public async Task CorsPolicy_ShouldAllowWhitelistedHeaders()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
    request.Headers.Add("Origin", "http://localhost:3000");
    request.Headers.Add("Access-Control-Request-Method", "GET");
    request.Headers.Add("Access-Control-Request-Headers", "Content-Type,X-Correlation-ID");

    // Act
    var response = await _client.SendAsync(request);

    // Assert
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Contains("Content-Type", response.Headers.GetValues("Access-Control-Allow-Headers"));
    Assert.Contains("X-Correlation-ID", response.Headers.GetValues("Access-Control-Allow-Headers"));
}

[Fact]
public async Task CorsPolicy_ShouldRejectNonWhitelistedHeaders()
{
    // Arrange
    var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
    request.Headers.Add("Origin", "http://localhost:3000");
    request.Headers.Add("Access-Control-Request-Method", "GET");
    request.Headers.Add("Access-Control-Request-Headers", "X-Evil-Header");

    // Act
    var response = await _client.SendAsync(request);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Preflight succeeds but header not allowed
    Assert.DoesNotContain("X-Evil-Header", response.Headers.GetValues("Access-Control-Allow-Headers"));
}
```

---

## Alternatives Considered

### Alternative 1: Keep AllowAnyHeader + Input Validation
**Description**: Keep permissive CORS, validate headers in middleware

**Pros**:
- No frontend changes needed
- More flexible

**Cons**:
- Defense in depth violation (trust CORS layer)
- Extra validation overhead
- Harder to audit

**Decision**: Rejected - Prefer prevention over detection

### Alternative 2: Stricter Whitelist (Content-Type only)
**Description**: Allow only Content-Type header

**Pros**:
- Maximum security
- Minimal attack surface

**Cons**:
- Breaks authentication (no Authorization header)
- Breaks tracing (no X-Correlation-ID)
- Breaks API keys (no X-API-Key)

**Decision**: Rejected - Too restrictive, breaks functionality

### Alternative 3: Per-Endpoint CORS Policies
**Description**: Different header whitelists per endpoint

**Pros**:
- Fine-grained control
- Principle of least privilege

**Cons**:
- Complex to maintain
- Hard to test
- Overkill for current needs

**Decision**: Deferred to Phase 2 if needed

---

## Rollout Plan

### Phase 1: Code Changes (Sprint 1)
1. ✅ Update Program.cs CORS policy
2. ✅ Update WebApplicationExtensions.cs
3. ✅ Add configuration support
4. ✅ Update tests

### Phase 2: Validation (Sprint 1)
5. ✅ Grep frontend for custom headers
6. ✅ Test all endpoints manually
7. ✅ Run integration test suite
8. ✅ CORS preflight tests

### Phase 3: Documentation (Sprint 1)
9. ✅ Update API documentation
10. ✅ docs/03-api/cors-configuration.md
11. ✅ This ADR
12. ✅ SECURITY.md

### Phase 4: Deployment (Sprint 1)
13. ⏳ Deploy to staging
14. ⏳ Monitor for CORS errors (1 week)
15. ⏳ Deploy to production

---

## Monitoring

**Metrics**:
```
cors_preflight_requests_total{status, origin}
cors_blocked_headers_total{header_name}
```

**Alerts**:
- CORS preflight failure rate >1%
- Blocked header attempts detected

**Logging**:
```csharp
if (request.Headers.Contains("X-Custom-Header"))
{
    _logger.LogWarning(
        "Blocked non-whitelisted CORS header: {Header} from {Origin}",
        "X-Custom-Header",
        request.Headers.Origin
    );
}
```

---

## Related Decisions

- **ADR-010**: Security Headers Middleware (companion security fix)
- **ADR-006**: Multi-Layer Validation (defense in depth)

---

## References

- [OWASP CORS Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Origin_Resource_Sharing_Cheat_Sheet.html)
- [MDN CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [Mozilla Web Security Guidelines](https://infosec.mozilla.org/guidelines/web_security)

---

**Decision Maker**: Engineering Lead
**Approval**: Pending Security Team Review
**Implementation**: Issue #TBD

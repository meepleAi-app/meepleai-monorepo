# 🔒 [Security] CORS Whitelist Headers (Remove AllowAnyHeader)

**Priority**: 🔴 CRITICAL
**Complexity**: Low
**Estimated Time**: 2-3 hours
**Dependencies**: None

## 🎯 Objective

Replace `AllowAnyHeader()` CORS policy with explicit whitelist of allowed headers to reduce attack surface.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Current Issue**: `Program.cs:214` uses `AllowAnyHeader()` which permits any custom header
**Risk**: Potential attack vector with malicious custom headers
**Impact**: Medium-High - Reduces CORS-based attack surface

## 🔧 Current Configuration

**Location**: `apps/api/src/Api/Program.cs:214` and `WebApplicationExtensions.cs:130`

```csharp
policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
```

## ✅ Proposed Configuration

```csharp
policy
    .WithHeaders(
        "Content-Type",
        "Authorization",
        "X-Correlation-ID",
        "X-API-Key"
    )
    .AllowAnyMethod()
    .AllowCredentials();
```

## ✅ Task Checklist

### Implementation
- [ ] Modify `apps/api/src/Api/Program.cs` (line 214)
- [ ] Modify `apps/api/src/Api/Extensions/WebApplicationExtensions.cs` (line 130)
- [ ] Replace `AllowAnyHeader()` with `WithHeaders(...)`
- [ ] Add configuration option in `appsettings.json` for allowed headers
- [ ] Update CORS documentation

### Testing
- [ ] Test legitimate requests with allowed headers succeed
- [ ] Test requests with non-whitelisted headers are rejected
- [ ] Test CORS preflight requests
- [ ] Test API key authentication header
- [ ] Test correlation ID header
- [ ] Integration tests for all endpoints

### Documentation
- [ ] Update `docs/03-api/cors-configuration.md`
- [ ] Document allowed headers in API specification
- [ ] Add migration guide for custom headers (if any)
- [ ] Update `SECURITY.md` with CORS policy

### Validation
- [ ] Manual test with Postman/curl with custom headers
- [ ] Browser DevTools network tab verification
- [ ] Security scan for CORS misconfiguration

## 📁 Files to Modify

```
apps/api/src/Api/Program.cs (MODIFY - line 214)
apps/api/src/Api/Extensions/WebApplicationExtensions.cs (MODIFY - line 130)
apps/api/tests/Api.Tests/Integration/CorsTests.cs (MODIFY/ADD)
docs/03-api/cors-configuration.md (UPDATE)
SECURITY.md (UPDATE)
```

## 🔗 References

- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [OWASP CORS Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Origin_Resource_Sharing_Cheat_Sheet.html)
- [MDN CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)

## 📊 Acceptance Criteria

- ✅ Only whitelisted headers allowed
- ✅ All existing functionality works (auth, correlation ID, etc.)
- ✅ CORS preflight requests handled correctly
- ✅ Test coverage >= 95%
- ✅ Documentation updated
- ✅ No breaking changes to frontend

## 🏷️ Labels

`priority: critical`, `type: security`, `area: backend`, `effort: small`, `sprint: 1`

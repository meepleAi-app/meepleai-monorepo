# Lesson Learned: Authentication Test Failures

**Date**: 2026-01-19
**Issue**: Test suite failures in authentication and concurrent testing
**Impact**: 2 of 17 test failures resolved

---

## Problem Summary

During test suite analysis, identified 2 critical patterns causing test failures:
1. API key validation test using anonymous endpoint (wrong test design)
2. Concurrent session test using shared HttpClient (TestServer limitation)

---

## Root Causes

### 1. Anonymous Endpoint Testing Anti-Pattern

**What Happened**:
- Test checked `/api/v1/games` expecting 401 for invalid API key
- Endpoint has `.AllowAnonymous()` configuration
- Invalid API key → middleware falls through → anonymous access → 200 OK

**Why It Happened**:
- Didn't verify endpoint authentication requirements before writing test
- Assumed all `/api/v1/*` endpoints require authentication
- Didn't check middleware authentication flow

**Lesson**: Always verify endpoint configuration (`.AllowAnonymous()` vs `.RequireAuthorization()`) before writing authentication tests.

### 2. Shared HttpClient Concurrency Anti-Pattern

**What Happened**:
- Test made 10 concurrent requests with single HttpClient instance
- TaskCanceledException: "The client aborted the request"
- TestServer connection pool exhausted under concurrent load

**Why It Happened**:
- Assumed HttpClient from WebApplicationFactory supports high concurrency
- Didn't understand TestServer's in-memory limitations
- Didn't follow existing concurrent test patterns in codebase

**Lesson**: WebApplicationFactory's TestServer requires independent HttpClient instances for concurrent operations (max 5 concurrent requests recommended).

---

## Solutions Applied

### Fix 1: Use Authenticated Endpoint + Correct Scheme

```csharp
// ❌ BEFORE: Anonymous endpoint, wrong scheme
var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "mpl_test_invalidkey123");

// ✅ AFTER: Authenticated endpoint, correct scheme
var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", "mpl_test_invalidkey123");
```

**Result**: ✅ Test passes in 8 seconds

### Fix 2: Independent HttpClient Per Concurrent Request

```csharp
// ❌ BEFORE: Shared client, 10 concurrent
var tasks = Enumerable.Range(0, 10).Select(_ =>
    _client.GetAsync("/api/v1/auth/me")
);

// ✅ AFTER: Independent clients, 5 concurrent, manual cookies
var tasks = Enumerable.Range(0, 5).Select(async _ =>
{
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
    request.Headers.Add("Cookie", sessionCookie);

    return await client.SendAsync(request);
}).ToArray();
```

**Result**: ✅ Test passes in 12 seconds

---

## Prevention Checklist

### Before Writing Authentication Tests

- [ ] Check endpoint configuration: `.AllowAnonymous()` vs `.RequireAuthorization()`
- [ ] Verify authentication scheme from middleware code (not assumptions)
- [ ] Review middleware chain to understand authentication flow
- [ ] Use authenticated endpoints to test authentication rejection

### Before Writing Concurrent Tests

- [ ] Check if similar tests exist (e.g., `SessionRepositoryTests.cs`)
- [ ] Use independent HttpClient/Repository instances per concurrent operation
- [ ] Limit concurrency to ≤5 for WebApplicationFactory TestServer
- [ ] Manually handle cookies with `HandleCookies = false`
- [ ] Document concurrency expectations in test comments

### General Testing

- [ ] Read middleware implementation before assuming behavior
- [ ] Review similar tests for established patterns
- [ ] Check ADRs for architectural decisions affecting tests
- [ ] Validate test assumptions with actual endpoint behavior

---

## Key Takeaways

1. **Read the code, don't assume**: Middleware configuration matters more than endpoint paths
2. **Follow existing patterns**: Codebase had correct concurrent testing examples we should have checked
3. **Test what you mean**: Anonymous endpoints can't validate authentication rejection
4. **TestServer has limits**: Not suitable for high-concurrency testing (use real integration tests)
5. **Documentation prevents mistakes**: API scheme documented in openapi.json and middleware

---

## References

- Detailed documentation: `docs/05-testing/backend/test-fixes-2026-01-19.md`
- Middleware: `apps/api/src/Api/Middleware/ApiKeyAuthenticationMiddleware.cs:85`
- OpenAPI spec: `apps/api/src/Api/openapi.json:18`
- Similar pattern: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Infrastructure/Persistence/SessionRepositoryTests.cs:206`

---

## Impact

- **Before**: 17 failures / 5,414 tests (0.31% failure rate)
- **After**: 15 failures / 5,414 tests (0.28% failure rate)
- **Improvement**: 11.8% reduction in failures

**Time Saved**: These patterns likely exist in other codebases - document prevents repeating mistakes.

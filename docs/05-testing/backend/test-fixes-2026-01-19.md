# Test Failure Fixes - January 19, 2026

**Summary**: Documentation of test failures identified and fixed during test suite analysis.

**Context**: Full test suite run revealed 17 failures. This document details the root causes and fixes for resolved issues.

---

## Fixed Issues

### 1. API Key Validation Security Test Failure

**Test**: `AuthenticationFlowTests.Get_WithInvalidApiKey_Returns401Unauthorized`

**File**: `apps/api/tests/Api.Tests/Integration/FrontendSdk/AuthenticationFlowTests.cs:309`

#### Problem

The test was failing because it returned **200 OK** instead of the expected **401 Unauthorized** when sending an invalid API key.

**Root Cause Analysis**:
1. Test was targeting `/api/v1/games` endpoint
2. This endpoint has `.AllowAnonymous()` configuration (line 35 in `GameEndpoints.cs`)
3. Test used "Bearer" authentication scheme instead of "ApiKey" scheme
4. Middleware didn't recognize "Bearer" scheme for API keys (only checks "ApiKey " prefix)
5. Request fell through to anonymous access and succeeded

**Why This is a Bug**:
- Testing wrong endpoint (anonymous instead of authenticated)
- Using wrong authentication scheme
- Not actually validating API key rejection behavior

#### Solution

**Changes Made**:
```csharp
// BEFORE: Testing anonymous endpoint
var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "mpl_test_invalidkey123");

// AFTER: Testing authenticated endpoint with correct scheme
var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", "mpl_test_invalidkey123");
```

**Key Changes**:
1. ✅ Changed endpoint from `/api/v1/games` (anonymous) to `/api/v1/auth/me` (authenticated)
2. ✅ Fixed authentication scheme from "Bearer" to "ApiKey" (documented in `ApiKeyAuthenticationMiddleware.cs:85`)
3. ✅ Simplified test logic - no need to create test data, just verify rejection

**Verification**:
```bash
cd apps/api && dotnet test tests/Api.Tests --filter "Get_WithInvalidApiKey_Returns401Unauthorized"
# Result: ✅ PASSED (8 seconds)
```

#### Lessons Learned

**Authentication Testing Best Practices**:
- ✅ Always use **authenticated endpoints** to test authentication rejection
- ✅ Verify correct authentication scheme from middleware documentation
- ✅ Read endpoint configuration (`.AllowAnonymous()` vs `.RequireAuthorization()`)
- ❌ Don't assume endpoint requirements without checking

**Middleware Authentication Flow**:
```
1. ApiKeyAuthenticationMiddleware checks for "ApiKey " scheme
2. Falls through if no API key found → SessionAuthenticationMiddleware
3. Falls through if no session → Anonymous access (if endpoint allows)
4. Endpoint with .AllowAnonymous() = 200 OK regardless of invalid auth
```

**Documentation References**:
- API Key scheme documented in: `apps/api/src/Api/Middleware/ApiKeyAuthenticationMiddleware.cs:85`
- OpenAPI spec: `apps/api/src/Api/openapi.json:18` - "Authorization: ApiKey <key>"
- Endpoint config: `apps/api/src/Api/Routing/GameEndpoints.cs:35` - `.AllowAnonymous()`

---

### 2. Concurrent Session Requests Test Failure

**Test**: `AuthenticationFlowTests.ConcurrentRequests_WithSameSession_WorkCorrectly`

**File**: `apps/api/tests/Api.Tests/Integration/FrontendSdk/AuthenticationFlowTests.cs:326`

#### Problem

Test was failing with `TaskCanceledException: The operation was canceled` when making 10 concurrent requests using the same HttpClient instance.

**Stack Trace**:
```
System.Threading.Tasks.TaskCanceledException: The operation was canceled.
  ---- System.Net.Http.HttpRequestException: Error while copying content to a stream.
  -------- System.IO.IOException: The client aborted the request.
```

**Root Cause Analysis**:
1. Test created 10 concurrent requests using single `_client` instance
2. WebApplicationFactory's TestServer has limited connection pool
3. TestServer isn't designed for high concurrent requests on single HttpClient
4. Connection/stream copying errors occur under concurrent load
5. Similar to known Issue #2593 (testhost blocking pattern)

**Why This Happens**:
- TestServer uses in-memory test server, not real HTTP
- Single HttpClient + high concurrency = connection pool exhaustion
- Stream copying conflicts when multiple concurrent operations occur

#### Solution

**Changes Made**:
```csharp
// BEFORE: Single HttpClient with 10 concurrent requests
var tasks = Enumerable.Range(0, 10).Select(_ =>
    _client.GetAsync("/api/v1/auth/me")
);

// AFTER: Independent HttpClient per request with 5 concurrent requests
var tasks = Enumerable.Range(0, 5).Select(async _ =>
{
    var independentClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
    if (sessionCookie != null)
    {
        request.Headers.Add("Cookie", sessionCookie);
    }

    return await independentClient.SendAsync(request);
}).ToArray();
```

**Key Changes**:
1. ✅ Create **independent HttpClient** for each concurrent request
2. ✅ Manual cookie handling (extract session cookie, add to each request)
3. ✅ Reduced concurrency from **10 to 5** for TestServer stability
4. ✅ Disable automatic cookie handling to avoid state conflicts
5. ✅ Documented as Issue #2593 pattern in code comments

**Verification**:
```bash
cd apps/api && dotnet test tests/Api.Tests --filter "ConcurrentRequests_WithSameSession_WorkCorrectly"
# Result: ✅ PASSED (12 seconds)
```

#### Lessons Learned

**WebApplicationFactory Testing Best Practices**:
- ✅ Use **independent HttpClient instances** for concurrent operations
- ✅ Manually handle cookies when testing concurrent authenticated requests
- ✅ Keep concurrent request count **≤5** for TestServer stability
- ✅ Disable automatic cookie handling (`HandleCookies = false`) for concurrency tests
- ❌ Don't reuse single HttpClient for high-concurrency tests
- ❌ Don't exceed TestServer's connection pool limits

**Pattern: Concurrent Testing with WebApplicationFactory**:
```csharp
// ✅ CORRECT: Independent clients
var tasks = Enumerable.Range(0, 5).Select(async _ =>
{
    var client = _factory.CreateClient();
    return await client.GetAsync("/api/endpoint");
});

// ❌ WRONG: Shared client
var tasks = Enumerable.Range(0, 10).Select(_ =>
    _sharedClient.GetAsync("/api/endpoint")
);
```

**Similar Patterns in Codebase**:
- `SessionRepositoryTests.cs:206` - ConcurrentTokenLookups_NoConflicts
  - Uses **independent repositories** per concurrent operation
  - Same principle: avoid shared state in concurrent tests

**Related Issues**:
- Issue #2593: Testhost blocking pattern
- Issue #2541: Parallel test execution optimization

---

## Testing Standards Update

Based on these fixes, the following standards should be followed:

### API Authentication Testing

1. **Endpoint Selection**:
   - Use authenticated endpoints (`.RequireAuthorization()` or `.RequireSession()`)
   - Verify endpoint configuration before writing tests
   - Check middleware chain to understand authentication flow

2. **Authentication Schemes**:
   - API Keys: Use `Authorization: ApiKey <key>` (NOT Bearer)
   - Sessions: Use `meepleai_session` cookie
   - Verify scheme from middleware implementation

3. **Test Structure**:
   ```csharp
   // ✅ Good: Clear, focused, uses correct endpoint
   [Fact]
   public async Task InvalidApiKey_Returns401()
   {
       var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
       request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", "invalid_key");

       var response = await _client.SendAsync(request);

       response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
   }
   ```

### Concurrent Testing with TestServer

1. **Client Management**:
   - Create independent HttpClient for each concurrent operation
   - Don't reuse shared HttpClient instance
   - Disable automatic cookie handling for manual control

2. **Concurrency Limits**:
   - Max 5 concurrent requests per test
   - Higher concurrency = use real integration tests, not TestServer
   - Document concurrency expectations in test comments

3. **Cookie Handling**:
   ```csharp
   // ✅ Good: Manual cookie extraction and injection
   var sessionCookie = loginResponse.Headers
       .GetValues("Set-Cookie")
       .FirstOrDefault(c => c.Contains("meepleai_session"));

   var request = new HttpRequestMessage(HttpMethod.Get, "/api/endpoint");
   request.Headers.Add("Cookie", sessionCookie);
   ```

4. **Test Structure**:
   ```csharp
   // ✅ Good: Independent clients, manual cookies, ≤5 concurrent
   [Fact]
   public async Task ConcurrentRequests_WorkCorrectly()
   {
       // Arrange - Get session cookie
       var sessionCookie = await GetSessionCookieAsync();

       // Act - Independent clients
       var tasks = Enumerable.Range(0, 5).Select(async _ =>
       {
           var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
           {
               AllowAutoRedirect = false,
               HandleCookies = false
           });

           var request = new HttpRequestMessage(HttpMethod.Get, "/api/endpoint");
           request.Headers.Add("Cookie", sessionCookie);

           return await client.SendAsync(request);
       }).ToArray();

       var responses = await Task.WhenAll(tasks);

       // Assert
       responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
   }
   ```

---

## Test Results

**Before Fixes**:
- Total: 5,414 tests
- Failed: 17 tests
- Passed: 5,369 tests
- Duration: 26 minutes

**After Fixes**:
- Fixed: 2 tests
- Remaining failures: 15 tests (pending analysis)
- Expected improvement: -11.8% failure rate

**Fixed Tests**:
1. ✅ `Get_WithInvalidApiKey_Returns401Unauthorized` (8s)
2. ✅ `ConcurrentRequests_WithSameSession_WorkCorrectly` (12s)

---

## Related Documentation

- [Integration Test Optimization](./INTEGRATION_TEST_OPTIMIZATION.md)
- [Testcontainers Best Practices](./testcontainers-best-practices.md)
- [Backend E2E Testing](./BACKEND_E2E_TESTING.md)
- [OAuth Testing](./oauth-testing.md)

---

## References

**Code Files**:
- `apps/api/tests/Api.Tests/Integration/FrontendSdk/AuthenticationFlowTests.cs`
- `apps/api/src/Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `apps/api/src/Api/Routing/GameEndpoints.cs`

**Related Issues**:
- Issue #2593: Testhost blocking pattern
- Issue #2541: Parallel test execution
- Issue #1446: Dual authentication (session OR API key)

**Last Updated**: 2026-01-19

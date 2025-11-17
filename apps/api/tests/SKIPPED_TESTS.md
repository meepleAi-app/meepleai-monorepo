# Skipped Integration Tests

**Last Updated:** 2025-11-17
**Purpose:** Track all skipped/disabled tests and reasons for skipping
**Status:** 8 integration tests currently skipped

---

## Summary

| Category | Count | Reason |
|----------|-------|--------|
| **Provider Health Checks** | 3 | Requires 10s+ warmup time |
| **LLM Client Streaming** | 5 | Complex SSE mock setup issues |

**Total Skipped:** 8 tests

---

## Provider Health Check Tests (3 tests)

**File:** `/apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/ProviderHealthCheckServiceTests.cs`

### 1. HealthCheck_SuccessfulResponse_UpdatesHealthyStatus
**Line:** 133
**Skip Reason:** `"Requires 10s warmup"`
**Test:** Verifies that health check updates provider status to healthy
**Why Skipped:** Health check endpoint requires 10-second warmup period before responding

**Recommendation:** Move to separate integration test pipeline with timeout handling

**Code:**
```csharp
[Fact(Skip = "Requires 10s warmup")]
public async Task HealthCheck_SuccessfulResponse_UpdatesHealthyStatus()
{
    // Test implementation...
}
```

### 2. HealthCheck_FailedResponse_UpdatesUnhealthyStatus
**Line:** 168
**Skip Reason:** `"Requires 10s warmup"`
**Test:** Verifies that failed health check updates provider status to unhealthy
**Why Skipped:** Same as above - requires warmup period

**Recommendation:** Move to integration pipeline

### 3. HealthCheck_Timeout_UpdatesUnhealthyStatus
**Line:** 203
**Skip Reason:** `"Requires 10s warmup + 5s timeout"`
**Test:** Verifies that timeout during health check marks provider as unhealthy
**Why Skipped:** Requires 15 seconds total (warmup + timeout simulation)

**Recommendation:** Move to integration pipeline with extended timeout configuration

---

## LLM Client Streaming Tests (5 tests)

**Files:**
- `/apps/api/tests/Api.Tests/Services/LlmClients/OllamaLlmClientTests.cs`
- `/apps/api/tests/Api.Tests/Services/LlmClients/OpenRouterLlmClientTests.cs`

### 4. Ollama - Test06_GenerateCompletion_ModelNotFound_ReturnsError
**File:** `OllamaLlmClientTests.cs`
**Line:** 125
**Skip Reason:** Mock setup complexity
**Test:** Verifies error handling when model is not found
**Why Skipped:** Requires complex HTTP mock for error responses

**Recommendation:**
- Simplify mock setup using Moq HttpMessageHandler pattern
- OR move to integration tests with actual Ollama instance

### 5. Ollama - Streaming Tests (Multiple)
**File:** `OllamaLlmClientTests.cs`
**Lines:** 161, 185
**Skip Reason:** SSE (Server-Sent Events) mock complexity
**Test:** Streaming completion tests
**Why Skipped:** Mocking SSE streams requires specialized HttpClient mocking

**Recommendation:**
- Use TestServer with SSE-capable middleware
- OR create StreamingTestHelper utility class
- OR move to integration tests

**Example Issue:**
```csharp
// Current problem: Moq doesn't easily support streaming responses
Mock<HttpMessageHandler>
    .Protected()
    .Setup<Task<HttpResponseMessage>>(...)
    .ReturnsAsync(...); // Can't easily return streaming content
```

### 6-8. OpenRouter - Streaming Tests (3 tests)
**File:** `OpenRouterLlmClientTests.cs`
**Lines:** Multiple
**Skip Reason:** Same SSE mock complexity as Ollama
**Test:** Streaming completion tests for OpenRouter client
**Why Skipped:** SSE mock setup issues

**Recommendation:** Same as Ollama streaming tests

---

## Proposed Solutions

### Option 1: Separate Integration Test Pipeline (RECOMMENDED)
**Create:** `.github/workflows/integration-tests.yml`

**Benefits:**
- Tests run on schedule (e.g., nightly)
- Can use actual service instances (Docker containers)
- No need for complex mocks
- Longer timeout tolerances acceptable

**Implementation:**
```yaml
name: Integration Tests
on:
  schedule:
    - cron: '0 2 * * *' # 2 AM daily
  workflow_dispatch:

jobs:
  integration-tests:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    services:
      ollama:
        image: ollama/ollama:latest
        ports:
          - 11434:11434
    steps:
      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration" --timeout 900000
```

### Option 2: StreamingTestHelper Utility
**Create:** `tests/Api.Tests/TestHelpers/StreamingTestHelper.cs`

**Purpose:** Simplify SSE mock setup for streaming tests

**Example:**
```csharp
public class StreamingTestHelper
{
    public static HttpMessageHandler CreateStreamingHandler(
        IEnumerable<string> chunks,
        TimeSpan? delay = null)
    {
        // Mock handler that returns SSE stream
    }
}
```

### Option 3: Use Microsoft.AspNetCore.TestHost
**Benefits:** Built-in support for streaming responses

**Example:**
```csharp
using var host = new HostBuilder()
    .ConfigureWebHost(webBuilder =>
    {
        webBuilder
            .UseTestServer()
            .ConfigureServices(services => { /* ... */ });
    })
    .Start();

var client = host.GetTestClient();
var stream = await client.GetStreamAsync("/api/stream");
```

---

## Action Plan

### Phase 1: Enable Health Check Tests (1-2 hours)
1. Create `integration-tests.yml` workflow
2. Add `Category=Integration` attribute to 3 health check tests
3. Remove `Skip` attribute
4. Configure timeout: 30 seconds per test
5. Run on schedule + manual trigger

**Files to Update:**
- `ProviderHealthCheckServiceTests.cs` (remove Skip, add Category)
- `.github/workflows/integration-tests.yml` (create)

### Phase 2: Enable Streaming Tests (3-4 hours)
Choose ONE approach:
- **A) Integration Pipeline:** Add to integration-tests.yml with real services
- **B) StreamingTestHelper:** Create utility + update 5 tests
- **C) TestServer:** Refactor to use ASP.NET TestHost

**Recommendation:** Start with Option A (integration pipeline) for fastest resolution

### Phase 3: Documentation (30 minutes)
1. Update this document after enabling tests
2. Add comments in test files explaining why tests were previously skipped
3. Update CI/CD documentation

---

## Test Coverage Impact

**Current State:**
- 8 tests skipped = ~5% of integration test suite
- Health check coverage: 0% (all tests skipped)
- Streaming coverage: Partial (only non-streaming tests run)

**After Enabling:**
- Integration test coverage: +5%
- Health check coverage: 100%
- Streaming coverage: 100%

---

## Maintenance

**Review Schedule:** Quarterly
**Owner:** Engineering Lead
**Next Review:** 2026-02-17

**Process:**
1. Review all skipped tests
2. Evaluate if skip reasons are still valid
3. Enable tests when blockers are resolved
4. Remove from this document when enabled

---

**Note:** This document should be updated whenever tests are skipped or enabled. Keep in sync with actual test codebase.

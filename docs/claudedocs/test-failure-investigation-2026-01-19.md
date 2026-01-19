# Test Failure Investigation - Remaining Issues

**Date**: 2026-01-19
**Status**: In Progress
**Context**: Investigating 15 remaining test failures after fixing 2 of 17

---

## Known Issues (Fixed)

1. ✅ **API Key Validation Test** - Fixed by using authenticated endpoint + correct scheme
2. ✅ **Concurrent Session Test** - Fixed by using independent HttpClient instances

---

## Catastrophic Test Process Crash

**Error**: `Test process crashed with exit code -1073741819`

### Analysis

**Exit Code**: `-1073741819` = `0xC0000005` = **ACCESS_VIOLATION** (Windows)

**Common Causes**:
1. **Memory corruption** - Unmanaged memory access, buffer overruns
2. **Native code crashes** - P/Invoke calls, native libraries (PDF processing, OCR, etc.)
3. **Stack overflow** - Deep recursion, large stack allocations
4. **Resource exhaustion** - Out of memory, handle leaks
5. **TestHost process issues** - Known Issue #2593 pattern

**Context from Test Output**:
- Crash occurred after 27 minutes of test execution
- Happened after concurrent session test failure
- Total tests before crash: ~5,414
- Passed before crash: 5,369
- Failed before crash: 17

### Likely Culprits

Based on test suite characteristics:

#### 1. PDF/Document Processing Tests
**Risk**: HIGH
- Uses native libraries (DocNet, Unstructured, SmolDocling)
- Large file processing in memory
- Potential unmanaged memory issues

**Files to investigate**:
```
- Integration/DocumentProcessing/DeletePdfIntegrationTests.cs
- BoundedContexts/DocumentProcessing/Infrastructure/External/DocnetPdfTextExtractorTests.cs
- Integration/DocumentProcessing/PdfUploadIntegrationTests.cs
```

**Potential issues**:
- Native PDF libraries crashing on malformed PDFs
- Memory leaks in PDF processing
- Unhandled exceptions in unmanaged code

#### 2. Qdrant Vector Database Tests
**Risk**: MEDIUM-HIGH
- Uses external Qdrant service
- Large vector operations
- Network timeouts
- Connection pool exhaustion

**Files to investigate**:
```
- Integration/KnowledgeBase/QdrantIntegrationTests.cs
- BoundedContexts/KnowledgeBase/Infrastructure/External/QdrantVectorStoreTests.cs
```

**Potential issues**:
- Qdrant health check timeout (Issue #2576 - just fixed in PR)
- Vector operations causing memory pressure
- Network issues causing cascading failures

#### 3. LLM Integration Tests
**Risk**: MEDIUM
- External API calls (OpenRouter, etc.)
- Large response payloads
- Timeout issues
- Rate limiting

**Files to investigate**:
```
- Integration/Administration/LlmHealthIntegrationTests.cs
- BoundedContexts/KnowledgeBase/Infrastructure/External/LlmServiceTests.cs
```

**Potential issues**:
- API timeouts causing test hangs
- Large response causing memory issues
- Rate limits causing test failures

#### 4. Concurrent/Parallel Tests
**Risk**: MEDIUM
- Already found issues with concurrent TestServer operations
- Resource contention
- Connection pool exhaustion

**Files with concurrent operations**:
```
- Integration/FrontendSdk/EdgeCaseTests.cs (streaming, uploads)
- Integration/Administration/BulkUserOperationsE2ETests.cs
- Integration/Authentication/TwoFactorSecurityPenetrationTests.cs
- BoundedContexts/Authentication/Infrastructure/Persistence/*RepositoryTests.cs
```

**Potential issues**:
- Similar to concurrent session test (now fixed)
- Database connection pool exhaustion
- TestHost resource limits
- Parallel test execution conflicts

#### 5. Edge Case Tests
**Risk**: MEDIUM
- Streaming responses (SSE)
- Large payloads
- File uploads (multipart/form-data)
- Request cancellation

**File**: `Integration/FrontendSdk/EdgeCaseTests.cs`

**Potential issues**:
- Streaming tests not properly disposing streams
- Large file uploads causing memory pressure
- Cancellation tokens not honored, causing leaks

---

## Investigation Strategy

### Phase 1: Reproduce with Isolation
```bash
# Run test suite until crash, collect full output
cd apps/api && dotnet test tests/Api.Tests --logger "trx;LogFileName=results.trx" 2>&1 | tee test-output.log

# Check which test was running when crash occurred
grep -B 50 "Catastrophic failure" test-output.log
```

### Phase 2: Test Suspicious Categories
```bash
# Test PDF processing in isolation
dotnet test --filter "FullyQualifiedName~DocumentProcessing"

# Test Qdrant integration
dotnet test --filter "FullyQualifiedName~Qdrant"

# Test LLM integration
dotnet test --filter "FullyQualifiedName~Llm"

# Test concurrent operations
dotnet test --filter "FullyQualifiedName~Concurrent"

# Test edge cases
dotnet test --filter "FullyQualifiedName~EdgeCase"
```

### Phase 3: Memory and Resource Profiling
```bash
# Run with detailed diagnostics
dotnet test --diag:test-diagnostics.log --logger "console;verbosity=detailed"

# Monitor resource usage
# Watch memory/CPU during test execution
```

### Phase 4: Incremental Test Execution
```bash
# Run tests in smaller batches to isolate crash
dotnet test --filter "FullyQualifiedName~Authentication"
dotnet test --filter "FullyQualifiedName~GameManagement"
# ... continue with other bounded contexts
```

---

## Patterns to Look For

### Memory Leak Indicators
```csharp
// ❌ BAD: Not disposing HttpClient
var client = _factory.CreateClient();
// ... use client
// Missing: client.Dispose()

// ❌ BAD: Not disposing streams
var stream = File.OpenRead(path);
// ... use stream
// Missing: await stream.DisposeAsync()

// ❌ BAD: Accumulating large objects
List<byte[]> largeList = new();
for (int i = 0; i < 10000; i++)
{
    largeList.Add(new byte[1024 * 1024]); // 1MB each
}
// No cleanup, GC pressure
```

### TestHost Issues (Issue #2593)
```csharp
// ❌ BAD: Shared HttpClient for concurrent operations
var tasks = Enumerable.Range(0, 100).Select(_ =>
    _sharedClient.GetAsync("/api/endpoint")
);

// ✅ GOOD: Independent clients
var tasks = Enumerable.Range(0, 5).Select(async _ =>
{
    var client = _factory.CreateClient();
    var result = await client.GetAsync("/api/endpoint");
    client.Dispose();
    return result;
});
```

### Unmanaged Resource Issues
```csharp
// ❌ BAD: P/Invoke without error handling
[DllImport("native.dll")]
static extern IntPtr ProcessPdf(string path);
var ptr = ProcessPdf(badPath); // Could crash on bad input

// ✅ GOOD: Wrapped with error handling
try
{
    var ptr = ProcessPdf(path);
    if (ptr == IntPtr.Zero)
        throw new InvalidOperationException("PDF processing failed");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Native PDF processing crashed");
}
```

---

## Hypotheses for Remaining 15 Failures

Based on patterns and test structure:

### Hypothesis 1: PDF Processing Tests (4-5 failures)
**Likelihood**: HIGH
- Native library crashes
- Large file memory issues
- Unhandled exceptions in DocNet

### Hypothesis 2: Qdrant Vector Tests (2-3 failures)
**Likelihood**: MEDIUM-HIGH
- Timing issues with Qdrant health check (related to Issue #2576)
- Vector operations timeout
- Connection issues

### Hypothesis 3: LLM Integration Tests (2-3 failures)
**Likelihood**: MEDIUM
- External API timeouts
- Rate limiting
- Network issues

### Hypothesis 4: Concurrent Tests (2-3 failures)
**Likelihood**: MEDIUM
- Similar to our fixed concurrent session test
- TestServer concurrency limits
- Resource contention

### Hypothesis 5: Edge Case Tests (2-3 failures)
**Likelihood**: MEDIUM
- Streaming test resource leaks
- Large payload memory issues
- Improper disposal

### Hypothesis 6: Database/Repository Tests (1-2 failures)
**Likelihood**: LOW-MEDIUM
- Connection pool exhaustion
- Transaction deadlocks
- Parallel test conflicts

---

## Next Steps

1. **Wait for current test run to complete**
   - Capture full output with all failure details
   - Identify which tests actually failed

2. **Analyze failure patterns**
   - Group failures by category
   - Identify common root causes

3. **Prioritize fixes by impact**
   - Critical: Catastrophic crash (blocks CI)
   - High: Flaky tests (reduce reliability)
   - Medium: Intermittent failures
   - Low: Edge cases

4. **Implement fixes systematically**
   - One category at a time
   - Verify fix before moving to next
   - Document patterns for future prevention

---

## Related Issues

- Issue #2593: TestHost blocking pattern
- Issue #2576: Qdrant health check timeout (PR #2642 - merged)
- Issue #2541: Parallel test execution optimization
- Issue #2424: Validation audit

---

## Status

**Current**: Waiting for test suite completion to get detailed failure information

**Next**: Analyze actual failures and validate hypotheses

**Target**: Reduce failures from 15 to 0, eliminate catastrophic crash

---

**Last Updated**: 2026-01-19

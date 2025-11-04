---
title: "[CODE QUALITY] Fix Missing Dispose Calls (601 instances)"
labels: ["code-quality", "priority-high", "P1", "code-scanning", "resource-leak"]
---

## Summary

**601 open warnings** for missing `Dispose()` calls on `IDisposable` objects, causing resource leaks (memory, file handles, HTTP connections).

### Impact
- **Severity**: ⚠️ **WARNING** (High Priority - P1)
- **CWE**: [CWE-404: Improper Resource Shutdown](https://cwe.mitre.org/data/definitions/404.html)
- **Risk**: Memory leaks, connection pool exhaustion, file handle depletion
- **Production Impact**: Service degradation, OOM crashes, slow response times

---

## Problem

`IDisposable` objects (e.g., `HttpRequestMessage`, `FileStream`, `StreamReader`, `HttpResponseMessage`, `StringContent`) are not being properly disposed, leading to resource leaks.

### Example Vulnerable Code

```csharp
// ❌ BAD: Missing using statement
var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
await _httpClient.SendAsync(request); // Leaks HttpRequestMessage + StringContent
```

```csharp
// ❌ BAD: FileStream not disposed
var stream = File.OpenRead(filePath);
var content = ReadContent(stream); // If exception occurs, stream leaks
return content;
```

```csharp
// ❌ BAD: IServiceScope not disposed
var scope = _scopeFactory.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IMyService>();
await service.DoWorkAsync(); // Leaks scope if exception occurs
```

### Secure Solution

```csharp
// ✅ GOOD: using statement ensures disposal
using var request = new HttpRequestMessage(HttpMethod.Post, url);
using var content = new StringContent(json, Encoding.UTF8, "application/json");
request.Content = content;
await _httpClient.SendAsync(request); // Both disposed automatically
```

```csharp
// ✅ GOOD: using statement for streams
using var stream = File.OpenRead(filePath);
var content = ReadContent(stream); // Stream disposed even if exception
return content;
```

```csharp
// ✅ GOOD: using for service scopes
using var scope = _scopeFactory.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IMyService>();
await service.DoWorkAsync(); // Scope disposed even if exception
```

```csharp
// ✅ GOOD: Async disposal
await using var asyncResource = new MyAsyncDisposable();
await asyncResource.ProcessAsync(); // Async disposal
```

---

## Common Patterns to Fix

### 1. HttpRequestMessage & StringContent

**Location**: Multiple files in `apps/api/src/Api/Services/`

```csharp
// Before
var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Content = new StringContent(json);

// After
using var request = new HttpRequestMessage(HttpMethod.Post, url);
using var content = new StringContent(json);
request.Content = content;
```

### 2. IServiceScope

**Location**: Background services, controllers

```csharp
// Before
var scope = _scopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

// After
using var scope = _scopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
```

### 3. FileStream & StreamReader

**Location**: PDF processing, file uploads

```csharp
// Before
var stream = File.OpenRead(path);
var reader = new StreamReader(stream);

// After
using var stream = File.OpenRead(path);
using var reader = new StreamReader(stream);
```

### 4. HttpResponseMessage

**Location**: HTTP client wrappers

```csharp
// Before
var response = await _httpClient.GetAsync(url);
var content = await response.Content.ReadAsStringAsync();

// After
using var response = await _httpClient.GetAsync(url);
var content = await response.Content.ReadAsStringAsync();
```

---

## Remediation Plan

### Phase 1: Critical Services (1-2 days)
- [ ] `apps/api/src/Api/Services/LlmService.cs` (HTTP requests)
- [ ] `apps/api/src/Api/Services/OAuthService.cs` (HTTP + scopes)
- [ ] `apps/api/src/Api/Services/BggApiService.cs` (HTTP requests)
- [ ] `apps/api/src/Api/Services/N8nConfigService.cs` (HTTP requests)

### Phase 2: Background Services (2-3 days)
- [ ] `apps/api/src/Api/Services/BackgroundTaskService.cs` (service scopes)
- [ ] `apps/api/src/Api/Services/SessionAutoRevocationService.cs` (scopes)
- [ ] All classes using `IServiceScopeFactory`

### Phase 3: File Processing (1-2 days)
- [ ] `apps/api/src/Api/Services/PdfStorageService.cs` (file streams)
- [ ] `apps/api/src/Api/Services/PdfTextExtractionService.cs` (streams)
- [ ] `apps/api/src/Api/Services/PdfTableExtractionService.cs` (streams)

### Phase 4: Remaining Services (3-4 days)
- [ ] All other services with IDisposable usage
- [ ] Controllers using IServiceScopeFactory
- [ ] Utility classes with resource management

---

## Testing

### Unit Test Template

```csharp
[Fact]
public async Task ServiceMethod_DisposesResourcesProperly()
{
    // Arrange
    var mockHttpMessageHandler = new MockHttpMessageHandler(
        (request) => {
            // Verify request is disposed by checking ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => _ = request.Content);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    var httpClient = new HttpClient(mockHttpMessageHandler);
    var service = new MyService(httpClient);

    // Act
    await service.CallExternalApiAsync();

    // Assert - No leaked resources
}
```

### Memory Leak Detection

```bash
# Run with memory profiler
dotnet test --logger "console;verbosity=detailed" \
  /p:CollectCoverage=true \
  --collect:"XPlat Code Coverage"

# Check for increasing memory usage
dotnet counters monitor --process-id <PID> \
  System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count]
```

---

## Prevention Strategy

### 1. Enable Roslyn Analyzers

Add to `.editorconfig`:

```ini
# CA2000: Dispose objects before losing scope
dotnet_diagnostic.CA2000.severity = error

# CA1001: Types that own disposable fields should be disposable
dotnet_diagnostic.CA1001.severity = warning

# IDE0067: Dispose objects before losing scope
dotnet_diagnostic.IDE0067.severity = error

# IDE0068: Use recommended dispose pattern
dotnet_diagnostic.IDE0068.severity = warning
```

### 2. Code Review Checklist

```markdown
- [ ] All `new HttpRequestMessage()` use `using var`
- [ ] All `new StringContent()` use `using var`
- [ ] All `CreateScope()` use `using var`
- [ ] All file streams use `using var` or `using` blocks
- [ ] No bare `Dispose()` calls (use using statements instead)
- [ ] Async disposable resources use `await using`
```

### 3. Pre-commit Hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Find IDisposable usage without using statement
ISSUES=$(git diff --cached --name-only | grep '\.cs$' | xargs grep -n 'new HttpRequestMessage\|new StringContent\|CreateScope()' | grep -v 'using var')

if [ ! -z "$ISSUES" ]; then
    echo "❌ Found IDisposable objects without using statement:"
    echo "$ISSUES"
    exit 1
fi
```

---

## Acceptance Criteria

- [ ] All 601 instances fixed (using statements added)
- [ ] No new CA2000/CA1001/IDE0067 warnings in build
- [ ] Memory profiler shows no leaks in load tests
- [ ] All unit tests pass
- [ ] Code review completed
- [ ] Prevention measures implemented (analyzers + hooks)

---

## Estimated Effort

- **Total Time**: 8-10 days (1 developer)
- **Complexity**: Medium (mostly mechanical fixes)
- **Risk**: Low (safe refactoring with high test coverage)

---

## References

- [Microsoft Docs: IDisposable pattern](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
- [CA2000: Dispose objects before losing scope](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2000)
- [CODE-01: IDisposable Best Practices](../../../CLAUDE.md#idisposable-best-practices-code-01)
- CWE-404: Improper Resource Shutdown
- CWE-772: Missing Release of Resource after Effective Lifetime

---

**Priority**: P1 - HIGH
**Category**: Code Quality > Resource Management
**Related Issues**: #[log-forging], #[code-scanning-tracker]

---
title: "[CODE QUALITY] Fix Generic Catch Clauses (220 instances)"
labels: ["code-quality", "priority-medium", "P2", "code-scanning", "error-handling"]
---

## Summary

**220 open notes** for overly generic `catch` clauses that catch `Exception` or use empty catch blocks, hiding errors and making debugging difficult.

### Impact
- **Severity**: 📝 **NOTE** (Medium Priority - P2)
- **CWE**: [CWE-396: Declaration of Catch for Generic Exception](https://cwe.mitre.org/data/definitions/396.html)
- **Risk**: Hidden errors, difficult debugging, swallowed exceptions
- **Production Impact**: Silent failures, data corruption, undetected issues

---

## Problem

Generic catch blocks (`catch (Exception ex)` or empty `catch { }`) hide specific errors and make it difficult to diagnose issues. They can mask bugs and lead to unexpected behavior.

### Example Vulnerable Code

```csharp
// ❌ BAD: Catches everything without distinction
public async Task<bool> ProcessDataAsync()
{
    try
    {
        await _service.ProcessAsync();
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing data");
        return false; // Hides specific error types
    }
}
```

```csharp
// ❌ BAD: Empty catch block (swallows exceptions)
public void CleanupResource()
{
    try
    {
        _resource.Dispose();
    }
    catch
    {
        // Silently ignores errors
    }
}
```

```csharp
// ❌ BAD: Catches Exception when specific types are expected
public async Task<Game?> GetGameAsync(int id)
{
    try
    {
        return await _dbContext.Games.FindAsync(id);
    }
    catch (Exception ex)
    {
        // Could be network, timeout, or database error
        _logger.LogError(ex, "Failed to get game");
        return null;
    }
}
```

### Secure Solution

```csharp
// ✅ GOOD: Catch specific exceptions
public async Task<bool> ProcessDataAsync()
{
    try
    {
        await _service.ProcessAsync();
        return true;
    }
    catch (ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation failed");
        return false;
    }
    catch (TimeoutException ex)
    {
        _logger.LogError(ex, "Operation timed out");
        throw; // Re-throw infrastructure errors
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, "Database update failed");
        throw;
    }
}
```

```csharp
// ✅ GOOD: Log and suppress only if safe
public void CleanupResource()
{
    try
    {
        _resource.Dispose();
    }
    catch (ObjectDisposedException)
    {
        // Already disposed, safe to ignore
        _logger.LogDebug("Resource already disposed");
    }
    catch (Exception ex)
    {
        // Unexpected error during cleanup - log but don't throw
        _logger.LogWarning(ex, "Error during resource cleanup");
    }
}
```

```csharp
// ✅ GOOD: Use when clause for conditional catching
public async Task<Game?> GetGameAsync(int id)
{
    try
    {
        return await _dbContext.Games.FindAsync(id);
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23503")
    {
        // Foreign key violation
        throw new NotFoundException($"Game {id} references invalid data");
    }
    catch (OperationCanceledException)
    {
        // Request cancelled by client
        _logger.LogInformation("Game fetch cancelled");
        throw;
    }
}
```

```csharp
// ✅ GOOD: Let exceptions propagate unless explicitly handled
public async Task<RuleSpec> CreateRuleSpecAsync(CreateRuleSpecRequest request)
{
    // No try-catch - let validation/db errors propagate
    var ruleSpec = new RuleSpecEntity
    {
        GameId = request.GameId,
        Content = request.Content
    };

    await _dbContext.RuleSpecs.AddAsync(ruleSpec);
    await _dbContext.SaveChangesAsync(); // Throws on constraint violation

    return MapToDto(ruleSpec);
}
```

---

## Common Patterns to Fix

### 1. Database Operations

**Location**: Services with EF Core operations

```csharp
// Before
catch (Exception ex)
{
    _logger.LogError(ex, "Database error");
    return null;
}

// After
catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
{
    throw new ConflictException("Resource already exists");
}
catch (DbUpdateConcurrencyException ex)
{
    throw new ConflictException("Resource was modified by another user");
}
```

### 2. HTTP Client Calls

**Location**: External API services

```csharp
// Before
catch (Exception ex)
{
    _logger.LogError(ex, "API call failed");
    return null;
}

// After
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error calling API");
    throw;
}
catch (TimeoutException ex)
{
    _logger.LogWarning(ex, "API request timed out");
    throw;
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Invalid JSON response from API");
    throw new ExternalServiceException("Invalid response format", ex);
}
```

### 3. File Operations

**Location**: PDF processing, file uploads

```csharp
// Before
catch (Exception ex)
{
    _logger.LogError(ex, "File operation failed");
}

// After
catch (FileNotFoundException ex)
{
    throw new NotFoundException($"File not found: {ex.FileName}");
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied to file");
    throw;
}
catch (IOException ex)
{
    _logger.LogError(ex, "I/O error during file operation");
    throw;
}
```

### 4. Background Services

**Location**: Hosted services, background tasks

```csharp
// Before
catch (Exception ex)
{
    _logger.LogError(ex, "Background task failed");
}

// After
catch (OperationCanceledException)
{
    // Shutdown requested, exit gracefully
    _logger.LogInformation("Background task cancelled");
    return;
}
catch (Exception ex)
{
    // Unexpected error - log with context
    _logger.LogCritical(ex, "Unhandled error in background task");
    throw; // Let host restart the service
}
```

---

## Remediation Plan

### Phase 1: Critical Services (2-3 days)
- [ ] `GameService.cs`, `RuleSpecService.cs` - Database operations
- [ ] `AuthService.cs`, `UserManagementService.cs` - Auth operations
- [ ] `LlmService.cs`, `RagService.cs` - AI/RAG operations

### Phase 2: External Integrations (1-2 days)
- [ ] `BggApiService.cs` - BGG API calls
- [ ] `N8nConfigService.cs` - n8n API calls
- [ ] `OAuthService.cs` - OAuth provider calls
- [ ] `AlertingService.cs` - Email/Slack/PagerDuty

### Phase 3: Background Services (1 day)
- [ ] `BackgroundTaskService.cs`
- [ ] `SessionAutoRevocationService.cs`
- [ ] All hosted services

### Phase 4: Remaining Code (2 days)
- [ ] Controllers and endpoints
- [ ] Utility classes
- [ ] Test helpers

---

## Testing

### Unit Test Template

```csharp
[Fact]
public async Task Operation_SpecificException_ThrowsExpectedException()
{
    // Arrange
    var mockService = new Mock<IExternalService>();
    mockService.Setup(s => s.CallAsync())
        .ThrowsAsync(new HttpRequestException("Network error"));

    var service = new MyService(mockService.Object, _logger);

    // Act & Assert
    await Assert.ThrowsAsync<HttpRequestException>(
        () => service.PerformOperationAsync()
    );
}

[Fact]
public async Task DatabaseOperation_UniqueConstraintViolation_ThrowsConflictException()
{
    // Arrange
    var service = CreateService();
    var existingEntity = CreateTestEntity();
    await _dbContext.Entities.AddAsync(existingEntity);
    await _dbContext.SaveChangesAsync();

    // Act & Assert - Try to add duplicate
    await Assert.ThrowsAsync<ConflictException>(
        () => service.CreateAsync(existingEntity)
    );
}
```

---

## Prevention Strategy

### 1. Code Review Checklist

```markdown
- [ ] No bare `catch (Exception)` without re-throwing
- [ ] No empty catch blocks
- [ ] Specific exceptions caught (DbUpdateException, HttpRequestException, etc.)
- [ ] Use `when` clauses for conditional catching
- [ ] Errors logged with appropriate level (Warning vs Error vs Critical)
- [ ] Infrastructure errors propagate (timeout, network, database)
- [ ] Business logic errors handled and mapped to domain exceptions
```

### 2. Custom Exception Hierarchy

Create domain exceptions:

```csharp
// Infrastructure/Exceptions/DomainExceptions.cs
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }
    public ValidationException(Dictionary<string, string[]> errors)
    {
        Errors = errors;
    }
}

public class ExternalServiceException : Exception
{
    public ExternalServiceException(string message, Exception inner)
        : base(message, inner) { }
}
```

### 3. Exception Filter Middleware

```csharp
// Middleware/ExceptionHandlingMiddleware.cs
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (statusCode, message) = exception switch
        {
            NotFoundException => (404, "Resource not found"),
            ConflictException => (409, "Conflict with existing resource"),
            ValidationException => (400, "Validation failed"),
            UnauthorizedAccessException => (403, "Access denied"),
            _ => (500, "Internal server error")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});
```

### 4. Roslyn Analyzers

Add to `.editorconfig`:

```ini
# CA1031: Do not catch general exception types
dotnet_diagnostic.CA1031.severity = warning

# CA2201: Do not raise reserved exception types
dotnet_diagnostic.CA2201.severity = error
```

---

## Exception Handling Guidelines

### When to Catch

✅ **DO catch** when:
- You can recover from the error
- You need to add context before re-throwing
- You're at a boundary (API controller, background service)
- Specific exception requires specific handling

❌ **DON'T catch** when:
- You can't do anything useful with the exception
- It's an infrastructure error (network, timeout, database)
- You're just logging and re-throwing (let middleware handle it)

### What to Catch

| Exception Type | When to Catch | Action |
|----------------|---------------|--------|
| `ArgumentException` | Input validation | Return 400 Bad Request |
| `NotFoundException` | Missing resource | Return 404 Not Found |
| `ConflictException` | Duplicate/conflict | Return 409 Conflict |
| `UnauthorizedAccessException` | Permission denied | Return 403 Forbidden |
| `ValidationException` | Business rule violation | Return 400 with errors |
| `DbUpdateConcurrencyException` | Optimistic locking | Return 409 Conflict |
| `TimeoutException` | External service timeout | Log and propagate |
| `HttpRequestException` | Network error | Log and propagate |
| `OperationCanceledException` | Request cancelled | Exit gracefully |
| `Exception` | Last resort boundary | Log and return 500 |

---

## Acceptance Criteria

- [ ] No bare `catch (Exception)` without re-throwing
- [ ] No empty catch blocks
- [ ] All database exceptions caught specifically
- [ ] All HTTP exceptions caught specifically
- [ ] Domain exceptions used for business logic errors
- [ ] Exception middleware handles all unhandled exceptions
- [ ] Unit tests cover exception scenarios
- [ ] CA1031 analyzer warnings resolved

---

## Estimated Effort

- **Total Time**: 6-8 days (1 developer)
- **Complexity**: Medium (requires understanding error scenarios)
- **Risk**: Medium (changes error handling behavior)

---

## References

- [Microsoft Docs: Exception handling best practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [CA1031: Do not catch general exception types](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1031)
- [CWE-396: Declaration of Catch for Generic Exception](https://cwe.mitre.org/data/definitions/396.html)

---

**Priority**: P2 - MEDIUM
**Category**: Code Quality > Error Handling
**Related Issues**: #[code-scanning-tracker]

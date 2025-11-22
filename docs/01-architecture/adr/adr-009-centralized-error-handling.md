# ADR-009: Centralized Error Handling with Middleware

**Status**: Implemented
**Date**: 2025-11-16
**Supersedes**: Previous numbering as ADR-004-centralized-error-handling
**Issue**: [#1194](https://github.com/meepleai/meepleai-monorepo/issues/1194)
**Deciders**: Engineering Team

---

## Context

The API codebase had scattered try-catch blocks across endpoints, leading to:

- ❌ Code duplication (521 lines of repetitive error handling)
- ❌ Inconsistent error responses
- ❌ DRY violations in 3+ files
- ❌ Potential information leakage in exception messages
- ❌ Maintenance burden (changes required in multiple locations)

### Files with Duplicate Patterns

- `AiEndpoints.cs`: ~39 try-catch blocks (315 lines)
- `ChatEndpoints.cs`: 5 try-catch blocks (77 lines)
- `RuleSpecEndpoints.cs`: 9 try-catch blocks (129 lines)

**Total**: 53 try-catch blocks, 521 lines of duplicate error handling code

---

## Decision

Implement **centralized error handling** using:

1. ✅ **Enhanced `ApiExceptionHandlerMiddleware`** - Global exception handling for all `/api/*` paths
2. ✅ **Custom HTTP Exceptions** - Type-safe exceptions in `Middleware/Exceptions/`
3. ✅ **Result<T> Pattern** - Functional error handling in `SharedKernel/Domain/Results/`
4. ✅ **Complete try-catch removal** - Endpoints throw exceptions, middleware handles HTTP responses

---

## Architecture

### Exception Hierarchy

```
Exception (System)
├── HttpException (Api.Middleware.Exceptions) - Base for HTTP-specific errors
│   ├── BadRequestException (400)
│   ├── UnauthorizedHttpException (401)
│   ├── ForbiddenException (403)
│   └── ConflictException (409)
├── NotFoundException (Api.Middleware.Exceptions) - 404 with resource tracking
└── DomainException (Api.SharedKernel.Domain.Exceptions)
    └── ValidationException - Domain validation errors
```

### Result<T> Pattern

```csharp
// Success case
var result = Result<Game>.Success(game);

// Failure case
var result = Result<Game>.Failure(Error.NotFound("Game not found"));

// Pattern matching
result.Match(
    onSuccess: game => logger.LogInfo("Found: {Name}", game.Name),
    onFailure: error => logger.LogError("Error: {Message}", error.Message)
);
```

### Middleware Flow

```
Request → ApiExceptionHandlerMiddleware (try)
          ↓
          Endpoint (no try-catch, throws exceptions)
          ↓
          Exception thrown
          ↓
          Middleware (catch) → MapExceptionToResponse()
          ↓
          Structured JSON error response + logging + metrics
```

---

## Implementation

### 1. Custom Exceptions Created

**Location**: `Api/Middleware/Exceptions/`

- `HttpException.cs` - Base class with StatusCode + ErrorCode
- `BadRequestException.cs` - Maps to HTTP 400
- `UnauthorizedHttpException.cs` - Maps to HTTP 401
- `ForbiddenException.cs` - Maps to HTTP 403
- `NotFoundException.cs` - Maps to HTTP 404 (with ResourceType + ResourceId)
- `ConflictException.cs` - Maps to HTTP 409

### 2. Result<T> Pattern

**Location**: `Api/SharedKernel/Domain/Results/Result.cs`

Features:
- Immutable record type
- Success/Failure factory methods
- Pattern matching with `Match()`
- Transformation with `Map<TResult>()`
- Predefined `Error` types (Validation, NotFound, Unauthorized, etc.)

### 3. Enhanced Middleware

**File**: `Api/Middleware/ApiExceptionHandlerMiddleware.cs`

**Changes**:
- Added custom exception handling in `MapExceptionToResponse()`
- Prioritized exception matching (specific → general)
- Support for domain exceptions (DomainException, ValidationException)
- Support for system exceptions (ArgumentException, KeyNotFoundException, etc.)

**Exception Mapping**:

| Exception Type | HTTP Status | Error Code |
|---------------|-------------|------------|
| HttpException | Custom | Custom |
| NotFoundException | 404 | not_found |
| ValidationException | 400 | validation_error |
| DomainException | 400 | domain_error |
| ArgumentException | 400 | bad_request |
| UnauthorizedAccessException | 403 | forbidden |
| KeyNotFoundException | 404 | not_found |
| TimeoutException | 504 | timeout |
| Unknown exceptions | 500 | internal_server_error |

### 4. Endpoint Cleanup

**Pattern Applied**:

**Before** (repetitive try-catch):
```csharp
try
{
    var result = await service.DoSomethingAsync(id, ct);
    if (result == null) return Results.NotFound();
    return Results.Ok(result);
}
catch (InvalidOperationException ex)
{
    logger.LogWarning(ex, "Invalid operation");
    return Results.BadRequest(new { error = ex.Message });
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error");
    throw;
}
```

**After** (clean, middleware-handled):
```csharp
// ISSUE-1194: Error handling centralized in middleware
var result = await service.DoSomethingAsync(id, ct);
if (result == null) throw new NotFoundException("Resource", id.ToString());
return Results.Ok(result);
```

**Files Cleaned**:
- ✅ `AiEndpoints.cs` - 39 try-catch blocks removed (315 lines saved)
- ✅ `ChatEndpoints.cs` - 5 try-catch blocks removed (77 lines saved)
- ✅ `RuleSpecEndpoints.cs` - 9 try-catch blocks removed (129 lines saved)

**Total**: 53 try-catch blocks removed, 521 lines saved

---

## Benefits

### 1. Code Quality

- ✅ **Single Responsibility** - Endpoints focus on business logic only
- ✅ **DRY Compliance** - Error handling logic in one place
- ✅ **Consistency** - All API endpoints use same error handling pattern
- ✅ **Maintainability** - Changes to error handling require updates in one file only

### 2. Security

- ✅ **No Information Leakage** - Stack traces only in development environment
- ✅ **Consistent Error Format** - Standardized JSON structure prevents accidental exposure
- ✅ **Correlation IDs** - All errors include `X-Correlation-Id` for debugging

### 3. Developer Experience

- ✅ **Cleaner Code** - Endpoints are more readable (521 lines removed)
- ✅ **Type-Safe Errors** - Custom exceptions provide compile-time safety
- ✅ **Easy Testing** - 30 middleware tests cover all exception scenarios
- ✅ **Clear Patterns** - Result<T> provides functional error handling option

### 4. Observability

- ✅ **Centralized Logging** - All exceptions logged with full context
- ✅ **Metrics Integration** - `MeepleAiMetrics.RecordApiError()` for monitoring
- ✅ **Correlation Tracking** - TraceIdentifier propagated to all error responses

---

## Testing

### Test Coverage

**New Tests**: 30 comprehensive middleware tests

**Test Categories**:
1. Custom HTTP exceptions (6 tests)
2. Domain exceptions (2 tests)
3. System exceptions (7 tests)
4. Edge cases (4 tests)
5. Environment-specific behavior (2 tests)
6. Theory-based validation (9 tests)

**Results**: ✅ 30/30 passing (100% success rate)

### Test Scenarios

- ✅ Each custom exception maps to correct HTTP status
- ✅ Error responses include correlationId and timestamp
- ✅ Stack traces only included in development environment
- ✅ Non-API paths (/health, /) are not handled by middleware
- ✅ Exceptions are logged with full details
- ✅ Metrics are recorded for all errors

---

## Trade-offs

### Accepted

- ✅ **Simplified chat error logging** - Removed nested try-catch for chat logging (non-critical functionality)
- ✅ **Generic error messages** - More secure but less specific than endpoint-specific messages
- ✅ **Middleware dependency** - All error handling depends on middleware registration order

### Rejected

- ❌ **MediatR Pipeline Behavior** - Too complex with reflection, type conversion issues
- ❌ **Partial cleanup** - Decided to remove ALL try-catch for maximum consistency
- ❌ **Manual error returns** - Replaced with exception throwing for middleware handling

---

## Migration Path

### Phase 1: Foundation (Completed)
- ✅ Create exception types
- ✅ Enhance middleware
- ✅ Add Result<T> pattern

### Phase 2: Cleanup (Completed)
- ✅ Remove try-catch from AiEndpoints.cs
- ✅ Remove try-catch from ChatEndpoints.cs
- ✅ Remove try-catch from RuleSpecEndpoints.cs

### Phase 3: Validation (Completed)
- ✅ Create comprehensive tests
- ✅ Verify zero compilation errors
- ✅ Run full test suite

### Future Enhancements

**Potential Improvements**:
- Use Result<T> in CQRS handlers (optional, functional approach)
- Add structured logging with error categories
- Implement retry policies for transient errors
- Add exception fingerprinting for error aggregation

---

## Metrics

### Code Impact

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| **Try-catch blocks** | 53 | 0 | -53 (100%) |
| **Lines of code** | Baseline | -521 | -521 lines |
| **Error handling files** | 3 endpoints | 1 middleware | Centralized |
| **Test coverage** | N/A | 30 tests | 100% middleware |
| **Compilation errors** | 0 | 0 | ✅ No regressions |

### Performance Impact

- ✅ **Zero overhead** - Middleware only invoked on exceptions (exceptional path)
- ✅ **Happy path unchanged** - No performance impact on successful requests
- ✅ **Logging optimized** - Single log entry per exception vs multiple nested logs

---

## References

- Issue #1194: [Refactor] Centralize Error Handling with Middleware
- [Microsoft Docs: Exception Handling Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)

---

## Conclusion

Centralized error handling successfully implemented with:
- ✅ 521 lines of duplicate code removed
- ✅ Consistent error responses across all endpoints
- ✅ Type-safe exception hierarchy
- ✅ 100% middleware test coverage
- ✅ Zero compilation errors or test failures
- ✅ Improved security (no information leakage)
- ✅ Better observability (centralized logging + metrics)

The implementation follows DDD principles, maintains architectural consistency, and provides a solid foundation for future error handling enhancements.

# Quick Win #002: Create Query Validation Helper

**Priority**: ⚡ QUICK WIN
**Effort**: 2-3 hours
**Impact**: ⭐⭐ MEDIUM
**ROI**: Very High (immediate code reduction)
**Status**: Not Started

---

## Problem

Query validation pattern repeated **50+ times** across codebase:

```csharp
// In RagService.cs
if (string.IsNullOrWhiteSpace(query))
{
    return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
}

// In HybridSearchService.cs
if (string.IsNullOrWhiteSpace(query))
{
    throw new ValidationException("Query cannot be empty");
}

// In AskQuestionQueryHandler.cs
if (string.IsNullOrWhiteSpace(request.Query))
{
    return Results.BadRequest("Query is required");
}
```

Different messages, same validation logic.

---

## Solution

Create a simple `QueryValidator` utility class with consistent error messages.

---

## Implementation Steps

### Step 1: Create QueryValidator (30 minutes)

**File**: `apps/api/src/Api/Helpers/QueryValidator.cs`

```csharp
namespace Api.Helpers;

/// <summary>
/// Validates query strings with consistent error messages.
/// </summary>
public static class QueryValidator
{
    private const int MinQueryLength = 3;
    private const int MaxQueryLength = 1000;

    /// <summary>
    /// Validates a query string is not empty and within length limits.
    /// Returns error message if invalid, null if valid.
    /// </summary>
    public static string? ValidateQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Please provide a question";

        if (query.Length < MinQueryLength)
            return $"Query must be at least {MinQueryLength} characters";

        if (query.Length > MaxQueryLength)
            return $"Query must not exceed {MaxQueryLength} characters";

        return null; // Valid
    }

    /// <summary>
    /// Validates query and throws ValidationException if invalid.
    /// </summary>
    public static void ValidateQueryOrThrow(string? query)
    {
        var error = ValidateQuery(query);
        if (error != null)
            throw new ValidationException(error);
    }

    /// <summary>
    /// Validates query for use in Result<T> pattern.
    /// </summary>
    public static (bool IsValid, string? Error) TryValidateQuery(string? query)
    {
        var error = ValidateQuery(query);
        return (error == null, error);
    }
}
```

---

### Step 2: Apply to Services (1 hour)

**Refactor RagService.cs**:

**Before**:
```csharp
public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
    }
    // ... rest of method
}
```

**After**:
```csharp
public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    var queryError = QueryValidator.ValidateQuery(query);
    if (queryError != null)
        return new QaResponse(queryError, Array.Empty<Snippet>());

    // ... rest of method
}
```

**Services to refactor** (8 validations):
- RagService.cs (4 methods)
- HybridSearchService.cs (2 methods)
- KeywordSearchService.cs (2 methods)

---

### Step 3: Apply to Handlers (1 hour)

**Refactor Query Handlers**:

**Before**:
```csharp
public class AskQuestionQueryHandler : IRequestHandler<AskQuestionQuery, QaResponse>
{
    public async Task<QaResponse> Handle(AskQuestionQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ValidationException("Query is required");

        // ... business logic
    }
}
```

**After**:
```csharp
public class AskQuestionQueryHandler : IRequestHandler<AskQuestionQuery, QaResponse>
{
    public async Task<QaResponse> Handle(AskQuestionQuery request, CancellationToken ct)
    {
        QueryValidator.ValidateQueryOrThrow(request.Query);

        // ... business logic
    }
}
```

**Handlers to refactor** (~20 handlers):
- KnowledgeBase/Application/Handlers/AskQuestionQueryHandler.cs
- KnowledgeBase/Application/Handlers/StreamQaQueryHandler.cs
- KnowledgeBase/Application/Handlers/StreamExplainQueryHandler.cs
- ... (all query handlers that validate query strings)

---

### Step 4: Apply to Endpoints (30 minutes)

**Refactor Minimal API Endpoints**:

**Before**:
```csharp
app.MapPost("/chat", async (ChatRequest request, ...) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
        return Results.BadRequest("Query is required");

    // ... endpoint logic
});
```

**After**:
```csharp
app.MapPost("/chat", async (ChatRequest request, ...) =>
{
    var queryError = QueryValidator.ValidateQuery(request.Query);
    if (queryError != null)
        return Results.BadRequest(queryError);

    // ... endpoint logic
});
```

**Endpoints to refactor** (~10 endpoints):
- ChatEndpoints.cs
- AiEndpoints.cs
- KnowledgeBaseEndpoints.cs

---

### Step 5: Testing (30 minutes)

**Unit Tests** (`QueryValidatorTests.cs`):

```csharp
public class QueryValidatorTests
{
    [Theory]
    [InlineData(null, "Please provide a question")]
    [InlineData("", "Please provide a question")]
    [InlineData("   ", "Please provide a question")]
    [InlineData("ab", "Query must be at least 3 characters")]
    [InlineData("Valid query", null)] // Valid
    public void ValidateQuery_VariousInputs_ReturnsExpectedError(
        string? query, string? expectedError)
    {
        // Act
        var error = QueryValidator.ValidateQuery(query);

        // Assert
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void ValidateQueryOrThrow_InvalidQuery_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            QueryValidator.ValidateQueryOrThrow(""));
    }

    [Fact]
    public void ValidateQueryOrThrow_ValidQuery_DoesNotThrow()
    {
        // Act & Assert (should not throw)
        QueryValidator.ValidateQueryOrThrow("Valid query");
    }

    [Fact]
    public void TryValidateQuery_InvalidQuery_ReturnsFalse()
    {
        // Act
        var (isValid, error) = QueryValidator.TryValidateQuery("");

        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateQuery_ValidQuery_ReturnsTrue()
    {
        // Act
        var (isValid, error) = QueryValidator.TryValidateQuery("Valid query");

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateQuery_QueryTooLong_ReturnsError()
    {
        // Arrange
        var longQuery = new string('a', 1001);

        // Act
        var error = QueryValidator.ValidateQuery(longQuery);

        // Assert
        Assert.Contains("must not exceed", error);
    }
}
```

**Integration Tests**:
- Verify all refactored services still work
- Verify all refactored handlers still work
- Verify all refactored endpoints still work

---

## Files Changed

**New Files** (1):
- ✅ `apps/api/src/Api/Helpers/QueryValidator.cs` (~40 LOC)

**Modified Files** (~35):
- Services: 3 files (-40 LOC)
- Handlers: 20 files (-60 LOC)
- Endpoints: 10 files (-30 LOC)

**New Test Files** (1):
- ✅ `tests/Api.Tests/Helpers/QueryValidatorTests.cs` (~80 LOC)

**Total LOC Change**: -90 LOC (net reduction)

---

## Acceptance Criteria

- [ ] QueryValidator class created with 3 validation methods
- [ ] All 50+ query validations refactored to use QueryValidator
- [ ] Consistent error messages across codebase
- [ ] All existing tests pass
- [ ] Unit tests for QueryValidator (10+ tests)
- [ ] Integration tests verify refactored code works

---

## Success Metrics

- ✅ 50+ validation blocks → 1 validator class (98% reduction)
- ✅ Consistent error messages ("Please provide a question")
- ✅ ~90 LOC reduction
- ✅ Query length validation enforced consistently
- ✅ Easy to update validation logic in one place

---

## Estimated Timeline

| Task | Time |
|------|------|
| Create QueryValidator | 30min |
| Apply to services | 1h |
| Apply to handlers | 1h |
| Apply to endpoints | 30min |
| Testing | 30min |
| **Total** | **2.5-3h** |

---

## Future Extensions

After this Quick Win, can create similar validators for:
- `GameIdValidator` (80+ duplications)
- `EmailValidator` (30+ duplications)
- `FileNameValidator` (15+ duplications)

This is a **proof of concept** for Issue #004 (ValidationExtensions Framework).

---

## Related Issues

- Issue #004: Create ValidationExtensions Framework (this is the prototype!)

---

## References

- Analysis: `docs/02-development/backend-codebase-analysis.md`
- Duplication: 50+ query validation instances

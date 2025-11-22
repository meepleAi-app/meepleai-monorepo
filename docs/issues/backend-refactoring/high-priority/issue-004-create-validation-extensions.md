# Issue #004: Create ValidationExtensions Framework

**Priority**: 🟠 HIGH
**Effort**: 20-30 hours
**Impact**: ⭐⭐ MEDIUM (20-30% code reduction)
**Category**: Code Duplication Elimination
**Status**: Not Started

---

## Problem Description

**399 instances** of `string.IsNullOrWhiteSpace()` validation checks are duplicated across the codebase, creating:
- **Code duplication** (~800+ LOC of repeated validation logic)
- **Inconsistent error messages** (same validation, different messages)
- **Maintenance burden** (changing validation logic requires editing 399 locations)
- **Testing overhead** (same validation tested 399 times)

### Distribution

| Location | Instances |
|----------|-----------|
| `BoundedContexts/` | 148 |
| `Services/` | 100+ |
| `Routing/` | 64 |
| Other | 87 |
| **Total** | **399** |

---

## Current Duplication Examples

### Pattern 1: Query Validation (repeated 50+ times)
```csharp
// In RagService.cs
if (string.IsNullOrWhiteSpace(query))
{
    return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
}

// In HybridSearchService.cs - SAME VALIDATION, DIFFERENT MESSAGE
if (string.IsNullOrWhiteSpace(query))
{
    throw new ValidationException("Query cannot be empty");
}

// In AskQuestionQueryHandler.cs - SAME VALIDATION, YET ANOTHER MESSAGE
if (string.IsNullOrWhiteSpace(request.Query))
{
    return Results.BadRequest("Query is required");
}
```

### Pattern 2: GameId Validation (repeated 80+ times)
```csharp
// In GameEndpoints.cs
if (string.IsNullOrWhiteSpace(gameId))
{
    return Results.BadRequest("Game ID is required");
}

// In RagService.cs - SAME VALIDATION
if (string.IsNullOrWhiteSpace(gameId))
{
    return new QaResponse("Game ID is required.", Array.Empty<Snippet>());
}

// In GetGameByIdQueryHandler.cs - SAME VALIDATION
if (string.IsNullOrWhiteSpace(request.GameId))
{
    throw new ValidationException("Game ID cannot be empty");
}
```

### Pattern 3: Email Validation (repeated 30+ times)
```csharp
// In AuthEndpoints.cs
if (string.IsNullOrWhiteSpace(email))
{
    return Results.BadRequest("Email is required");
}

// In RegisterCommandHandler.cs - SAME VALIDATION
if (string.IsNullOrWhiteSpace(request.Email))
{
    throw new ValidationException("Email is required");
}
```

---

## Proposed Solution

Create a **ValidationExtensions** framework in `SharedKernel` with:
1. **Result<T> pattern** for functional validation
2. **Common validators** for repeated patterns
3. **Consistent error messages**
4. **Composable validation rules**

### File Structure

```
apps/api/src/Api/SharedKernel/
├── Validation/
│   ├── ValidationExtensions.cs      # Core validation methods
│   ├── Result.cs                     # Result<T> type
│   ├── ValidationError.cs            # Error details
│   └── CommonValidators.cs           # Domain-specific validators
```

---

## Implementation Plan

### Phase 1: Create Result<T> Pattern (4 hours)

#### 1.1 Create Result.cs

**File**: `apps/api/src/Api/SharedKernel/Validation/Result.cs`

```csharp
namespace Api.SharedKernel.Validation;

/// <summary>
/// Represents the result of an operation that may fail.
/// Used for functional validation without exceptions.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }
    public ValidationError Error { get; }

    private Result(bool isSuccess, T value, ValidationError error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value)
        => new(true, value, ValidationError.None);

    public static Result<T> Failure(string errorMessage, string? fieldName = null)
        => new(false, default!, new ValidationError(errorMessage, fieldName));

    public static Result<T> Failure(ValidationError error)
        => new(false, default!, error);

    // Map: Transform successful result
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        => IsSuccess
            ? Result<TNew>.Success(mapper(Value))
            : Result<TNew>.Failure(Error);

    // Bind: Chain validations
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
        => IsSuccess
            ? binder(Value)
            : Result<TNew>.Failure(Error);
}

/// <summary>
/// Non-generic result for operations without return value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ValidationError Error { get; }

    private Result(bool isSuccess, ValidationError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, ValidationError.None);
    public static Result Failure(string errorMessage, string? fieldName = null)
        => new(false, new ValidationError(errorMessage, fieldName));
}
```

---

#### 1.2 Create ValidationError.cs

**File**: `apps/api/src/Api/SharedKernel/Validation/ValidationError.cs`

```csharp
namespace Api.SharedKernel.Validation;

public record ValidationError(string Message, string? FieldName = null)
{
    public static readonly ValidationError None = new(string.Empty);

    public bool HasError => !string.IsNullOrEmpty(Message);

    public override string ToString()
        => FieldName != null
            ? $"{FieldName}: {Message}"
            : Message;
}

public class ValidationException : Exception
{
    public ValidationError Error { get; }

    public ValidationException(ValidationError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public ValidationException(string message, string? fieldName = null)
        : this(new ValidationError(message, fieldName))
    {
    }
}
```

---

### Phase 2: Create ValidationExtensions (8 hours)

#### 2.1 Core String Validation

**File**: `apps/api/src/Api/SharedKernel/Validation/ValidationExtensions.cs`

```csharp
namespace Api.SharedKernel.Validation;

public static class ValidationExtensions
{
    /// <summary>
    /// Validates that a string is not null, empty, or whitespace.
    /// </summary>
    public static Result<string> ValidateNotEmpty(
        this string? value,
        string fieldName,
        string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var message = customMessage ?? $"{fieldName} cannot be empty";
            return Result<string>.Failure(message, fieldName);
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates string length is within range.
    /// </summary>
    public static Result<string> ValidateLength(
        this string value,
        string fieldName,
        int minLength,
        int? maxLength = null)
    {
        if (value.Length < minLength)
        {
            return Result<string>.Failure(
                $"{fieldName} must be at least {minLength} characters",
                fieldName);
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            return Result<string>.Failure(
                $"{fieldName} must not exceed {maxLength.Value} characters",
                fieldName);
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a value is not null.
    /// </summary>
    public static Result<T> ValidateNotNull<T>(
        this T? value,
        string fieldName) where T : class
    {
        if (value == null)
        {
            return Result<T>.Failure($"{fieldName} is required", fieldName);
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a collection is not null or empty.
    /// </summary>
    public static Result<IEnumerable<T>> ValidateNotEmpty<T>(
        this IEnumerable<T>? collection,
        string fieldName)
    {
        if (collection == null || !collection.Any())
        {
            return Result<IEnumerable<T>>.Failure(
                $"{fieldName} cannot be empty",
                fieldName);
        }

        return Result<IEnumerable<T>>.Success(collection);
    }

    /// <summary>
    /// Validates email format (basic regex).
    /// </summary>
    public static Result<string> ValidateEmail(this string email)
    {
        var result = email.ValidateNotEmpty("Email");
        if (result.IsFailure) return result;

        var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!Regex.IsMatch(email, emailRegex))
        {
            return Result<string>.Failure(
                "Invalid email format",
                "Email");
        }

        return Result<string>.Success(email);
    }

    /// <summary>
    /// Validates that a numeric value is within range.
    /// </summary>
    public static Result<int> ValidateRange(
        this int value,
        string fieldName,
        int min,
        int max)
    {
        if (value < min || value > max)
        {
            return Result<int>.Failure(
                $"{fieldName} must be between {min} and {max}",
                fieldName);
        }

        return Result<int>.Success(value);
    }

    /// <summary>
    /// Validates that a Guid is not empty.
    /// </summary>
    public static Result<Guid> ValidateNotEmpty(this Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            return Result<Guid>.Failure($"{fieldName} is required", fieldName);
        }

        return Result<Guid>.Success(value);
    }
}
```

---

### Phase 3: Create Domain-Specific Validators (6 hours)

#### 3.1 CommonValidators.cs

**File**: `apps/api/src/Api/SharedKernel/Validation/CommonValidators.cs`

```csharp
namespace Api.SharedKernel.Validation;

/// <summary>
/// Common validators for frequently validated types in MeepleAI.
/// Reduces duplication of validation logic across the codebase.
/// </summary>
public static class CommonValidators
{
    // ========== Game-related Validators ==========

    public static Result<string> ValidateGameId(string? gameId)
        => gameId.ValidateNotEmpty("Game ID", "Game ID is required");

    public static Result<string> ValidateGameTitle(string? title)
        => title.ValidateNotEmpty("Game title", "Game title is required")
            .Bind(t => t.ValidateLength("Game title", 1, 200));

    // ========== Query Validators ==========

    public static Result<string> ValidateQuery(string? query)
        => query.ValidateNotEmpty("Query", "Please provide a question")
            .Bind(q => q.ValidateLength("Query", 3, 1000));

    public static Result<string> ValidateTopic(string? topic)
        => topic.ValidateNotEmpty("Topic", "Topic is required")
            .Bind(t => t.ValidateLength("Topic", 3, 200));

    // ========== User-related Validators ==========

    public static Result<string> ValidateEmail(string? email)
        => email.ValidateNotEmpty("Email")
            .Bind(e => e.ValidateEmail());

    public static Result<string> ValidatePassword(string? password)
        => password.ValidateNotEmpty("Password", "Password is required")
            .Bind(p => p.ValidateLength("Password", 8, 100))
            .Bind(ValidatePasswordComplexity);

    private static Result<string> ValidatePasswordComplexity(string password)
    {
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasUpper || !hasLower || !hasDigit)
        {
            return Result<string>.Failure(
                "Password must contain uppercase, lowercase, and digit",
                "Password");
        }

        return Result<string>.Success(password);
    }

    public static Result<string> ValidateDisplayName(string? displayName)
        => displayName.ValidateNotEmpty("Display name", "Display name is required")
            .Bind(n => n.ValidateLength("Display name", 2, 50));

    // ========== Configuration Validators ==========

    public static Result<string> ValidateConfigKey(string? key)
        => key.ValidateNotEmpty("Configuration key", "Configuration key is required")
            .Bind(k => k.ValidateLength("Configuration key", 1, 100));

    public static Result<string> ValidateConfigValue(string? value)
        => value.ValidateNotEmpty("Configuration value", "Configuration value is required");

    // ========== Pagination Validators ==========

    public static Result<int> ValidatePage(int page)
        => page.ValidateRange("Page", 1, int.MaxValue);

    public static Result<int> ValidatePageSize(int pageSize)
        => pageSize.ValidateRange("Page size", 1, 100);

    // ========== API Key Validators ==========

    public static Result<string> ValidateApiKeyName(string? name)
        => name.ValidateNotEmpty("API key name", "API key name is required")
            .Bind(n => n.ValidateLength("API key name", 3, 50));

    // ========== File Validators ==========

    public static Result<string> ValidateFileName(string? fileName)
        => fileName.ValidateNotEmpty("File name", "File name is required")
            .Bind(f => f.ValidateLength("File name", 1, 255));

    public static Result<string> ValidatePdfFile(string? fileName)
        => ValidateFileName(fileName)
            .Bind(f =>
            {
                if (!f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure("File must be a PDF", "File name");
                }
                return Result<string>.Success(f);
            });
}
```

---

### Phase 4: Refactor Existing Code (10 hours)

#### 4.1 Example: Refactor RagService.cs

**Before** (repeated 8 times in RagService):
```csharp
public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
    }

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return new QaResponse("Game ID is required.", Array.Empty<Snippet>());
    }

    // ... rest of method
}
```

**After** (using ValidationExtensions):
```csharp
public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    // Validate query
    var queryResult = CommonValidators.ValidateQuery(query);
    if (queryResult.IsFailure)
        return new QaResponse(queryResult.Error.Message, Array.Empty<Snippet>());

    // Validate gameId
    var gameIdResult = CommonValidators.ValidateGameId(gameId);
    if (gameIdResult.IsFailure)
        return new QaResponse(gameIdResult.Error.Message, Array.Empty<Snippet>());

    // ... rest of method (use queryResult.Value and gameIdResult.Value)
}
```

**Even better** (with early return pattern):
```csharp
public async Task<QaResponse> AskAsync(string gameId, string query, ...)
{
    return await ValidateInputs(gameId, query)
        .Bind(async validated =>
        {
            // ... business logic using validated.gameId and validated.query
            return await GenerateResponseAsync(validated.gameId, validated.query);
        })
        .Match(
            success: response => response,
            failure: error => new QaResponse(error.Message, Array.Empty<Snippet>()));
}

private Result<(string gameId, string query)> ValidateInputs(string gameId, string query)
{
    var gameIdResult = CommonValidators.ValidateGameId(gameId);
    if (gameIdResult.IsFailure) return Result<(string, string)>.Failure(gameIdResult.Error);

    var queryResult = CommonValidators.ValidateQuery(query);
    if (queryResult.IsFailure) return Result<(string, string)>.Failure(queryResult.Error);

    return Result<(string, string)>.Success((gameIdResult.Value, queryResult.Value));
}
```

---

#### 4.2 Example: Refactor Handlers

**Before** (in AskQuestionQueryHandler.cs):
```csharp
public class AskQuestionQueryHandler : IRequestHandler<AskQuestionQuery, QaResponse>
{
    public async Task<QaResponse> Handle(AskQuestionQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.GameId))
            throw new ValidationException("Game ID is required");

        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ValidationException("Query is required");

        // ... business logic
    }
}
```

**After** (using ValidationExtensions):
```csharp
public class AskQuestionQueryHandler : IRequestHandler<AskQuestionQuery, QaResponse>
{
    public async Task<QaResponse> Handle(AskQuestionQuery request, CancellationToken ct)
    {
        // Validate all inputs
        var validation = ValidateRequest(request);
        if (validation.IsFailure)
            throw new ValidationException(validation.Error);

        // ... business logic
    }

    private Result ValidateRequest(AskQuestionQuery request)
    {
        var gameIdResult = CommonValidators.ValidateGameId(request.GameId);
        if (gameIdResult.IsFailure) return Result.Failure(gameIdResult.Error);

        var queryResult = CommonValidators.ValidateQuery(request.Query);
        if (queryResult.IsFailure) return Result.Failure(queryResult.Error);

        return Result.Success();
    }
}
```

---

#### 4.3 Refactoring Priority List

Target files with most duplication first:

**Phase 4A - Services (100+ instances)**:
1. RagService.cs (8 validations)
2. HybridSearchService.cs (5 validations)
3. QdrantService.cs (4 validations)
4. EmbeddingService.cs (6 validations)
5. ConfigurationService.cs (12 validations)

**Phase 4B - Handlers (148 instances)**:
1. All Query handlers in KnowledgeBase (20+ handlers)
2. All Command handlers in Authentication (15+ handlers)
3. All handlers in GameManagement (25+ handlers)
4. All handlers in DocumentProcessing (10+ handlers)

**Phase 4C - Endpoints (64 instances)**:
1. AdminEndpoints.cs (10 validations)
2. AuthEndpoints.cs (9 validations)
3. AiEndpoints.cs (8 validations)
4. GameEndpoints.cs (12 validations)

---

### Phase 5: Testing (6 hours)

#### 5.1 Unit Tests for ValidationExtensions

**File**: `tests/Api.Tests/SharedKernel/Validation/ValidationExtensionsTests.cs`

```csharp
public class ValidationExtensionsTests
{
    [Fact]
    public void ValidateNotEmpty_WithNull_ReturnsFailure()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.ValidateNotEmpty("Test field");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Test field", result.Error.FieldName);
        Assert.Contains("cannot be empty", result.Error.Message);
    }

    [Fact]
    public void ValidateNotEmpty_WithWhitespace_ReturnsFailure()
    {
        // Arrange
        var value = "   ";

        // Act
        var result = value.ValidateNotEmpty("Test field");

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateNotEmpty_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "valid value";

        // Act
        var result = value.ValidateNotEmpty("Test field");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("valid value", result.Value);
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("@example.com", false)]
    [InlineData("test@", false)]
    public void ValidateEmail_VariousInputs_ReturnsExpectedResult(
        string email, bool expectedSuccess)
    {
        // Act
        var result = email.ValidateEmail();

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }

    [Fact]
    public void ValidateLength_BelowMinimum_ReturnsFailure()
    {
        // Arrange
        var value = "ab";

        // Act
        var result = value.ValidateLength("Test field", minLength: 3);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("at least 3 characters", result.Error.Message);
    }

    [Fact]
    public void ValidateRange_OutsideRange_ReturnsFailure()
    {
        // Arrange
        var value = 150;

        // Act
        var result = value.ValidateRange("Test field", min: 1, max: 100);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("between 1 and 100", result.Error.Message);
    }
}
```

---

#### 5.2 Unit Tests for CommonValidators

**File**: `tests/Api.Tests/SharedKernel/Validation/CommonValidatorsTests.cs`

```csharp
public class CommonValidatorsTests
{
    [Theory]
    [InlineData("tic-tac-toe", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void ValidateGameId_VariousInputs_ReturnsExpectedResult(
        string? gameId, bool expectedSuccess)
    {
        // Act
        var result = CommonValidators.ValidateGameId(gameId);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }

    [Theory]
    [InlineData("How to play?", true)]
    [InlineData("ab", false)] // Too short
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateQuery_VariousInputs_ReturnsExpectedResult(
        string? query, bool expectedSuccess)
    {
        // Act
        var result = CommonValidators.ValidateQuery(query);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }

    [Theory]
    [InlineData("ValidPass1", true)]
    [InlineData("short", false)] // Too short
    [InlineData("alllowercase1", false)] // No uppercase
    [InlineData("ALLUPPERCASE1", false)] // No lowercase
    [InlineData("NoDigits", false)] // No digit
    public void ValidatePassword_VariousInputs_ReturnsExpectedResult(
        string? password, bool expectedSuccess)
    {
        // Act
        var result = CommonValidators.ValidatePassword(password);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ValidatePage_VariousInputs_ReturnsExpectedResult(
        int page, bool expectedSuccess)
    {
        // Act
        var result = CommonValidators.ValidatePage(page);

        // Assert
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }
}
```

---

#### 5.3 Integration Tests

**Verify refactored code works**:
```bash
# Run all tests
dotnet test

# Verify no regressions
dotnet test --filter "FullyQualifiedName~Validation"
```

---

## Migration Checklist

### Pre-migration
- [ ] Create ValidationExtensions framework
- [ ] Create CommonValidators
- [ ] Write comprehensive unit tests
- [ ] Document migration pattern

### During migration
- [ ] Identify all validation locations (grep for IsNullOrWhiteSpace)
- [ ] Refactor one file at a time
- [ ] Test after each refactoring
- [ ] Track progress (399 instances total)

### Post-migration
- [ ] Verify all 399 instances refactored
- [ ] Run full test suite
- [ ] Code review for consistency
- [ ] Update documentation

---

## Success Metrics

- ✅ ValidationExtensions framework created
- ✅ CommonValidators with 15+ domain validators
- ✅ 399 validation instances refactored to use framework
- ✅ ~800 LOC code reduction (20-30% of duplication)
- ✅ Consistent error messages across codebase
- ✅ 50+ unit tests for validation framework
- ✅ All existing tests pass
- ✅ Zero breaking changes to API

---

## Dependencies

**Blocks**:
- None

**Blocked by**:
- None

**Synergizes with**:
- All other refactoring issues (can apply ValidationExtensions during refactoring)

---

## Estimated Timeline

**Total Effort**: 20-30 hours

| Phase | Task | Hours |
|-------|------|-------|
| 1 | Result<T> pattern | 4h |
| 2 | ValidationExtensions | 8h |
| 3 | CommonValidators | 6h |
| 4 | Refactor existing code | 10h |
| 5 | Testing | 6h |
| - | **Buffer** | 6-10h |

**Recommended approach**: 1-2 week sprint (incremental migration)

---

## References

- Analysis: `docs/02-development/backend-codebase-analysis.md`
- Duplication Count: 399 instances (verified via grep)

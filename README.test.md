# Testing Guidelines

This document provides guidelines for writing and organizing tests in the MeepleAI monorepo.

## Table of Contents

- [Naming Convention](#naming-convention)
- [Frontend Testing (Jest/TypeScript)](#frontend-testing-jesttypescript)
- [Backend Testing (xUnit/C#)](#backend-testing-xunitc)
- [Common Patterns](#common-patterns)
- [Anti-Patterns](#anti-patterns)

## Naming Convention

We follow **BDD-style naming conventions** for all tests to ensure clarity and consistency across the codebase. Test names should clearly describe:

1. **What behavior is being tested** (the expected outcome)
2. **Under what conditions** (the context or scenario)

This approach makes tests self-documenting and easier to understand for all team members.

### Why BDD-Style?

BDD (Behavior-Driven Development) naming provides:
- **Clarity**: Anyone can understand what the test verifies without reading the implementation
- **Documentation**: Test names serve as living documentation of system behavior
- **Consistency**: Uniform style across frontend and backend tests
- **Maintainability**: Easier to identify what broke when a test fails

## Frontend Testing (Jest/TypeScript)

**Location**: `apps/web/src/**/__tests__/`

**Framework**: Jest with React Testing Library

### Naming Pattern

```typescript
it('should [expected behavior] when [condition]')
```

### Examples

```typescript
// ✅ Good Examples

describe('Upload Page', () => {
  it('should disable upload button when no game is selected', () => {
    // Test implementation
  });

  it('should show error message when API returns 500', () => {
    // Test implementation
  });

  it('should allow creating a new game when user clicks "Create New"', () => {
    // Test implementation
  });

  it('should display validation errors when required fields are empty', () => {
    // Test implementation
  });

  it('should enable submit button when all required fields are filled', () => {
    // Test implementation
  });
});

describe('API Client', () => {
  it('should include credentials when making requests', () => {
    // Test implementation
  });

  it('should redirect to login when receiving 401 response', () => {
    // Test implementation
  });

  it('should retry failed requests when network error occurs', () => {
    // Test implementation
  });
});
```

### When to Use "when" vs Simple Descriptions

For simple, unconditional behaviors, you can omit "when":

```typescript
// Simple behaviors (no specific condition)
it('should render the page title')
it('should initialize with default values')

// Complex behaviors (condition-dependent)
it('should show loading spinner when data is fetching')
it('should display error boundary when component throws error')
```

### Nested Describes for Context

Use `describe` blocks to group related scenarios:

```typescript
describe('ChatPage', () => {
  describe('when user is authenticated', () => {
    it('should load previous chat history');
    it('should enable message input field');
  });

  describe('when user is not authenticated', () => {
    it('should redirect to login page');
    it('should clear any stored session data');
  });
});
```

## Backend Testing (xUnit/C#)

**Location**: `apps/api/tests/Api.Tests/`

**Framework**: xUnit with Moq and Testcontainers

### Naming Pattern

```csharp
Should_[ExpectedBehavior]_When_[Condition]
```

### Examples

```csharp
// ✅ Good Examples

public class GameServiceTests
{
    [Fact]
    public async Task Should_ReturnGame_When_GameExists()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public async Task Should_Return404_When_GameNotFound()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public async Task Should_CreateGame_When_ValidDataProvided()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_GameNameIsEmpty()
    {
        // Arrange
        // Act & Assert
    }
}

public class AuthServiceTests
{
    [Fact]
    public async Task Should_ReturnSuccess_When_CredentialsAreValid()
    {
        // Test implementation
    }

    [Fact]
    public async Task Should_Return401_When_UserNotAuthenticated()
    {
        // Test implementation
    }

    [Fact]
    public async Task Should_CreateSession_When_LoginSucceeds()
    {
        // Test implementation
    }
}
```

### Parameterized Tests (Theory)

For data-driven tests using `[Theory]`, name the test to describe the general behavior:

```csharp
[Theory]
[InlineData("")]
[InlineData(null)]
[InlineData("   ")]
public async Task Should_ThrowValidationException_When_GameNameIsInvalid(string invalidName)
{
    // Test implementation
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(int.MinValue)]
public async Task Should_ReturnBadRequest_When_InvalidIdProvided(int invalidId)
{
    // Test implementation
}
```

### Integration Tests

Integration tests follow the same pattern:

```csharp
public class GameEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_ReturnGamesList_When_GamesExist()
    {
        // Test implementation
    }

    [Fact]
    public async Task Should_ReturnEmptyArray_When_NoGamesExist()
    {
        // Test implementation
    }
}
```

## Common Patterns

| Scenario | Frontend (Jest) | Backend (xUnit) |
|----------|-----------------|-----------------|
| **Happy Path** | `it('should load games when component mounts')` | `Should_ReturnGames_When_RequestIsValid` |
| **Error Handling** | `it('should show error when API fails')` | `Should_ThrowException_When_DataIsInvalid` |
| **Authentication** | `it('should redirect when user is not authenticated')` | `Should_Return401_When_UserNotAuthenticated` |
| **Validation** | `it('should disable submit when form is invalid')` | `Should_ReturnBadRequest_When_ValidationFails` |
| **Edge Cases** | `it('should handle empty list when no data available')` | `Should_ReturnEmptyList_When_NoRecordsExist` |
| **State Changes** | `it('should update UI when data changes')` | `Should_UpdateDatabase_When_EntityModified` |
| **Async Operations** | `it('should show loading spinner when fetching data')` | `Should_ReturnTask_When_OperationIsAsync` |
| **Permissions** | `it('should hide admin button when user is not admin')` | `Should_Return403_When_UserLacksPermission` |

## Anti-Patterns

### ❌ Avoid These Patterns

#### Frontend (Jest)

```typescript
// ❌ Too vague - doesn't describe behavior
it('upload button')
it('handles errors')

// ❌ Missing condition - when does this happen?
it('shows error message')
it('disables button')

// ❌ Implementation details instead of behavior
it('calls setState with new value')
it('triggers onClick handler')

// ❌ Too verbose
it('should make sure that the upload button is disabled when there is no game selected by the user in the dropdown')

// ✅ Better alternatives
it('should disable upload button when no game is selected')
it('should show error message when API returns 500')
it('should update game state when user selects game')
it('should disable upload button when no game is selected')
```

#### Backend (xUnit)

```csharp
// ❌ Old-style method_outcome naming
[Fact]
public async Task GetGame_Returns200()

// ❌ Missing condition
[Fact]
public async Task Should_ReturnError()

// ❌ Implementation details
[Fact]
public async Task Should_CallDatabaseQuery_When_GettingGame()

// ❌ Too verbose
[Fact]
public async Task Should_MakeSureToReturnTheCorrectGameObject_When_TheUserRequestsAGameThatExistsInTheDatabase()

// ✅ Better alternatives
Should_ReturnGame_When_GameExists
Should_ThrowException_When_InvalidInput
Should_UpdateEntity_When_ValidDataProvided
Should_ReturnGame_When_GameExists
```

### General Anti-Patterns (Both)

```
❌ Test_1, Test_2, Test_3
❌ TestMethod, TestCase, Example
❌ Works, ItWorks, ThisWorks
❌ Bug123, FixForIssue456
```

## Additional Resources

- **Frontend Tests**: See `apps/web/README.md` for Jest configuration and coverage requirements
- **Backend Tests**: See `apps/api/tests/README.md` for xUnit patterns and Testcontainers setup
- **CI/CD**: Test execution and coverage thresholds defined in `.github/workflows/ci.yml`

## Questions?

If you're unsure about naming a specific test:

1. Ask yourself: "What behavior am I testing?"
2. Ask yourself: "Under what condition does this behavior occur?"
3. Combine them: `should [behavior] when [condition]` (frontend) or `Should_[Behavior]_When_[Condition]` (backend)

When in doubt, favor clarity over brevity. A slightly longer, clear test name is better than a short, ambiguous one.

# MeepleAI Test Writing Guide

**Goal**: Write your first test in under 1 hour

## Table of Contents

1. [Overview](#overview)
2. [Test Types](#test-types)
3. [Frontend Testing (Jest + React Testing Library)](#frontend-testing-jest--react-testing-library)
4. [Backend Testing (xUnit + .NET)](#backend-testing-xunit--net)
5. [E2E Testing (Playwright)](#e2e-testing-playwright)
6. [Testing Patterns](#testing-patterns)
7. [Mocking Strategies](#mocking-strategies)
8. [Common Pitfalls](#common-pitfalls)
9. [Best Practices](#best-practices)
10. [Resources](#resources)

---

## Overview

This guide provides practical examples and patterns for writing tests in the MeepleAI monorepo. All examples are taken from real production tests in this codebase.

**Quick Start**:
- Frontend: Run `pnpm test` in `apps/web`
- Backend: Run `dotnet test` in `apps/api`
- E2E: Run `pnpm test:e2e` in `apps/web`

---

## Test Types

### Unit Tests
Test individual functions, components, or services in isolation.

**When to use**: Testing pure logic, utility functions, isolated components
**Speed**: Fast (<10ms per test)
**Dependencies**: Mocked

### Integration Tests
Test how multiple units work together (e.g., service + database).

**When to use**: Testing service interactions, database operations, API endpoints
**Speed**: Medium (100ms-1s per test)
**Dependencies**: Real (Testcontainers) or partially mocked

### E2E Tests
Test complete user workflows through the UI.

**When to use**: Testing critical user journeys, form submissions, multi-page flows
**Speed**: Slow (5-30s per test)
**Dependencies**: Real frontend + mocked/test backend

---

## Frontend Testing (Jest + React Testing Library)

### Component Testing

#### Basic Component Test
```typescript
// apps/web/__tests__/components/LoadingButton.test.tsx
import { render, screen } from '@testing-library/react';
import { LoadingButton } from '@/components/LoadingButton';

describe('LoadingButton', () => {
  it('renders button text when not loading', () => {
    render(<LoadingButton>Save Changes</LoadingButton>);
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
  });

  it('shows loading spinner when isLoading is true', () => {
    render(<LoadingButton isLoading>Save Changes</LoadingButton>);
    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it('disables button when disabled prop is true', () => {
    render(<LoadingButton disabled>Save Changes</LoadingButton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
```

**Key patterns**:
- Use `screen.getByRole()` for accessible queries
- Use regex for case-insensitive text matching: `/save changes/i`
- Test behavior, not implementation details

#### Component with User Interaction
```typescript
// apps/web/__tests__/components/DiffViewModeToggle.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiffViewModeToggle } from '@/components/DiffViewModeToggle';

describe('DiffViewModeToggle', () => {
  it('calls onChange with "unified" when Unified button clicked', async () => {
    const user = userEvent.setup();
    const handleChange = jest.fn();

    render(<DiffViewModeToggle mode="split" onChange={handleChange} />);

    const unifiedButton = screen.getByRole('button', { name: /unified/i });
    await user.click(unifiedButton);

    expect(handleChange).toHaveBeenCalledWith('unified');
  });

  it('applies active styling to current mode', () => {
    render(<DiffViewModeToggle mode="split" onChange={jest.fn()} />);

    const splitButton = screen.getByRole('button', { name: /split/i });
    expect(splitButton).toHaveClass('bg-blue-500');
  });
});
```

**Key patterns**:
- Always `await` user interactions with `userEvent`
- Set up `userEvent` with `userEvent.setup()` before rendering
- Use `jest.fn()` for mock callbacks
- Verify callbacks were called with correct arguments

### Hook Testing

```typescript
// apps/web/__tests__/hooks/useToast.test.tsx
import { renderHook, act } from '@testing-library/react';
import { useToast } from '@/hooks/useToast';

describe('useToast', () => {
  it('adds success toast', () => {
    const { result } = renderHook(() => useToast());

    act(() => {
      result.current.success('Operation successful');
    });

    expect(result.current.toasts).toHaveLength(1);
    expect(result.current.toasts[0]).toMatchObject({
      type: 'success',
      message: 'Operation successful'
    });
  });

  it('removes toast after timeout', async () => {
    jest.useFakeTimers();
    const { result } = renderHook(() => useToast());

    act(() => {
      result.current.success('Test');
    });

    expect(result.current.toasts).toHaveLength(1);

    act(() => {
      jest.advanceTimersByTime(5000);
    });

    expect(result.current.toasts).toHaveLength(0);

    jest.useRealTimers();
  });
});
```

**Key patterns**:
- Use `renderHook()` to test custom hooks
- Wrap state updates in `act()` to ensure React updates are flushed
- Use `jest.useFakeTimers()` for time-based logic
- Always restore real timers with `jest.useRealTimers()` in cleanup

### ⚠️ AVOID: container.firstChild Anti-Pattern

**NEVER use `container.firstChild`** - it bypasses accessibility and semantics.

```typescript
// ❌ WRONG - Anti-pattern
const { container } = render(<Component />);
expect(container.firstChild).toBeInTheDocument();

// ✅ CORRECT - Semantic query
render(<Component />);
expect(screen.getByRole('button')).toBeInTheDocument();
// OR
expect(screen.getByText('Submit')).toBeInTheDocument();
```

**Why avoid container.firstChild?**
- Bypasses accessibility tree
- Fragile to DOM structure changes
- Doesn't reflect how users/assistive tech interact
- Fails to test semantic HTML

**When is container acceptable?**
- Snapshot tests: `expect(container.firstChild).toMatchSnapshot()`
- Specific styling assertions: `container.querySelector('.custom-class')`
- Complex DOM structure queries as last resort

**Prefer this query priority:**
1. `getByRole` - Semantic, accessible (button, textbox, alert)
2. `getByLabelText` - Form fields with labels
3. `getByText` - Visible text content
4. `getByTestId` - Last resort for complex cases

### Context Provider Testing

```typescript
// apps/web/__tests__/providers/ChatProvider.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatProvider, useChatContext } from '@/providers/ChatProvider';

const TestComponent = () => {
  const { messages, sendMessage } = useChatContext();
  return (
    <div>
      <div data-testid="message-count">{messages.length}</div>
      <button onClick={() => sendMessage('Hello')}>Send</button>
    </div>
  );
};

describe('ChatProvider', () => {
  it('provides chat context to children', async () => {
    const user = userEvent.setup();

    render(
      <ChatProvider>
        <TestComponent />
      </ChatProvider>
    );

    expect(screen.getByTestId('message-count')).toHaveTextContent('0');

    await user.click(screen.getByRole('button', { name: /send/i }));

    await waitFor(() => {
      expect(screen.getByTestId('message-count')).toHaveTextContent('1');
    });
  });
});
```

**Key patterns**:
- Create a test component that consumes the context
- Use `waitFor()` for async state updates
- Test provider behavior through consumer component

### API Mocking

```typescript
// apps/web/__tests__/pages/chat.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import ChatPage from '@/pages/chat';
import * as api from '@/lib/api';

jest.mock('@/lib/api');

describe('ChatPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('displays chat messages from API', async () => {
    const mockMessages = [
      { id: '1', content: 'Hello', role: 'user', timestamp: new Date() },
      { id: '2', content: 'Hi there!', role: 'assistant', timestamp: new Date() }
    ];

    jest.spyOn(api, 'getChatMessages').mockResolvedValue(mockMessages);

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText('Hello')).toBeInTheDocument();
      expect(screen.getByText('Hi there!')).toBeInTheDocument();
    });
  });

  it('handles API errors gracefully', async () => {
    jest.spyOn(api, 'getChatMessages').mockRejectedValue(new Error('Network error'));

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText(/error loading messages/i)).toBeInTheDocument();
    });
  });
});
```

**Key patterns**:
- Mock entire modules with `jest.mock()`
- Use `jest.spyOn()` to mock specific functions
- Clear mocks in `beforeEach()` to prevent test pollution
- Test both success and error cases

### Async State Updates

```typescript
// apps/web/__tests__/components/ErrorBoundary.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { ErrorBoundary } from '@/components/ErrorBoundary';

const ThrowError = () => {
  throw new Error('Test error');
};

describe('ErrorBoundary', () => {
  beforeEach(() => {
    jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('catches errors and displays fallback UI', async () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
    });
  });

  it('logs error to console', async () => {
    render(
      <ErrorBoundary>
        <ThrowError />
      </ErrorBoundary>
    );

    await waitFor(() => {
      expect(console.error).toHaveBeenCalled();
    });
  });
});
```

**Key patterns**:
- Mock `console.error` to suppress error logs in tests
- Restore mocks in `afterEach()` for cleanup
- Use `waitFor()` for error boundary state updates

---

## Backend Testing (xUnit + .NET)

### Service Unit Testing

> **⚠️ DEPRECATED**: This section shows legacy service-based testing.
> **Current approach**: Test CQRS handlers (Commands/Queries) instead.
> **See**: `apps/api/tests/Api.Tests/BoundedContexts/{Context}/Application/` for current CQRS handler tests.

<details>
<summary>Legacy Service Testing Example (Deprecated)</summary>

```csharp
// LEGACY EXAMPLE - apps/api/tests/Api.Tests/Services/AuthServiceTests.cs
// NOTE: AuthService removed in DDD migration. Use CQRS handlers instead.
using Xunit;
using Moq;
using MeepleAi.Api.Services;
using MeepleAi.Api.Infrastructure;

public class AuthServiceTests
{
    private readonly Mock<MeepleAiDbContext> _mockDbContext;
    private readonly Mock<ISessionManagementService> _mockSessionService;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockDbContext = new Mock<MeepleAiDbContext>();
        _mockSessionService = new Mock<ISessionManagementService>();
        _authService = new AuthService(_mockDbContext.Object, _mockSessionService.Object);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSession()
    {
        // Arrange
        var email = "test@example.com";
        var password = "ValidPassword123!";
        var expectedSession = new Session { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        _mockSessionService
            .Setup(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _authService.LoginAsync(email, password, "127.0.0.1", "TestAgent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSession.Id, result.Id);
        _mockSessionService.Verify(s => s.CreateSessionAsync(
            It.IsAny<Guid>(),
            "127.0.0.1",
            "TestAgent"
        ), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var email = "test@example.com";
        var password = "WrongPassword";

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.LoginAsync(email, password, "127.0.0.1", "TestAgent")
        );
    }
}
```

**Key patterns**:
- Use AAA (Arrange-Act-Assert) pattern
- Initialize mocks in constructor
- Use `Mock<T>` from Moq for dependencies
- Verify mock interactions with `.Verify()`
- Use `[Fact]` for simple tests, `[Theory]` for parameterized tests

</details>

### Database Integration Testing

```csharp
// apps/api/tests/Api.Tests/Services/ApiKeyAuthenticationServiceTests.cs
using Xunit;
using Microsoft.EntityFrameworkCore;
using MeepleAi.Api.Services;
using MeepleAi.Api.Infrastructure;
using MeepleAi.Api.Infrastructure.Entities;

public class ApiKeyAuthenticationServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ApiKeyAuthenticationService _service;

    public ApiKeyAuthenticationServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _service = new ApiKeyAuthenticationService(_dbContext);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsUser()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.User
        };
        await _dbContext.Users.AddAsync(user);

        var apiKey = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            KeyHash = "hashed_key",
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };
        await _dbContext.ApiKeys.AddAsync(apiKey);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateApiKeyAsync("valid_key_string");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ReturnsNull()
    {
        // Arrange
        var apiKey = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            KeyHash = "hashed_key",
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
        };
        await _dbContext.ApiKeys.AddAsync(apiKey);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateApiKeyAsync("expired_key");

        // Assert
        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
```

**Key patterns**:
- Use `UseInMemoryDatabase()` for fast unit-style tests
- Use unique database name per test class to prevent conflicts
- Implement `IDisposable` to clean up database after tests
- Seed test data in each test method for clarity
- Use `SaveChangesAsync()` to persist changes

### Testcontainers Integration Testing

```csharp
// apps/api/tests/Api.Tests/Integration/GameControllerTests.cs
using Xunit;
using Testcontainers.PostgreSql;
using Microsoft.AspNetCore.Mvc.Testing;

public class GameControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private WebApplicationFactory<Program> _factory;

    public GameControllerTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:Postgres"] = _postgresContainer.GetConnectionString()
                    });
                });
            });
    }

    [Fact]
    public async Task GetGame_ReturnsGameDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        var gameId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/games/{gameId}");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _factory.DisposeAsync();
    }
}
```

**Key patterns**:
- Use `IAsyncLifetime` for async setup/teardown
- Start Testcontainers in `InitializeAsync()`
- Override connection string in test configuration
- Use `WebApplicationFactory<Program>` for integration tests
- Clean up containers in `DisposeAsync()`

### RAG Validation Pipeline Integration Testing

Integration tests for RAG validation pipeline with Testcontainers and mocked LLM.

**File**: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/RagValidationPipelineIntegrationTests.cs`
**Issue**: #978 (BGAI-036)

```csharp
// Test RAG validation pipeline with Testcontainers PostgreSQL
[Collection("RagValidationPipelineIntegration")]
[Trait("Category", "Integration")]
[Trait("Dependency", "Testcontainers")]
public class RagValidationPipelineIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private IRagValidationPipelineService? _validationPipeline;

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL Testcontainer
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "rag_validation_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(CancellationToken.None);

        // IMPORTANT: Wait 2s for container stability
        await Task.Delay(2000);

        // Setup DI with validation services
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddDbContext<MeepleAiDbContext>(/* ... */);
        services.AddScoped<IRagValidationPipelineService, RagValidationPipelineService>();
        // Mock MultiModelValidationService to avoid OpenRouter dependency
    }

    [Fact]
    public async Task FullPipeline_AllFourLayers_WithMockedMultiModel()
    {
        // Arrange
        var qaResponse = new QaResponse(
            answer: "Players start with 10 credits.",
            snippets: new List<Snippet> { /* ... */ },
            confidence: 0.88
        );

        // Act - Test all 4 validation layers
        var result = await _validationPipeline!.ValidateWithMultiModelAsync(
            qaResponse,
            gameId.ToString(),
            systemPrompt,
            userPrompt,
            "en",
            CancellationToken.None
        );

        // Assert
        Assert.True(result.TotalLayers >= 4); // 4 or 5 layers
        Assert.True(result.LayersPassed >= 3);
    }
}
```

**Key patterns**:
- Use Testcontainers for PostgreSQL (citation validation needs DB)
- Add 2s delay after container start for stability
- Mock `IMultiModelValidationService` to avoid real LLM calls
- Test validation pipeline logic, not LLM integration
- Run with: `dotnet test --filter "FullyQualifiedName~RagValidationPipelineIntegrationTests"`

### ⚠️ Docker Hijack Prevention (Issue #2031)

**CRITICAL**: Avoid `.UntilCommandIsCompleted()` wait strategy in Testcontainers tests.

**Problem**: Command execution uses Docker stream hijacking which fails intermittently:
```
System.InvalidOperationException: cannot hijack chunked or content length stream
```

**❌ AVOID** (hijack-prone):
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))

.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilCommandIsCompleted("redis-cli", "ping"))
```

**✅ PREFER** (robust alternatives):
```csharp
// Option 1: Default TCP wait + delay (simplest, recommended)
.WithPortBinding(5432, true)
.Build();

await _postgresContainer.StartAsync(CancellationToken.None);
await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for full readiness

// Option 2: HTTP health check (if service supports it)
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilHttpRequestIsSucceeded(r => r
        .ForPath("/health")
        .ForPort(8080)
        .ForStatusCode(HttpStatusCode.OK)))
```

**When It Fails**:
- Concurrent test execution (CI environment)
- Docker API under load
- Windows + WSL2 + Docker Desktop

**Centralized Fix Applied** (Issue #2031):
- ✅ `SharedTestcontainersFixture.cs`: PostgreSQL + Redis containers fixed
- ✅ Impacts 30+ test classes automatically via shared fixture
- ✅ No individual test modifications required
- Related: Issue #895 (initial discovery), Issue #2005 (migration retry)

---

## E2E Testing (Playwright)

### User Flow Testing

```typescript
// apps/web/e2e/chat.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Chat Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/chat');
  });

  test('user can send message and receive response', async ({ page }) => {
    // Type message
    await page.fill('[data-testid="chat-input"]', 'What is the objective of Catan?');
    await page.click('[data-testid="send-button"]');

    // Wait for user message to appear
    await expect(page.locator('text=What is the objective of Catan?')).toBeVisible();

    // Wait for assistant response
    await expect(page.locator('[data-testid="assistant-message"]').first()).toBeVisible({
      timeout: 10000
    });
  });

  test('displays chat history on page load', async ({ page }) => {
    const messages = page.locator('[data-testid="chat-message"]');
    await expect(messages).toHaveCount(3);
  });
});
```

**Key patterns**:
- Use `page.goto()` to navigate to pages
- Use `data-testid` attributes for reliable selectors
- Use `expect().toBeVisible()` to wait for elements
- Set explicit timeouts for slow operations
- Reset state with `test.beforeEach()`

### Form Validation Testing

```typescript
// apps/web/e2e/editor.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Rule Spec Editor', () => {
  test('shows validation errors for empty required fields', async ({ page }) => {
    await page.goto('/editor/new');

    // Try to submit without filling required fields
    await page.click('[data-testid="save-button"]');

    // Verify error messages appear
    await expect(page.locator('text=Game name is required')).toBeVisible();
    await expect(page.locator('text=Version is required')).toBeVisible();
  });

  test('successfully saves valid rule spec', async ({ page }) => {
    await page.goto('/editor/new');

    // Fill form
    await page.fill('[name="gameName"]', 'Test Game');
    await page.fill('[name="version"]', '1.0');
    await page.fill('[name="description"]', 'Test description');

    // Submit
    await page.click('[data-testid="save-button"]');

    // Verify success message
    await expect(page.locator('text=Rule spec saved successfully')).toBeVisible();
  });
});
```

**Key patterns**:
- Test validation before testing success paths
- Use semantic selectors (`[name="..."]`) for form fields
- Verify error messages are displayed
- Test both invalid and valid form submissions

### Navigation Testing

```typescript
// apps/web/e2e/navigation.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Navigation', () => {
  test('navigates between pages using nav menu', async ({ page }) => {
    await page.goto('/');

    // Click Chat link
    await page.click('nav >> text=Chat');
    await expect(page).toHaveURL('/chat');

    // Click Upload link
    await page.click('nav >> text=Upload');
    await expect(page).toHaveURL('/upload');

    // Click Admin link
    await page.click('nav >> text=Admin');
    await expect(page).toHaveURL('/admin');
  });

  test('redirects to login when accessing protected page', async ({ page }) => {
    await page.goto('/admin/users');

    // Should redirect to login
    await expect(page).toHaveURL(/\/login/);
  });
});
```

**Key patterns**:
- Use `page.click()` for navigation actions
- Use `expect(page).toHaveURL()` to verify navigation
- Use regex for flexible URL matching: `/\/login/`
- Test authentication redirects

---

## Testing Patterns

### AAA (Arrange-Act-Assert) Pattern

**Definition**: Structure tests in three clear phases:
1. **Arrange**: Set up test data and dependencies
2. **Act**: Execute the code under test
3. **Assert**: Verify the outcome

**Frontend Example**:
```typescript
// apps/web/__tests__/components/LoadingButton.test.tsx
it('disables button when disabled prop is true', () => {
  // Arrange
  render(<LoadingButton disabled>Save Changes</LoadingButton>);

  // Act (implicit - render triggers the behavior)

  // Assert
  expect(screen.getByRole('button')).toBeDisabled();
});
```

**Backend Example** (⚠️ LEGACY - AuthService removed, use CQRS handlers):
```csharp
// LEGACY: apps/api/tests/Api.Tests/Services/AuthServiceTests.cs
[Fact]
public async Task LoginAsync_WithValidCredentials_ReturnsSession()
{
    // Arrange
    var email = "test@example.com";
    var password = "ValidPassword123!";

    // Act
    var result = await _authService.LoginAsync(email, password, "127.0.0.1", "TestAgent");

    // Assert
    Assert.NotNull(result);
}
```

### Test Naming Conventions

**Frontend**: `describes what + when condition + expected outcome`
```typescript
describe('LoadingButton', () => {
  it('renders button text when not loading', () => { });
  it('shows loading spinner when isLoading is true', () => { });
  it('disables button when disabled prop is true', () => { });
});
```

**Backend**: `MethodName_Condition_ExpectedResult`
```csharp
[Fact]
public async Task LoginAsync_WithValidCredentials_ReturnsSession() { }

[Fact]
public async Task LoginAsync_WithInvalidPassword_ThrowsUnauthorizedException() { }

[Fact]
public async Task ValidateApiKeyAsync_WithExpiredKey_ReturnsNull() { }
```

### Test Timeout Guidelines

**CRITICAL**: All integration tests with Testcontainers, network calls, or database operations MUST have explicit timeouts to prevent indefinite hangs.

**Timeout Values**:
- **Unit tests**: 5000ms (5s) - Fast, in-memory operations
- **Integration tests**: 30000ms (30s) - Testcontainers, database, network operations
- **Performance tests**: 60000ms (60s) - Memory profiling, large file processing

**Example**:
```csharp
[Fact(Timeout = 30000)] // 30s for Testcontainers integration tests
public async Task UploadPdf_WithValidFile_Succeeds()
{
    // Test with real database, blob storage, and network calls
}

[Fact(Timeout = 60000)] // 60s for performance/memory tests
public async Task UploadPdf_WithLargeFile_HandlesMemoryEfficiently()
{
    // Test with 5MB+ files and memory profiling
}
```

**Rationale**: Tests without timeouts can hang indefinitely if infrastructure fails (database connection timeout, Testcontainers startup issue, network unavailability), blocking CI/CD pipelines and wasting resources.

---

## Mocking Strategies

### API Mocking (Frontend)

```typescript
// apps/web/__tests__/pages/chat.test.tsx
import * as api from '@/lib/api';

jest.mock('@/lib/api');

beforeEach(() => {
  jest.clearAllMocks();
});

it('fetches chat messages on mount', async () => {
  const mockMessages = [{ id: '1', content: 'Hello', role: 'user' }];
  jest.spyOn(api, 'getChatMessages').mockResolvedValue(mockMessages);

  render(<ChatPage />);

  await waitFor(() => {
    expect(api.getChatMessages).toHaveBeenCalledTimes(1);
  });
});
```

### Router Mocking (Next.js)

```typescript
// apps/web/__tests__/pages/editor.test.tsx
import { useRouter } from 'next/router';

jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

it('redirects to list page after save', async () => {
  const mockPush = jest.fn();
  (useRouter as jest.Mock).mockReturnValue({
    push: mockPush,
    pathname: '/editor/new',
    query: {}
  });

  render(<EditorPage />);

  // ... trigger save action

  await waitFor(() => {
    expect(mockPush).toHaveBeenCalledWith('/editor');
  });
});
```

### Browser API Mocking

```typescript
// apps/web/__tests__/utils/localStorage.test.ts
const mockLocalStorage = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => { store[key] = value; },
    removeItem: (key: string) => { delete store[key]; },
    clear: () => { store = {}; }
  };
})();

beforeEach(() => {
  Object.defineProperty(window, 'localStorage', {
    value: mockLocalStorage,
    writable: true
  });
  mockLocalStorage.clear();
});
```

### Date/Time Mocking

```typescript
// apps/web/__tests__/hooks/useToast.test.tsx
beforeEach(() => {
  jest.useFakeTimers();
  jest.setSystemTime(new Date('2024-01-01T00:00:00Z'));
});

afterEach(() => {
  jest.useRealTimers();
});

it('expires toast after 5 seconds', () => {
  const { result } = renderHook(() => useToast());

  act(() => {
    result.current.success('Test message');
  });

  expect(result.current.toasts).toHaveLength(1);

  act(() => {
    jest.advanceTimersByTime(5000);
  });

  expect(result.current.toasts).toHaveLength(0);
});
```

### Service Mocking (Backend)

> **⚠️ DEPRECATED**: AuthService removed. For current CQRS handler mocking examples, see `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/`.

```csharp
// LEGACY: apps/api/tests/Api.Tests/Services/AuthServiceTests.cs
private readonly Mock<ISessionManagementService> _mockSessionService;

public AuthServiceTests()
{
    _mockSessionService = new Mock<ISessionManagementService>();
    _authService = new AuthService(_mockSessionService.Object);
}

[Fact]
public async Task LoginAsync_CreatesSession()
{
    // Arrange
    _mockSessionService
        .Setup(s => s.CreateSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
        .ReturnsAsync(new Session { Id = Guid.NewGuid() });

    // Act
    await _authService.LoginAsync("user@example.com", "password", "127.0.0.1", "Agent");

    // Assert
    _mockSessionService.Verify(s => s.CreateSessionAsync(
        It.IsAny<Guid>(),
        "127.0.0.1",
        "Agent"
    ), Times.Once);
}
```

### FluentAssertions (Recommended)

**MeepleAI uses FluentAssertions** for more readable and expressive test assertions. FluentAssertions 8.8.0 is installed and preferred for all new tests.

**Basic Assertions**:
```csharp
// ✅ Good: FluentAssertions (preferred)
result.Should().NotBeNull();
result.Value.Should().Be(expected);
result.Success.Should().BeTrue();
collection.Should().HaveCount(5);
text.Should().Contain("expected");

// ❌ Avoid: xUnit Assert (legacy, less readable)
Assert.NotNull(result);
Assert.Equal(expected, result.Value);
Assert.True(result.Success);
Assert.Equal(5, collection.Count);
Assert.Contains("expected", text);
```

**Exception Assertions**:
```csharp
// ✅ Good: FluentAssertions
Action act = () => handler.Handle(invalidCommand);
act.Should().Throw<ArgumentNullException>()
    .WithParameterName("command");

// ❌ Avoid: xUnit Assert
var exception = Assert.Throws<ArgumentNullException>(() => handler.Handle(invalidCommand));
Assert.Equal("command", exception.ParamName);
```

**Custom Assertions** (ISSUE-1818):
```csharp
// Custom assertions for domain types
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers.Assertions;

var uploadResult = await handler.Handle(command, ct);

// Custom domain-specific assertions
uploadResult.Should().BeSuccessful();
uploadResult.Should().HaveDocument();
uploadResult.Should().HaveMessage("Upload successful");
uploadResult.Document.Id.Should().NotBeEmpty();
```

**String Comparison**:
```csharp
// Case-insensitive contains
text.Should().ContainEquivalentOf("expected");  // ✅ FluentAssertions
// NOT: text.Should().Contain("expected", StringComparison.OrdinalIgnoreCase); ❌
```

**Collection Assertions**:
```csharp
collection.Should().NotBeEmpty();
collection.Should().HaveCount(5);
collection.Should().Contain(x => x.Id == expected);
collection.Should().OnlyContain(x => x.IsValid);
collection.Should().ContainSingle(x => x.IsDefault);
```

---

## Common Pitfalls

### 1. Act() Warnings

**Problem**: State updates outside `act()` cause warnings.

**Bad**:
```typescript
it('updates count on click', async () => {
  render(<Counter />);
  const button = screen.getByRole('button');
  button.click(); // ❌ Missing await and act()
  expect(screen.getByText('Count: 1')).toBeInTheDocument();
});
```

**Good**:
```typescript
it('updates count on click', async () => {
  const user = userEvent.setup();
  render(<Counter />);

  await user.click(screen.getByRole('button')); // ✅ Properly awaited

  expect(screen.getByText('Count: 1')).toBeInTheDocument();
});
```

**Hook Testing**:
```typescript
it('updates state', () => {
  const { result } = renderHook(() => useCounter());

  act(() => {
    result.current.increment(); // ✅ Wrapped in act()
  });

  expect(result.current.count).toBe(1);
});
```

### 2. Testing Implementation Details

**Problem**: Tests break when refactoring internal implementation.

**Bad**:
```typescript
it('calls handleSubmit when form submitted', () => {
  const handleSubmit = jest.fn();
  render(<LoginForm onSubmit={handleSubmit} />);

  // ❌ Testing implementation (internal function name)
  fireEvent.click(screen.getByTestId('submit-button'));
  expect(handleSubmit).toHaveBeenCalled();
});
```

**Good**:
```typescript
it('submits form data when user clicks submit', async () => {
  const user = userEvent.setup();
  const handleSubmit = jest.fn();
  render(<LoginForm onSubmit={handleSubmit} />);

  await user.type(screen.getByLabelText(/email/i), 'test@example.com');
  await user.type(screen.getByLabelText(/password/i), 'password123');
  await user.click(screen.getByRole('button', { name: /sign in/i }));

  // ✅ Testing behavior (user outcome)
  expect(handleSubmit).toHaveBeenCalledWith({
    email: 'test@example.com',
    password: 'password123'
  });
});
```

### 3. Incomplete Mocks

**Problem**: Missing mock methods cause undefined errors.

**Bad**:
```typescript
jest.mock('next/router', () => ({
  useRouter: jest.fn().mockReturnValue({
    push: jest.fn()
    // ❌ Missing pathname, query, etc.
  })
}));
```

**Good**:
```typescript
jest.mock('next/router', () => ({
  useRouter: jest.fn().mockReturnValue({
    push: jest.fn(),
    pathname: '/',
    query: {},
    asPath: '/',
    route: '/',
    // ✅ Complete router mock
  })
}));
```

### 4. Timer Anti-Patterns

**Problem**: Forgetting to restore timers breaks other tests.

**Bad**:
```typescript
it('delays action', () => {
  jest.useFakeTimers();
  // ... test code
  // ❌ Never restored, affects other tests
});
```

**Good**:
```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.useRealTimers(); // ✅ Always restore
});
```

### 5. Not Waiting for Async Updates

**Problem**: Assertions run before async state updates complete.

**Bad**:
```typescript
it('displays data from API', () => {
  render(<DataDisplay />);
  // ❌ Data not loaded yet
  expect(screen.getByText('Data loaded')).toBeInTheDocument();
});
```

**Good**:
```typescript
it('displays data from API', async () => {
  render(<DataDisplay />);

  // ✅ Wait for async update
  await waitFor(() => {
    expect(screen.getByText('Data loaded')).toBeInTheDocument();
  });
});
```

### 6. Hardcoding Test Data

**Problem**: Test data conflicts with other tests or production data.

**Bad**:
```csharp
[Fact]
public async Task CreateUser_SavesUser()
{
    var user = new User { Email = "test@example.com" }; // ❌ Hardcoded
    await _service.CreateUserAsync(user);
}
```

**Good**:
```csharp
[Fact]
public async Task CreateUser_SavesUser()
{
    var user = new User
    {
        Email = $"test-{Guid.NewGuid()}@example.com" // ✅ Unique
    };
    await _service.CreateUserAsync(user);
}
```

---

## Best Practices

### 1. Test Behavior, Not Implementation

**Goal**: Tests should verify what the code does, not how it does it.

```typescript
// ❌ Bad: Testing implementation
expect(component.state.isLoading).toBe(true);

// ✅ Good: Testing behavior
expect(screen.getByText(/loading/i)).toBeInTheDocument();
```

### 2. Clear Test Descriptions

**Goal**: Test names should describe the scenario and expected outcome.

```typescript
// ❌ Bad: Vague description
it('works correctly', () => { });

// ✅ Good: Clear scenario and outcome
it('disables submit button when form is invalid', () => { });
```

### 3. Proper Cleanup

**Goal**: Reset state between tests to prevent pollution.

```typescript
// Frontend
afterEach(() => {
  jest.clearAllMocks();
  jest.useRealTimers();
  cleanup(); // React Testing Library cleanup
});

// Backend
public void Dispose()
{
    _dbContext.Database.EnsureDeleted();
    _dbContext.Dispose();
}
```

### 4. Mock External Dependencies

**Goal**: Isolate unit under test from external systems.

```typescript
// ✅ Mock API calls
jest.mock('@/lib/api');

// ✅ Mock timers
jest.useFakeTimers();

// ✅ Mock router
jest.mock('next/router');
```

### 5. Keep Tests Fast

**Goal**: Fast tests encourage frequent execution.

| Test Type | Target Time | Strategy |
|-----------|-------------|----------|
| Unit | <10ms | Use mocks, avoid I/O |
| Integration | <1s | Use in-memory DB or Testcontainers |
| E2E | <30s | Mock API, test critical paths only |

```typescript
// ✅ Fast: Mock API
jest.spyOn(api, 'getData').mockResolvedValue(mockData);

// ❌ Slow: Real API call
const data = await api.getData(); // Network latency
```

### 6. Avoid Test Interdependencies

**Goal**: Each test should run independently.

```typescript
// ❌ Bad: Tests depend on execution order
describe('User Management', () => {
  let userId: string;

  it('creates user', () => {
    userId = createUser(); // Test 2 depends on this
  });

  it('updates user', () => {
    updateUser(userId); // ❌ Depends on test 1
  });
});

// ✅ Good: Independent tests
describe('User Management', () => {
  beforeEach(() => {
    // Each test gets fresh data
    userId = createUser();
  });

  it('updates user', () => {
    updateUser(userId); // ✅ Independent
  });
});
```

### 7. Use Descriptive Assertions

**Goal**: Failures should be easy to understand.

```typescript
// ❌ Bad: Unclear what failed
expect(result).toBeTruthy();

// ✅ Good: Clear assertion
expect(result.status).toBe('success');
expect(result.data.length).toBeGreaterThan(0);
```

### 8. Test Edge Cases

**Goal**: Cover boundary conditions and error cases.

```typescript
describe('Pagination', () => {
  it('handles first page', () => { });
  it('handles middle page', () => { });
  it('handles last page', () => { });
  it('handles empty results', () => { });
  it('handles single result', () => { });
  it('handles invalid page number', () => { });
});
```

---

## Resources

### Official Documentation

- **Jest**: https://jestjs.io/docs/getting-started
- **React Testing Library**: https://testing-library.com/docs/react-testing-library/intro
- **Playwright**: https://playwright.dev/docs/intro
- **xUnit**: https://xunit.net/docs/getting-started
- **Moq**: https://github.com/moq/moq4/wiki/Quickstart

### Testing Philosophy

- **Testing Library Guiding Principles**: https://testing-library.com/docs/guiding-principles/
- **Kent C. Dodds - Common Testing Mistakes**: https://kentcdodds.com/blog/common-mistakes-with-react-testing-library

### MeepleAI-Specific Resources

- **Test Execution Guide**: `docs/testing/test-execution-guide.md`
- **Test Pattern Analysis**: `docs/testing/test-pattern-analysis.md`
- **Coverage Reports**: Run `pwsh tools/measure-coverage.ps1 -GenerateHtml`

### Quick Reference Commands

```bash
# Frontend
cd apps/web
pnpm test                    # Run all tests
pnpm test:watch              # Watch mode
pnpm test:coverage           # With coverage
pnpm test:e2e                # E2E tests
pnpm test:e2e:ui             # E2E with UI

# Backend
cd apps/api
dotnet test                  # Run all tests
dotnet test --logger "console;verbosity=detailed"  # Verbose output
dotnet test /p:CollectCoverage=true               # With coverage

# Coverage Reports
pwsh tools/measure-coverage.ps1 -Project api      # API coverage
pwsh tools/measure-coverage.ps1 -Project web      # Web coverage
pwsh tools/measure-coverage.ps1 -GenerateHtml     # HTML report
```

---

## Next Steps

After reading this guide:

1. **Choose a component/service** to test from your current work
2. **Copy a similar example** from this guide as a starting point
3. **Write your first test** following the AAA pattern
4. **Run the test** and iterate until it passes
5. **Add edge cases** to increase coverage
6. **Share your patterns** with the team

**Goal**: First test written in under 1 hour ✓

For questions or clarifications, refer to:
- Existing tests in `apps/web/__tests__` and `apps/api/tests`
- `docs/testing/test-execution-guide.md` for CI/CD integration
- Team's testing channel for peer support
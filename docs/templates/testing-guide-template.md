# [Feature Name] - Testing Guide

## Test Pyramid Strategy

```
        /\
       /E2E\        5% Coverage - Full user flows
      /------\      - API endpoint tests
     /  INT   \     25% Coverage - Integration tests
    /----------\    - Handlers + Database
   /    UNIT    \   70% Coverage - Business logic
  /--------------\  - Domain entities, validators, services
```

**Coverage Targets**:
| Layer | Backend | Frontend |
|-------|---------|----------|
| **Unit** | 90%+ | 85%+ |
| **Integration** | 85%+ | 80%+ |
| **E2E** | 70%+ | 75%+ |

## Test Categories

| Category | Purpose | Tools | Example |
|----------|---------|-------|---------|
| **Unit** | Isolated logic | xUnit, Vitest | `GameSession.Create()` |
| **Integration** | Component interaction | Testcontainers, MSW | Handler + DB |
| **E2E** | Full user flow | Playwright, API tests | Login → Create → List |
| **Performance** | Load/stress testing | k6, Benchmark.NET | Query 10K records |
| **Security** | Auth/authz validation | Custom fixtures | Unauthorized access |

## Quick Reference Commands

```bash
# Backend Tests
dotnet test                                         # All tests
dotnet test --filter "Category=Unit"                # Unit only
dotnet test --filter "Category=Integration"         # Integration only
dotnet test --filter "BoundedContext=GameManagement" # By context
dotnet test /p:CollectCoverage=true                 # With coverage

# Frontend Tests
pnpm test                                           # Unit + integration
pnpm test:coverage                                  # With coverage report
pnpm test:e2e                                       # Playwright E2E
pnpm test:watch                                     # Watch mode
pnpm typecheck                                      # Type validation
```

## Backend Testing Patterns

### 1. Unit Tests (Domain Logic)

**Test Structure**:
```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GameSessionTests
{
    [Fact]
    public void Create_WithValidName_ReturnsSession()
    {
        // Arrange
        var name = "Test Session";

        // Act
        var session = GameSession.Create(name);

        // Assert
        session.Should().NotBeNull();
        session.Name.Should().Be(name);
        session.Id.Should().NotBeEmpty();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        Action act = () => GameSession.Create(invalidName!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateName_WithValidName_UpdatesSuccessfully()
    {
        // Arrange
        var session = GameSession.Create("Original");
        var newName = "Updated";

        // Act
        session.UpdateName(newName);

        // Assert
        session.Name.Should().Be(newName);
    }
}
```

**Test Patterns**:
| Pattern | When to Use | Example |
|---------|-------------|---------|
| **[Fact]** | Single scenario | Valid input → expected output |
| **[Theory] + [InlineData]** | Multiple inputs | Boundary conditions, invalid inputs |
| **Should().Throw<>()** | Exception validation | Business rule violations |
| **BeCloseTo()** | DateTime/decimal | Avoid precision issues |

---

### 2. Validator Tests

**Test Structure**:
```csharp
[Trait("Category", TestCategories.Unit)]
public class CreateGameSessionCommandValidatorTests
{
    private readonly CreateGameSessionCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_Passes()
    {
        // Arrange
        var command = new CreateGameSessionCommand("Valid Name");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyName_Fails(string invalidName)
    {
        // Arrange
        var command = new CreateGameSessionCommand(invalidName);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_WithTooLongName_Fails()
    {
        // Arrange
        var longName = new string('a', 101);
        var command = new CreateGameSessionCommand(longName);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Name" &&
            e.ErrorMessage.Contains("100"));
    }
}
```

---

### 3. Integration Tests (Handlers + DB)

**Test Structure**:
```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public class CreateGameSessionHandlerTests : IClassFixture<ApiFactory>
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _context;

    public CreateGameSessionHandlerTests(ApiFactory factory)
    {
        _mediator = factory.Services.GetRequiredService<IMediator>();
        _context = factory.Services.GetRequiredService<ApplicationDbContext>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesSession()
    {
        // Arrange
        var command = new CreateGameSessionCommand("Integration Test Session");

        // Act
        var sessionId = await _mediator.Send(command);

        // Assert
        sessionId.Should().NotBeEmpty();

        var session = await _context.GameSessions.FindAsync(sessionId);
        session.Should().NotBeNull();
        session!.Name.Should().Be("Integration Test Session");
    }

    [Fact]
    public async Task Handle_CreatesAuditTrail()
    {
        // Arrange
        var command = new CreateGameSessionCommand("Audit Test");

        // Act
        var sessionId = await _mediator.Send(command);

        // Assert
        var session = await _context.GameSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == sessionId);

        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.UpdatedAt.Should().BeNull(); // No updates yet
    }
}
```

**Integration Test Checklist**:
- [ ] Uses `IClassFixture<ApiFactory>` for DI
- [ ] Trait attributes: `[Trait("Category", TestCategories.Integration)]`
- [ ] Tests actual DB persistence
- [ ] Validates side effects (events, audit trails)
- [ ] Cleans up test data (via transaction rollback)

---

### 4. E2E Tests (Full Flow)

**Test Structure**:
```csharp
[Trait("Category", TestCategories.E2E)]
public class GameSessionEndpointTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public GameSessionEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GetTestToken());
    }

    [Fact]
    public async Task CreateSession_WithValidData_Returns201()
    {
        // Arrange
        var request = new { name = "E2E Test Session" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var sessionId = await response.Content.ReadFromJsonAsync<Guid>();
        sessionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAndRetrieveSession_FullFlow_Succeeds()
    {
        // Arrange & Act - Create
        var createRequest = new { name = "Flow Test" };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/sessions", createRequest);
        var sessionId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act - Retrieve
        var getResponse = await _client.GetAsync($"/api/v1/sessions/{sessionId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await getResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        session.Should().NotBeNull();
        session!.Name.Should().Be("Flow Test");
    }
}
```

## Frontend Testing Patterns

### 1. Component Tests

**Test Structure**:
```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameSessionList } from './game-session-list';

describe('GameSessionList', () => {
  it('renders empty state when no sessions', () => {
    render(<GameSessionList sessions={[]} />);

    expect(screen.getByText(/no sessions/i)).toBeInTheDocument();
  });

  it('renders session list correctly', () => {
    const mockSessions = [
      { id: '1', name: 'Session 1', createdAt: '2026-02-12T10:00:00Z' },
      { id: '2', name: 'Session 2', createdAt: '2026-02-12T11:00:00Z' },
    ];

    render(<GameSessionList sessions={mockSessions} />);

    expect(screen.getByText('Session 1')).toBeInTheDocument();
    expect(screen.getByText('Session 2')).toBeInTheDocument();
  });

  it('handles click events', async () => {
    const mockOnClick = vi.fn();
    const sessions = [{ id: '1', name: 'Test', createdAt: '2026-02-12T10:00:00Z' }];

    render(<GameSessionList sessions={sessions} onSessionClick={mockOnClick} />);

    await userEvent.click(screen.getByText('Test'));

    expect(mockOnClick).toHaveBeenCalledWith('1');
  });
});
```

---

### 2. Hook Tests

**Test Structure**:
```tsx
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach } from 'vitest';
import { useGameSessions } from './use-game-sessions';

describe('useGameSessions', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );

  it('fetches sessions successfully', async () => {
    const { result } = renderHook(() => useGameSessions('user-1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toBeDefined();
    expect(result.current.data?.length).toBeGreaterThan(0);
  });

  it('handles errors correctly', async () => {
    // Mock API failure
    server.use(
      rest.get('/api/v1/sessions', (req, res, ctx) => {
        return res(ctx.status(500));
      })
    );

    const { result } = renderHook(() => useGameSessions('user-1'), { wrapper });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });
  });
});
```

---

### 3. E2E Tests (Playwright)

**Test Structure**:
```typescript
import { test, expect } from '@playwright/test';

test.describe('Game Session Management', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="password"]', 'Test123!');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL('/dashboard');
  });

  test('creates new session successfully', async ({ page }) => {
    // Navigate to sessions
    await page.goto('/sessions');

    // Click create button
    await page.click('button:has-text("New Session")');

    // Fill form
    await page.fill('input[name="name"]', 'E2E Test Session');
    await page.click('button:has-text("Create")');

    // Verify success
    await expect(page.locator('text=E2E Test Session')).toBeVisible();
  });

  test('validates required fields', async ({ page }) => {
    await page.goto('/sessions');
    await page.click('button:has-text("New Session")');

    // Submit without filling
    await page.click('button:has-text("Create")');

    // Check validation message
    await expect(page.locator('text=Name is required')).toBeVisible();
  });
});
```

## Test Data Patterns

### Deterministic Mock Data

```typescript
// ✅ CORRECT - Deterministic
export const generateMockSessions = (count: number) => {
  return Array.from({ length: count }, (_, i) => ({
    id: `session-${i + 1}`,
    name: `Session ${i + 1}`,
    createdAt: new Date(2026, 1, i + 1).toISOString(),
  }));
};

// ❌ WRONG - Non-deterministic (causes hydration issues)
export const generateMockSessions = (count: number) => {
  return Array.from({ length: count }, () => ({
    id: Math.random().toString(),
    name: `Session ${Math.random()}`,
    createdAt: new Date().toISOString(),
  }));
};
```

### Fixture Builders

```csharp
public static class GameSessionBuilder
{
    public static GameSession Build(
        string? name = null,
        DateTime? createdAt = null)
    {
        return GameSession.Create(name ?? "Test Session");
    }

    public static List<GameSession> BuildMany(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => Build(name: $"Session {i}"))
            .ToList();
    }
}
```

## Coverage Analysis

**Check Coverage**:
```bash
# Backend
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
# View: coverlet.xml or use IDE extensions

# Frontend
pnpm test:coverage
# View: coverage/index.html
```

**Coverage Report Structure**:
```
Coverage Report
├── Overall: 92%
├── Domain/
│   ├── Entities: 95%
│   ├── ValueObjects: 98%
│   └── Events: 88%
├── Application/
│   ├── Commands: 90%
│   ├── Queries: 93%
│   └── Validators: 96%
└── Infrastructure/
    ├── Repositories: 85%
    └── Services: 82%
```

## Common Test Scenarios

| Scenario | Test Type | Priority |
|----------|-----------|----------|
| **Happy path** | Unit | 🔴 Critical |
| **Validation errors** | Unit | 🔴 Critical |
| **Null/empty inputs** | Unit | 🔴 Critical |
| **Boundary conditions** | Unit | 🟡 Important |
| **Concurrency** | Integration | 🟡 Important |
| **Cache invalidation** | Integration | 🟡 Important |
| **Performance** | Integration | 🟢 Nice-to-have |
| **Full user flow** | E2E | 🔴 Critical |
| **Error recovery** | E2E | 🟡 Important |

## Test Anti-Patterns

| ❌ Anti-Pattern | ✅ Correct Pattern |
|----------------|-------------------|
| Testing implementation details | Test behavior and outcomes |
| Brittle selectors (`div > span:nth-child(3)`) | Semantic selectors (`role="button"`) |
| `DateTime.Now` in tests | Fixed timestamps or `DateTime.UtcNow` with tolerance |
| Shared mutable state | Isolated test data per test |
| Flaky tests (random failures) | Deterministic data and proper waits |
| No cleanup | Transaction rollback or explicit cleanup |
| Testing framework code | Test business logic only |

## Troubleshooting Tests

```
Tests fail?
├─ Flaky tests?
│   ├─ Check for DateTime.Now usage → Use fixed dates
│   ├─ Check for Math.random() → Use deterministic generators
│   └─ Check for race conditions → Add proper async/await
│
├─ Integration tests fail?
│   ├─ Docker not running? → docker compose up -d
│   ├─ Port conflicts? → Check ports 5432, 6379, 6333
│   └─ DB state issues? → Tests should use transactions
│
├─ E2E tests fail?
│   ├─ Timeouts? → Increase wait times
│   ├─ Selectors not found? → Check accessibility labels
│   └─ Network errors? → Check API mocking
│
└─ Coverage too low?
    ├─ Missing scenarios? → Add boundary/edge case tests
    ├─ Untested code? → Check coverage report for gaps
    └─ Test quality? → Ensure assertions verify behavior
```

## Related Documentation

- **Architecture**: [Link to architecture template]
- **API Reference**: [Link to API template]
- **Development Guide**: [Link to development template]

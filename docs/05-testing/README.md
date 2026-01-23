# Testing Guide

**MeepleAI Testing Documentation** - Comprehensive testing strategy and best practices

---

## 📂 Documentation Structure

```
05-testing/
├── README.md                   # ⭐ Master testing guide (this file)
│   ├── Test types overview (unit, integration, e2e)
│   ├── Running tests locally
│   ├── Writing new tests (with examples)
│   ├── Mock Service Worker (MSW) guide
│   ├── Page Object Model patterns
│   ├── Testcontainers usage
│   ├── Best practices
│   ├── CI/CD integration
│   └── Troubleshooting guide
├── backend/                    # Backend-specific testing guides
│   ├── BACKEND_E2E_TESTING.md
│   ├── testcontainers-best-practices.md
│   ├── INTEGRATION_TEST_OPTIMIZATION.md
│   ├── oauth-testing.md
│   ├── shared-catalog-fts-performance-validation.sql
│   └── log-generation-test-plan.md
├── frontend/                   # Frontend-specific testing guides
│   └── week4-frontend-component-test-plan.md
├── e2e/                        # E2E-specific testing guides
│   ├── E2E_TEST_GUIDE.md
│   ├── e2e-selector-best-practices.md
│   ├── playwright-report-guide.md
│   ├── BackgroundRulebookAnalysis-ManualTesting.md
│   └── RulebookAnalysis-ManualTesting.md
├── CI_CD_PIPELINE.md           # CI/CD pipeline documentation
├── playwright-best-practices.md # Playwright E2E patterns
├── performance-benchmarks.md    # Performance baselines
└── admin-dashboard-*.md        # Dashboard-specific guides
```

---

## Quick Reference

| Test Type | Stack | Coverage Target | Run Command |
|-----------|-------|-----------------|-------------|
| **Backend Unit** | xUnit + Moq | >90% | `dotnet test` |
| **Frontend Unit** | Vitest + Testing Library | >90% | `pnpm test` |
| **E2E** | Playwright | Critical paths | `pnpm test:e2e` |
| **Integration** | Testcontainers | API endpoints | `dotnet test --filter Integration` |

---

## Testing Philosophy

### Principles

1. **Test Behavior, Not Implementation**: Focus on what code does, not how it does it
2. **Arrange-Act-Assert (AAA)**: Structure tests consistently
3. **Isolation**: Each test should be independent and repeatable
4. **Fast Feedback**: Unit tests < 100ms, integration tests < 5s, E2E < 30s
5. **Coverage Quality > Quantity**: 90%+ meaningful coverage, not just numbers

### Test Pyramid

```
       ╱╲        E2E (30 tests)
      ╱  ╲       Integration (162 tests)
     ╱────╲      Unit (4,033 tests)
    ╱      ╲
   ──────────
```

**Distribution**:
- **70% Unit Tests**: Fast, isolated, focused on business logic
- **20% Integration Tests**: Database, external services, API contracts
- **10% E2E Tests**: Critical user journeys end-to-end

---

## Backend Testing (C# / xUnit)

### Setup

**Test Project Structure**:
```
tests/
└── Api.Tests/
    ├── BoundedContexts/
    │   ├── Authentication/
    │   │   ├── Commands/
    │   │   │   └── RegisterCommandTests.cs
    │   │   ├── Queries/
    │   │   │   └── GetUserQueryTests.cs
    │   │   └── Integration/
    │   │       └── AuthenticationIntegrationTests.cs
    │   └── KnowledgeBase/
    ├── Fixtures/
    │   └── WebApplicationFactory.cs
    └── Helpers/
        └── TestDataBuilder.cs
```

### Unit Tests

**Example: Command Handler Test**
```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands;

public class RegisterCommandTests
{
    private readonly Mock<AppDbContext> _mockDb;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandTests()
    {
        _mockDb = new Mock<AppDbContext>();
        _handler = new RegisterCommandHandler(_mockDb.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateUser()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "user@example.com",
            Password: "SecurePassword123!",
            ConfirmPassword: "SecurePassword123!"
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("user@example.com");
        _mockDb.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldThrowException()
    {
        // Arrange
        _mockDb.Setup(db => db.Users.AnyAsync(
            It.IsAny<Expression<Func<User, bool>>>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(true);

        var command = new RegisterCommand(
            Email: "existing@example.com",
            Password: "SecurePassword123!",
            ConfirmPassword: "SecurePassword123!"
        );

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
    }
}
```

### Integration Tests (Testcontainers)

**Example: API Endpoint Integration Test**
```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Integration;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgreSqlContainer _postgres;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("meepleai_test")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Apply migrations to test database
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task RegisterUser_ValidData_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var registerDto = new
        {
            email = "test@example.com",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsCookie()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterTestUserAsync(client);

        var loginDto = new
        {
            email = "test@example.com",
            password = "SecurePassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        response.Headers.GetValues("Set-Cookie").Should().Contain(
            c => c.Contains(".AspNetCore.Identity.Application")
        );
    }
}
```

### Run Backend Tests

```bash
cd apps/api

# All tests
dotnet test

# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~RegisterCommandTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Frontend Testing (Vitest + Testing Library)

### Unit Tests

**Example: Component Test**
```typescript
import { render, screen, waitFor } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { LoginForm } from '@/components/auth/LoginForm';

describe('LoginForm', () => {
  it('should render login form', () => {
    render(<LoginForm />);

    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /login/i })).toBeInTheDocument();
  });

  it('should submit form with valid credentials', async () => {
    const user = userEvent.setup();
    const mockLogin = vi.fn();

    render(<LoginForm onSubmit={mockLogin} />);

    // Fill form
    await user.type(screen.getByLabelText(/email/i), 'user@example.com');
    await user.type(screen.getByLabelText(/password/i), 'SecurePassword123!');

    // Submit
    await user.click(screen.getByRole('button', { name: /login/i }));

    // Assert
    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith({
        email: 'user@example.com',
        password: 'SecurePassword123!',
      });
    });
  });

  it('should show validation errors for empty fields', async () => {
    const user = userEvent.setup();
    render(<LoginForm />);

    // Submit without filling
    await user.click(screen.getByRole('button', { name: /login/i }));

    // Assert errors
    expect(await screen.findByText(/email is required/i)).toBeInTheDocument();
    expect(await screen.findByText(/password is required/i)).toBeInTheDocument();
  });
});
```

**Example: Hook Test**
```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { useGameQuery } from '@/hooks/useGameQuery';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

describe('useGameQuery', () => {
  const createWrapper = () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    return ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };

  it('should fetch game data successfully', async () => {
    const gameId = '550e8400-e29b-41d4-a716-446655440000';

    const { result } = renderHook(() => useGameQuery(gameId), {
      wrapper: createWrapper(),
    });

    // Initially loading
    expect(result.current.isLoading).toBe(true);

    // Wait for success
    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Assert data
    expect(result.current.data).toEqual({
      id: gameId,
      name: 'Catan',
      minPlayers: 3,
      maxPlayers: 4,
    });
  });

  it('should handle fetch errors', async () => {
    const { result } = renderHook(() => useGameQuery('invalid-id'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toBeDefined();
  });
});
```

### Run Frontend Tests

```bash
cd apps/web

# All unit tests
pnpm test

# Watch mode (interactive)
pnpm test:watch

# Coverage report
pnpm test:coverage

# Specific test file
pnpm test LoginForm.test.tsx

# Update snapshots
pnpm test -u
```

---

## E2E Testing (Playwright)

### Setup

**Playwright Configuration** (`playwright.config.ts`):
```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
});
```

### E2E Tests

**Example: User Login Flow**
```typescript
import { test, expect } from '@playwright/test';

test.describe('User Authentication', () => {
  test('user can register, login, and view games', async ({ page }) => {
    // 1. Register
    await page.goto('/auth/register');
    await page.fill('input[name="email"]', 'e2e-test@example.com');
    await page.fill('input[name="password"]', 'SecurePassword123!');
    await page.fill('input[name="confirmPassword"]', 'SecurePassword123!');
    await page.click('button[type="submit"]');

    // Verify redirect to dashboard
    await expect(page).toHaveURL('/dashboard');

    // 2. Logout
    await page.click('button[aria-label="User menu"]');
    await page.click('text=Logout');

    // Verify redirect to home
    await expect(page).toHaveURL('/');

    // 3. Login with created account
    await page.goto('/auth/login');
    await page.fill('input[name="email"]', 'e2e-test@example.com');
    await page.fill('input[name="password"]', 'SecurePassword123!');
    await page.click('button[type="submit"]');

    // Verify successful login
    await expect(page).toHaveURL('/dashboard');

    // 4. View games
    await page.click('text=Games');
    await expect(page.locator('h1')).toContainText('Board Games');

    // Verify games list loaded
    const gameCards = page.locator('[data-testid="game-card"]');
    await expect(gameCards).toHaveCountGreaterThan(0);
  });

  test('user cannot login with invalid credentials', async ({ page }) => {
    await page.goto('/auth/login');
    await page.fill('input[name="email"]', 'invalid@example.com');
    await page.fill('input[name="password"]', 'WrongPassword');
    await page.click('button[type="submit"]');

    // Verify error message
    await expect(page.locator('text=Invalid email or password')).toBeVisible();
    await expect(page).toHaveURL('/auth/login');
  });
});
```

**Example: RAG Chat Flow**
```typescript
import { test, expect } from '@playwright/test';

test.describe('RAG Chat', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/auth/login');
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="password"]', 'TestPassword123!');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL('/dashboard');
  });

  test('user can ask question about game rules', async ({ page }) => {
    // Navigate to game
    await page.goto('/games/catan');

    // Ask question
    await page.fill('textarea[name="question"]', 'Come si gioca a Catan?');
    await page.click('button[aria-label="Ask question"]');

    // Wait for streaming response
    const answerBox = page.locator('[data-testid="answer-box"]');
    await expect(answerBox).toBeVisible({ timeout: 10000 });

    // Verify answer contains expected content
    await expect(answerBox).toContainText('Catan');

    // Verify confidence score
    const confidence = page.locator('[data-testid="confidence-score"]');
    await expect(confidence).toBeVisible();
    const confidenceText = await confidence.textContent();
    expect(parseFloat(confidenceText!)).toBeGreaterThan(0.7);

    // Verify sources
    const sources = page.locator('[data-testid="source-link"]');
    await expect(sources).toHaveCountGreaterThan(0);
  });
});
```

### Run E2E Tests

```bash
cd apps/web

# All E2E tests (headless)
pnpm test:e2e

# With UI (headed mode)
pnpm test:e2e --headed

# Specific browser
pnpm test:e2e --project=chromium

# Debug mode
pnpm test:e2e --debug

# View test report
pnpm playwright show-report
```

---

## Test Coverage

### Backend Coverage

**Generate Coverage Report**:
```bash
cd apps/api
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# Open report
open coverage-report/index.html
```

**Coverage Targets**:
- **Overall**: >90%
- **Domain Layer**: >95% (critical business logic)
- **Application Layer**: >90% (command/query handlers)
- **Infrastructure Layer**: >80% (external integrations)

### Frontend Coverage

**Generate Coverage Report**:
```bash
cd apps/web
pnpm test:coverage

# Open report
open coverage/index.html
```

**Coverage Targets**:
- **Overall**: >90%
- **Components**: >85%
- **Hooks**: >90%
- **Utilities**: >95%

---

## Testing Best Practices

### DO ✅

**1. Use Descriptive Test Names**
```typescript
// ✅ GOOD - Clear intent
test('user can login with valid credentials and access dashboard')

// ❌ BAD - Vague
test('login works')
```

**2. Arrange-Act-Assert Structure**
```csharp
[Fact]
public async Task Test()
{
    // Arrange: Setup test data and dependencies
    var command = new RegisterCommand("test@example.com", "pass", "pass");

    // Act: Execute the behavior being tested
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert: Verify expected outcome
    result.Email.Should().Be("test@example.com");
}
```

**3. Test One Thing Per Test**
```typescript
// ✅ GOOD - Single responsibility
test('should validate email format', () => { });
test('should validate password strength', () => { });

// ❌ BAD - Multiple concerns
test('should validate form', () => {
  // tests email, password, confirmPassword...
});
```

**4. Mock External Dependencies**
```csharp
// ✅ GOOD - Mock external service
var mockEmailService = new Mock<IEmailService>();
mockEmailService.Setup(s => s.SendAsync(It.IsAny<Email>())).ReturnsAsync(true);

// ❌ BAD - Real external call
var emailService = new SmtpEmailService(); // Slow, unreliable
```

### DON'T ❌

**1. Don't Test Implementation Details**
```typescript
// ❌ BAD - Testing internal state
expect(component.state.count).toBe(5);

// ✅ GOOD - Testing observable behavior
expect(screen.getByText('Count: 5')).toBeInTheDocument();
```

**2. Don't Share State Between Tests**
```csharp
// ❌ BAD - Shared mutable state
private static User _testUser;

[Fact]
public void Test1() { _testUser = ...; }

[Fact]
public void Test2() { /* uses _testUser */ } // Flaky!

// ✅ GOOD - Isolated state
[Fact]
public void Test1()
{
    var testUser = CreateTestUser();
}
```

**3. Don't Ignore Flaky Tests**
```typescript
// ❌ BAD - Disabling test
test.skip('sometimes fails', () => { });

// ✅ GOOD - Fix root cause (e.g., race condition)
test('stable test', async () => {
  await waitFor(() => expect(result).toBeDefined());
});
```

---

## CI/CD Integration

### GitHub Actions

**Backend Tests** (`.github/workflows/ci-api.yml`):
```yaml
name: Backend CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test
        run: dotnet test --no-build --collect:"XPlat Code Coverage"

      - name: Upload coverage
        uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
```

**Frontend Tests** (`.github/workflows/ci-web.yml`):
```yaml
name: Frontend CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v3
        with:
          version: 9

      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'pnpm'

      - name: Install dependencies
        run: pnpm install

      - name: Lint
        run: pnpm lint

      - name: Type check
        run: pnpm typecheck

      - name: Unit tests
        run: pnpm test:coverage

      - name: E2E tests
        run: pnpm test:e2e

      - name: Upload coverage
        uses: codecov/codecov-action@v4
```

---

## Test Data Management

### Test Data Builders

**Backend**:
```csharp
public class TestDataBuilder
{
    public static User CreateUser(string email = "test@example.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Game CreateGame(string name = "Test Game")
    {
        return new Game
        {
            Id = Guid.NewGuid(),
            Name = name,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 60,
            MinAge = 10
        };
    }
}
```

**Frontend**:
```typescript
export const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: '550e8400-e29b-41d4-a716-446655440000',
  name: 'Test Game',
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  minAge: 10,
  ...overrides,
});
```

### Fixtures

**xUnit Class Fixtures**:
```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public AppDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;

        DbContext = new AppDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();
    }
}

public class MyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public MyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
}
```

---

## Performance Testing

### Load Testing (k6)

**Install k6**:
```bash
# macOS
brew install k6

# Linux
sudo apt-get install k6
```

**Load Test Script** (`k6/load-test.js`):
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 20 },  // Ramp up to 20 users
    { duration: '1m', target: 20 },   // Stay at 20 users
    { duration: '30s', target: 0 },   // Ramp down to 0
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% requests < 500ms
  },
};

export default function () {
  // Test API endpoint
  const res = http.get('http://localhost:8080/api/v1/games');

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });

  sleep(1);
}
```

**Run Load Test**:
```bash
k6 run k6/load-test.js
```

---

## Accessibility Testing

### Automated a11y Tests

**Frontend (Vitest + jest-axe)**:
```typescript
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { LoginForm } from '@/components/auth/LoginForm';

expect.extend(toHaveNoViolations);

describe('LoginForm Accessibility', () => {
  it('should not have accessibility violations', async () => {
    const { container } = render(<LoginForm />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

**E2E (Playwright + axe-core)**:
```typescript
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('homepage should not have accessibility violations', async ({ page }) => {
  await page.goto('/');

  const accessibilityScanResults = await new AxeBuilder({ page }).analyze();

  expect(accessibilityScanResults.violations).toEqual([]);
});
```

---

## Mock Service Worker (MSW)

### Overview

**MSW (Mock Service Worker)** intercepts HTTP requests at the network level for reliable API mocking in tests.

**Use Cases**:
- Frontend unit tests requiring API responses
- Component testing with complex data flows
- E2E tests with controlled backend scenarios
- Development server with mock data

**Advantages**:
- ✅ Works in browser and Node.js (Vitest)
- ✅ Same handlers for dev and test
- ✅ Network-level interception (no code changes)
- ✅ TypeScript-safe with zod schemas

### Setup

**Install MSW**:
```bash
cd apps/web
pnpm add -D msw@latest
npx msw init public/ --save
```

**Create Handlers** (`src/mocks/handlers.ts`):
```typescript
import { http, HttpResponse } from 'msw';
import type { Game } from '@/types/game';

export const handlers = [
  // GET /api/v1/games
  http.get('/api/v1/games', () => {
    return HttpResponse.json<Game[]>([
      {
        id: '550e8400-e29b-41d4-a716-446655440000',
        name: 'Catan',
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        minAge: 10,
      },
      {
        id: '550e8400-e29b-41d4-a716-446655440001',
        name: 'Ticket to Ride',
        minPlayers: 2,
        maxPlayers: 5,
        playingTime: 60,
        minAge: 8,
      },
    ]);
  }),

  // POST /api/v1/auth/login
  http.post('/api/v1/auth/login', async ({ request }) => {
    const body = await request.json() as { email: string; password: string };

    if (body.email === 'test@example.com' && body.password === 'TestPassword123!') {
      return HttpResponse.json({
        id: '550e8400-e29b-41d4-a716-446655440000',
        email: 'test@example.com',
        role: 'user',
      }, {
        headers: {
          'Set-Cookie': '.AspNetCore.Identity.Application=mock-cookie; Path=/; HttpOnly',
        },
      });
    }

    return HttpResponse.json(
      { message: 'Invalid email or password' },
      { status: 401 }
    );
  }),

  // GET /api/v1/games/:id
  http.get('/api/v1/games/:id', ({ params }) => {
    const { id } = params;

    if (id === '550e8400-e29b-41d4-a716-446655440000') {
      return HttpResponse.json<Game>({
        id: id as string,
        name: 'Catan',
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        minAge: 10,
      });
    }

    return HttpResponse.json(
      { message: 'Game not found' },
      { status: 404 }
    );
  }),
];
```

**Setup for Vitest** (`src/mocks/setup.ts`):
```typescript
import { afterAll, afterEach, beforeAll } from 'vitest';
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);

// Start server before all tests
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

// Reset handlers after each test (important for test isolation)
afterEach(() => server.resetHandlers());

// Clean up after all tests
afterAll(() => server.close());
```

**Configure Vitest** (`vitest.config.ts`):
```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/mocks/setup.ts'],
  },
});
```

### Usage in Tests

**Component Test with MSW**:
```typescript
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { server } from '@/mocks/setup';
import { http, HttpResponse } from 'msw';
import { GameList } from '@/components/games/GameList';

describe('GameList with MSW', () => {
  it('should fetch and display games', async () => {
    render(<GameList />);

    // Wait for games to load (using default handlers)
    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    });
  });

  it('should handle empty games list', async () => {
    // Override handler for this test
    server.use(
      http.get('/api/v1/games', () => {
        return HttpResponse.json([]);
      })
    );

    render(<GameList />);

    await waitFor(() => {
      expect(screen.getByText(/no games found/i)).toBeInTheDocument();
    });
  });

  it('should handle API errors', async () => {
    // Override handler to simulate error
    server.use(
      http.get('/api/v1/games', () => {
        return HttpResponse.json(
          { message: 'Internal server error' },
          { status: 500 }
        );
      })
    );

    render(<GameList />);

    await waitFor(() => {
      expect(screen.getByText(/error loading games/i)).toBeInTheDocument();
    });
  });
});
```

**Hook Test with MSW**:
```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { server } from '@/mocks/setup';
import { http, HttpResponse } from 'msw';
import { useGameQuery } from '@/hooks/useGameQuery';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

describe('useGameQuery with MSW', () => {
  const createWrapper = () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    return ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };

  it('should fetch game successfully', async () => {
    const gameId = '550e8400-e29b-41d4-a716-446655440000';

    const { result } = renderHook(() => useGameQuery(gameId), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual({
      id: gameId,
      name: 'Catan',
      minPlayers: 3,
      maxPlayers: 4,
      playingTime: 90,
      minAge: 10,
    });
  });

  it('should handle 404 errors', async () => {
    const { result } = renderHook(() => useGameQuery('invalid-id'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error?.message).toContain('Game not found');
  });
});
```

### Best Practices

**DO ✅**:
- Use `server.resetHandlers()` in `afterEach` for test isolation
- Create reusable handler factories for common scenarios
- Type your handlers with Zod schemas for runtime validation
- Group handlers by feature/domain
- Use `server.use()` for test-specific overrides

**DON'T ❌**:
- Don't share mutable state between handlers
- Don't mix MSW with axios-mock-adapter (choose one)
- Don't forget to call `server.close()` after all tests
- Don't use MSW for simple sync logic (use plain mocks)

### Handler Patterns

**Pagination**:
```typescript
http.get('/api/v1/games', ({ request }) => {
  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get('page') || '1');
  const pageSize = parseInt(url.searchParams.get('pageSize') || '10');

  const allGames = [...]; // Full dataset
  const start = (page - 1) * pageSize;
  const paginatedGames = allGames.slice(start, start + pageSize);

  return HttpResponse.json({
    data: paginatedGames,
    total: allGames.length,
    page,
    pageSize,
  });
})
```

**Search/Filtering**:
```typescript
http.get('/api/v1/games', ({ request }) => {
  const url = new URL(request.url);
  const search = url.searchParams.get('search') || '';

  const allGames = [...]; // Full dataset
  const filteredGames = allGames.filter(game =>
    game.name.toLowerCase().includes(search.toLowerCase())
  );

  return HttpResponse.json(filteredGames);
})
```

**Delayed Response (Simulate Network)**:
```typescript
import { delay } from 'msw';

http.get('/api/v1/games', async () => {
  await delay(1000); // 1 second delay
  return HttpResponse.json([...]);
})
```

**Conditional Responses**:
```typescript
let requestCount = 0;

http.get('/api/v1/games', () => {
  requestCount++;

  // Fail first request, succeed on retry
  if (requestCount === 1) {
    return HttpResponse.json(
      { message: 'Temporary error' },
      { status: 503 }
    );
  }

  return HttpResponse.json([...]);
})
```

### Debugging MSW

**Enable Verbose Logging**:
```typescript
import { setupServer } from 'msw/node';

export const server = setupServer(...handlers);

// In development
if (process.env.NODE_ENV === 'development') {
  server.listen({
    onUnhandledRequest: 'warn', // Log unhandled requests
  });
}
```

**Inspect Request/Response**:
```typescript
http.get('/api/v1/games', async ({ request }) => {
  console.log('Request:', request.method, request.url);
  console.log('Headers:', Object.fromEntries(request.headers));

  const body = await request.json();
  console.log('Body:', body);

  const response = HttpResponse.json([...]);
  console.log('Response:', response);

  return response;
})
```

### Resources

- [MSW Official Docs](https://mswjs.io/)
- [MSW Migration Guide (v1 → v2)](https://mswjs.io/docs/migrations/1.x-to-2.x/)
- [MSW Recipes](https://mswjs.io/docs/recipes/)

---

## Page Object Model (E2E)

### Overview

**Page Object Model (POM)** encapsulates page structure and interactions into reusable classes, improving E2E test maintainability.

**Benefits**:
- ✅ Single source of truth for selectors
- ✅ Reusable interaction patterns
- ✅ Easier refactoring when UI changes
- ✅ Type-safe with TypeScript

### Pattern Structure

**Base Page** (`e2e/pages/BasePage.ts`):
```typescript
import { Page, Locator } from '@playwright/test';

export class BasePage {
  protected readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async navigate(path: string) {
    await this.page.goto(path);
  }

  async waitForUrl(urlPattern: string | RegExp) {
    await this.page.waitForURL(urlPattern);
  }

  protected getByTestId(testId: string): Locator {
    return this.page.locator(`[data-testid="${testId}"]`);
  }
}
```

**Login Page** (`e2e/pages/LoginPage.ts`):
```typescript
import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

export class LoginPage extends BasePage {
  // Selectors
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    super(page);
    this.emailInput = page.locator('input[name="email"]');
    this.passwordInput = page.locator('input[name="password"]');
    this.submitButton = page.locator('button[type="submit"]');
    this.errorMessage = page.locator('[data-testid="error-message"]');
  }

  // Actions
  async goto() {
    await this.navigate('/auth/login');
  }

  async login(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }

  async expectLoginSuccess() {
    await expect(this.page).toHaveURL('/dashboard');
  }

  async expectLoginError(message: string) {
    await expect(this.errorMessage).toContainText(message);
  }
}
```

**Dashboard Page** (`e2e/pages/DashboardPage.ts`):
```typescript
import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

export class DashboardPage extends BasePage {
  readonly welcomeMessage: Locator;
  readonly gamesList: Locator;
  readonly logoutButton: Locator;

  constructor(page: Page) {
    super(page);
    this.welcomeMessage = page.locator('h1');
    this.gamesList = this.getByTestId('games-list');
    this.logoutButton = page.locator('button[aria-label="Logout"]');
  }

  async expectWelcomeMessage(username: string) {
    await expect(this.welcomeMessage).toContainText(`Welcome, ${username}`);
  }

  async expectGamesLoaded() {
    await expect(this.gamesList).toBeVisible();
    const gameCards = this.gamesList.locator('[data-testid="game-card"]');
    await expect(gameCards).toHaveCountGreaterThan(0);
  }

  async logout() {
    await this.logoutButton.click();
  }
}
```

### Usage in Tests

**Login Flow Test** (`e2e/auth/login.spec.ts`):
```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';

test.describe('User Login', () => {
  test('successful login redirects to dashboard', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const dashboardPage = new DashboardPage(page);

    await loginPage.goto();
    await loginPage.login('test@example.com', 'TestPassword123!');
    await loginPage.expectLoginSuccess();
    await dashboardPage.expectWelcomeMessage('Test User');
    await dashboardPage.expectGamesLoaded();
  });

  test('invalid credentials show error', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login('invalid@example.com', 'WrongPassword');
    await loginPage.expectLoginError('Invalid email or password');
  });
});
```

**Complex Multi-Page Flow** (`e2e/games/add-to-library.spec.ts`):
```typescript
import { test } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';
import { GameDetailsPage } from '../pages/GameDetailsPage';
import { LibraryPage } from '../pages/LibraryPage';

test('user can add game to library', async ({ page }) => {
  // 1. Login
  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.login('test@example.com', 'TestPassword123!');

  // 2. Navigate to game
  const dashboardPage = new DashboardPage(page);
  await dashboardPage.expectGamesLoaded();
  await dashboardPage.clickGame('Catan');

  // 3. Add to library
  const gameDetailsPage = new GameDetailsPage(page);
  await gameDetailsPage.expectGameTitle('Catan');
  await gameDetailsPage.addToLibrary();
  await gameDetailsPage.expectAddedToLibrary();

  // 4. Verify in library
  const libraryPage = new LibraryPage(page);
  await libraryPage.goto();
  await libraryPage.expectGameInLibrary('Catan');
});
```

### Best Practices

**DO ✅**:
- One page object per logical page/component
- Group selectors at the top of the class
- Methods represent user actions (not implementation)
- Return page objects for chaining
- Use `data-testid` for stable selectors
- Include assertions in page objects (expectX methods)

**DON'T ❌**:
- Don't use CSS selectors tied to implementation
- Don't put test logic in page objects
- Don't share state between page objects
- Don't create god objects (split complex pages)

### Advanced Patterns

**Component Objects** (for reusable UI components):
```typescript
export class NavigationComponent {
  private readonly page: Page;
  readonly homeLink: Locator;
  readonly gamesLink: Locator;
  readonly libraryLink: Locator;

  constructor(page: Page) {
    this.page = page;
    this.homeLink = page.locator('nav a[href="/"]');
    this.gamesLink = page.locator('nav a[href="/games"]');
    this.libraryLink = page.locator('nav a[href="/library"]');
  }

  async navigateTo(section: 'home' | 'games' | 'library') {
    const links = {
      home: this.homeLink,
      games: this.gamesLink,
      library: this.libraryLink,
    };
    await links[section].click();
  }
}
```

**Using Component Objects in Pages**:
```typescript
export class DashboardPage extends BasePage {
  readonly navigation: NavigationComponent;

  constructor(page: Page) {
    super(page);
    this.navigation = new NavigationComponent(page);
  }
}
```

### Resources

- [Playwright Page Object Model Guide](https://playwright.dev/docs/pom)
- [Testing Best Practices](https://playwright.dev/docs/best-practices)

---

## Troubleshooting Guide

### Backend (C# / xUnit)

#### Tests Not Discovered

**Symptom**: `dotnet test` shows "0 tests found"

**Causes & Solutions**:

1. **Missing xUnit Package**
   ```bash
   # Check if xUnit is installed
   dotnet list package | grep xUnit

   # If missing, install
   dotnet add package xUnit
   dotnet add package xUnit.runner.visualstudio
   ```

2. **Test Class Not Public**
   ```csharp
   // ❌ BAD - private class
   class MyTests { }

   // ✅ GOOD - public class
   public class MyTests { }
   ```

3. **Missing [Fact] or [Theory] Attribute**
   ```csharp
   // ❌ BAD - no attribute
   public void MyTest() { }

   // ✅ GOOD - with [Fact]
   [Fact]
   public void MyTest() { }
   ```

#### Testcontainers Fails to Start

**Symptom**: `PostgresContainer failed to start: timeout`

**Causes & Solutions**:

1. **Docker Not Running**
   ```bash
   # Check Docker status
   docker ps

   # Start Docker Desktop (Windows/Mac)
   # Or start Docker daemon (Linux)
   sudo systemctl start docker
   ```

2. **Port Conflict**
   ```bash
   # Check if port 5432 is in use
   netstat -ano | findstr :5432  # Windows
   lsof -i :5432                 # macOS/Linux

   # Kill process or use random port
   var postgres = new PostgreSqlBuilder()
       .WithPortBinding(0) // Random port
       .Build();
   ```

3. **Insufficient Resources**
   ```bash
   # Increase Docker memory/CPU limits
   # Docker Desktop → Settings → Resources
   # Minimum: 4GB RAM, 2 CPUs
   ```

4. **Migration Lock Timeout (Issue #2577)**
   ```csharp
   // Use SharedDatabaseTestBase (includes lock handling)
   public class MyTests : SharedDatabaseTestBase<AppDbContext>
   {
       // Lock handled automatically
   }
   ```

#### Flaky Integration Tests

**Symptom**: Tests pass locally but fail in CI

**Causes & Solutions**:

1. **Race Conditions**
   ```csharp
   // ❌ BAD - no await
   var result = _handler.Handle(command, CancellationToken.None);

   // ✅ GOOD - await completion
   var result = await _handler.Handle(command, CancellationToken.None);
   ```

2. **Shared State**
   ```csharp
   // ❌ BAD - shared static state
   private static User _testUser;

   // ✅ GOOD - isolated state per test
   [Fact]
   public async Task Test()
   {
       var testUser = await CreateTestUserAsync();
   }
   ```

3. **Database Cleanup**
   ```csharp
   // Use SharedDatabaseTestBase for automatic cleanup
   public class MyTests : SharedDatabaseTestBase<AppDbContext>
   {
       protected override async Task DisposeAsync()
       {
           // Cleanup handled by base class
           await base.DisposeAsync();
       }
   }
   ```

#### Testhost Blocking (Issue #2593)

**Symptom**: `testhost.exe` process persists after tests

**Solution**:
```bash
# Find testhost processes
tasklist | grep testhost

# Kill all testhost processes
taskkill //IM testhost.exe //F

# Or use PowerShell
Get-Process testhost | Stop-Process -Force
```

**Prevention**:
```bash
# Kill testhost before running tests
taskkill //IM testhost.exe //F 2>$null; dotnet test
```

### Frontend (TypeScript / Vitest)

#### Tests Timeout

**Symptom**: `Test timed out in 5000ms`

**Causes & Solutions**:

1. **Missing `await` in Async Test**
   ```typescript
   // ❌ BAD - no await
   it('fetches data', () => {
     renderHook(() => useGameQuery(id));
   });

   // ✅ GOOD - await completion
   it('fetches data', async () => {
     const { result } = renderHook(() => useGameQuery(id));
     await waitFor(() => expect(result.current.isSuccess).toBe(true));
   });
   ```

2. **Increase Timeout**
   ```typescript
   // For specific test
   it('slow test', async () => {
     // ...
   }, 10000); // 10 seconds

   // For all tests in file
   describe.configure({ timeout: 10000 });
   ```

3. **MSW Handler Not Matching**
   ```typescript
   // Debug unhandled requests
   server.listen({ onUnhandledRequest: 'warn' });

   // Check handler URL matches request
   http.get('/api/v1/games', () => { }) // ✅ Correct
   http.get('/api/games', () => { })    // ❌ Wrong path
   ```

#### Component Not Found

**Symptom**: `Unable to find element with text "Submit"`

**Causes & Solutions**:

1. **Text Mismatch**
   ```typescript
   // ❌ BAD - exact match
   screen.getByText('Submit')

   // ✅ GOOD - case-insensitive regex
   screen.getByText(/submit/i)
   ```

2. **Element Not Rendered**
   ```typescript
   // ✅ Wait for element
   await waitFor(() => {
     expect(screen.getByText(/submit/i)).toBeInTheDocument();
   });
   ```

3. **Wrong Query Type**
   ```typescript
   // For text content
   screen.getByText(/login/i)

   // For labels
   screen.getByLabelText(/email/i)

   // For roles (buttons, links, etc.)
   screen.getByRole('button', { name: /submit/i })

   // For test IDs
   screen.getByTestId('submit-button')
   ```

#### MSW Not Intercepting Requests

**Symptom**: Real API called instead of mock

**Causes & Solutions**:

1. **MSW Not Setup**
   ```typescript
   // vitest.config.ts
   export default defineConfig({
     test: {
       setupFiles: ['./src/mocks/setup.ts'], // Add this
     },
   });
   ```

2. **Handler URL Mismatch**
   ```typescript
   // Check API base URL vs handler
   // API calls: http://localhost:3000/api/v1/games
   // Handler must match exact path
   http.get('/api/v1/games', () => { }) // ✅
   http.get('http://localhost:3000/api/v1/games', () => { }) // ❌ Absolute URL
   ```

3. **Server Not Started**
   ```typescript
   // Ensure setup.ts has:
   beforeAll(() => server.listen());
   afterEach(() => server.resetHandlers());
   afterAll(() => server.close());
   ```

### E2E (Playwright)

#### Browser Not Installed

**Symptom**: `Browser not found. Run 'npx playwright install'`

**Solution**:
```bash
npx playwright install
npx playwright install-deps # Install system dependencies
```

#### Selector Not Found

**Symptom**: `Timeout: element not visible`

**Causes & Solutions**:

1. **Wrong Selector**
   ```typescript
   // ❌ BAD - fragile CSS selector
   await page.click('.btn-primary')

   // ✅ GOOD - data-testid
   await page.click('[data-testid="submit-button"]')

   // ✅ GOOD - role + name
   await page.getByRole('button', { name: 'Submit' }).click()
   ```

2. **Element Not Loaded**
   ```typescript
   // ✅ Wait for element
   await page.waitForSelector('[data-testid="submit-button"]');
   await page.click('[data-testid="submit-button"]');
   ```

3. **Element Hidden**
   ```typescript
   // Check visibility first
   const isVisible = await page.isVisible('[data-testid="submit-button"]');
   if (!isVisible) {
     // Debug: take screenshot
     await page.screenshot({ path: 'debug.png' });
   }
   ```

#### Test Flakiness

**Symptom**: Tests pass/fail randomly

**Causes & Solutions**:

1. **Network Delays**
   ```typescript
   // ❌ BAD - hardcoded delay
   await page.waitForTimeout(5000);

   // ✅ GOOD - wait for condition
   await page.waitForLoadState('networkidle');
   await page.waitForSelector('[data-testid="content"]');
   ```

2. **Animation Interference**
   ```typescript
   // Disable animations in test
   await page.goto('/login');
   await page.addStyleTag({
     content: `
       *, *::before, *::after {
         animation-duration: 0s !important;
         transition-duration: 0s !important;
       }
     `,
   });
   ```

3. **Race Conditions**
   ```typescript
   // ✅ Wait for navigation before asserting
   await Promise.all([
     page.waitForURL('/dashboard'),
     page.click('button[type="submit"]'),
   ]);
   ```

#### Screenshots/Videos Not Generated

**Symptom**: No artifacts after test failure

**Causes & Solutions**:

1. **Screenshot on Failure**
   ```typescript
   // playwright.config.ts
   export default defineConfig({
     use: {
       screenshot: 'only-on-failure',
       video: 'retain-on-failure',
     },
   });
   ```

2. **Trace on Retry**
   ```typescript
   export default defineConfig({
     use: {
       trace: 'on-first-retry',
     },
   });
   ```

3. **View Trace**
   ```bash
   npx playwright show-trace trace.zip
   ```

### General

#### High Memory Usage

**Symptom**: Tests crash with OOM errors

**Solutions**:

1. **Limit Parallel Tests**
   ```bash
   # Backend
   dotnet test --parallel 4

   # Frontend
   pnpm test --maxWorkers=4

   # Playwright
   pnpm test:e2e --workers=2
   ```

2. **Cleanup Resources**
   ```csharp
   // Backend
   public async Task DisposeAsync()
   {
       await _dbContext.DisposeAsync();
       await _postgres.DisposeAsync();
   }
   ```

3. **Docker Memory Limits**
   ```bash
   # Increase Docker memory allocation
   # Docker Desktop → Settings → Resources → Memory: 8GB
   ```

#### Slow Test Execution

**Symptom**: Test suite takes >5 minutes

**Solutions**:

1. **Profile Test Execution**
   ```bash
   # Backend
   dotnet test --logger "console;verbosity=detailed"

   # Frontend
   pnpm test --reporter=verbose
   ```

2. **Identify Slow Tests**
   ```typescript
   // Vitest: tests > 5s will show as slow
   // Mark slow tests explicitly
   it.slow('slow test', async () => { }, 30000);
   ```

3. **Optimize Container Startup** (Backend)
   ```csharp
   // Use SharedTestcontainersFixture (parallel startup)
   [Collection("SharedTestcontainers")]
   public class MyTests { }
   ```

4. **Use Test.Only for Debugging**
   ```typescript
   // Run single test during debugging
   it.only('debug this test', () => { });
   ```

### Performance Baselines

**Expected Execution Times**:
- Unit test: < 100ms
- Integration test: < 5s
- E2E test: < 30s
- Full backend suite: < 3 min (with Testcontainers)
- Full frontend suite: < 2 min
- Full E2E suite: < 5 min (30 tests across 6 browsers)

**If Exceeding**:
1. Profile slow tests
2. Check for resource contention
3. Optimize test data fixtures
4. Reduce unnecessary assertions

### Diagnostic Commands

**Backend**:
```bash
# List all tests
dotnet test --list-tests

# Run with detailed logging
dotnet test --logger "console;verbosity=detailed"

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTests"

# Check Testcontainers logs
docker logs <container-id>
```

**Frontend**:
```bash
# List all tests
pnpm test --reporter=verbose

# Run specific test
pnpm test MyComponent.test.tsx

# Debug mode
pnpm test --inspect-brk
```

**Playwright**:
```bash
# Debug mode
pnpm test:e2e --debug

# Show report
pnpm playwright show-report

# View trace
pnpm playwright show-trace trace.zip

# Generate code from recording
pnpm playwright codegen http://localhost:3000
```

### Getting Help

**Common Issues**:
- Check [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues) for similar problems
- Review [CI logs](.github/workflows/) for detailed error messages
- Ask in team chat with error logs and test file

**Escalation**:
- Backend tests: @backend-team
- Frontend tests: @frontend-team
- E2E tests: @qa-team
- Infrastructure: @devops-team

---

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Vitest Guide](https://vitest.dev/)
- [Playwright Documentation](https://playwright.dev/)
- [Testing Library Best Practices](https://testing-library.com/docs/queries/about)
- [FluentAssertions](https://fluentassertions.com/)
- [Testcontainers](https://dotnet.testcontainers.org/)

---

## Document Metadata

**Version**: 2.0
**Last Updated**: 2026-01-23
**Issue**: [#2922](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2922)
**Total Tests**: 4,225 (4,033 FE + 162 BE + 30 E2E)
**Coverage**: Backend >90%, Frontend >90%
**Maintainers**: QA Team

**Change Log**:
- v2.0 (2026-01-23): Added MSW guide, Page Object Model patterns, comprehensive troubleshooting
- v1.0 (2026-01-01): Initial comprehensive testing guide

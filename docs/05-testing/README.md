# Testing Guide

**MeepleAI Testing Documentation** - Comprehensive testing strategy and best practices

---

## 📂 Documentation Structure

```
05-testing/
├── backend/                    # Backend testing guides
│   ├── testcontainers-best-practices.md
│   ├── INTEGRATION_TEST_OPTIMIZATION.md
│   ├── oauth-testing.md
│   ├── shared-catalog-fts-performance-validation.sql
│   └── log-generation-test-plan.md
├── frontend/                   # Frontend testing guides
│   └── week4-frontend-component-test-plan.md
├── e2e/                        # End-to-End testing guides
│   ├── E2E_TEST_GUIDE.md
│   ├── e2e-selector-best-practices.md
│   ├── playwright-report-guide.md
│   ├── BackgroundRulebookAnalysis-ManualTesting.md
│   └── RulebookAnalysis-ManualTesting.md
└── README.md                   # This file
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

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Vitest Guide](https://vitest.dev/)
- [Playwright Documentation](https://playwright.dev/)
- [Testing Library Best Practices](https://testing-library.com/docs/queries/about)
- [FluentAssertions](https://fluentassertions.com/)
- [Testcontainers](https://dotnet.testcontainers.org/)

---

**Version**: 1.0
**Last Updated**: 2026-01-01
**Total Tests**: 4,225 (4,033 FE + 162 BE + 30 E2E)
**Coverage**: >90%
**Maintainers**: QA Team

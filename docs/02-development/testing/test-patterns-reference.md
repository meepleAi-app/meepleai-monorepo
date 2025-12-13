# Test Patterns Reference

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Purpose**: Quick reference for common testing patterns
**Location**: Consolidated from `test-patterns.md` + `e2e-patterns.md` + `frontend/testing-react-19-patterns.md`

---

## Table of Contents

1. [Unit Test Patterns](#unit-test-patterns)
2. [Integration Test Patterns](#integration-test-patterns)
3. [E2E Test Patterns](#e2e-test-patterns)
4. [React 19 Specific Patterns](#react-19-specific-patterns)
5. [Mocking Patterns](#mocking-patterns)
6. [Performance Test Patterns](#performance-test-patterns)
7. [Quick Reference](#quick-reference)

---

## Unit Test Patterns

### AAA Pattern (Arrange, Act, Assert)

```typescript
// Frontend (Jest/Vitest)
describe('formatDate', () => {
  it('formats ISO date to readable format', () => {
    // Arrange
    const isoDate = '2025-12-08T10:30:00Z';

    // Act
    const result = formatDate(isoDate);

    // Assert
    expect(result).toBe('08 Dec 2025');
  });
});
```

```csharp
// Backend (xUnit)
public class ConfidenceScoreTests
{
    [Fact]
    public void Constructor_ValidScore_CreatesInstance()
    {
        // Arrange
        var value = 0.85m;

        // Act
        var score = new ConfidenceScore(value);

        // Assert
        Assert.Equal(0.85m, score.Value);
    }
}
```

### Test Data Builders

```typescript
// Frontend
class GameBuilder {
  private game: Partial<Game> = {
    id: 'test-id',
    name: 'Test Game',
    minPlayers: 2,
    maxPlayers: 4
  };

  withName(name: string) {
    this.game.name = name;
    return this;
  }

  withPlayers(min: number, max: number) {
    this.game.minPlayers = min;
    this.game.maxPlayers = max;
    return this;
  }

  build(): Game {
    return this.game as Game;
  }
}

// Usage
const game = new GameBuilder()
  .withName('Catan')
  .withPlayers(3, 4)
  .build();
```

```csharp
// Backend
public class GameTestBuilder
{
    private readonly Game _game = new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Game",
        MinPlayers = 2,
        MaxPlayers = 4
    };

    public GameTestBuilder WithName(string name)
    {
        _game.Name = name;
        return this;
    }

    public GameTestBuilder WithPlayers(int min, int max)
    {
        _game.MinPlayers = min;
        _game.MaxPlayers = max;
        return this;
    }

    public Game Build() => _game;
}

// Usage
var game = new GameTestBuilder()
    .WithName("Catan")
    .WithPlayers(3, 4)
    .Build();
```

### Parameterized Tests

```typescript
// Frontend (Jest)
describe('validateEmail', () => {
  it.each([
    ['valid@example.com', true],
    ['invalid@', false],
    ['@invalid.com', false],
    ['no-at-sign.com', false],
  ])('validates %s as %s', (email, expected) => {
    expect(validateEmail(email)).toBe(expected);
  });
});
```

```csharp
// Backend (xUnit Theory)
public class EmailValidatorTests
{
    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("invalid@", false)]
    [InlineData("@invalid.com", false)]
    [InlineData("no-at-sign.com", false)]
    public void ValidateEmail_ReturnsExpectedResult(string email, bool expected)
    {
        var result = EmailValidator.IsValid(email);
        Assert.Equal(expected, result);
    }
}
```

---

## Integration Test Patterns

### API Endpoint Testing (xUnit + WebApplicationFactory)

```csharp
// apps/api/tests/Integration/ChatEndpointTests.cs
public class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly TestDatabase _testDb;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _testDb = factory.Services.GetRequiredService<TestDatabase>();
    }

    [Fact]
    public async Task PostChat_ValidQuestion_ReturnsAnswer()
    {
        // Arrange
        var gameId = await _testDb.CreateTestGameAsync("Catan");
        await _testDb.SeedPdfDocumentAsync(gameId, "catan-rules.pdf");

        var request = new { gameId, question = "Come si gioca?" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chat", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0.70m);
    }

    public void Dispose() => _testDb?.Dispose();
}
```

### Repository Testing (Testcontainers)

```csharp
// apps/api/tests/Integration/GameRepositoryTests.cs
public class GameRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres;
    private IGameRepository _repository;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();

        _repository = CreateRepository(_postgres.GetConnectionString());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGame_ReturnsGame()
    {
        // Arrange
        var gameId = await _repository.AddAsync(new Game { Name = "Catan" });

        // Act
        var game = await _repository.GetByIdAsync(gameId);

        // Assert
        Assert.NotNull(game);
        Assert.Equal("Catan", game.Name);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

**Key Patterns**:
- ✅ Use `IAsyncLifetime` for async setup/teardown
- ✅ Use Testcontainers for real database
- ✅ Dispose containers properly

---

## E2E Test Patterns

### Page Object Model (POM)

```typescript
// apps/web/e2e/pages/LoginPage.ts
export class LoginPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('http://localhost:3000/login');
  }

  async login(email: string, password: string) {
    await this.page.getByLabel(/email/i).fill(email);
    await this.page.getByLabel(/password/i).fill(password);
    await this.page.getByRole('button', { name: /sign in/i }).click();
  }

  async expectLoginSuccess() {
    await expect(this.page).toHaveURL(/\/dashboard/);
  }

  async expectLoginError(message: string) {
    await expect(this.page.getByText(message)).toBeVisible();
  }
}
```

**Usage**:
```typescript
test('user can login successfully', async ({ page }) => {
  const loginPage = new LoginPage(page);

  await loginPage.goto();
  await loginPage.login('demo@meepleai.dev', 'Demo123!');
  await loginPage.expectLoginSuccess();
});
```

### User Journey Pattern

```typescript
// apps/web/e2e/chat-flow.spec.ts
test.describe('Chat Flow', () => {
  let loginPage: LoginPage;
  let dashboardPage: DashboardPage;
  let chatPage: ChatPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    dashboardPage = new DashboardPage(page);
    chatPage = new ChatPage(page);

    // Setup: Login user
    await loginPage.goto();
    await loginPage.login('demo@meepleai.dev', 'Demo123!');
  });

  test('complete chat journey', async () => {
    // 1. Select game
    await dashboardPage.selectGame('Catan');

    // 2. Ask question
    await chatPage.askQuestion('Come si gioca?');

    // 3. Verify answer
    await chatPage.expectAnswerVisible();
    await chatPage.expectConfidenceAbove(0.70);

    // 4. Verify citations
    await chatPage.expectCitationsPresent();
  });
});
```

**Key Patterns**:
- ✅ Use Page Objects for reusability
- ✅ Setup common state in `beforeEach`
- ✅ Test complete user journeys
- ✅ Use accessible queries

### Waiting Strategies

```typescript
// ❌ BAD: Fixed delays
await page.waitForTimeout(5000);

// ✅ GOOD: Wait for condition
await expect(page.getByText('Success')).toBeVisible({ timeout: 5000 });

// ✅ GOOD: Wait for navigation
await page.waitForURL(/\/dashboard/);

// ✅ GOOD: Wait for network idle
await page.waitForLoadState('networkidle');

// ✅ GOOD: Wait for specific request
await page.waitForResponse(resp => resp.url().includes('/api/chat'));
```

---

## React 19 Specific Patterns

### Testing Hooks with React 19

```typescript
// apps/web/__tests__/hooks/useGameData.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { useGameData } from '@/hooks/useGameData';
import { api } from '@/lib/api';

jest.mock('@/lib/api');
const mockedApi = api as jest.Mocked<typeof api>;

describe('useGameData', () => {
  it('fetches and returns game data', async () => {
    mockedApi.getGame.mockResolvedValue({
      id: '1',
      name: 'Catan',
      players: '3-4'
    });

    const { result } = renderHook(() => useGameData('1'));

    // Initial state
    expect(result.current.loading).toBe(true);
    expect(result.current.data).toBeUndefined();

    // Wait for data
    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.data).toEqual({
      id: '1',
      name: 'Catan',
      players: '3-4'
    });
  });
});
```

### Testing Server Components (Next.js 16)

```typescript
// apps/web/__tests__/app/dashboard/page.test.tsx
import { render, screen } from '@testing-library/react';
import DashboardPage from '@/app/dashboard/page';
import { getGames } from '@/lib/server-actions';

jest.mock('@/lib/server-actions');
const mockedGetGames = getGames as jest.MockedFunction<typeof getGames>;

describe('DashboardPage', () => {
  it('renders games from server action', async () => {
    mockedGetGames.mockResolvedValue([
      { id: '1', name: 'Catan' },
      { id: '2', name: 'Ticket to Ride' }
    ]);

    // Render server component
    render(await DashboardPage());

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });
});
```

**Key Patterns for React 19**:
- ✅ `await` server components before rendering
- ✅ Mock server actions, not fetch directly
- ✅ Test with Suspense boundaries

### Testing Zustand Stores

```typescript
// apps/web/__tests__/stores/useGameStore.test.tsx
import { renderHook, act } from '@testing-library/react';
import { useGameStore } from '@/stores/useGameStore';

describe('useGameStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useGameStore.setState({ selectedGame: null });
  });

  it('selects game and updates state', () => {
    const { result } = renderHook(() => useGameStore());

    act(() => {
      result.current.selectGame({ id: '1', name: 'Catan' });
    });

    expect(result.current.selectedGame).toEqual({ id: '1', name: 'Catan' });
  });

  it('clears selected game', () => {
    const { result } = renderHook(() => useGameStore());

    act(() => {
      result.current.selectGame({ id: '1', name: 'Catan' });
      result.current.clearSelection();
    });

    expect(result.current.selectedGame).toBeNull();
  });
});
```

---

## Mocking Patterns

### Mock API Client (Frontend)

```typescript
// apps/web/__tests__/setup.ts
jest.mock('@/lib/api', () => ({
  api: {
    getGames: jest.fn(),
    getGame: jest.fn(),
    uploadPdf: jest.fn(),
    askQuestion: jest.fn(),
  }
}));
```

**Usage in tests**:
```typescript
import { api } from '@/lib/api';
const mockedApi = api as jest.Mocked<typeof api>;

// In test
mockedApi.getGames.mockResolvedValue([...games]);
mockedApi.getGame.mockRejectedValue(new Error('Not found'));
```

### Mock Repository (Backend)

```csharp
// Backend (Moq)
var mockRepository = new Mock<IGameRepository>();

mockRepository
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(testGame);

mockRepository
    .Setup(r => r.GetAllAsync())
    .ReturnsAsync(new List<Game> { game1, game2 });

// Verify method called
mockRepository.Verify(
    r => r.GetByIdAsync(It.Is<Guid>(id => id == testGameId)),
    Times.Once
);
```

### Mock Web Worker (Frontend)

```typescript
// apps/web/__tests__/mocks/uploadWorker.mock.ts
export class MockUploadWorker {
  onmessage: ((event: MessageEvent) => void) | null = null;

  postMessage(data: any) {
    // Simulate worker processing
    setTimeout(() => {
      this.onmessage?.({
        data: { type: 'progress', progress: 100 }
      } as MessageEvent);
    }, 100);
  }

  terminate() {}
}

// Setup in test
global.Worker = MockUploadWorker as any;
```

See: [Worker Mocking Patterns](./frontend/worker-mocking-patterns.md)

### Mock SSE Stream (Frontend)

```typescript
// Mock Server-Sent Events
class MockEventSource {
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;

  constructor(public url: string) {
    setTimeout(() => {
      this.onmessage?.({
        data: JSON.stringify({ content: 'Test message' })
      } as MessageEvent);
    }, 100);
  }

  close() {}
}

global.EventSource = MockEventSource as any;
```

---

## Performance Test Patterns

### k6 Load Test

```javascript
// tests/performance/chat-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 50 },   // Ramp up
    { duration: '5m', target: 50 },   // Stay at 50 users
    { duration: '2m', target: 100 },  // Ramp to 100
    { duration: '5m', target: 100 },  // Stay at 100
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'],  // 95% < 1s
    http_req_failed: ['rate<0.01'],     // Error rate < 1%
  },
};

export default function () {
  const payload = JSON.stringify({
    gameId: '550e8400-e29b-41d4-a716-446655440000',
    question: 'Come si gioca a Catan?',
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Cookie': __ENV.AUTH_COOKIE,  // From environment
    },
  };

  const res = http.post('http://localhost:8080/api/v1/chat', payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'confidence >= 0.70': (r) => JSON.parse(r.body).confidence >= 0.70,
    'response time < 5s': (r) => r.timings.duration < 5000,
  });

  sleep(1);  // Think time between requests
}
```

See: [k6 Performance Testing](./performance/k6-performance-testing.md)

---

## Quick Reference

### Query Selectors (React Testing Library)

**Priority Order**:

1. **getByRole** (Accessibility-first):
   ```typescript
   screen.getByRole('button', { name: /submit/i })
   screen.getByRole('textbox', { name: /email/i })
   screen.getByRole('heading', { name: /title/i })
   ```

2. **getByLabelText** (Form elements):
   ```typescript
   screen.getByLabelText(/email address/i)
   screen.getByLabelText(/password/i)
   ```

3. **getByPlaceholderText** (Inputs):
   ```typescript
   screen.getByPlaceholderText(/search games/i)
   ```

4. **getByText** (Content):
   ```typescript
   screen.getByText(/welcome back/i)
   ```

5. **getByTestId** (Last resort):
   ```typescript
   screen.getByTestId('chat-message')
   ```

### Async Waiting Patterns

```typescript
// Wait for element to appear
await waitFor(() => {
  expect(screen.getByText('Success')).toBeInTheDocument();
});

// Wait for element to disappear
await waitFor(() => {
  expect(screen.queryByText('Loading')).not.toBeInTheDocument();
});

// Wait with custom timeout
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
}, { timeout: 10000 });

// Find async (alternative)
const element = await screen.findByText('Success'); // Auto-waits
```

### Common Assertions

**Frontend (Jest/Vitest)**:
```typescript
// Existence
expect(element).toBeInTheDocument();
expect(element).toBeVisible();

// Text content
expect(element).toHaveTextContent('Expected text');
expect(element).toHaveTextContent(/pattern/i);

// Attributes
expect(element).toHaveAttribute('href', '/dashboard');
expect(element).toBeDisabled();

// Form values
expect(input).toHaveValue('test value');
expect(checkbox).toBeChecked();

// Function calls
expect(mockFn).toHaveBeenCalled();
expect(mockFn).toHaveBeenCalledWith('arg1', 'arg2');
expect(mockFn).toHaveBeenCalledTimes(2);
```

**Backend (xUnit)**:
```csharp
// Equality
Assert.Equal(expected, actual);
Assert.NotEqual(unexpected, actual);

// Nullability
Assert.NotNull(value);
Assert.Null(value);

// Boolean
Assert.True(condition);
Assert.False(condition);

// Collections
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Contains(item, collection);
Assert.Equal(3, collection.Count);

// Exceptions
Assert.Throws<ArgumentException>(() => method());
var ex = Assert.Throws<ValidationException>(() => method());
Assert.Equal("Expected message", ex.Message);

// Async
var result = await Assert.ThrowsAsync<Exception>(async () => await methodAsync());
```

---

## Best Practices Summary

### General

✅ **DO**:
- Test behavior, not implementation
- Use descriptive test names
- Follow AAA pattern
- Mock external dependencies
- Clean up resources in teardown
- Maintain 90%+ coverage

❌ **DON'T**:
- Test private methods directly
- Share state between tests
- Use fixed delays (use `waitFor`)
- Ignore failing tests
- Skip teardown/cleanup

### Frontend

✅ **DO**:
- Use `screen.getByRole()` first
- Use `userEvent` for interactions
- Test accessibility
- Mock API calls
- Test error states

❌ **DON'T**:
- Query by className or ID
- Test implementation details
- Forget to mock timers/dates
- Skip loading states

### Backend

✅ **DO**:
- Use Testcontainers for integration tests
- Test with real database
- Use `async/await` consistently
- Dispose resources properly
- Test validation and error cases

❌ **DON'T**:
- Use `.Result` or `.Wait()` (use `await`)
- Share database between tests
- Mock repository in integration tests
- Forget to test edge cases

### E2E

✅ **DO**:
- Use Page Object Model
- Test critical user journeys
- Wait for elements explicitly
- Use accessible queries
- Reset state between tests

❌ **DON'T**:
- Test every possible path (too slow)
- Use XPath or CSS selectors
- Rely on exact text matching
- Skip error scenarios

---

## Related Documentation

### Core Testing
- **[Comprehensive Testing Guide](./comprehensive-testing-guide.md)** - This guide
- **[Testing Strategy](./core/testing-strategy.md)** - Overall approach
- **[Testing Quick Reference](./core/testing-quick-reference.md)** - Cheat sheet

### Backend Testing
- **[Backend Test Architecture](./backend/test-architecture.md)** - Organization
- **[Integration Tests Quick Reference](./backend/integration-tests-quick-reference.md)** - Patterns
- **[Mock Implementation](./backend/mock-implementation.md)** - Mocking strategies

### Frontend Testing
- **[Worker Mocking Patterns](./frontend/worker-mocking-patterns.md)** - Web Worker testing
- **[Upload Test Guide](./frontend/UPLOAD_TEST_GUIDE.md)** - File upload testing
- **[POM Architecture](./pom-architecture-design.md)** - Page Object Model
- **[POM Coding Standards](./pom-coding-standards.md)** - POM best practices

### Specialized
- **[Accessibility Testing](./accessibility-testing-guide.md)** - A11y compliance
- **[Concurrency Testing](./concurrency-testing-guide.md)** - Race conditions
- **[Golden Dataset Testing](./golden-dataset-testing-guide.md)** - RAG accuracy
- **[Visual Testing](./visual-testing-guide.md)** - Chromatic

### Performance
- **[k6 Performance Testing](./performance/k6-performance-testing.md)** - Load testing
- **[Performance Testing Guide](./performance/performance-testing-guide.md)** - Strategy

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `test-writing-guide.md` + `manual-testing-guide.md` + `specialized/manual-testing-guide.md`
- ✅ Merged `test-patterns.md` + `e2e-patterns.md` + `frontend/testing-react-19-patterns.md`
- ✅ Added React 19 specific patterns section
- ✅ Added comprehensive quick reference
- ✅ Organized by test type (unit, integration, E2E, performance)
- ✅ Updated all cross-references

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Test Types Covered**: Unit, Integration, E2E, Manual, Performance, Visual
**Patterns**: 20+ documented patterns with examples
**Documentation**: Single comprehensive pattern reference


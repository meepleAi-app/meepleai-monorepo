# Comprehensive Testing Guide - MeepleAI

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-08
**Goal**: Complete testing reference for all test types
**Location**: Consolidated from `test-writing-guide.md` + `manual-testing-guide.md` + `specialized/manual-testing-guide.md`

---

## Table of Contents

1. [Testing Philosophy](#testing-philosophy)
2. [Test Types Overview](#test-types-overview)
3. [Unit Testing](#unit-testing)
4. [Integration Testing](#integration-testing)
5. [E2E Testing](#e2e-testing)
6. [Manual Testing](#manual-testing)
7. [Performance Testing](#performance-testing)
8. [Visual Regression Testing](#visual-regression-testing)
9. [Testing Patterns](#testing-patterns)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)
12. [Related Documentation](#related-documentation)

---

## Testing Philosophy

### Coverage Requirements

| Layer | Target | Enforced | Tools |
|-------|--------|----------|-------|
| **Frontend** | 90%+ | ✅ Yes | Jest, Vitest, React Testing Library |
| **Backend** | 90%+ | ✅ Yes | xUnit, Moq, Testcontainers |
| **E2E** | Critical paths | ⚠️ Recommended | Playwright |
| **Visual** | Key components | 📈 Growing | Chromatic, Storybook |

### Test Pyramid

```
        /\
       /  \      E2E Tests
      /____\     (10% - Slow, expensive)
     /      \
    /        \   Integration Tests
   /__________\  (30% - Medium speed)
  /            \
 /______________\ Unit Tests
                  (60% - Fast, cheap)
```

### Quality Gates

**All PRs Must**:
- ✅ Pass all tests (100%)
- ✅ Maintain 90%+ coverage
- ✅ Pass linting & type checking
- ✅ Have no console errors in tests

---

## Test Types Overview

### Unit Tests

**Purpose**: Test individual functions, components, or services in isolation

**Characteristics**:
- **Speed**: Fast (<10ms per test)
- **Dependencies**: All mocked
- **Scope**: Single function/component
- **When**: Testing pure logic, utilities, isolated components

**Example Use Cases**:
- Utility functions (formatDate, validateEmail)
- React components (Button, Input, Card)
- Domain logic (value objects, aggregates)
- Business rules validation

### Integration Tests

**Purpose**: Test how multiple units work together

**Characteristics**:
- **Speed**: Medium (100ms-1s per test)
- **Dependencies**: Real (Testcontainers) or partially mocked
- **Scope**: Service + database, API endpoint + dependencies
- **When**: Testing service interactions, database ops, API endpoints

**Example Use Cases**:
- API endpoints with database
- RAG pipeline (retrieval + generation)
- PDF processing (extraction + validation)
- Authentication flow (service + repository)

### E2E Tests

**Purpose**: Test complete user workflows through UI

**Characteristics**:
- **Speed**: Slow (5-30s per test)
- **Dependencies**: Real frontend + test backend
- **Scope**: Multi-page user journey
- **When**: Testing critical paths, form submissions, authentication flows

**Example Use Cases**:
- User registration and login
- PDF upload and chat interaction
- Settings management
- Game selection and question asking

---

## Unit Testing

### Frontend Unit Tests (Jest/Vitest + React Testing Library)

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
    expect(screen.getByTestId('loading-spinner')).toBeInTheDocument();
  });

  it('disables button when disabled prop is true', () => {
    render(<LoadingButton disabled>Save Changes</LoadingButton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
```

**Key Patterns**:
- ✅ Use `screen.getByRole()` for accessible queries
- ✅ Use regex for case-insensitive matching: `/text/i`
- ✅ Test behavior, not implementation details
- ❌ Don't test props directly, test rendered output

#### Component with User Interaction

```typescript
// apps/web/__tests__/components/SearchBar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchBar } from '@/components/SearchBar';

describe('SearchBar', () => {
  it('calls onSearch when user submits', async () => {
    const user = userEvent.setup();
    const handleSearch = jest.fn();

    render(<SearchBar onSearch={handleSearch} />);

    // Type in search box
    const input = screen.getByRole('textbox', { name: /search/i });
    await user.type(input, 'Catan');

    // Submit form
    const submitButton = screen.getByRole('button', { name: /search/i });
    await user.click(submitButton);

    // Verify callback
    expect(handleSearch).toHaveBeenCalledWith('Catan');
    expect(handleSearch).toHaveBeenCalledTimes(1);
  });
});
```

**Key Patterns**:
- ✅ Use `userEvent.setup()` for realistic interactions
- ✅ Use `await` with user interactions
- ✅ Verify function calls with `jest.fn()`

#### Component with API Calls (Mocked)

```typescript
// apps/web/__tests__/components/GameList.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { GameList } from '@/components/GameList';
import { api } from '@/lib/api';

jest.mock('@/lib/api');
const mockedApi = api as jest.Mocked<typeof api>;

describe('GameList', () => {
  it('displays games after loading', async () => {
    mockedApi.getGames.mockResolvedValue([
      { id: '1', name: 'Catan', players: '3-4' },
      { id: '2', name: 'Ticket to Ride', players: '2-5' }
    ]);

    render(<GameList />);

    // Initially shows loading
    expect(screen.getByText(/loading/i)).toBeInTheDocument();

    // Wait for games to load
    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    });
  });

  it('displays error message when API fails', async () => {
    mockedApi.getGames.mockRejectedValue(new Error('Network error'));

    render(<GameList />);

    await waitFor(() => {
      expect(screen.getByText(/error loading games/i)).toBeInTheDocument();
    });
  });
});
```

**Key Patterns**:
- ✅ Mock external dependencies (`jest.mock()`)
- ✅ Use `waitFor()` for async updates
- ✅ Test both success and error states

### Backend Unit Tests (xUnit + .NET)

#### Domain Logic Test

```csharp
// apps/api/tests/Domain/ValueObjects/ConfidenceScoreTests.cs
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
        Assert.True(score.IsHighConfidence);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_InvalidScore_ThrowsArgumentException(decimal value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ConfidenceScore(value));
    }

    [Fact]
    public void IsHighConfidence_ReturnsTrue_WhenAboveThreshold()
    {
        // Arrange
        var score = new ConfidenceScore(0.75m);

        // Act & Assert
        Assert.True(score.IsHighConfidence);
    }
}
```

**Key Patterns**:
- ✅ AAA pattern (Arrange, Act, Assert)
- ✅ Use `[Theory]` with `[InlineData]` for multiple test cases
- ✅ Test domain invariants and validation

---

## Integration Testing

### Backend Integration Tests (Testcontainers)

#### API Endpoint Integration Test

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

        var request = new
        {
            gameId,
            question = "Come si gioca?"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/chat", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0.70m);
        Assert.NotEmpty(result.Answer);
    }
}
```

**Key Patterns**:
- ✅ Use `WebApplicationFactory` for in-process testing
- ✅ Use `TestDatabase` helper for test data
- ✅ Clean up test data in `Dispose()`

#### Repository Integration Test

```csharp
// apps/api/tests/Integration/GameRepositoryTests.cs
public class GameRepositoryTests : IDisposable
{
    private readonly PostgresContainer _postgres;
    private readonly IGameRepository _repository;

    public GameRepositoryTests()
    {
        _postgres = new PostgresBuilder().Build();
        _postgres.StartAsync().Wait();

        // Setup repository with test connection
        _repository = CreateRepository(_postgres.GetConnectionString());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGame_ReturnsGame()
    {
        // Arrange
        var gameId = await _repository.AddAsync(new Game
        {
            Name = "Catan",
            MinPlayers = 3,
            MaxPlayers = 4
        });

        // Act
        var game = await _repository.GetByIdAsync(gameId);

        // Assert
        Assert.NotNull(game);
        Assert.Equal("Catan", game.Name);
        Assert.Equal(3, game.MinPlayers);
    }

    public void Dispose()
    {
        _postgres.DisposeAsync().AsTask().Wait();
    }
}
```

**Key Patterns**:
- ✅ Use Testcontainers for real database
- ✅ Dispose containers in `Dispose()`
- ✅ Test with real SQL, not mocked repository

---

## E2E Testing

### Playwright E2E Tests

#### Complete User Journey

```typescript
// apps/web/e2e/chat-flow.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Chat Flow', () => {
  test('user can ask question and receive answer', async ({ page }) => {
    // 1. Navigate and login
    await page.goto('http://localhost:3000');
    await page.getByRole('link', { name: /login/i }).click();
    await page.getByLabel(/email/i).fill('demo@meepleai.dev');
    await page.getByLabel(/password/i).fill('Demo123!');
    await page.getByRole('button', { name: /sign in/i }).click();

    // 2. Select game
    await expect(page).toHaveURL(/\/dashboard/);
    await page.getByRole('link', { name: /catan/i }).click();

    // 3. Ask question
    await page.getByRole('textbox', { name: /ask a question/i }).fill('Come si gioca?');
    await page.getByRole('button', { name: /send/i }).click();

    // 4. Verify answer appears
    await expect(page.getByTestId('chat-message')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/per iniziare/i)).toBeVisible();

    // 5. Verify confidence score
    const confidence = await page.getByTestId('confidence-badge').textContent();
    expect(parseFloat(confidence!)).toBeGreaterThan(0.70);
  });
});
```

**Key Patterns**:
- ✅ Use Page Object Model for reusability
- ✅ Use accessible queries (`getByRole`, `getByLabel`)
- ✅ Add timeouts for async operations
- ✅ Verify UI state changes

See: [E2E Patterns](./e2e-patterns.md) for more examples

---

## Manual Testing

### Setup Test Environment

#### Start Full Stack

```bash
# Terminal 1: Infrastructure
cd infra
docker compose --profile dev up -d

# Verify services
curl http://localhost:8080/health

# Terminal 2: API
cd apps/api/src/Api
dotnet run

# Terminal 3: Frontend
cd apps/web
pnpm dev
```

**Expected Services**:
- ✅ PostgreSQL: http://localhost:5432
- ✅ Qdrant: http://localhost:6333/dashboard
- ✅ Redis: http://localhost:6379
- ✅ Prometheus: http://localhost:9090
- ✅ Grafana: http://localhost:3001 (admin/admin)
- ✅ API: http://localhost:8080/health
- ✅ Web: http://localhost:3000

### Authentication Testing

#### User Registration

**Steps**:
1. Navigate to http://localhost:3000
2. Click "Register" or go to `/register`
3. Fill form:
   - Email: `tester@meepleai.dev`
   - Password: `Test123!`
   - Confirm Password: `Test123!`
4. Click "Create Account"

**Expected Result**:
- ✅ Redirect to dashboard
- ✅ Welcome message displayed
- ✅ Session cookie set (`meepleai_session`)

**Verification**:
```bash
# Check user in database
docker compose exec postgres psql -U meepleai -c \
  "SELECT email FROM users WHERE email = 'tester@meepleai.dev';"
```

#### Login Flow

**Steps**:
1. Go to http://localhost:3000/login
2. Enter credentials:
   - Email: `demo@meepleai.dev`
   - Password: `Demo123!`
3. Click "Sign In"

**Expected Result**:
- ✅ Redirect to `/dashboard`
- ✅ User name displayed in header
- ✅ Session persists on page refresh

#### 2FA Testing

**Setup**:
1. Login with demo user
2. Go to Settings → Privacy
3. Enable 2FA
4. Scan QR code with authenticator app (Google Authenticator, Authy)

**Test Login with 2FA**:
1. Logout
2. Login with email/password
3. **Expected**: Redirect to 2FA code page
4. Enter 6-digit code from app
5. **Expected**: Login successful, redirect to dashboard

**Test Backup Codes**:
1. Generate backup codes in settings
2. Save codes securely
3. Logout and login
4. Use backup code instead of authenticator
5. **Expected**: Login successful, code marked as used

See: [2FA Security Assessment](../../06-security/2fa-security-assessment-issue-576.md)

### PDF Upload & Processing

**Steps**:
1. Login to application
2. Navigate to PDF upload page
3. Select PDF file (test with: `tests/fixtures/sample-catan-rules.pdf`)
4. Click "Upload"

**Expected Result**:
- ✅ Upload progress bar (0-100%)
- ✅ Processing status updates (Queue → Processing → Complete)
- ✅ Success notification after processing
- ✅ PDF appears in document list

**Verification**:
```bash
# Check PDF in database
docker compose exec postgres psql -U meepleai -c \
  "SELECT id, file_name, processing_status FROM pdf_documents ORDER BY created_at DESC LIMIT 1;"

# Check vectors in Qdrant
curl http://localhost:6333/collections/game_rules/points/count
```

**Quality Metrics**:
- Stage used (1=Unstructured, 2=SmolDocling, 3=Docnet)
- Quality score (target: ≥0.80)
- Processing time (target: <10s)

See: [PDF Processing Guide](../guides/pdf-processing-guide.md)

### RAG Chat Testing

**Prerequisites**:
- Game exists in database
- PDF processed and indexed

**Steps**:
1. Navigate to game chat page
2. Type question: "Come si posizionano le strade in Catan?"
3. Click "Send" or press Enter

**Expected Result**:
- ✅ Streaming response (text appears progressively)
- ✅ Confidence score displayed (target: ≥0.70)
- ✅ Source citations shown (PDF name + pages)
- ✅ Response time <5s (P95)

**Test Cases**:
```
✓ Simple question → High confidence (≥0.85)
✓ Complex question → Medium confidence (0.70-0.85)
✓ Ambiguous question → System clarification request
✓ Out-of-scope question → Polite decline
✓ Follow-up question → Context awareness
```

**Verification**:
```bash
# Check chat in database
docker compose exec postgres psql -U meepleai -c \
  "SELECT question, confidence_score FROM chat_messages ORDER BY created_at DESC LIMIT 5;"

# Check Prometheus metrics
curl 'http://localhost:9090/api/v1/query?query=meepleai_rag_confidence_score'
```

See: [RAG Validation Pipeline](../rag-validation-pipeline.md)

### Configuration Management

**Test Dynamic Configuration** (Issue #CONFIG-01-06):

**Steps**:
1. Login as admin
2. Navigate to `/admin/configuration`
3. Update configuration value
4. Click "Save"

**Expected Result**:
- ✅ Configuration saved to database
- ✅ Version incremented
- ✅ Change logged in audit trail
- ✅ New value takes effect immediately (no restart)

**Test Rollback**:
1. Click "History" on configuration item
2. Select previous version
3. Click "Restore"
4. **Expected**: Configuration reverted, version incremented

See: [Admin Console](../../01-architecture/components/admin-console.md)

---

## Performance Testing

### k6 Load Testing

**Setup**:
```bash
cd tests/performance
k6 run load-test.js
```

**Test Scenarios**:

#### API Endpoint Load Test

```javascript
// tests/performance/api-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95% < 500ms
    http_req_failed: ['rate<0.01'],    // Error rate < 1%
  },
};

export default function () {
  const res = http.post('http://localhost:8080/api/v1/chat', JSON.stringify({
    gameId: '550e8400-e29b-41d4-a716-446655440000',
    question: 'Come si gioca a Catan?',
  }), {
    headers: { 'Content-Type': 'application/json' },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'confidence >= 0.70': (r) => JSON.parse(r.body).confidence >= 0.70,
    'response time < 5s': (r) => r.timings.duration < 5000,
  });

  sleep(1);
}
```

**SLA Targets**:
- P95 latency: <500ms
- Error rate: <1%
- Throughput: >100 req/s

See: [k6 Performance Testing](./performance/k6-performance-testing.md)

---

## Visual Regression Testing

### Chromatic + Storybook

**Setup**:
```bash
cd apps/web
pnpm storybook  # Start Storybook dev server
```

**Run Visual Tests**:
```bash
# Local visual testing
pnpm test:visual

# CI visual regression (non-blocking)
pnpm test:visual:ci

# Debug mode
pnpm test:visual:debug
```

**Review Changes**:
1. Open Chromatic dashboard (link in CI output)
2. Review visual diffs
3. Accept or reject changes

**Status** (Phase 1 Complete):
- Coverage: 0% (Phase 2 in planning)
- Mode: Non-blocking (will enable blocking at 50%+ coverage)
- CI Integration: Automatic on PRs

See: [Visual Testing Guide](./visual-testing-guide.md)

---

## Testing Patterns

### AAA Pattern (Arrange, Act, Assert)

```csharp
[Fact]
public void Example_Test()
{
    // Arrange: Setup test data and dependencies
    var service = new MyService();
    var input = "test data";

    // Act: Execute the operation being tested
    var result = service.DoSomething(input);

    // Assert: Verify expected outcome
    Assert.Equal("expected", result);
}
```

### Test Naming Convention

**Pattern**: `MethodName_Scenario_ExpectedBehavior`

**Examples**:
- ✅ `GetByIdAsync_ExistingGame_ReturnsGame`
- ✅ `ValidateInput_InvalidEmail_ThrowsValidationException`
- ✅ `ProcessPdf_LargeFile_UsesStreamingExtraction`

### Mocking External Dependencies

**Frontend** (Jest):
```typescript
jest.mock('@/lib/api');
const mockedApi = api as jest.Mocked<typeof api>;

mockedApi.getGames.mockResolvedValue([...games]);
```

**Backend** (Moq):
```csharp
var mockRepository = new Mock<IGameRepository>();
mockRepository
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(testGame);
```

See: [Test Patterns Reference](./test-patterns-reference.md) for comprehensive patterns

---

## Best Practices

### General

1. **Test Behavior, Not Implementation**
   - ✅ Test what component does
   - ❌ Don't test internal state or private methods

2. **Descriptive Test Names**
   - ✅ `ProcessPdf_LargeFile_UsesStreamingExtraction`
   - ❌ `TestPdfProcessing`

3. **One Assertion Per Test** (when possible)
   - Focus on single behavior
   - Easier to identify failures

4. **Avoid Test Interdependence**
   - Each test should run independently
   - Use setup/teardown, not shared state

5. **Use Test Data Builders**
   - Create helper functions for test data
   - Improves readability and maintainability

### Frontend Testing

1. **Query Priority** (React Testing Library):
   ```
   1. getByRole        (Accessibility-first)
   2. getByLabelText   (Form elements)
   3. getByPlaceholder (Inputs)
   4. getByText        (Content)
   5. getByTestId      (Last resort)
   ```

2. **Avoid Testing Implementation Details**:
   - ❌ Don't test component state
   - ❌ Don't test CSS classes
   - ✅ Test user-visible behavior

3. **Mock External Dependencies**:
   - API calls → Use `jest.mock()`
   - Date/time → Use `jest.useFakeTimers()`
   - Random → Use seeded random

### Backend Testing

1. **Use Testcontainers for Integration Tests**:
   - Real PostgreSQL, Redis, Qdrant
   - Isolated test environment
   - Parallel test execution safe

2. **Async/Await Consistently**:
   ```csharp
   [Fact]
   public async Task MyTest()
   {
       var result = await serviceAsync();  // ✅
       // NOT: service.Wait() or .Result
   }
   ```

3. **Dispose Resources**:
   ```csharp
   public void Dispose()
   {
       _testDb?.Dispose();
       _httpClient?.Dispose();
   }
   ```

### E2E Testing

1. **Use Page Object Model (POM)**:
   - Encapsulate page interactions
   - Reusable across tests
   - Easier maintenance

See: [POM Architecture](./pom-architecture-design.md)

2. **Wait for Elements**:
   ```typescript
   // ✅ Wait for element
   await expect(page.getByText('Success')).toBeVisible({ timeout: 5000 });

   // ❌ Don't use fixed delays
   await page.waitForTimeout(5000);
   ```

3. **Independent Tests**:
   - Each test should reset state
   - Use `beforeEach` for setup
   - Don't depend on test execution order

---

## Troubleshooting

### Tests Failing Locally

**Common Causes**:

1. **Services not running**:
   ```bash
   docker compose ps  # Check all services UP
   curl http://localhost:8080/health
   ```

2. **Port conflicts**:
   ```bash
   lsof -i :5432  # Check if PostgreSQL port in use
   ```

3. **Stale test data**:
   ```bash
   docker compose down -v  # Remove volumes
   docker compose up -d     # Fresh start
   ```

4. **Outdated dependencies**:
   ```bash
   pnpm install  # Frontend
   dotnet restore  # Backend
   ```

### Tests Passing Locally, Failing in CI

**Common Causes**:

1. **Timing issues**:
   - Add timeouts for async operations
   - Use `waitFor()` instead of fixed delays

2. **Environment differences**:
   - Check env vars in CI
   - Verify Docker images versions match

3. **Resource constraints**:
   - CI may have less memory/CPU
   - Increase timeouts for slow operations

### Flaky Tests

**Diagnosis**:

1. **Run test multiple times**:
   ```bash
   pnpm test -- --testNamePattern="MyTest" --runInBand --repeat=10
   ```

2. **Check for**:
   - Race conditions
   - Async operations without proper waiting
   - Shared state between tests
   - External dependencies (time, random)

**Fixes**:
- Add proper `await` and `waitFor()`
- Use `jest.useFakeTimers()` for time-dependent code
- Reset mocks in `beforeEach`
- Use deterministic test data

---

## Related Documentation

### Core Testing Docs
- **[Test Patterns Reference](./test-patterns-reference.md)** - Common testing patterns
- **[Testing Strategy](./core/testing-strategy.md)** - Overall testing approach
- **[Testing Quick Reference](./core/testing-quick-reference.md)** - Cheat sheet

### Backend Testing
- **[Backend Test Architecture](./backend/test-architecture.md)** - Backend test organization
- **[Integration Tests Quick Reference](./backend/integration-tests-quick-reference.md)** - Integration patterns
- **[Mock Implementation](./backend/mock-implementation.md)** - Mocking strategies

### Frontend Testing
- **[E2E Patterns](./e2e-patterns.md)** - Playwright patterns
- **[Worker Mocking Patterns](./frontend/worker-mocking-patterns.md)** - Web Worker testing
- **[React 19 Testing Patterns](./frontend/testing-react-19-patterns.md)** - React 19 specifics
- **[Upload Test Guide](./frontend/UPLOAD_TEST_GUIDE.md)** - File upload testing

### Specialized Testing
- **[Accessibility Testing](./accessibility-testing-guide.md)** - A11y compliance
- **[Concurrency Testing](./concurrency-testing-guide.md)** - Race conditions
- **[Golden Dataset Testing](./golden-dataset-testing-guide.md)** - RAG accuracy
- **[Visual Testing](./visual-testing-guide.md)** - Chromatic integration

### Performance Testing
- **[k6 Performance Testing](./performance/k6-performance-testing.md)** - Load testing
- **[Performance Testing Guide](./performance/performance-testing-guide.md)** - Overall strategy
- **[Integration Tests Performance](./performance/integration-tests-performance-guide.md)** - Optimization

### Automation
- **[Test Automation Pipeline](../guides/test-automation-pipeline-guide.md)** - CI/CD integration
- **[CI Infrastructure Troubleshooting](./ci-infrastructure-troubleshooting.md)** - CI issues

---

## Quick Commands Reference

### Frontend

```bash
# Run all tests
pnpm test

# Run specific test file
pnpm test LoadingButton.test.tsx

# Run tests in watch mode
pnpm test:watch

# Run tests with coverage
pnpm test:coverage

# Run E2E tests
pnpm test:e2e

# Run visual regression tests
pnpm test:visual
```

### Backend

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ChatEndpointTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GetByIdAsync_ExistingGame_ReturnsGame"

# Verbose output
dotnet test --verbosity detailed
```

### Performance

```bash
# Load test
k6 run tests/performance/load-test.js

# Smoke test
k6 run tests/performance/smoke-test.js

# Stress test
k6 run tests/performance/stress-test.js
```

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `test-writing-guide.md` + `manual-testing-guide.md` + `specialized/manual-testing-guide.md`
- ✅ Added complete coverage of all test types (unit, integration, E2E, manual, performance, visual)
- ✅ Added comprehensive troubleshooting section
- ✅ Added quick commands reference
- ✅ Organized by test type with clear navigation
- ✅ Updated all cross-references to new docs structure

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Coverage Requirements**: 90%+ (enforced)
**Test Count**: 4,225 total (4,033 frontend + 162 backend + 30 E2E)
**Documentation**: Single comprehensive testing guide

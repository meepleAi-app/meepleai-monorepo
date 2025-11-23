# Testing Guide - MeepleAI

**Audience**: QA engineers, testers, and developers responsible for testing.

## 📋 Table of Contents

1. [Testing Overview](#testing-overview)
2. [Testing Stack](#testing-stack)
3. [Backend Testing](#backend-testing)
4. [Frontend Testing](#frontend-testing)
5. [Integration Testing](#integration-testing)
6. [E2E Testing](#e2e-testing)
7. [Coverage Requirements](#coverage-requirements)
8. [Running Tests](#running-tests)
9. [Writing Tests](#writing-tests)
10. [Test Data Management](#test-data-management)
11. [CI/CD Testing](#cicd-testing)
12. [Performance Testing](#performance-testing)
13. [Security Testing](#security-testing)
14. [Troubleshooting](#troubleshooting)

## 📊 Testing Overview

### Testing Philosophy

MeepleAI maintains **90%+ test coverage** across all components with a comprehensive testing strategy:

- **Unit Tests**: Fast, isolated, test single components
- **Integration Tests**: Test component interactions and external services
- **E2E Tests**: Test complete user workflows
- **Performance Tests**: Ensure SLAs are met
- **Security Tests**: Verify security controls

### Current Test Suite

| Type | Count | Coverage | Framework |
|------|-------|----------|-----------|
| **Frontend Unit** | 4,033 | 90.03% | Jest + RTL |
| **Backend Unit** | 162 | 90%+ | xUnit |
| **E2E** | 30 | Critical paths | Playwright |
| **Total** | **4,225** | **90%+** | Mixed |

### Quality Gates

All PRs must meet:
- ✅ All tests pass (100%)
- ✅ Coverage ≥90% (no decrease)
- ✅ No critical security issues
- ✅ Build succeeds
- ✅ Linting passes

## 🛠️ Testing Stack

### Backend (C# / .NET)

- **xUnit**: Test framework
- **Moq**: Mocking framework
- **FluentAssertions**: Assertion library
- **Testcontainers**: Docker containers for integration tests
- **Bogus**: Fake data generation

### Frontend (TypeScript / React)

- **Jest**: Test framework
- **React Testing Library**: Component testing
- **@testing-library/user-event**: User interaction simulation
- **MSW**: API mocking
- **Playwright**: E2E testing

### Integration

- **Testcontainers**: PostgreSQL, Qdrant, Redis, Unstructured, SmolDocling
- **WebApplicationFactory**: API testing
- **Docker Compose**: Full stack integration

## 🔧 Backend Testing

### Unit Tests

**Location**: `apps/api/tests/`

**Structure**:
```
tests/
├── Domain/              # Domain logic tests
├── Application/         # CQRS handler tests
├── Infrastructure/      # Repository tests
└── Integration/         # Integration tests
```

**Example Unit Test**:
```csharp
using Xunit;
using Moq;
using FluentAssertions;

public class UpdateGameTitleCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repositoryMock;
    private readonly Mock<ILogger<UpdateGameTitleCommandHandler>> _loggerMock;
    private readonly UpdateGameTitleCommandHandler _handler;

    public UpdateGameTitleCommandHandlerTests()
    {
        _repositoryMock = new Mock<IGameRepository>();
        _loggerMock = new Mock<ILogger<UpdateGameTitleCommandHandler>>();
        _handler = new UpdateGameTitleCommandHandler(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidTitle_UpdatesGameSuccessfully()
    {
        // Arrange
        var game = new Game { Id = 1, Title = "Old Title" };
        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateGameTitleCommand(1, "New Title");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        game.Title.Should().Be("New Title");
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyTitle_ReturnsDomainException()
    {
        // Arrange
        var command = new UpdateGameTitleCommand(1, "");

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Title cannot be empty");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_InvalidTitle_ThrowsException(string invalidTitle)
    {
        // Arrange
        var command = new UpdateGameTitleCommand(1, invalidTitle);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
```

### Integration Tests

**Using Testcontainers**:
```csharp
public class GameRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private ApplicationDbContext _context;

    public GameRepositoryIntegrationTests()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new ApplicationDbContext(options);
        await _context.Database.MigrateAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGame_ReturnsGame()
    {
        // Arrange
        var game = new Game { Title = "Catan", Publisher = "Kosmos" };
        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();

        var repository = new GameRepository(_context);

        // Act
        var result = await repository.GetByIdAsync(game.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Catan");
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

### API Testing

**Using WebApplicationFactory**:
```csharp
public class GameEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GameEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGames_ReturnsOkWithGames()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var games = await response.Content.ReadFromJsonAsync<List<GameDto>>();
        games.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateGame_ValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreateGameRequest { Title = "Catan", Publisher = "Kosmos" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/games", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }
}
```

### Running Backend Tests

```bash
# All tests
cd apps/api
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~GameRepositoryTests"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

## 🎨 Frontend Testing

### Component Tests

**Location**: `apps/web/tests/` or colocated `__tests__/`

**Example Component Test**:
```typescript
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { GameCard } from '@/components/GameCard'

describe('GameCard', () => {
  it('renders game title', () => {
    render(<GameCard gameId={1} title="Catan" />)

    expect(screen.getByText('Catan')).toBeInTheDocument()
  })

  it('calls onSelect when clicked', async () => {
    const onSelect = jest.fn()
    render(<GameCard gameId={1} title="Catan" onSelect={onSelect} />)

    const button = screen.getByRole('button', { name: /select/i })
    await userEvent.click(button)

    expect(onSelect).toHaveBeenCalledWith(1)
  })

  it('shows loading state', () => {
    render(<GameCard gameId={1} title="Catan" isLoading />)

    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})
```

### Hook Tests

```typescript
import { renderHook, waitFor } from '@testing-library/react'
import { useGames } from '@/hooks/useGames'

describe('useGames', () => {
  it('fetches games on mount', async () => {
    const { result } = renderHook(() => useGames())

    expect(result.current.loading).toBe(true)

    await waitFor(() => {
      expect(result.current.loading).toBe(false)
    })

    expect(result.current.games).toHaveLength(10)
  })

  it('handles errors', async () => {
    // Mock API error
    jest.spyOn(api, 'get').mockRejectedValue(new Error('API Error'))

    const { result } = renderHook(() => useGames())

    await waitFor(() => {
      expect(result.current.error).toBe('API Error')
    })
  })
})
```

### API Mocking with MSW

```typescript
import { rest } from 'msw'
import { setupServer } from 'msw/node'

const server = setupServer(
  rest.get('/api/v1/games', (req, res, ctx) => {
    return res(
      ctx.json([
        { id: 1, title: 'Catan' },
        { id: 2, title: 'Wingspan' }
      ])
    )
  }),

  rest.post('/api/v1/games', (req, res, ctx) => {
    return res(ctx.status(201), ctx.json({ id: 3 }))
  })
)

beforeAll(() => server.listen())
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
```

### Running Frontend Tests

```bash
# All tests
cd apps/web
pnpm test

# Watch mode
pnpm test --watch

# Coverage
pnpm test --coverage

# Specific file
pnpm test GameCard.test.tsx

# Update snapshots
pnpm test -u
```

## 🔗 Integration Testing

### Database Integration

Tests with real PostgreSQL via Testcontainers:

```csharp
[Collection("Database")]
public class GameIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private ApplicationDbContext _context;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new ApplicationDbContext(options);
        await _context.Database.MigrateAsync();
    }

    [Fact]
    public async Task CompleteWorkflow_CreateUpdateDelete_Succeeds()
    {
        // Create
        var game = new Game { Title = "Catan" };
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Update
        game.UpdateTitle("New Catan");
        await _context.SaveChangesAsync();

        // Verify
        var updated = await _context.Games.FindAsync(game.Id);
        updated.Title.Should().Be("New Catan");

        // Delete
        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        var deleted = await _context.Games.FindAsync(game.Id);
        deleted.Should().BeNull();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

### Vector Database Integration

Tests with Qdrant:

```csharp
public class QdrantIntegrationTests : IAsyncLifetime
{
    private readonly IContainer _qdrant;

    public QdrantIntegrationTests()
    {
        _qdrant = new ContainerBuilder()
            .WithImage("qdrant/qdrant:latest")
            .WithPortBinding(6333, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _qdrant.StartAsync();
    }

    [Fact]
    public async Task SearchVectors_ReturnsRelevantResults()
    {
        // Arrange
        var client = new QdrantClient(_qdrant.GetMappedPublicPort(6333));

        // Act
        var results = await client.SearchAsync("game rules");

        // Assert
        results.Should().NotBeEmpty();
    }

    public async Task DisposeAsync()
    {
        await _qdrant.DisposeAsync();
    }
}
```

### PDF Processing Integration

Tests with Unstructured and SmolDocling:

```csharp
public class PdfProcessingIntegrationTests : IAsyncLifetime
{
    private readonly IContainer _unstructured;
    private readonly IContainer _smoldocling;

    public PdfProcessingIntegrationTests()
    {
        _unstructured = new ContainerBuilder()
            .WithImage("downloads.unstructured.io/unstructured-io/unstructured-api:latest")
            .WithPortBinding(8001, true)
            .Build();

        _smoldocling = new ContainerBuilder()
            .WithImage("ghcr.io/morphllm/smoldocling:latest")
            .WithPortBinding(8002, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _unstructured.StartAsync(),
            _smoldocling.StartAsync()
        );
    }

    [Fact]
    public async Task ExtractPdf_HighQuality_ReturnsText()
    {
        // Arrange
        var orchestrator = new EnhancedPdfProcessingOrchestrator(...);
        var pdfBytes = await File.ReadAllBytesAsync("test.pdf");

        // Act
        var result = await orchestrator.ProcessAsync(pdfBytes);

        // Assert
        result.Text.Should().NotBeEmpty();
        result.Quality.Should().BeGreaterThan(0.8);
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _unstructured.DisposeAsync().AsTask(),
            _smoldocling.DisposeAsync().AsTask()
        );
    }
}
```

## 🌐 E2E Testing

### Playwright Setup

**Config**: `apps/web/playwright.config.ts`

```typescript
import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
})
```

### Example E2E Test

```typescript
import { test, expect } from '@playwright/test'

test.describe('Game Search', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/')
  })

  test('user can search for games', async ({ page }) => {
    // Navigate to search
    await page.click('text=Search')

    // Enter search query
    await page.fill('input[name="search"]', 'Catan')
    await page.press('input[name="search"]', 'Enter')

    // Wait for results
    await page.waitForSelector('[data-testid="game-card"]')

    // Verify results
    const results = await page.locator('[data-testid="game-card"]').count()
    expect(results).toBeGreaterThan(0)

    // Check first result
    const firstResult = page.locator('[data-testid="game-card"]').first()
    await expect(firstResult).toContainText('Catan')
  })

  test('user can ask a question', async ({ page }) => {
    // Navigate to chat
    await page.click('text=Chat')

    // Type question
    await page.fill('textarea[name="question"]', 'How does trading work in Catan?')
    await page.click('button[type="submit"]')

    // Wait for response
    await page.waitForSelector('[data-testid="ai-response"]')

    // Verify response
    const response = page.locator('[data-testid="ai-response"]')
    await expect(response).toBeVisible()
    await expect(response).toContainText('trading')
  })

  test('complete user workflow', async ({ page }) => {
    // Register
    await page.click('text=Sign Up')
    await page.fill('input[name="email"]', 'test@example.com')
    await page.fill('input[name="password"]', 'Test123!@#')
    await page.click('button[type="submit"]')

    // Should be logged in
    await expect(page.locator('text=Dashboard')).toBeVisible()

    // Ask question
    await page.click('text=Chat')
    await page.fill('textarea[name="question"]', 'How to play Catan?')
    await page.click('button[type="submit"]')

    // Wait for response
    await page.waitForSelector('[data-testid="ai-response"]')

    // Logout
    await page.click('text=Logout')
    await expect(page.locator('text=Sign In')).toBeVisible()
  })
})
```

### Running E2E Tests

```bash
cd apps/web

# Install browsers (first time)
pnpm exec playwright install

# Run tests
pnpm test:e2e

# Run in UI mode
pnpm exec playwright test --ui

# Run specific test
pnpm exec playwright test game-search.spec.ts

# Debug mode
pnpm exec playwright test --debug
```

## 📈 Coverage Requirements

### Coverage Targets

| Component | Target | Current |
|-----------|--------|---------|
| **Overall** | ≥90% | 90%+ ✅ |
| **Backend** | ≥90% | 90%+ ✅ |
| **Frontend** | ≥90% | 90.03% ✅ |
| **Critical Paths** | 100% | 100% ✅ |

### Critical Paths (Must have 100% coverage)

- Authentication (login, register, 2FA)
- Payment processing (if applicable)
- Data deletion/modification
- Security-critical operations
- RAG pipeline

### Generating Coverage Reports

**Backend**:
```bash
cd apps/api
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
dotnet reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

**Frontend**:
```bash
cd apps/web
pnpm test --coverage
open coverage/lcov-report/index.html
```

### Coverage Enforcement

**CI/CD**: Coverage checked on every PR
**Threshold**: PR fails if coverage drops below 90%
**Exception**: Can be overridden with justification

## 🏃 Running Tests

### Local Development

**Quick Test** (before commit):
```bash
# Backend
cd apps/api && dotnet test --no-build

# Frontend
cd apps/web && pnpm test --bail --findRelatedTests
```

**Full Test Suite**:
```bash
# Backend
cd apps/api && dotnet test

# Frontend
cd apps/web && pnpm test

# E2E
cd apps/web && pnpm test:e2e
```

### Continuous Integration

Tests run automatically on:
- Every push
- Every PR
- Scheduled (nightly)

**CI Workflow**:
1. Lint (ESLint, Prettier)
2. Type check (TypeScript, C#)
3. Unit tests (Jest, xUnit)
4. Integration tests (Testcontainers)
5. E2E tests (Playwright)
6. Coverage check
7. Security scan

### Test Parallelization

**Backend**: xUnit runs tests in parallel by default
**Frontend**: Jest runs with `--maxWorkers=50%`
**E2E**: Playwright runs with 1 worker in CI, unlimited locally

## ✍️ Writing Tests

### Test Naming Conventions

**Pattern**: `MethodName_Scenario_ExpectedBehavior`

**Examples**:
```csharp
// Good ✅
GetGameById_ValidId_ReturnsGame()
GetGameById_InvalidId_ThrowsNotFoundException()
CreateGame_DuplicateTitle_ThrowsDomainException()

// Bad ❌
TestGame()
Test1()
GameTest()
```

### AAA Pattern

All tests should follow **Arrange-Act-Assert**:

```csharp
[Fact]
public async Task CreateGame_ValidData_ReturnsGameId()
{
    // Arrange - Setup test data and mocks
    var command = new CreateGameCommand("Catan", "Kosmos");
    var repository = new Mock<IGameRepository>();

    // Act - Execute the code under test
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert - Verify the outcome
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeGreaterThan(0);
}
```

### Test Data Builders

Use builders for complex test data:

```csharp
public class GameBuilder
{
    private string _title = "Default Game";
    private string _publisher = "Default Publisher";

    public GameBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public GameBuilder WithPublisher(string publisher)
    {
        _publisher = publisher;
        return this;
    }

    public Game Build()
    {
        return new Game { Title = _title, Publisher = _publisher };
    }
}

// Usage
var game = new GameBuilder()
    .WithTitle("Catan")
    .WithPublisher("Kosmos")
    .Build();
```

### Parameterized Tests

Use `[Theory]` for testing multiple inputs:

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(null)]
public async Task CreateGame_InvalidTitle_ThrowsException(string invalidTitle)
{
    var command = new CreateGameCommand(invalidTitle, "Publisher");
    await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
}
```

## 🗄️ Test Data Management

### Test Database

**Strategy**: Fresh database per test class (Testcontainers)

**Benefits**:
- Isolation between tests
- No shared state
- Realistic testing

**Example**:
```csharp
public class GameRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres;
    private ApplicationDbContext _context;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();

        _context = CreateContext();
        await _context.Database.MigrateAsync();
        await SeedData();
    }

    private async Task SeedData()
    {
        _context.Games.AddRange(
            new Game { Title = "Catan" },
            new Game { Title = "Wingspan" }
        );
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
```

### Test Fixtures

Share expensive setup across tests:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; private set; }
    public string ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        Postgres = new PostgreSqlBuilder().Build();
        await Postgres.StartAsync();
        ConnectionString = Postgres.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
    }
}

[Collection("Database")]
public class GameTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public GameTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
}
```

## 🤖 CI/CD Testing

### GitHub Actions

**Workflow**: `.github/workflows/ci.yml`

```yaml
name: CI

on: [push, pull_request]

jobs:
  test-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'
      - run: dotnet restore
      - run: dotnet build
      - run: dotnet test --no-build /p:CollectCoverage=true

  test-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v3
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'pnpm'
      - run: pnpm install
      - run: pnpm test --coverage
      - run: pnpm test:e2e
```

### Test Reports

**Backend**: xUnit XML reports
**Frontend**: Jest JSON reports
**E2E**: Playwright HTML reports

**Artifacts**: Uploaded to GitHub Actions artifacts

## ⚡ Performance Testing

### Load Testing

Use k6 for load testing:

```javascript
import http from 'k6/http'
import { check } from 'k6'

export const options = {
  vus: 100,        // 100 virtual users
  duration: '30s', // 30 seconds
}

export default function () {
  const res = http.get('http://localhost:8080/api/v1/games')

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  })
}
```

**Run**:
```bash
k6 run load-test.js
```

### Performance Benchmarks

**Target SLAs**:
- P50 response time: <500ms
- P95 response time: <2s
- P99 response time: <5s
- Throughput: >100 req/s

## 🔒 Security Testing

### SAST (Static Analysis)

**Tools**:
- CodeQL (GitHub)
- SonarQube
- .NET analyzers
- ESLint security plugins

**Run Locally**:
```bash
# .NET
dotnet list package --vulnerable

# npm
pnpm audit --audit-level=high
```

### DAST (Dynamic Analysis)

**Tools**:
- OWASP ZAP
- Burp Suite

**Automated**: OWASP ZAP in CI pipeline

### Dependency Scanning

**Dependabot**: Enabled, weekly scans
**Action**: Auto-PRs for security updates

## 🐛 Troubleshooting

### Common Issues

**"Tests fail locally but pass in CI"**:
- Check Docker is running
- Verify environment variables
- Check file paths (case-sensitivity)
- Clear caches: `bash tools/cleanup-caches.sh`

**"Testcontainers timeout"**:
- Increase timeout in test configuration
- Check Docker resources (memory, CPU)
- Ensure Docker daemon is running

**"Coverage decreased"**:
- Run coverage locally: `dotnet test /p:CollectCoverage=true`
- Identify uncovered code
- Add missing tests

**"E2E tests flaky"**:
- Add explicit waits: `await page.waitForSelector()`
- Increase timeouts
- Use `data-testid` attributes
- Avoid timing-dependent assertions

### Debug Tests

**Backend (VS Code)**:
```json
{
  "type": "coreclr",
  "request": "launch",
  "name": "Debug Tests",
  "program": "dotnet",
  "args": ["test", "--filter", "FullyQualifiedName~GameTests"],
  "cwd": "${workspaceFolder}/apps/api"
}
```

**Frontend**:
```bash
# Debug single test
pnpm test --watch GameCard.test.tsx

# Debug with Node inspector
node --inspect-brk node_modules/.bin/jest --runInBand
```

**E2E**:
```bash
# Debug mode
pnpm exec playwright test --debug

# Headed mode
pnpm exec playwright test --headed
```

## 📚 Additional Resources

- **[Testing Guide (Main Docs)](../docs/02-development/testing/testing-guide.md)** - Detailed testing documentation
- **[Developer Guide](./02-developer-guide.md)** - Development workflow
- **[Contributing Guide](./07-contributing-guide.md)** - How to contribute
- **xUnit**: https://xunit.net/
- **Jest**: https://jestjs.io/
- **Playwright**: https://playwright.dev/
- **Testcontainers**: https://dotnet.testcontainers.org/

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: QA Engineers & Testers

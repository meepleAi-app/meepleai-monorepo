# Testing Guide

This document describes how to run tests and generate coverage reports for the MeepleAI monorepo.

## Quick Start

### Run All Tests with Coverage

```powershell
# Run both frontend and backend tests with coverage
.\scripts\test-coverage.ps1

# Run only backend tests
.\scripts\test-coverage.ps1 -BackendOnly

# Run only frontend tests
.\scripts\test-coverage.ps1 -FrontendOnly

# Run tests and automatically open coverage reports
.\scripts\test-coverage.ps1 -OpenReport
```

## Frontend Testing (apps/web)

### Technology Stack
- **Test Runner**: Jest 30.x
- **Testing Library**: @testing-library/react 16.x
- **Coverage**: Built-in Jest coverage

### Commands

```bash
cd apps/web

# Run tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with coverage
npm run test:coverage

# Run linter
npm run lint

# Fix linting issues
npm run lint:fix

# Type check
npm run typecheck
```

### Coverage Thresholds

The frontend has a **90% coverage threshold** configured in `jest.config.js`:
- **Branches**: 90%
- **Functions**: 90%
- **Lines**: 90%
- **Statements**: 90%

Tests will fail if coverage falls below these thresholds.

### Coverage Report Location

After running `npm run test:coverage`, view the HTML report at:
```
apps/web/coverage/lcov-report/index.html
```

## Backend Testing (apps/api)

### Technology Stack
- **Test Framework**: xUnit 2.6.x
- **Mocking**: Moq 4.20.x
- **Coverage**: Coverlet 6.0.x
- **Reporting**: ReportGenerator 5.4.x

### Commands

```bash
cd apps/api

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Generate HTML coverage report
reportgenerator `
  -reports:"TestResults/**/coverage.cobertura.xml" `
  -targetdir:"TestResults/CoverageReport" `
  -reporttypes:"Html;TextSummary"
```

### Test Organization

```
apps/api/tests/Api.Tests/
├── AuthServiceTests.cs          # Authentication service tests
├── RateLimitServiceTests.cs     # Rate limiting tests
├── QaEndpointTests.cs           # Q&A endpoint integration tests
├── TenantIsolationTests.cs      # Multi-tenancy tests
├── EmbeddingServiceTests.cs     # Embedding generation tests
├── TextChunkingServiceTests.cs  # Text chunking tests
├── QdrantServiceTests.cs        # Vector DB tests
├── RuleSpecServiceTests.cs      # Rule specification tests
├── AuditServiceTests.cs         # Audit logging tests
└── PdfTextExtractionServiceTests.cs  # PDF processing tests
```

### Coverage Report Location

After running the coverage commands, view the HTML report at:
```
apps/api/TestResults/CoverageReport/index.html
```

## End-to-End Testing

### Technology Stack
- **Framework**: Playwright 1.55.1
- **Status**: ✅ Implemented

### Commands

```bash
cd apps/web

# Run E2E tests
npm run test:e2e

# Run in UI mode (interactive)
npm run test:e2e:ui

# View HTML report
npm run test:e2e:report
```

### Test Coverage

E2E tests are located in `apps/web/e2e/`:
- `home.spec.ts` - Home page functionality
- `chat.spec.ts` - Chat page authentication

### Configuration

Playwright is configured in `playwright.config.ts`:
- Runs tests against `http://localhost:3000`
- Automatically starts dev server if not running
- Generates HTML reports in `playwright-report/`

## Linting

### Frontend Linting

The frontend uses ESLint with Next.js configuration plus custom rules:

```bash
cd apps/web

# Run linter
npm run lint

# Fix auto-fixable issues
npm run lint:fix
```

**Configured Rules**:
- `@typescript-eslint/no-explicit-any`: Warn on explicit `any` types
- `@typescript-eslint/no-unused-vars`: Warn on unused variables
- `no-console`: Warn on console.log (allow warn/error)
- `prefer-const`: Warn when `let` could be `const`
- `no-var`: Error on `var` usage
- `eqeqeq`: Require strict equality (`===`)
- `curly`: Require curly braces for all control structures

### Backend Linting

The backend uses built-in .NET code analyzers:

```bash
cd apps/api

# Format code
dotnet format
```

## Coverage Goals

The project aims for **90% code coverage** across both frontend and backend:

- ✅ Unit tests for core business logic
- ✅ Integration tests for API endpoints
- ⏳ E2E tests for critical user flows (planned)

### Current Coverage Status

Run the coverage scripts to see current metrics:

```powershell
.\scripts\test-coverage.ps1
```

## Continuous Integration

Tests are automatically run on:
- Pull request creation
- Commits to main branch
- Manual workflow dispatch

CI configuration can be found in `.github/workflows/` (if configured).

## Writing Tests

### Frontend Test Example

```typescript
// apps/web/src/lib/api.test.ts
import { api } from './api';

describe('api client', () => {
  beforeEach(() => {
    global.fetch = jest.fn();
  });

  it('should handle GET requests', async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      json: async () => ({ data: 'test' }),
    });

    const result = await api.get('/test');
    expect(result).toEqual({ data: 'test' });
  });
});
```

### Backend Test Example

```csharp
// apps/api/tests/Api.Tests/RuleSpecServiceTests.cs
using Xunit;

public class RuleSpecServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RuleSpecService _service;

    public RuleSpecServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        _service = new RuleSpecService(_dbContext);
    }

    [Fact]
    public async Task GetRuleSpecAsync_ReturnsNull_WhenNotExists()
    {
        var result = await _service.GetRuleSpecAsync("game");
        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
    }
}
```

## Troubleshooting

### Frontend Tests Failing

1. **"Cannot find module" errors**: Run `npm install` in `apps/web`
2. **Jest configuration issues**: Check `jest.config.js` and `jest.setup.js`
3. **Coverage threshold not met**: Write more tests or adjust thresholds

### Backend Tests Failing

1. **Database connection issues**: Tests use in-memory SQLite, ensure the package is installed
2. **Dependency injection errors**: Check service constructors match test setup
3. **Coverage report empty**: Ensure coverlet collector is installed in the test project

### reportgenerator Not Found

Install globally:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Best Practices

1. **Write tests first** - Follow TDD when implementing new features
2. **Test behavior, not implementation** - Focus on what code does, not how
3. **Keep tests isolated** - Use mocks/stubs for external dependencies
4. **Use descriptive names** - Test names should describe the scenario
5. **Maintain coverage** - Don't let coverage drop below 90%
6. **Run tests before committing** - Catch issues early

## Resources

- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Testing Library](https://testing-library.com/docs/react-testing-library/intro/)
- [xUnit Documentation](https://xunit.net/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)

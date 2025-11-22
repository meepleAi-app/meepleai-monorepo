# Contributing to MeepleAI

Thank you for your interest in contributing to MeepleAI! This document provides guidelines and best practices for contributing to this project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Guidelines](#issue-guidelines)
- [Documentation](#documentation)
- [Getting Help](#getting-help)

## Code of Conduct

We are committed to providing a welcoming and inclusive environment. Please:

- Be respectful and considerate in all interactions
- Provide constructive feedback
- Focus on what is best for the community
- Show empathy towards other community members

## Getting Started

### Development Setup

#### Prerequisites

Ensure you have the following installed:

**Backend (.NET API)**:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

**Frontend (Next.js)**:
- [Node.js 20+](https://nodejs.org/)
- [pnpm 9](https://pnpm.io/installation)

**Optional Tools**:
- [pre-commit](https://pre-commit.com/) for git hooks
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) for scripts

### Initial Setup

1. **Fork and Clone**:
   ```bash
   gh repo fork DegrassiAaron/meepleai-monorepo --clone
   cd meepleai-monorepo
   ```

2. **Install Dependencies**:
   ```bash
   # Backend
   cd apps/api
   dotnet restore

   # Frontend
   cd ../web
   pnpm install
   ```

3. **Setup Environment Files**:
   ```bash
   # Copy template files
   cd ../../infra/env
   cp api.env.dev.example api.env.dev
   cp web.env.dev.example web.env.dev

   # Edit with your API keys (see SECURITY.md)
   ```

4. **Install Git Hooks** (recommended):
   ```bash
   pre-commit install
   ```

5. **Start Infrastructure**:
   ```bash
   cd ../../infra
   docker compose up -d postgres qdrant redis seq
   ```

6. **Run Database Migrations**:
   ```bash
   cd ../apps/api/src/Api
   dotnet ef database update
   ```

7. **Verify Setup**:
   ```bash
   # Backend tests
   cd ../../
   dotnet test

   # Frontend tests
   cd ../web
   pnpm test
   ```

See [CLAUDE.md](./CLAUDE.md) for detailed architecture and commands reference.

## Development Workflow

### Branching Strategy

- `main` - Production-ready code
- `feature/<issue-id>-<description>` - New features
- `fix/<issue-id>-<description>` - Bug fixes
- `docs/<issue-id>-<description>` - Documentation updates

Example: `feature/AI-06-rag-evaluation`

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types**: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `ci`, `chore`

**Examples**:
```bash
feat(ai): implement RAG offline evaluation system
fix(auth): resolve session timeout inconsistency
docs(api): update API versioning documentation
test(pdf): increase PDF processing coverage to 90%
```

### Development Process

1. **Pick an Issue**:
   - Browse [open issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
   - Comment on the issue to claim it
   - Wait for maintainer confirmation

2. **Create a Branch**:
   ```bash
   git checkout -b feature/<issue-id>-<description>
   ```

3. **Develop with BDD Approach** (see [Testing Guidelines](#testing-guidelines)):
   - Define behavior scenarios (Given-When-Then)
   - Write tests first (red)
   - Implement minimal code to pass (green)
   - Refactor while keeping tests green

4. **Commit Regularly**:
   ```bash
   git add .
   git commit -m "feat(scope): description"
   ```

5. **Keep Branch Updated**:
   ```bash
   git fetch origin main
   git rebase origin/main
   ```

6. **Push and Create PR**:
   ```bash
   git push -u origin feature/<issue-id>-<description>
   gh pr create
   ```

## Coding Standards

### C# (.NET API)

**Conventions**:
- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Prefer async/await for I/O operations
- Use dependency injection (configured in `Program.cs`)
- Use structured logging with `ILogger<T>` and Serilog

**File Organization**:
```
apps/api/
  src/Api/
    Services/           # Business logic
    Infrastructure/     # Database, entities
    Models/            # DTOs, request/response models
    Middleware/        # Custom middleware
    Migrations/        # EF Core migrations
    Program.cs         # Entry point, DI configuration
  tests/Api.Tests/
    Services/          # Service unit tests
    Endpoints/         # Integration tests
    TestData/          # Test fixtures
```

**Example Service**:
```csharp
public class ExampleService
{
    private readonly ILogger<ExampleService> _logger;
    private readonly MeepleAiDbContext _dbContext;

    public ExampleService(
        ILogger<ExampleService> logger,
        MeepleAiDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<Result> DoSomethingAsync(Request request)
    {
        _logger.LogInformation("Processing request with ID {RequestId}", request.Id);

        try
        {
            // Implementation
            var result = await _dbContext.Entities
                .Where(e => e.Id == request.Id)
                .FirstOrDefaultAsync();

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process request {RequestId}", request.Id);
            return Result.Failure("Processing failed");
        }
    }
}
```

### TypeScript/React (Next.js Frontend)

**Conventions**:
- Use TypeScript strict mode
- Follow [Airbnb React/JSX Style Guide](https://airbnb.io/javascript/react/)
- Prefer functional components with hooks
- Use meaningful variable and function names
- Avoid `any` type - use proper types or `unknown`

**File Organization**:
```
apps/web/
  src/
    pages/            # Next.js routes
    lib/              # Utilities, API client
    components/       # Reusable components (if needed)
    __tests__/        # Jest tests
  e2e/                # Playwright E2E tests
```

**Example API Client Usage**:
```typescript
import { get, post } from '@/lib/api';

export async function fetchGames() {
  return get<Game[]>('/api/v1/games');
}

export async function createGame(data: CreateGameRequest) {
  return post<Game>('/api/v1/games', data);
}
```

**React Component Example**:
```typescript
import { useState, useEffect } from 'react';
import { fetchGames } from '@/lib/api';

interface Game {
  id: string;
  title: string;
  description: string;
}

export default function GamesPage() {
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadGames = async () => {
      try {
        const data = await fetchGames();
        setGames(data);
      } catch (err) {
        setError('Failed to load games');
      } finally {
        setLoading(false);
      }
    };

    loadGames();
  }, []);

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;

  return (
    <div>
      <h1>Games</h1>
      <ul>
        {games.map(game => (
          <li key={game.id}>{game.title}</li>
        ))}
      </ul>
    </div>
  );
}
```

## Testing Guidelines

We follow **Behavior-Driven Development (BDD)** principles with comprehensive test coverage.

### BDD Principles

1. **Write Scenarios First**: Define behavior in Given-When-Then format
2. **Tests as Specification**: Tests document expected behavior
3. **Outside-In**: Start with acceptance tests, add unit tests for details
4. **Living Documentation**: Tests serve as up-to-date documentation
5. **Refactoring Safety**: Tests enable confident refactoring

### Test Naming Convention (BDD Style)

**Format**: `MethodName_WhenCondition_ThenExpectedBehavior`

**Examples**:
```csharp
// C# - xUnit
[Fact]
public async Task SearchAsync_WhenQueryMatchesDocuments_ReturnsRankedResults()
{
    // Arrange
    var query = "how to win chess";
    var expectedDocCount = 3;

    // Act
    var results = await _ragService.SearchAsync(query, topK: 5);

    // Assert
    Assert.NotNull(results);
    Assert.True(results.Documents.Count >= expectedDocCount);
    Assert.True(results.Documents[0].Score > 0.7);
}

[Fact]
public async Task GenerateApiKeyAsync_WhenUserNotFound_ThrowsNotFoundException()
{
    // Arrange
    var nonExistentUserId = Guid.NewGuid();

    // Act & Assert
    await Assert.ThrowsAsync<NotFoundException>(
        () => _apiKeyService.GenerateApiKeyAsync(nonExistentUserId, "test-key")
    );
}
```

```typescript
// TypeScript - Jest
describe('fetchGames', () => {
  it('should return games list when API call succeeds', async () => {
    // Arrange
    const mockGames = [
      { id: '1', title: 'Chess', description: 'Classic game' },
    ];
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => mockGames,
    });

    // Act
    const result = await fetchGames();

    // Assert
    expect(result).toEqual(mockGames);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/games'),
      expect.any(Object)
    );
  });

  it('should throw error when API call fails with 500', async () => {
    // Arrange
    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 500,
    });

    // Act & Assert
    await expect(fetchGames()).rejects.toThrow('HTTP error! status: 500');
  });
});
```

### Coverage Requirements

- **Backend**: 80%+ coverage (enforced by CI)
- **Frontend**: 90%+ coverage (enforced by Jest config)
- **Critical paths**: 100% coverage (auth, payment, data integrity)

### Running Tests

**Backend**:
```bash
cd apps/api

# All tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true

# Specific test
dotnet test --filter "FullyQualifiedName~RagServiceTests"

# Quick coverage check
pwsh ../../tools/measure-coverage.ps1 -Project api
```

**Frontend**:
```bash
cd apps/web

# All tests
pnpm test

# Watch mode
pnpm test:watch

# Coverage
pnpm test:coverage

# E2E tests
pnpm test:e2e

# E2E with UI
pnpm test:e2e:ui
```

### Test Organization

**Backend (xUnit)**:
- `*Tests.cs` - Unit tests (in-memory, mocked dependencies)
- `*IntegrationTests.cs` - Integration tests (Testcontainers)
- Use `Arrange-Act-Assert` pattern
- One assertion per test when possible

**Frontend (Jest + Playwright)**:
- `__tests__/*.test.tsx` - Component unit tests
- `e2e/*.spec.ts` - End-to-end tests
- Use `describe/it` blocks for grouping
- Mock external dependencies

## Pull Request Process

### Before Creating a PR

**Checklist**:
- [ ] All tests pass locally
- [ ] Code follows project style guidelines
- [ ] New tests added for new functionality (BDD naming)
- [ ] Coverage thresholds met (80% backend, 90% frontend)
- [ ] Documentation updated (if applicable)
- [ ] No console warnings or errors
- [ ] Commits follow conventional commit format
- [ ] Branch is up-to-date with `main`

### Creating a PR

1. **Push Your Branch**:
   ```bash
   git push -u origin feature/<issue-id>-<description>
   ```

2. **Create PR via GitHub CLI**:
   ```bash
   gh pr create --fill
   ```

   Or use the [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md) on GitHub UI

3. **Fill Out Template Completely**:
   - Summary of changes
   - Link to related issue (`Closes #123`)
   - Type of change
   - Testing performed
   - Screenshots (if UI changes)

4. **Request Review**:
   - PRs require at least 1 approval
   - Address review comments promptly
   - Use "Resolve conversation" when addressed

### Code Review

**For Authors**:
- Respond to feedback constructively
- Make requested changes in new commits
- Don't force-push during review (breaks review flow)
- Update PR description if scope changes

**For Reviewers**:
- Be respectful and constructive
- Focus on code quality, not personal style
- Approve when satisfied (even if minor suggestions remain)
- Use "Request changes" only for blocking issues

### CI/CD Requirements

All PRs must pass CI checks before merge:

1. **Backend CI** (`.github/workflows/ci.yml`):
   - ✅ Build succeeds
   - ✅ All tests pass
   - ✅ Coverage thresholds met

2. **Frontend CI**:
   - ✅ Linting passes
   - ✅ Type checking passes
   - ✅ Tests pass
   - ✅ Coverage thresholds met

3. **Security Scan** (`.github/workflows/security-scan.yml`):
   - ✅ CodeQL analysis passes
   - ✅ No HIGH/CRITICAL vulnerabilities
   - ✅ .NET analyzers pass

### Merge Process

- PRs are merged using **Squash and Merge** strategy
- Ensure final commit message follows conventional commits
- Delete branch after merge

## Issue Guidelines

### Creating Issues

Use the appropriate issue template:

- **[Bug Report](.github/ISSUE_TEMPLATE/bug_report.yml)**: Report unexpected behavior
- **[Feature Request](.github/ISSUE_TEMPLATE/feature_request.yml)**: Suggest new functionality
- **[Technical Task](.github/ISSUE_TEMPLATE/technical_task.yml)**: Technical improvements
- **[User Story](.github/ISSUE_TEMPLATE/user_story.yml)**: User-facing features

### Issue Best Practices

**Good Issue Characteristics**:
- Clear, descriptive title
- Detailed description with context
- **BDD scenarios** (Given-When-Then) when applicable
- Steps to reproduce (for bugs)
- Acceptance criteria
- Labels for categorization
- Linked to relevant Epic or parent issue

**Example Bug Report**:
```markdown
## Description
User sessions are not timing out after 30 days of inactivity

## Steps to Reproduce
1. Log in as a user
2. Wait 31 days without any activity
3. Return to the site
4. Session is still active (should be expired)

## BDD Scenario
Given a user logged in 31 days ago
And the user has not performed any activity
When the user attempts to access a protected page
Then the session should be expired
And the user should be redirected to login

## Expected Behavior
Session should expire after 30 days

## Actual Behavior
Session remains active indefinitely

## Environment
- OS: Windows 11
- Browser: Chrome 120
- Version: v1.2.3
```

**Example Feature Request**:
```markdown
## User Story
As a game rules editor
I want to export rule specifications to PDF
So that I can share them with players offline

## Problem/Opportunity
Users need offline access to rule specs for conventions and print

## BDD Scenarios
Scenario: Export rule spec to PDF
  Given I am viewing a published rule specification
  When I click the "Export to PDF" button
  Then a PDF download should start
  And the PDF should contain all rule sections
  And the PDF should be formatted for printing

Scenario: Export fails for unpublished spec
  Given I am viewing a draft rule specification
  When I click the "Export to PDF" button
  Then I should see an error message
  And the message should explain only published specs can be exported

## Acceptance Criteria
- [ ] Export button visible on published specs
- [ ] PDF contains all rule sections
- [ ] PDF is formatted for A4 printing
- [ ] Draft specs cannot be exported
- [ ] Export tracked in audit log
```

## Documentation

### Updating Documentation

When making changes, update relevant documentation:

- **[CLAUDE.md](./CLAUDE.md)**: Codebase structure, architecture, commands
- **[README.md](./README.md)**: High-level project overview
- **[docs/](./docs/)**: Detailed guides and technical documentation
- **[schemas/](./schemas/)**: JSON schema documentation
- **Code comments**: Complex logic, non-obvious decisions

### Documentation Structure

```
docs/
  guide/           # User guides and how-tos
  technic/         # Technical design documents
  issue/           # Issue resolution documentation
  *.md             # General documentation (security, coverage, etc.)
```

### Documentation Best Practices

- Write clear, concise documentation
- Include examples and code snippets
- Keep documentation up-to-date with code changes
- Use diagrams for complex architectures
- Link between related documents

## Getting Help

### Resources

- **Codebase Reference**: See [CLAUDE.md](./CLAUDE.md)
- **Security**: See [SECURITY.md](./SECURITY.md)
- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Pull Requests**: [GitHub PRs](https://github.com/DegrassiAaron/meepleai-monorepo/pulls)

### Communication

- **Questions**: Open a [Discussion](https://github.com/DegrassiAaron/meepleai-monorepo/discussions) or comment on relevant issue
- **Bugs**: Use [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.yml)
- **Ideas**: Use [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml)
- **Security Issues**: See [Security Policy](./SECURITY.md#reporting-a-vulnerability)

### Need Support?

If you're stuck or have questions:

1. Check existing [documentation](./docs/)
2. Search [closed issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is%3Aissue+is%3Aclosed)
3. Open a new issue with the question
4. Be patient - maintainers respond as time permits

---

## Thank You!

Your contributions make MeepleAI better for everyone. We appreciate your time and effort! 🎲🤖

**Happy Contributing!**

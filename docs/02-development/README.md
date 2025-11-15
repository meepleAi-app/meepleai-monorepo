# Development Documentation

**Developer Guides, Testing, Refactoring, and Implementation Notes** - Everything developers need to build and maintain MeepleAI.

---

## 📁 Directory Structure

```
02-development/
├── guides/                        # Technical implementation guides
│   ├── ai-agents-guide.md
│   ├── ai-provider-integration.md
│   ├── codebase-maintenance.md
│   ├── dependency-management.md
│   ├── llm-integration-guide.md
│   ├── migration-management.md
│   └── pdf-processing-guide.md   ⭐
├── refactoring/                   # DDD migration documentation
│   ├── legacy-code-inventory.md  # Complete inventory (150+ pages)
│   ├── legacy-code-dashboard.md  # Progress tracking ⭐
│   ├── implementation-notes.md   # Migration notes
│   └── next-steps.md             # Remaining tasks
├── implementation/                # Implementation findings
│   ├── bgai-023-ragservice-migration.md
│   └── bgai-026-cost-tracking.md
├── testing/                       # Testing documentation (21 files)
│   ├── testing-guide.md          # Comprehensive test writing guide (60+ pages) ⭐
│   ├── testing-strategy.md       # Test pyramid, quality gates (30 pages) ⭐
│   ├── testing-quick-reference.md
│   ├── testing-react-19-patterns.md
│   ├── testing-specialized.md
│   ├── manual-testing-guide.md   # 🇮🇹 Italian manual QA guide
│   └── ...15 more testing docs
└── README.md                      # This file
```

---

## 🚀 Quick Start for Developers

**New developer onboarding** - Read in this order:

1. **[Quick Start](../00-getting-started/quick-start.md)** - Get MeepleAI running locally in 15 minutes
2. **[Testing Guide](./testing/testing-guide.md)** - Learn how to write tests (90%+ coverage required)
3. **[DDD Quick Reference](../01-architecture/ddd/quick-reference.md)** - Understand DDD patterns used in codebase
4. **[Legacy Code Dashboard](./refactoring/legacy-code-dashboard.md)** - Current DDD migration status
5. **[PDF Processing Guide](./guides/pdf-processing-guide.md)** - Understand PDF pipeline (if working on PDF features)
6. **[LLM Integration Guide](./guides/llm-integration-guide.md)** - Understand LLM integration (if working on RAG/chat)

---

## 📚 Documentation by Category

### Technical Guides

**Practical implementation guides for common development tasks.**

| Guide | Description | Priority | Pages |
|-------|-------------|----------|-------|
| [PDF Processing Guide](./guides/pdf-processing-guide.md) | 3-stage PDF pipeline implementation (Unstructured, SmolDocling, Docnet) | ⭐ Essential (if PDF work) | 20+ |
| [LLM Integration Guide](./guides/llm-integration-guide.md) | Integrate OpenRouter, OpenAI, Claude for RAG/chat | ⭐ Essential (if LLM work) | 15+ |
| [AI Provider Integration](./guides/ai-provider-integration.md) | Add new AI providers (OpenRouter, OpenAI, etc.) | Recommended | 10 |
| [Dependency Management](./guides/dependency-management.md) | Managing NuGet/npm dependencies, versioning | Recommended | 8 |
| [Migration Management](./guides/migration-management.md) | EF Core migrations best practices | Recommended | 8 |
| [Codebase Maintenance](./guides/codebase-maintenance.md) | Code quality, refactoring, cleanup | Optional | 10 |
| [AI Agents Guide](./guides/ai-agents-guide.md) | AI agent framework (Agent Lightning) | Optional | 12 |

**When to use each guide**:

- **PDF Processing**: Working on document upload, extraction, or quality validation
- **LLM Integration**: Adding RAG features, chat improvements, or new LLM providers
- **AI Provider Integration**: Switching LLM providers (e.g., OpenRouter → Azure OpenAI)
- **Dependency Management**: Updating packages, resolving version conflicts
- **Migration Management**: Creating/applying database migrations
- **Codebase Maintenance**: Refactoring, removing technical debt
- **AI Agents**: Building autonomous AI agents (experimental)

---

### Testing Documentation

**Comprehensive testing guides - 90%+ coverage enforced across all code.**

| Document | Description | Priority | Pages |
|----------|-------------|----------|-------|
| [Testing Guide](./testing/testing-guide.md) | Complete test writing guide (unit, integration, E2E) | ⭐ Essential | 60+ |
| [Testing Strategy](./testing/testing-strategy.md) | Test pyramid, quality gates, coverage requirements | ⭐ Essential | 30 |
| [Testing Quick Reference](./testing/testing-quick-reference.md) | Quick lookup for test patterns (AAA, mocking, etc.) | Recommended | 5 |
| [Testing React 19 Patterns](./testing/testing-react-19-patterns.md) | React 19 specific testing patterns | Recommended | 10 |
| [Testing Specialized](./testing/testing-specialized.md) | Manual, accessibility, concurrency, API testing | Recommended | 15 |
| [Manual Testing Guide](./testing/manual-testing-guide.md) | 🇮🇹 Italian manual QA procedures | Optional | 20 |

**Additional Testing Docs** (15 more files in [testing/](./testing/)):
- E2E contribution guides
- Performance testing
- Accessibility testing
- API testing
- Concurrency testing
- Test patterns

**Testing Quick Reference**:

```csharp
// Backend (xUnit + Moq)
[Fact]
public async Task LoginCommand_ValidCredentials_ReturnsSuccess()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Email = "test@example.com" });
    var handler = new LoginCommandHandler(mockRepo.Object);

    // Act
    var result = await handler.Handle(new LoginCommand("test@example.com", "password123"), CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
}
```

```typescript
// Frontend (Jest + RTL)
import { render, screen, userEvent } from '@/test-utils';
import { LoginForm } from '../LoginForm';

test('submits login form', async () => {
  const onSubmit = jest.fn();
  render(<LoginForm onSubmit={onSubmit} />);

  await userEvent.type(screen.getByLabelText(/email/i), 'user@example.com');
  await userEvent.type(screen.getByLabelText(/password/i), 'password123');
  await userEvent.click(screen.getByRole('button', { name: /login/i }));

  expect(onSubmit).toHaveBeenCalledWith({
    email: 'user@example.com',
    password: 'password123'
  });
});
```

**Coverage Requirements**:
- **Overall**: 90%+ enforced
- **Domain Logic**: 95%+ (pure business logic, no excuses)
- **Application Handlers**: 90%+
- **Infrastructure**: 80%+
- **Frontend Components**: 90%+

---

### Refactoring (DDD Migration)

**DDD migration documentation - 99% complete as of 2025-11-15.**

| Document | Description | Priority | Pages |
|----------|-------------|----------|-------|
| [Legacy Code Dashboard](./refactoring/legacy-code-dashboard.md) | Visual progress tracking dashboard | ⭐ Essential | 10 |
| [Legacy Code Inventory](./refactoring/legacy-code-inventory.md) | Complete inventory of legacy services | Recommended | 150+ |
| [Implementation Notes](./refactoring/implementation-notes.md) | Migration notes and lessons learned | Recommended | 15 |
| [Next Steps](./refactoring/next-steps.md) | Remaining refactoring tasks (1%) | Optional | 5 |

**DDD Migration Status** (2025-11-15):

✅ **99% Complete**

**Achievements**:
- ✅ 7/7 bounded contexts migrated (6 at 100%, 1 at 95%)
- ✅ 72+ CQRS handlers operational
- ✅ 60+ endpoints migrated to MediatR
- ✅ 2,070 lines legacy code removed
  - GameService: 181 lines
  - AuthService: 346 lines
  - PDF services: 1,300 lines
  - UserManagementService: 243 lines
- ✅ 99.1% test pass rate maintained
- ✅ Zero build errors

**Retained Services** (orchestration/infrastructure):
- ConfigurationService
- AdminStatsService
- AlertingService
- RagService (RAG orchestration)

**Pattern Reused for Migration**:
1. Implement CQRS handlers (Commands/Queries)
2. Migrate HTTP endpoints to use `IMediator.Send()`
3. Run tests (ensure 90%+ coverage)
4. Remove legacy service
5. Commit and push

**Example Migration**:

Before (Legacy):
```csharp
// Routing/AuthRoutes.cs
app.MapPost("/api/v1/auth/login", async (LoginRequest req, AuthService authService) =>
{
    var result = await authService.LoginAsync(req.Email, req.Password);
    return Results.Ok(result);
});
```

After (DDD + CQRS):
```csharp
// Routing/AuthenticationRoutes.cs
app.MapPost("/api/v1/auth/login", async (LoginRequest req, IMediator mediator) =>
{
    var command = new LoginCommand(req.Email, req.Password);
    var result = await mediator.Send(command);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
});

// BoundedContexts/Authentication/Application/Handlers/LoginCommandHandler.cs
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        // Domain logic here
    }
}
```

---

### Implementation Findings

**Specific implementation investigations and findings.**

| Document | Description |
|----------|-------------|
| [RAG Service Migration](./implementation/bgai-023-ragservice-migration.md) | RAG service migration findings (BGAI-023) |
| [Cost Tracking Verification](./implementation/bgai-026-cost-tracking.md) | LLM cost tracking verification (BGAI-026) |

---

## 🎯 Common Development Tasks

### Adding a New Feature (DDD Pattern)

**Example**: Add "Delete Game Session" feature

**Step 1: Domain Logic**
```csharp
// BoundedContexts/GameManagement/Domain/Aggregates/GameSession.cs
public class GameSession : AggregateRoot
{
    public void Delete(Guid userId)
    {
        // Business rules validation
        if (IsActive)
            throw new InvalidOperationException("Cannot delete active session");

        if (CreatedBy != userId)
            throw new UnauthorizedAccessException("Not authorized to delete");

        Status = SessionStatus.Deleted;
        DeletedAt = DateTime.UtcNow;

        // Raise domain event
        AddDomainEvent(new GameSessionDeletedEvent(Id));
    }
}
```

**Step 2: Application Command**
```csharp
// BoundedContexts/GameManagement/Application/Commands/DeleteGameSessionCommand.cs
public record DeleteGameSessionCommand(Guid SessionId, Guid UserId) : IRequest<Result<Unit>>;
```

**Step 3: Command Handler**
```csharp
// BoundedContexts/GameManagement/Application/Handlers/DeleteGameSessionCommandHandler.cs
public class DeleteGameSessionCommandHandler : IRequestHandler<DeleteGameSessionCommand, Result<Unit>>
{
    private readonly IGameSessionRepository _repository;
    private readonly ILogger<DeleteGameSessionCommandHandler> _logger;

    public async Task<Result<Unit>> Handle(DeleteGameSessionCommand request, CancellationToken ct)
    {
        var session = await _repository.GetByIdAsync(request.SessionId, ct);
        if (session == null)
            return Result.Failure<Unit>("Session not found");

        session.Delete(request.UserId);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Game session {SessionId} deleted by {UserId}", request.SessionId, request.UserId);

        return Result.Success(Unit.Value);
    }
}
```

**Step 4: HTTP Endpoint**
```csharp
// Routing/GameRoutes.cs
app.MapDelete("/api/v1/games/sessions/{id}", async (Guid id, IMediator mediator, ClaimsPrincipal user) =>
{
    var userId = GetUserIdFromClaims(user);
    var command = new DeleteGameSessionCommand(id, userId);
    var result = await mediator.Send(command);

    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
});
```

**Step 5: Tests**
```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/DeleteGameSessionCommandHandlerTests.cs
public class DeleteGameSessionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidSession_DeletesSuccessfully()
    {
        // Arrange
        var mockRepo = new Mock<IGameSessionRepository>();
        var session = new GameSession { Id = Guid.NewGuid(), CreatedBy = userId };
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

        var handler = new DeleteGameSessionCommandHandler(mockRepo.Object, Mock.Of<ILogger>());
        var command = new DeleteGameSessionCommand(session.Id, userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Deleted, session.Status);
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

### Writing Tests (90%+ Coverage Required)

**Backend Test (xUnit + Moq)**:
```csharp
[Fact]
public async Task RegisterCommand_ValidRequest_CreatesUser()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
    mockRepo.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

    var handler = new RegisterCommandHandler(mockRepo.Object, Mock.Of<IPasswordHasher>());
    var command = new RegisterCommand("user@example.com", "Password123!");

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    mockRepo.Verify(r => r.AddAsync(It.Is<User>(u => u.Email == "user@example.com")), Times.Once);
}
```

**Frontend Test (Jest + RTL)**:
```typescript
import { render, screen } from '@/test-utils';
import { GameCard } from '../GameCard';

test('displays game information', () => {
  const game = {
    id: '1',
    name: 'Catan',
    players: '3-4',
    duration: '60-90 min'
  };

  render(<GameCard game={game} />);

  expect(screen.getByText('Catan')).toBeInTheDocument();
  expect(screen.getByText('3-4')).toBeInTheDocument();
  expect(screen.getByText('60-90 min')).toBeInTheDocument();
});
```

---

### Managing Dependencies

**Backend (NuGet)**:
```bash
# Add package
dotnet add package Serilog.Sinks.Seq

# Update package
dotnet add package Serilog.Sinks.Seq --version 8.0.0

# Remove package
dotnet remove package Serilog.Sinks.Seq

# List packages
dotnet list package

# Check for vulnerabilities
dotnet list package --vulnerable
```

**Frontend (pnpm)**:
```bash
# Add package
pnpm add react-query

# Add dev dependency
pnpm add -D jest

# Update package
pnpm update react-query

# Remove package
pnpm remove react-query

# List outdated
pnpm outdated

# Audit for vulnerabilities
pnpm audit --audit-level=high
```

---

### Creating Database Migrations

```bash
# Add migration
cd apps/api/src/Api
dotnet ef migrations add AddGameSessionDeletedAt

# Update database (apply migrations)
dotnet ef database update

# Rollback to specific migration
dotnet ef database update <PreviousMigrationName>

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script
dotnet ef migrations script > migration.sql
```

**Migration Best Practices**:
- Use descriptive names (`AddUserEmailIndex`, not `Migration1`)
- Test migrations on local database first
- Review generated SQL before applying
- Always have rollback plan
- Never modify applied migrations (create new migration instead)

See [Migration Management Guide](./guides/migration-management.md) for details.

---

## 🔍 Finding What You Need

### By Role

**I'm a Backend Developer**:
1. [DDD Quick Reference](../01-architecture/ddd/quick-reference.md) - Domain patterns
2. [Testing Guide](./testing/testing-guide.md) - How to write tests
3. [Legacy Code Dashboard](./refactoring/legacy-code-dashboard.md) - DDD migration status
4. [PDF Processing Guide](./guides/pdf-processing-guide.md) - PDF features
5. [LLM Integration Guide](./guides/llm-integration-guide.md) - RAG/chat features

**I'm a Frontend Developer**:
1. [Testing Guide](./testing/testing-guide.md) - Jest + RTL patterns
2. [Testing React 19 Patterns](./testing/testing-react-19-patterns.md) - React 19 specific
3. [Frontend Architecture](../04-frontend/architecture.md) - Frontend design
4. [Accessibility Standards](../04-frontend/accessibility-standards.md) - A11y requirements

**I'm a QA Engineer**:
1. [Testing Strategy](./testing/testing-strategy.md) - Overall strategy
2. [Manual Testing Guide](./testing/manual-testing-guide.md) - 🇮🇹 Italian manual QA
3. [Testing Specialized](./testing/testing-specialized.md) - Accessibility, E2E, API testing
4. [E2E Contribution Guide](./testing/e2e-contribution-guide.md) - Playwright tests

### By Task

**Writing Tests**:
- [Testing Guide](./testing/testing-guide.md) - Comprehensive guide
- [Testing Quick Reference](./testing/testing-quick-reference.md) - Quick patterns

**Adding New Feature**:
- [DDD Quick Reference](../01-architecture/ddd/quick-reference.md) - Domain patterns
- [CQRS Flow Diagram](../01-architecture/diagrams/cqrs-mediatr-flow.md) - Request flow
- Common Development Tasks section above

**Working on RAG**:
- [LLM Integration Guide](./guides/llm-integration-guide.md)
- [ADR-001: Hybrid RAG](../01-architecture/adr/adr-001-hybrid-rag.md)
- [RAG System Diagram](../01-architecture/diagrams/rag-system-detailed.md)

**Working on PDF**:
- [PDF Processing Guide](./guides/pdf-processing-guide.md)
- [ADR-003b: Unstructured PDF](../01-architecture/adr/adr-003b-unstructured-pdf.md)
- [PDF Pipeline Diagram](../01-architecture/diagrams/pdf-pipeline-detailed.md)

**Refactoring**:
- [Legacy Code Dashboard](./refactoring/legacy-code-dashboard.md)
- [Implementation Notes](./refactoring/implementation-notes.md)
- [Codebase Maintenance](./guides/codebase-maintenance.md)

---

## 🤝 Contributing

### Code Quality Standards

**C# (.NET)**:
- Nullable references enabled
- Async/await for all I/O operations
- Dependency injection (constructor injection)
- ILogger for logging (structured logging with Serilog)
- IDisposable: Always use `using`, avoid resource leaks
- Repository pattern for data access

**TypeScript/React**:
- Strict mode enabled
- ESLint + Prettier
- Avoid `any` type
- Functional components + hooks
- Absolute imports (`@/components/...`)

### Pull Request Checklist

**Before submitting PR**:
- [ ] Tests written (90%+ coverage)
- [ ] All tests passing (`dotnet test` / `pnpm test`)
- [ ] Code formatted (`dotnet format` / `pnpm lint:fix`)
- [ ] TypeScript errors fixed (`pnpm typecheck`)
- [ ] Build succeeds (`dotnet build` / `pnpm build`)
- [ ] Documentation updated (if public API changed)
- [ ] Migration created (if database schema changed)
- [ ] Commits follow convention (`feat:`, `fix:`, `refactor:`, etc.)

**PR Naming Convention**:
```
[CATEGORY] Brief description

Categories:
- [API] - Backend API changes
- [WEB] - Frontend changes
- [INFRA] - Infrastructure changes
- [DOCS] - Documentation only
- [TEST] - Test only changes
- [REFACTOR] - Code refactoring
- [FIX] - Bug fixes

Example: [API] Add delete game session endpoint
```

---

## 🔗 Related Documentation

- **[CLAUDE.md](../../CLAUDE.md)** - Complete development guide
- **[Architecture](../01-architecture/)** - System architecture & ADRs
- **[API Specification](../03-api/board-game-ai-api-specification.md)** - REST API docs
- **[Frontend](../04-frontend/)** - Frontend architecture & guides
- **[Operations](../05-operations/)** - Deployment & runbooks

---

**Last Updated**: 2025-11-15
**Maintainer**: Development Team
**Total Documents**: 30+ files
**Test Coverage**: 90%+ enforced

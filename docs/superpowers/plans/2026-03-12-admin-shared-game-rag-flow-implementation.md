# Admin Shared Game + RAG Flow — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the E2E admin flow: BGG search → game creation → PDF upload → embedding progress → agent auto-creation → publish → RAG chat

**Architecture:** Event-driven agent auto-creation already exists (`AutoCreateAgentOnPdfReadyHandler`). The wizard SSE hardcodes `AgentExists = false` — fix it to query real agent state. Add `QuickPublish` domain method for one-step Draft→Published. Add Descent to seeding manifests. E2E integration test validates entire flow.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, EF Core, PostgreSQL, Qdrant, xUnit, Testcontainers, FluentAssertions, Moq

**Epic:** #245 | **Issues:** #246, #247, #248, #249, #250

---

## File Structure

### Modified Files
| File | Responsibility | Issue |
|------|---------------|-------|
| `apps/api/src/Api/Routing/AdminGameWizardEndpoints.cs` | Fix wizard SSE to query agent existence | #247 |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs` | Add `QuickPublish()` domain method | #250 |
| `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs` | Add quick-publish endpoint | #250 |
| `apps/api/src/Api/Infrastructure/Seeders/Manifests/dev.yml` | Add Descent entry | #248 |
| `apps/api/src/Api/Infrastructure/Seeders/Manifests/staging.yml` | Add Descent entry | #248 |

### New Files
| File | Responsibility | Issue |
|------|---------------|-------|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/SharedGameQuickPublishedEvent.cs` | Domain event for quick-publish | #250 |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommand.cs` | Command record | #250 |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandler.cs` | Handler | #250 |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandValidator.cs` | FluentValidation | #250 |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameQuickPublishTests.cs` | Domain method tests | #250 |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandlerTests.cs` | Handler tests | #250 |
| `tests/Api.Tests/Routing/AdminGameWizardAgentExistsTests.cs` | Wizard SSE agent check tests | #247 |
| `data/rulebook/descent_rulebook.pdf` | Descent rulebook PDF | #246 |

---

## Chunk 1: Fix Wizard SSE Agent Check (Issue #247)

### Task 1: Fix wizard SSE to query real agent existence

**Context:** `AutoCreateAgentOnPdfReadyHandler` already creates agents when PDFs reach Ready state. But `AdminGameWizardEndpoints.cs:193` hardcodes `AgentExists = false`. The `MapStateToPercent` method already handles `agentExists` — it maps Ready+agent to 100%, Ready without agent to 90%.

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminGameWizardEndpoints.cs:167-200`

- [ ] **Step 1: Write failing test for agent existence check**

Create `tests/Api.Tests/Routing/AdminGameWizardAgentExistsTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AdminGameWizardAgentExistsTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;

    public AdminGameWizardAgentExistsTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AgentExistsForGame_WhenAgentDefinitionExists_ReturnsTrue()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        // Add an AgentDefinition for the game (used by KB bounded context)
        _dbContext.AgentDefinitions.Add(new AgentDefinition
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Name = "Test Agent",
            IsActive = true,
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var exists = await _dbContext.AgentDefinitions
            .AnyAsync(a => a.GameId == gameId && a.IsActive);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AgentExistsForGame_WhenNoAgent_ReturnsFalse()
    {
        var gameId = Guid.NewGuid();
        var exists = await _dbContext.AgentDefinitions
            .AnyAsync(a => a.GameId == gameId && a.IsActive);
        exists.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it compiles and the pattern works**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AdminGameWizardAgentExistsTests" --no-build -v n`
Expected: Tests should pass (these test the query pattern, not the endpoint yet)

- [ ] **Step 3: Fix the wizard SSE to query agent existence**

Modify `apps/api/src/Api/Routing/AdminGameWizardEndpoints.cs`. Replace the hardcoded `false` with an actual DB query:

```csharp
// Inside the while loop, AFTER the pdfInfo query (line 183), ADD:
var agentExists = await dbContext.Agents
    .AnyAsync(a => a.GameId == resolvedGameId, cancellationToken)
    .ConfigureAwait(false);

// Then update the progressEvent (lines 189-200):
var progressEvent = new WizardProgressEvent
{
    CurrentStep = pdfState,
    PdfState = pdfState,
    AgentExists = agentExists, // Was: false
    OverallPercent = MapStateToPercent(pdfState, agentExists), // Was: false
    Message = BuildProgressMessage(pdfState, agentExists, pdfInfo?.FileName), // Was: false
    IsComplete = isComplete && agentExists, // Complete only when agent exists too
    ErrorMessage = isFailed ? pdfInfo?.ProcessingError : null,
    Priority = pdfInfo?.ProcessingPriority ?? "Normal",
    Timestamp = DateTime.UtcNow
};
```

**Important**: Check which DbSet to use — the handler creates via `CreateGameAgentCommand` which goes through `AgentEntity`. The DbSet is `dbContext.Agents` (line 64 of MeepleAiDbContext). Query: `dbContext.Agents.AnyAsync(a => a.GameId == resolvedGameId)`.

- [ ] **Step 4: Build and verify no compilation errors**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeds

- [ ] **Step 5: Run existing AutoCreateAgentOnPdfReadyHandler tests to verify no regression**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AutoCreateAgentOnPdfReadyHandlerTests" -v n`
Expected: All 10 tests pass

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminGameWizardEndpoints.cs
git add tests/Api.Tests/Routing/AdminGameWizardAgentExistsTests.cs
git commit -m "fix(wizard): query real agent existence in SSE progress stream (#247)

Replace hardcoded AgentExists=false with actual DB query.
MapStateToPercent now correctly reports 100% when agent is created.
IsComplete now requires both PDF Ready and agent existence."
```

---

## Chunk 2: Quick-Publish Endpoint (Issue #250)

### Task 2: Add QuickPublish domain method

**Context:** `SharedGame` has `SubmitForApproval()` (Draft→PendingApproval) and `ApprovePublication()` (PendingApproval→Published). QuickPublish combines both: Draft→Published directly.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/SharedGameQuickPublishedEvent.cs`
- Create: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameQuickPublishTests.cs`

- [ ] **Step 1: Write failing test for QuickPublish domain method**

Create `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameQuickPublishTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameQuickPublishTests
{
    private static SharedGame CreateDraftGame() =>
        SharedGame.Create("Test Game", "A test game", Guid.NewGuid());

    [Fact]
    public void QuickPublish_WhenDraft_TransitionsToPublished()
    {
        var game = CreateDraftGame();
        var adminId = Guid.NewGuid();

        game.QuickPublish(adminId);

        game.Status.Should().Be(GameStatus.Published);
    }

    [Fact]
    public void QuickPublish_WhenDraft_RaisesQuickPublishedEvent()
    {
        var game = CreateDraftGame();
        var adminId = Guid.NewGuid();

        game.QuickPublish(adminId);

        game.DomainEvents.Should().ContainSingle(e => e is SharedGameQuickPublishedEvent);
    }

    [Fact]
    public void QuickPublish_WhenNotDraft_ThrowsInvalidOperation()
    {
        var game = CreateDraftGame();
        var adminId = Guid.NewGuid();
        game.SubmitForApproval(adminId); // Now PendingApproval

        var act = () => game.QuickPublish(adminId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public void QuickPublish_WithEmptyGuid_ThrowsArgumentException()
    {
        var game = CreateDraftGame();

        var act = () => game.QuickPublish(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails (QuickPublish method doesn't exist)**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~SharedGameQuickPublishTests" --no-build -v n`
Expected: FAIL — compilation error, `QuickPublish` method not found

- [ ] **Step 3: Create SharedGameQuickPublishedEvent**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/SharedGameQuickPublishedEvent.cs`:

```csharp
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

public sealed class SharedGameQuickPublishedEvent : DomainEventBase
{
    public Guid GameId { get; }
    public Guid PublishedBy { get; }

    public SharedGameQuickPublishedEvent(Guid gameId, Guid publishedBy)
    {
        GameId = gameId;
        PublishedBy = publishedBy;
    }
}
```

- [ ] **Step 4: Add QuickPublish method to SharedGame aggregate**

Add to `SharedGame.cs` after the `ApprovePublication` method (~line 452):

```csharp
/// <summary>
/// Quick-publishes the game, transitioning directly from Draft to Published.
/// Only available for admin users who have both submit and approve permissions.
/// Issue #250: Quick-publish endpoint for admin shared games
/// </summary>
public void QuickPublish(Guid publishedBy)
{
    if (_status != GameStatus.Draft)
        throw new InvalidOperationException(
            $"Cannot quick-publish game in {_status} status. Only Draft games can be quick-published.");

    if (publishedBy == Guid.Empty)
        throw new ArgumentException("PublishedBy cannot be empty", nameof(publishedBy));

    _status = GameStatus.Published;
    _modifiedBy = publishedBy;
    _modifiedAt = DateTime.UtcNow;

    AddDomainEvent(new SharedGameQuickPublishedEvent(_id, publishedBy));
}
```

- [ ] **Step 5: Run domain tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~SharedGameQuickPublishTests" -v n`
Expected: All 4 tests PASS

- [ ] **Step 6: Commit domain changes**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameQuickPublishTests.cs
git commit -m "feat(shared-game): add QuickPublish domain method (#250)

Draft → Published in single step, skipping PendingApproval.
Raises SharedGameQuickPublishedEvent. Validates Draft status."
```

### Task 3: Add QuickPublish command, handler, validator, and endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandValidator.cs`
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`
- Create: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandlerTests.cs`

- [ ] **Step 1: Write failing handler test**

Create `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class QuickPublishSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly QuickPublishSharedGameCommandHandler _handler;

    public QuickPublishSharedGameCommandHandlerTests()
    {
        _mockRepo = new Mock<ISharedGameRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new QuickPublishSharedGameCommandHandler(
            _mockRepo.Object,
            _mockUnitOfWork.Object,
            NullLogger<QuickPublishSharedGameCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenGameIsDraft_QuickPublishesSuccessfully()
    {
        // Arrange
        var game = SharedGame.Create("Test", "Desc", Guid.NewGuid());
        var gameId = game.Id;
        var adminId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new QuickPublishSharedGameCommand(gameId, adminId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        _mockRepo.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new QuickPublishSharedGameCommand(Guid.NewGuid(), Guid.NewGuid());
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await _handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<Exception>(); // NotFoundException
    }
}
```

- [ ] **Step 2: Create Command record**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommand.cs`:

```csharp
using Api.SharedKernel.Application;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Quick-publishes a shared game directly from Draft to Published status.
/// Combines Submit + Approve in a single operation for admin users.
/// Issue #250
/// </summary>
internal record QuickPublishSharedGameCommand(
    Guid GameId,
    Guid PublishedBy
) : ICommand<Unit>;
```

- [ ] **Step 3: Create Validator**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class QuickPublishSharedGameCommandValidator
    : AbstractValidator<QuickPublishSharedGameCommand>
{
    public QuickPublishSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PublishedBy).NotEmpty();
    }
}
```

- [ ] **Step 4: Create Handler**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandler.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class QuickPublishSharedGameCommandHandler
    : IRequestHandler<QuickPublishSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QuickPublishSharedGameCommandHandler> _logger;

    public QuickPublishSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<QuickPublishSharedGameCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        QuickPublishSharedGameCommand command,
        CancellationToken cancellationToken)
    {
        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken)
            ?? throw new NotFoundException($"SharedGame {command.GameId} not found");

        game.QuickPublish(command.PublishedBy);

        await _repository.UpdateAsync(game, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SharedGame {GameId} quick-published by {AdminId}",
            command.GameId, command.PublishedBy);

        return Unit.Value;
    }
}
```

- [ ] **Step 5: Run handler tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~QuickPublishSharedGameCommandHandlerTests" -v n`
Expected: All tests PASS

- [ ] **Step 6: Add endpoint to SharedGameCatalogEndpoints**

Add to `SharedGameCatalogEndpoints.cs`, after the approve-publication endpoint mapping (~line 175). Follow the exact same pattern as `HandleApprovePublication`:

```csharp
// In MapAdminSharedGameEndpoints method, after approve-publication:
adminGroup.MapPost("/{id:guid}/quick-publish", HandleQuickPublish)
    .WithName("QuickPublishSharedGame")
    .WithDescription("Quick-publishes a shared game directly from Draft to Published (admin only)")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status400BadRequest)
    .RequireAuthorization("AdminOnlyPolicy");
```

Add the handler method (follow `HandleApprovePublication` pattern):

```csharp
private static async Task<IResult> HandleQuickPublish(
    Guid id,
    IMediator mediator,
    HttpContext httpContext,
    CancellationToken cancellationToken)
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
        return Results.Unauthorized();

    try
    {
        await mediator.Send(
            new QuickPublishSharedGameCommand(id, parsedUserId),
            cancellationToken);
        return Results.NoContent();
    }
    catch (NotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}
```

- [ ] **Step 7: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeds

- [ ] **Step 8: Commit CQRS layer + endpoint**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublish*
git add apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/QuickPublishSharedGameCommandHandlerTests.cs
git commit -m "feat(shared-game): add quick-publish endpoint (#250)

POST /admin/shared-games/{id}/quick-publish
Draft → Published in single step. AdminOnlyPolicy.
CQRS: Command + Handler + Validator + Endpoint."
```

---

## Chunk 3: Descent PDF + YAML Manifests (Issues #246, #248)

### Task 4: Acquire Descent rulebook PDF

**Context:** The `data/rulebook/` directory contains 32 PDFs. Descent: Journeys in the Dark is missing. The PDF must be text-extractable (not image-only scan) and ≤100MB.

**Files:**
- Create: `data/rulebook/descent_rulebook.pdf`

- [ ] **Step 1: Acquire the Descent: Journeys in the Dark (Second Edition) rulebook PDF**

The official rulebook is publicly available from Fantasy Flight Games. Download it and save as `data/rulebook/descent_rulebook.pdf`.

Verify the PDF is text-extractable:
```bash
# Quick check — if this outputs text, it's extractable
pwsh -c "& { Add-Type -Path 'path/to/iTextSharp.dll'; ... }"
# Or simply verify file size is reasonable
ls -la data/rulebook/descent_rulebook.pdf
```

- [ ] **Step 2: Commit the PDF (if within Git LFS or size policy)**

```bash
git add data/rulebook/descent_rulebook.pdf
git commit -m "data: add Descent rulebook PDF (#246)"
```

### Task 5: Add Descent to YAML seeding manifests

**Context:** The seeding architecture (Epic #234, branch `feature/seeding-architecture`) uses YAML manifests. Add Descent to `dev.yml` and `staging.yml`.

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Manifests/dev.yml`
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Manifests/staging.yml`

**Note:** This task should be done on the `feature/seeding-architecture` branch or after it's merged to `main-dev`.

- [ ] **Step 1: Look up Descent BGG ID**

Descent: Journeys in the Dark (Second Edition) has BGG ID **104162**.

- [ ] **Step 2: Add Descent entry to dev.yml**

Add under `catalog.games`:
```yaml
    - title: "Descent: Journeys in the Dark (Second Edition)"
      bggId: 104162
      language: en
      pdf: "descent_rulebook.pdf"
      seedAgent: true
```

- [ ] **Step 3: Add Descent entry to staging.yml**

Same entry as dev.yml.

- [ ] **Step 4: Commit manifest changes**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Manifests/dev.yml
git add apps/api/src/Api/Infrastructure/Seeders/Manifests/staging.yml
git commit -m "data(seeding): add Descent to dev and staging manifests (#248)"
```

---

## Chunk 4: E2E Integration Test (Issue #249)

### Task 6: Create E2E integration test for admin game→PDF→RAG flow

**Context:** Uses `SharedTestcontainersFixture` pattern with `WebApplicationFactory<Program>`. Tests the full cross-BC flow: game creation → PDF upload → processing → agent auto-creation → chat.

**Important:** This test requires real PDF services (embedding, extraction). It should be gated by `ENABLE_PDF_SERVICES` environment variable and skipped in CI if services aren't available.

**Files:**
- Create: `tests/Api.Tests/E2E/AdminGameRagFlowE2ETests.cs`
- Create: `tests/Api.Tests/E2E/TestData/test_rulebook_1page.pdf` (small test PDF)

- [ ] **Step 1: Create a minimal test PDF**

Create a 1-page PDF with known content for assertion matching. Content should include a distinctive phrase like "When a hero attacks a monster, roll the blue attack die and any additional dice shown on the weapon."

- [ ] **Step 2: Write the E2E test skeleton**

Create `tests/Api.Tests/E2E/AdminGameRagFlowE2ETests.cs`:

```csharp
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests.E2E;

/// <summary>
/// E2E integration test: admin creates game → uploads PDF → monitors embedding →
/// agent auto-created → publishes game → tests RAG chat.
/// Requires PDF services (embedding, extraction) to be available.
/// Issue #249
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "CrossCutting")]
public sealed class AdminGameRagFlowE2ETests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public AdminGameRagFlowE2ETests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.ArePdfServicesEnabled)
            return; // Skip setup if services not available

        // Setup WebApplicationFactory with test containers
        // Follow SharedGameCatalogEndpointsIntegrationTests pattern
        // Configure admin auth headers
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
    }

    [SkippableFact]
    public async Task Admin_FullGameRagFlow_CreatesPublishedGameWithWorkingChat()
    {
        Skip.IfNot(_fixture.ArePdfServicesEnabled, "PDF services not available");

        // 1. Create game via wizard
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/admin/games/wizard/create",
            new { BggId = 104162 }); // Descent
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Upload test PDF
        // 3. Launch processing
        // 4. Poll progress until Ready (timeout 5min)
        // 5. Verify agent auto-created (AgentExists = true in progress)
        // 6. Quick-publish game
        // 7. Send RAG chat message
        // 8. Assert response contains rulebook content
    }
}
```

- [ ] **Step 3: Implement the full test flow**

Fill in steps 2-8 following existing integration test patterns. Use:
- `X-Test-UserId` and `X-Test-Role: admin` headers for authentication
- `MultipartFormDataContent` for PDF upload
- Polling loop with `Task.Delay(2000)` for progress, timeout after 5 minutes
- SSE parsing for chat response validation

- [ ] **Step 4: Run the test (if PDF services available)**

Run: `cd apps/api && ENABLE_PDF_SERVICES=true dotnet test --filter "FullyQualifiedName~AdminGameRagFlowE2ETests" -v n`
Expected: Test passes end-to-end

- [ ] **Step 5: Commit E2E test**

```bash
git add tests/Api.Tests/E2E/
git commit -m "test(e2e): add admin game→PDF→RAG flow integration test (#249)

Tests full cross-BC flow: game creation → PDF upload → embedding →
agent auto-creation → quick-publish → RAG chat.
Gated by ENABLE_PDF_SERVICES environment variable."
```

---

## Execution Order & Dependencies

```
Chunk 1 (#247): Wizard SSE fix ─────────────────────┐
                                                      │
Chunk 2 (#250): Quick-publish ──────────────────────┤──→ Chunk 4 (#249): E2E test
                                                      │
Chunk 3 (#246, #248): Descent PDF + manifests ──────┘
```

**Recommended order:** Chunk 1 → Chunk 2 → Chunk 3 → Chunk 4

Chunks 1-3 are independent and could be parallelized if using subagent-driven development.

---

## Verification Checklist

Before marking the epic complete:

- [ ] `dotnet build` succeeds with no warnings
- [ ] `dotnet test --filter "Category=Unit"` — all pass
- [ ] Wizard SSE reports `AgentExists = true` and 100% when agent exists
- [ ] `POST /admin/shared-games/{id}/quick-publish` returns 204 for Draft games
- [ ] `POST /admin/shared-games/{id}/quick-publish` returns 400 for non-Draft games
- [ ] Descent PDF exists in `data/rulebook/`
- [ ] Descent entries in dev.yml and staging.yml
- [ ] E2E test passes (when PDF services available)
- [ ] PR created to parent branch (main-dev), not to main
- [ ] Issues #246-#250 updated on GitHub

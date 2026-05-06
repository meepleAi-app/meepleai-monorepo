# Private PDF Agent Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Auto-include private PDFs in agent RAG scope when uploaded, and let users control which PDFs the agent uses via a document selection UI (radio for Base rulebook, checkboxes for expansions/errata/house rules).

**Architecture:** Backend: event handler listens to `PrivatePdfAssociatedEvent` and auto-adds PDF to agent config. New query returns combined shared+private documents with selection state. New command updates selection with Base-uniqueness validation. Frontend: reusable `DocumentSelectionPanel` component integrated in both ConfigAgentStep wizard and Knowledge Zone.

**Tech Stack:** .NET 9, MediatR, EF Core, xUnit + Moq + FluentAssertions | Next.js 16, React 19, TanStack Query, Tailwind 4, shadcn/ui

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs` | Create | Auto-add private PDF to agent's SelectedDocumentIds |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs` | Create | Unit tests for event handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQuery.cs` | Create | Query + handler + DTOs for available documents |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQueryHandlerTests.cs` | Create | Unit tests for query handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommand.cs` | Create | Command + handler for user document selection |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommandHandlerTests.cs` | Create | Unit tests for command handler |
| `apps/api/src/Api/Routing/UserLibrary/UserLibraryAgentEndpoints.cs` | Create | GET + PUT endpoints for agent documents |
| `apps/api/src/Api/Routing/UserLibraryEndpoints.cs` | Modify | Register new endpoint group |
| `apps/web/src/lib/api/schemas/agent-documents.schemas.ts` | Create | Zod schemas for DTOs |
| `apps/web/src/lib/api/clients/agentDocumentsClient.ts` | Create | API client for agent document endpoints |
| `apps/web/src/lib/api/clients/index.ts` | Modify | Export new client |
| `apps/web/src/lib/api/index.ts` | Modify | Register client in ApiClient |
| `apps/web/src/hooks/queries/useAgentDocuments.ts` | Create | TanStack Query hooks |
| `apps/web/src/components/library/DocumentSelectionPanel.tsx` | Create | Reusable radio/checkbox panel |
| `apps/web/src/app/(authenticated)/library/private/add/steps/ConfigAgentStep.tsx` | Modify | Integrate DocumentSelectionPanel |
| `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx` | Modify | Add document management section |

---

## Task 1: PrivatePdfAssociatedEventHandler

**Context:** When a user uploads a private PDF for a game, `UserLibraryEntry.AssociatePrivatePdf()` raises `PrivatePdfAssociatedEvent`. Currently no handler listens. We need a handler in KnowledgeBase BC that auto-adds the PDF's document ID to the agent's `SelectedDocumentIds` if an agent exists for that game.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs`

- [ ] **Step 1.1: Write failing tests**

Create test file:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

[Trait("Category", "Unit")]
public sealed class PrivatePdfAssociatedEventHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<ILogger<PrivatePdfAssociatedEventHandler>> _mockLogger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly PrivatePdfAssociatedEventHandler _handler;

    public PrivatePdfAssociatedEventHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<PrivatePdfAssociatedEventHandler>>();
        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PrivatePdfEventTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());

        _handler = new PrivatePdfAssociatedEventHandler(
            _mockAgentRepo.Object,
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Should_Add_PdfId_To_Agent_SelectedDocuments_When_Agent_Exists()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var existingDocId = Guid.NewGuid();

        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent,
            AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
            true, gameId: gameId);

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { agent });

        // Seed existing agent config with one document
        var config = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { existingDocId }),
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        _dbContext.Set<AgentConfigurationEntity>().Add(config);
        await _dbContext.SaveChangesAsync();

        var domainEvent = new PrivatePdfAssociatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), gameId, pdfDocumentId);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        var updatedConfig = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == agentId && c.IsCurrent);
        var docIds = JsonSerializer.Deserialize<List<Guid>>(updatedConfig.SelectedDocumentIdsJson!);
        docIds.Should().HaveCount(2);
        docIds.Should().Contain(existingDocId);
        docIds.Should().Contain(pdfDocumentId);
    }

    [Fact]
    public async Task Should_Noop_When_No_Agent_Exists_For_Game()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var domainEvent = new PrivatePdfAssociatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), gameId, Guid.NewGuid());

        // Act — should not throw
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — no db changes
        _dbContext.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Should_Be_Idempotent_When_PdfId_Already_Selected()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        var agent = new Agent(agentId, "TestAgent", AgentType.RagAgent,
            AgentStrategy.Custom("default", new Dictionary<string, object>(StringComparer.Ordinal)),
            true, gameId: gameId);

        _mockAgentRepo
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { agent });

        // Config already contains the PDF
        var config = new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(new List<Guid> { pdfDocumentId }),
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        _dbContext.Set<AgentConfigurationEntity>().Add(config);
        await _dbContext.SaveChangesAsync();

        var domainEvent = new PrivatePdfAssociatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), gameId, pdfDocumentId);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — still only one document
        var updatedConfig = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == agentId && c.IsCurrent);
        var docIds = JsonSerializer.Deserialize<List<Guid>>(updatedConfig.SelectedDocumentIdsJson!);
        docIds.Should().ContainSingle();
        docIds.Should().Contain(pdfDocumentId);
    }
}
```

- [ ] **Step 1.2: Run tests to verify they fail**

Run: `cd apps/api && dotnet build --no-restore`

Expected: Compilation error — `PrivatePdfAssociatedEventHandler` does not exist.

- [ ] **Step 1.3: Implement event handler**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles PrivatePdfAssociatedEvent from UserLibrary BC.
/// Auto-adds the uploaded PDF to the agent's SelectedDocumentIds if an agent exists for that game.
/// If no agent exists, does nothing — the user will select documents when creating the agent.
/// </summary>
internal sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private readonly IAgentRepository _agentRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;

    public PrivatePdfAssociatedEventHandler(
        IAgentRepository agentRepository,
        MeepleAiDbContext dbContext,
        ILogger<PrivatePdfAssociatedEventHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        await HandleAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    internal async Task HandleAsync(PrivatePdfAssociatedEvent domainEvent, CancellationToken cancellationToken)
    {
        var agents = await _agentRepository
            .GetByGameIdAsync(domainEvent.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (agents.Count == 0)
        {
            _logger.LogInformation(
                "No agent found for game {GameId} — skipping auto-add of PDF {PdfId}",
                domainEvent.GameId, domainEvent.PdfDocumentId);
            return;
        }

        foreach (var agent in agents)
        {
            var config = await _dbContext.AgentConfigurations
                .FirstOrDefaultAsync(
                    c => c.AgentId == agent.Id && c.IsCurrent,
                    cancellationToken)
                .ConfigureAwait(false);

            if (config == null)
            {
                _logger.LogWarning(
                    "Agent {AgentId} has no current config — skipping auto-add",
                    agent.Id);
                continue;
            }

            var selectedIds = new List<Guid>();
            if (!string.IsNullOrEmpty(config.SelectedDocumentIdsJson))
            {
                try
                {
                    selectedIds = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson)
                        ?? new List<Guid>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse SelectedDocumentIdsJson for agent {AgentId}", agent.Id);
                    selectedIds = new List<Guid>();
                }
            }

            // Idempotency: skip if already present
            if (selectedIds.Contains(domainEvent.PdfDocumentId))
            {
                _logger.LogInformation(
                    "PDF {PdfId} already in agent {AgentId} selected documents — skipping",
                    domainEvent.PdfDocumentId, agent.Id);
                continue;
            }

            selectedIds.Add(domainEvent.PdfDocumentId);
            config.SelectedDocumentIdsJson = JsonSerializer.Serialize(selectedIds);

            _logger.LogInformation(
                "Auto-added PDF {PdfId} to agent {AgentId} for game {GameId}. Total docs: {Count}",
                domainEvent.PdfDocumentId, agent.Id, domainEvent.GameId, selectedIds.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 1.4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "PrivatePdfAssociatedEventHandlerTests" --no-restore`

Expected: All 3 tests PASS.

- [ ] **Step 1.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs
git commit -m "feat(kb): auto-add private PDF to agent SelectedDocumentIds on upload

Listens to PrivatePdfAssociatedEvent from UserLibrary BC. When a user
uploads a private PDF for a game that has an agent, the PDF is
automatically added to the agent's document scope. Idempotent —
skips if already present. Noop if no agent exists."
```

---

## Task 2: GetAvailableDocumentsForGameQuery

**Context:** The frontend needs to know which documents are available for a game (shared + private), their types, processing states, and whether they're currently selected in the agent config. This query returns all that information split by document type (Base vs Additional).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQuery.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQueryHandlerTests.cs`

- [ ] **Step 2.1: Write failing tests**

Create test file:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", "Unit")]
public sealed class GetAvailableDocumentsForGameQueryHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<GetAvailableDocumentsForGameQueryHandler>> _mockLogger;
    private readonly GetAvailableDocumentsForGameQueryHandler _handler;

    public GetAvailableDocumentsForGameQueryHandlerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AvailableDocsTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
        _mockLogger = new Mock<ILogger<GetAvailableDocumentsForGameQueryHandler>>();
        _handler = new GetAvailableDocumentsForGameQueryHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Should_Return_Documents_Split_By_Type()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var baseDocId = Guid.NewGuid();
        var expansionDocId = Guid.NewGuid();

        SeedPdfDocument(baseDocId, gameId, "Rulebook.pdf", "base", PdfProcessingState.Ready, userId);
        SeedPdfDocument(expansionDocId, gameId, "Expansion.pdf", "expansion", PdfProcessingState.Ready, userId);
        SeedAgent(agentId, gameId);
        SeedAgentConfig(agentId, new List<Guid> { baseDocId });
        await _dbContext.SaveChangesAsync();

        var query = new GetAvailableDocumentsForGameQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentId.Should().Be(agentId);
        result.BaseDocuments.Should().ContainSingle();
        result.BaseDocuments[0].DocumentId.Should().Be(baseDocId);
        result.BaseDocuments[0].IsSelected.Should().BeTrue();
        result.AdditionalDocuments.Should().ContainSingle();
        result.AdditionalDocuments[0].DocumentId.Should().Be(expansionDocId);
        result.AdditionalDocuments[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Return_Null_AgentId_When_No_Agent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedPdfDocument(Guid.NewGuid(), gameId, "Rules.pdf", "base", PdfProcessingState.Ready, userId);
        await _dbContext.SaveChangesAsync();

        var query = new GetAvailableDocumentsForGameQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AgentId.Should().BeNull();
        result.BaseDocuments.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_Include_Private_Pdf_With_IsPrivate_Flag()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var privatePdfId = Guid.NewGuid();

        SeedPdfDocument(privatePdfId, gameId, "MyRules.pdf", "base", PdfProcessingState.Extracting, userId, isPrivate: true);
        await _dbContext.SaveChangesAsync();

        var query = new GetAvailableDocumentsForGameQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.BaseDocuments.Should().ContainSingle();
        var doc = result.BaseDocuments[0];
        doc.IsPrivate.Should().BeTrue();
        doc.ProcessingState.Should().Be("Extracting");
    }

    #region Helpers

    private void SeedPdfDocument(Guid id, Guid gameId, string fileName, string docType,
        PdfProcessingState state, Guid uploadedBy, bool isPrivate = false)
    {
        _dbContext.PdfDocuments.Add(new Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
        {
            Id = id,
            GameId = gameId,
            FileName = fileName,
            FilePath = $"/uploads/{fileName}",
            FileSizeBytes = 1_000_000,
            ContentType = "application/pdf",
            UploadedByUserId = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = state.ToString(),
            DocumentType = docType,
            DocumentCategory = "Rulebook",
            Language = "it",
            IsPrivate = isPrivate,
            IsActiveForRag = true
        });
    }

    private void SeedAgent(Guid agentId, Guid gameId)
    {
        _dbContext.Agents.Add(new Infrastructure.Entities.KnowledgeBase.AgentEntity
        {
            Id = agentId,
            Name = "TestAgent",
            Type = "RagAgent",
            GameId = gameId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void SeedAgentConfig(Guid agentId, List<Guid> selectedDocIds)
    {
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            LlmProvider = 0,
            LlmModel = "test-model",
            AgentMode = 0,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(selectedDocIds),
            Temperature = 0.7m,
            MaxTokens = 1000,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    #endregion
}
```

- [ ] **Step 2.2: Implement query, handler, and DTOs**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQuery.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

// DTOs
internal record AvailableDocumentsDto(
    Guid? AgentId,
    List<DocumentSelectionItemDto> BaseDocuments,
    List<DocumentSelectionItemDto> AdditionalDocuments
);

internal record DocumentSelectionItemDto(
    Guid DocumentId,
    string FileName,
    string DocumentType,
    string ProcessingState,
    bool IsPrivate,
    bool IsSelected,
    int? PageCount
);

// Query
internal record GetAvailableDocumentsForGameQuery(
    Guid GameId,
    Guid UserId
) : IRequest<AvailableDocumentsDto>;

// Handler
internal sealed class GetAvailableDocumentsForGameQueryHandler
    : IRequestHandler<GetAvailableDocumentsForGameQuery, AvailableDocumentsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetAvailableDocumentsForGameQueryHandler> _logger;

    public GetAvailableDocumentsForGameQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetAvailableDocumentsForGameQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AvailableDocumentsDto> Handle(
        GetAvailableDocumentsForGameQuery request, CancellationToken cancellationToken)
    {
        // 1. Find agent for this game
        var agent = await _dbContext.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.GameId == request.GameId, cancellationToken)
            .ConfigureAwait(false);

        // 2. Get selected document IDs from agent config
        var selectedDocIds = new HashSet<Guid>();
        if (agent != null)
        {
            var config = await _dbContext.AgentConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.AgentId == agent.Id && c.IsCurrent,
                    cancellationToken)
                .ConfigureAwait(false);

            if (config != null && !string.IsNullOrEmpty(config.SelectedDocumentIdsJson))
            {
                try
                {
                    var ids = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson);
                    if (ids != null) selectedDocIds = ids.ToHashSet();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse SelectedDocumentIdsJson for agent {AgentId}", agent.Id);
                }
            }
        }

        // 3. Get all PDF documents for this game (shared + private belonging to this user)
        var documents = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(d => d.GameId == request.GameId && d.IsActiveForRag)
            .Where(d => !d.IsPrivate || d.UploadedByUserId == request.UserId)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.FileName)
            .Select(d => new DocumentSelectionItemDto(
                d.Id,
                d.FileName,
                d.DocumentType ?? "base",
                d.ProcessingState ?? "Pending",
                d.IsPrivate,
                selectedDocIds.Contains(d.Id),
                d.PageCount))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 4. Split by type
        var baseDocuments = documents
            .Where(d => d.DocumentType.Equals("base", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var additionalDocuments = documents
            .Where(d => !d.DocumentType.Equals("base", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new AvailableDocumentsDto(
            agent?.Id,
            baseDocuments,
            additionalDocuments);
    }
}
```

- [ ] **Step 2.3: Run tests**

Run: `cd apps/api && dotnet test --filter "GetAvailableDocumentsForGameQueryHandlerTests" --no-restore`

Expected: All 3 tests PASS.

- [ ] **Step 2.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQuery.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQueryHandlerTests.cs
git commit -m "feat(kb): add GetAvailableDocumentsForGameQuery

Returns combined shared + private PDFs for a game, split by type
(Base vs Expansion/Errata/HomeRule). Includes processing state,
privacy flag, and whether each document is selected in the agent config."
```

---

## Task 3: UpdateUserAgentDocumentsCommand

**Context:** Users need to update which documents their agent uses. The key validation rule: at most 1 document with `DocumentType.Base` can be selected (radio button behavior). Expansion/Errata/HomeRule are additive (checkboxes).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommand.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommandHandlerTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create test file:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateUserAgentDocumentsCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly UpdateUserAgentDocumentsCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateUserAgentDocumentsCommandHandlerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UpdateUserAgentDocsTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
        _handler = new UpdateUserAgentDocumentsCommandHandler(
            _dbContext,
            Mock.Of<ILogger<UpdateUserAgentDocumentsCommandHandler>>());
    }

    [Fact]
    public async Task Should_Update_SelectedDocumentIds_Successfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var baseDocId = Guid.NewGuid();
        var expansionDocId = Guid.NewGuid();

        SeedAgent(agentId, gameId);
        SeedAgentConfig(agentId, new List<Guid>());
        SeedPdfDocument(baseDocId, gameId, "base");
        SeedPdfDocument(expansionDocId, gameId, "expansion");
        SeedLibraryEntry(gameId, _userId);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            gameId, _userId, new List<Guid> { baseDocId, expansionDocId });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var config = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == agentId && c.IsCurrent);
        var ids = JsonSerializer.Deserialize<List<Guid>>(config.SelectedDocumentIdsJson!);
        ids.Should().HaveCount(2);
        ids.Should().Contain(baseDocId);
        ids.Should().Contain(expansionDocId);
    }

    [Fact]
    public async Task Should_Reject_When_Multiple_Base_Documents_Selected()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var baseDoc1 = Guid.NewGuid();
        var baseDoc2 = Guid.NewGuid();

        SeedAgent(agentId, gameId);
        SeedAgentConfig(agentId, new List<Guid>());
        SeedPdfDocument(baseDoc1, gameId, "base");
        SeedPdfDocument(baseDoc2, gameId, "base");
        SeedLibraryEntry(gameId, _userId);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            gameId, _userId, new List<Guid> { baseDoc1, baseDoc2 });

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*regolamento base*");
    }

    [Fact]
    public async Task Should_Reject_When_Game_Not_In_User_Library()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        SeedAgent(agentId, gameId);
        SeedAgentConfig(agentId, new List<Guid>());
        // No library entry seeded
        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            gameId, _userId, new List<Guid>());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Should_Reject_When_Document_Not_Belonging_To_Game()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var foreignDocId = Guid.NewGuid();

        SeedAgent(agentId, gameId);
        SeedAgentConfig(agentId, new List<Guid>());
        SeedPdfDocument(foreignDocId, otherGameId, "base"); // belongs to different game
        SeedLibraryEntry(gameId, _userId);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateUserAgentDocumentsCommand(
            gameId, _userId, new List<Guid> { foreignDocId });

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #region Helpers

    private void SeedAgent(Guid agentId, Guid gameId)
    {
        _dbContext.Agents.Add(new Infrastructure.Entities.KnowledgeBase.AgentEntity
        {
            Id = agentId, Name = "TestAgent", Type = "RagAgent",
            GameId = gameId, IsActive = true, CreatedAt = DateTime.UtcNow
        });
    }

    private void SeedAgentConfig(Guid agentId, List<Guid> docIds)
    {
        _dbContext.Set<AgentConfigurationEntity>().Add(new AgentConfigurationEntity
        {
            Id = Guid.NewGuid(), AgentId = agentId, LlmProvider = 0,
            LlmModel = "test", AgentMode = 0,
            SelectedDocumentIdsJson = JsonSerializer.Serialize(docIds),
            Temperature = 0.7m, MaxTokens = 1000, IsCurrent = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        });
    }

    private void SeedPdfDocument(Guid id, Guid gameId, string docType)
    {
        _dbContext.PdfDocuments.Add(new Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
        {
            Id = id, GameId = gameId, FileName = $"{docType}.pdf",
            FilePath = $"/uploads/{docType}.pdf", FileSizeBytes = 1_000_000,
            ContentType = "application/pdf", UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow, ProcessingState = "Ready",
            DocumentType = docType, DocumentCategory = "Rulebook",
            Language = "it", IsActiveForRag = true
        });
    }

    private void SeedLibraryEntry(Guid gameId, Guid userId)
    {
        _dbContext.UserLibraryEntries.Add(new Infrastructure.Entities.UserLibrary.UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(), UserId = userId, GameId = gameId,
            AddedAt = DateTime.UtcNow, IsFavorite = false
        });
    }

    #endregion
}
```

- [ ] **Step 3.2: Implement command and handler**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommand.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

internal record UpdateUserAgentDocumentsCommand(
    Guid GameId,
    Guid UserId,
    List<Guid> SelectedDocumentIds
) : IRequest<Unit>;

internal sealed class UpdateUserAgentDocumentsCommandHandler
    : IRequestHandler<UpdateUserAgentDocumentsCommand, Unit>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateUserAgentDocumentsCommandHandler> _logger;

    public UpdateUserAgentDocumentsCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<UpdateUserAgentDocumentsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        UpdateUserAgentDocumentsCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate game is in user's library
        var libraryEntry = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.GameId == request.GameId && e.UserId == request.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        if (libraryEntry == null)
            throw new ForbiddenException("Gioco non presente nella tua libreria");

        // 2. Find agent for this game
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.GameId == request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
            throw new NotFoundException($"Nessun agente trovato per il gioco {request.GameId}");

        // 3. Validate all documents belong to this game
        if (request.SelectedDocumentIds.Count > 0)
        {
            var validDocIds = await _dbContext.PdfDocuments
                .Where(d => d.GameId == request.GameId && request.SelectedDocumentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.DocumentType })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var validIdSet = validDocIds.Select(d => d.Id).ToHashSet();
            var invalidIds = request.SelectedDocumentIds.Where(id => !validIdSet.Contains(id)).ToList();
            if (invalidIds.Count > 0)
                throw new NotFoundException(
                    $"Documenti non trovati per questo gioco: {string.Join(", ", invalidIds)}");

            // 4. Validate max 1 Base document
            var baseCount = validDocIds.Count(d =>
                string.Equals(d.DocumentType, "base", StringComparison.OrdinalIgnoreCase));
            if (baseCount > 1)
                throw new ConflictException(
                    "Puoi selezionare al massimo un regolamento base");
        }

        // 5. Update agent config
        var config = await _dbContext.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == agent.Id && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        if (config == null)
            throw new NotFoundException($"Nessuna configurazione attiva per l'agente {agent.Id}");

        config.SelectedDocumentIdsJson = JsonSerializer.Serialize(request.SelectedDocumentIds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} updated agent {AgentId} documents for game {GameId}: {Count} documents selected",
            request.UserId, agent.Id, request.GameId, request.SelectedDocumentIds.Count);

        return Unit.Value;
    }
}
```

- [ ] **Step 3.3: Run tests**

Run: `cd apps/api && dotnet test --filter "UpdateUserAgentDocumentsCommandHandlerTests" --no-restore`

Expected: All 4 tests PASS.

- [ ] **Step 3.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommand.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommandHandlerTests.cs
git commit -m "feat(kb): add UpdateUserAgentDocumentsCommand with Base-uniqueness validation

User-facing command for selecting which PDFs the agent uses.
Validates: game in user library, documents belong to game,
max 1 Base document (radio behavior). Italian error messages."
```

---

## Task 4: User Library Agent Endpoints

**Context:** Register the GET and PUT endpoints under `/api/v1/library/games/{gameId}/agent/documents`. Follow the existing `UserLibraryCoreEndpoints` pattern for auth validation.

**Files:**
- Create: `apps/api/src/Api/Routing/UserLibrary/UserLibraryAgentEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/UserLibraryEndpoints.cs`

- [ ] **Step 4.1: Create endpoint file**

Create `apps/api/src/Api/Routing/UserLibrary/UserLibraryAgentEndpoints.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.UserLibrary;

internal static class UserLibraryAgentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapGetAgentDocuments(group);
        MapUpdateAgentDocuments(group);
    }

    private static void MapGetAgentDocuments(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/agent/documents", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await mediator.Send(
                    new GetAvailableDocumentsForGameQuery(gameId, userId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<AvailableDocumentsDto>(200)
        .Produces(401)
        .Produces(404)
        .WithName("GetAgentDocumentsForGame")
        .WithTags("Library")
        .WithSummary("Get available documents for agent configuration")
        .WithOpenApi();
    }

    private static void MapUpdateAgentDocuments(RouteGroupBuilder group)
    {
        group.MapPut("/library/games/{gameId:guid}/agent/documents", async (
            Guid gameId,
            [FromBody] UpdateAgentDocumentsRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            try
            {
                await mediator.Send(
                    new UpdateUserAgentDocumentsCommand(gameId, userId, request.SelectedDocumentIds), ct)
                    .ConfigureAwait(false);
                return Results.Ok(new { message = "Documenti agente aggiornati" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return Results.Forbid();
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .Produces(409)
        .WithName("UpdateAgentDocumentsForGame")
        .WithTags("Library")
        .WithSummary("Update which documents the agent uses for RAG")
        .WithOpenApi();
    }
}

internal record UpdateAgentDocumentsRequest(List<Guid> SelectedDocumentIds);
```

- [ ] **Step 4.2: Register in UserLibraryEndpoints.cs**

Add to `apps/api/src/Api/Routing/UserLibraryEndpoints.cs`, inside `MapUserLibraryEndpoints`:

```csharp
UserLibraryAgentEndpoints.Map(group);
```

- [ ] **Step 4.3: Build to verify**

Run: `cd apps/api && dotnet build --no-restore`

Expected: Build succeeds.

- [ ] **Step 4.4: Commit**

```bash
git add apps/api/src/Api/Routing/UserLibrary/UserLibraryAgentEndpoints.cs
git add apps/api/src/Api/Routing/UserLibraryEndpoints.cs
git commit -m "feat(routing): add user-facing agent document endpoints

GET /api/v1/library/games/{gameId}/agent/documents — available docs
PUT /api/v1/library/games/{gameId}/agent/documents — update selection
Both require user auth and validate game ownership."
```

---

## Task 5: Frontend API Client + Hooks

**Context:** Create the Zod schemas, API client, and TanStack Query hooks for the two new endpoints.

**Files:**
- Create: `apps/web/src/lib/api/schemas/agent-documents.schemas.ts`
- Create: `apps/web/src/lib/api/clients/agentDocumentsClient.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts`
- Modify: `apps/web/src/lib/api/index.ts`
- Create: `apps/web/src/hooks/queries/useAgentDocuments.ts`

- [ ] **Step 5.1: Create Zod schemas**

Create `apps/web/src/lib/api/schemas/agent-documents.schemas.ts`:

```typescript
import { z } from 'zod';

export const DocumentSelectionItemSchema = z.object({
  documentId: z.string(),
  fileName: z.string(),
  documentType: z.string(),
  processingState: z.string(),
  isPrivate: z.boolean(),
  isSelected: z.boolean(),
  pageCount: z.number().nullable(),
});

export const AvailableDocumentsSchema = z.object({
  agentId: z.string().nullable(),
  baseDocuments: z.array(DocumentSelectionItemSchema),
  additionalDocuments: z.array(DocumentSelectionItemSchema),
});

export type DocumentSelectionItem = z.infer<typeof DocumentSelectionItemSchema>;
export type AvailableDocuments = z.infer<typeof AvailableDocumentsSchema>;
```

- [ ] **Step 5.2: Create API client**

Create `apps/web/src/lib/api/clients/agentDocumentsClient.ts`:

```typescript
import type { HttpClient } from '../core/httpClient';
import {
  AvailableDocumentsSchema,
  type AvailableDocuments,
} from '../schemas/agent-documents.schemas';

export interface AgentDocumentsClient {
  getAvailableDocuments(gameId: string): Promise<AvailableDocuments>;
  updateSelectedDocuments(gameId: string, selectedDocumentIds: string[]): Promise<void>;
}

export function createAgentDocumentsClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): AgentDocumentsClient {
  return {
    async getAvailableDocuments(gameId: string): Promise<AvailableDocuments> {
      const data = await httpClient.get<AvailableDocuments>(
        `/api/v1/library/games/${gameId}/agent/documents`,
        AvailableDocumentsSchema
      );
      if (!data) throw new Error('Failed to fetch agent documents');
      return data;
    },

    async updateSelectedDocuments(
      gameId: string,
      selectedDocumentIds: string[]
    ): Promise<void> {
      await httpClient.put(
        `/api/v1/library/games/${gameId}/agent/documents`,
        { selectedDocumentIds }
      );
    },
  };
}
```

- [ ] **Step 5.3: Register client in API exports**

Add to `apps/web/src/lib/api/clients/index.ts`:

```typescript
export * from './agentDocumentsClient';
```

Add to `apps/web/src/lib/api/index.ts` — import and register in `createApiClient`:

```typescript
import { createAgentDocumentsClient, type AgentDocumentsClient } from './clients';
```

Add `agentDocuments: AgentDocumentsClient` to the `ApiClient` interface and:

```typescript
agentDocuments: createAgentDocumentsClient({ httpClient }),
```

to the client creation.

- [ ] **Step 5.4: Create TanStack Query hooks**

Create `apps/web/src/hooks/queries/useAgentDocuments.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { AvailableDocuments } from '@/lib/api/schemas/agent-documents.schemas';

export const agentDocumentKeys = {
  all: ['agentDocuments'] as const,
  forGame: (gameId: string) => [...agentDocumentKeys.all, gameId] as const,
};

export function useAgentDocuments(gameId: string, enabled: boolean = true) {
  return useQuery({
    queryKey: agentDocumentKeys.forGame(gameId),
    queryFn: () => api.agentDocuments.getAvailableDocuments(gameId),
    enabled,
    staleTime: 30 * 1000,
  });
}

export function useUpdateAgentDocuments(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (selectedDocumentIds: string[]) =>
      api.agentDocuments.updateSelectedDocuments(gameId, selectedDocumentIds),
    onMutate: async (newSelection) => {
      await queryClient.cancelQueries({
        queryKey: agentDocumentKeys.forGame(gameId),
      });
      const previous = queryClient.getQueryData<AvailableDocuments>(
        agentDocumentKeys.forGame(gameId)
      );
      // Optimistic update: toggle isSelected on cached data
      if (previous) {
        const selectionSet = new Set(newSelection);
        queryClient.setQueryData<AvailableDocuments>(
          agentDocumentKeys.forGame(gameId),
          {
            ...previous,
            baseDocuments: previous.baseDocuments.map((d) => ({
              ...d,
              isSelected: selectionSet.has(d.documentId),
            })),
            additionalDocuments: previous.additionalDocuments.map((d) => ({
              ...d,
              isSelected: selectionSet.has(d.documentId),
            })),
          }
        );
      }
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(
          agentDocumentKeys.forGame(gameId),
          context.previous
        );
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({
        queryKey: agentDocumentKeys.forGame(gameId),
      });
    },
  });
}
```

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/lib/api/schemas/agent-documents.schemas.ts
git add apps/web/src/lib/api/clients/agentDocumentsClient.ts
git add apps/web/src/lib/api/clients/index.ts
git add apps/web/src/lib/api/index.ts
git add apps/web/src/hooks/queries/useAgentDocuments.ts
git commit -m "feat(web): add agent documents API client and TanStack Query hooks

Zod schemas for AvailableDocuments DTO. API client for GET/PUT
agent document endpoints. useAgentDocuments query hook with
optimistic updates on selection change."
```

---

## Task 6: DocumentSelectionPanel Component

**Context:** Reusable component showing radio buttons for Base documents and checkboxes for Additional documents, with processing status badges. Used in both the wizard and the Knowledge Zone.

**Files:**
- Create: `apps/web/src/components/library/DocumentSelectionPanel.tsx`

- [ ] **Step 6.1: Create component**

Create `apps/web/src/components/library/DocumentSelectionPanel.tsx`:

```tsx
'use client';

import { useCallback, useEffect, useMemo, useRef } from 'react';
import {
  useAgentDocuments,
  useUpdateAgentDocuments,
} from '@/hooks/queries/useAgentDocuments';
import type { DocumentSelectionItem } from '@/lib/api/schemas/agent-documents.schemas';

interface DocumentSelectionPanelProps {
  gameId: string;
  onSelectionChange?: (selectedIds: string[]) => void;
  initialSelection?: string[];
  readOnly?: boolean;
  wizardMode?: boolean; // In wizard mode, don't auto-save — let parent handle it
}

function StatusBadge({ state }: { state: string }) {
  switch (state) {
    case 'Ready':
      return (
        <span className="inline-flex items-center gap-1 text-xs text-green-400">
          <span className="h-1.5 w-1.5 rounded-full bg-green-400" />
          Ready
        </span>
      );
    case 'Failed':
      return (
        <span className="inline-flex items-center gap-1 text-xs text-red-400">
          <span className="h-1.5 w-1.5 rounded-full bg-red-400" />
          Fallito
        </span>
      );
    default:
      return (
        <span className="inline-flex items-center gap-1 text-xs text-amber-400">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-400 animate-pulse" />
          In elaborazione
        </span>
      );
  }
}

function DocumentRow({
  doc,
  inputType,
  isSelected,
  onChange,
  groupName,
  readOnly,
}: {
  doc: DocumentSelectionItem;
  inputType: 'radio' | 'checkbox';
  isSelected: boolean;
  onChange: (docId: string, checked: boolean) => void;
  groupName?: string;
  readOnly?: boolean;
}) {
  return (
    <label
      className={`flex items-center gap-3 p-2 rounded-md cursor-pointer hover:bg-[#30363d] transition-colors ${
        readOnly ? 'opacity-60 pointer-events-none' : ''
      }`}
    >
      <input
        type={inputType}
        name={groupName}
        checked={isSelected}
        onChange={(e) => onChange(doc.documentId, e.target.checked)}
        disabled={readOnly}
        className="accent-amber-500"
      />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-sm text-gray-200 truncate">{doc.fileName}</span>
          {doc.isPrivate && (
            <span className="text-[10px] px-1.5 py-0.5 rounded bg-purple-900/50 text-purple-300">
              privato
            </span>
          )}
        </div>
        {doc.pageCount && (
          <span className="text-xs text-gray-500">{doc.pageCount} pag</span>
        )}
      </div>
      <StatusBadge state={doc.processingState} />
    </label>
  );
}

export function DocumentSelectionPanel({
  gameId,
  onSelectionChange,
  initialSelection,
  readOnly = false,
  wizardMode = false,
}: DocumentSelectionPanelProps) {
  const { data, isLoading } = useAgentDocuments(gameId);
  const updateMutation = useUpdateAgentDocuments(gameId);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Compute current selected IDs from data
  const selectedIds = useMemo(() => {
    if (!data) return new Set<string>(initialSelection ?? []);
    const ids = new Set<string>();
    for (const d of [...data.baseDocuments, ...data.additionalDocuments]) {
      if (d.isSelected) ids.add(d.documentId);
    }
    // On first load with initialSelection, merge them in
    if (initialSelection && ids.size === 0) {
      for (const id of initialSelection) ids.add(id);
    }
    return ids;
  }, [data, initialSelection]);

  const handleChange = useCallback(
    (docId: string, checked: boolean, isBase: boolean) => {
      const newSelection = new Set(selectedIds);

      if (isBase) {
        // Radio behavior: deselect all other base documents
        if (data) {
          for (const d of data.baseDocuments) {
            newSelection.delete(d.documentId);
          }
        }
        if (checked) newSelection.add(docId);
      } else {
        if (checked) newSelection.add(docId);
        else newSelection.delete(docId);
      }

      const ids = Array.from(newSelection);
      onSelectionChange?.(ids);

      if (!wizardMode) {
        // Debounced save
        if (debounceRef.current) clearTimeout(debounceRef.current);
        debounceRef.current = setTimeout(() => {
          updateMutation.mutate(ids);
        }, 300);
      }
    },
    [selectedIds, data, onSelectionChange, wizardMode, updateMutation]
  );

  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  if (isLoading) {
    return (
      <div className="animate-pulse space-y-2 p-4">
        <div className="h-4 bg-[#30363d] rounded w-1/3" />
        <div className="h-8 bg-[#30363d] rounded" />
        <div className="h-8 bg-[#30363d] rounded" />
      </div>
    );
  }

  if (!data) return null;

  if (!data.agentId && !wizardMode) {
    return (
      <p className="text-sm text-gray-500 italic p-4">
        Crea un agente prima di selezionare documenti
      </p>
    );
  }

  return (
    <div className="space-y-4">
      {/* Base Documents — Radio */}
      {data.baseDocuments.length > 0 && (
        <div>
          <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            Regolamento (scegli uno)
          </h4>
          <div className="space-y-1">
            {data.baseDocuments.map((doc) => (
              <DocumentRow
                key={doc.documentId}
                doc={doc}
                inputType="radio"
                isSelected={selectedIds.has(doc.documentId)}
                onChange={(id, checked) => handleChange(id, checked, true)}
                groupName={`base-${gameId}`}
                readOnly={readOnly}
              />
            ))}
          </div>
        </div>
      )}

      {/* Additional Documents — Checkbox */}
      {data.additionalDocuments.length > 0 && (
        <div>
          <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            Espansioni & Aggiunte
          </h4>
          <div className="space-y-1">
            {data.additionalDocuments.map((doc) => (
              <DocumentRow
                key={doc.documentId}
                doc={doc}
                inputType="checkbox"
                isSelected={selectedIds.has(doc.documentId)}
                onChange={(id, checked) => handleChange(id, checked, false)}
                readOnly={readOnly}
              />
            ))}
          </div>
        </div>
      )}

      {data.baseDocuments.length === 0 && data.additionalDocuments.length === 0 && (
        <p className="text-sm text-gray-500 italic">
          Nessun documento disponibile per questo gioco
        </p>
      )}
    </div>
  );
}
```

- [ ] **Step 6.2: Commit**

```bash
git add apps/web/src/components/library/DocumentSelectionPanel.tsx
git commit -m "feat(web): add DocumentSelectionPanel component

Radio buttons for Base rulebook (max 1), checkboxes for
expansions/errata/house rules. Status badges (Ready/Processing/Failed).
Debounced auto-save in non-wizard mode. Optimistic updates via
TanStack Query mutation."
```

---

## Task 7: ConfigAgentStep Integration

**Context:** Integrate `DocumentSelectionPanel` into the wizard's agent configuration step. In wizard mode, the panel doesn't auto-save — it passes selected IDs to the agent creation call.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/private/add/steps/ConfigAgentStep.tsx`

- [ ] **Step 7.1: Add DocumentSelectionPanel to ConfigAgentStep**

Read the current file, then add the panel below the strategy selector. Key changes:

1. Import `DocumentSelectionPanel`
2. Add `selectedDocIds` state initialized with `[pdfId]`
3. Render `DocumentSelectionPanel` with `wizardMode={true}` and `initialSelection={[pdfId]}`
4. Pass `selectedDocIds` in the agent creation payload

```typescript
import { DocumentSelectionPanel } from '@/components/library/DocumentSelectionPanel';
```

Add state:
```typescript
const [selectedDocIds, setSelectedDocIds] = useState<string[]>([pdfId]);
```

Add below the strategy selector in the JSX:
```tsx
{/* Document Selection */}
<div className="mt-6">
  <h3 className="text-sm font-medium text-gray-300 mb-2">
    Documenti per la Knowledge Base
  </h3>
  <div className="bg-[#21262d] rounded-lg border border-[#30363d] p-3">
    <DocumentSelectionPanel
      gameId={gameId}
      wizardMode={true}
      initialSelection={[pdfId]}
      onSelectionChange={setSelectedDocIds}
    />
  </div>
</div>
```

- [ ] **Step 7.2: Build to verify**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds (or warnings only, no errors).

- [ ] **Step 7.3: Commit**

```bash
git add apps/web/src/app/(authenticated)/library/private/add/steps/ConfigAgentStep.tsx
git commit -m "feat(web): integrate DocumentSelectionPanel in ConfigAgentStep wizard

Shows document selection with radio/checkbox below agent config.
Pre-selects the just-uploaded PDF. Wizard mode prevents auto-save."
```

---

## Task 8: Knowledge Zone Integration

**Context:** Add an expandable "Gestisci documenti agente" section in the Knowledge Zone on the game detail page. Only shown when an agent exists for the game. Changes save immediately.

**Files:**
- Modify: `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx`

- [ ] **Step 8.1: Add DocumentSelectionPanel to Knowledge Zone**

Read the current file, then add a collapsible section. Key changes:

1. Import `DocumentSelectionPanel`
2. Add an expandable section (using a simple `details/summary` or state toggle)
3. Render the panel with auto-save mode (non-wizard)

```typescript
import { DocumentSelectionPanel } from '@/components/library/DocumentSelectionPanel';
```

Add in the JSX, after the existing KB documents section:
```tsx
{/* Agent Document Selection */}
<details className="mt-3">
  <summary className="text-xs font-semibold text-gray-400 uppercase tracking-wider cursor-pointer hover:text-gray-300">
    Gestisci documenti agente
  </summary>
  <div className="mt-2 bg-[#21262d] rounded-lg border border-[#30363d] p-3">
    <DocumentSelectionPanel gameId={gameId} />
  </div>
</details>
```

- [ ] **Step 8.2: Build to verify**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds.

- [ ] **Step 8.3: Commit**

```bash
git add apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx
git commit -m "feat(web): add document management section to Knowledge Zone

Expandable 'Gestisci documenti agente' section with
DocumentSelectionPanel. Auto-saves selection changes.
Only shown when documents exist for the game."
```

---

## Summary

| Task | Type | Component | Effort |
|------|------|-----------|--------|
| 1 | Backend | PrivatePdfAssociatedEventHandler | ~25 min |
| 2 | Backend | GetAvailableDocumentsForGameQuery | ~25 min |
| 3 | Backend | UpdateUserAgentDocumentsCommand | ~25 min |
| 4 | Backend | Endpoint registration | ~15 min |
| 5 | Frontend | API client + hooks | ~20 min |
| 6 | Frontend | DocumentSelectionPanel | ~30 min |
| 7 | Frontend | ConfigAgentStep integration | ~15 min |
| 8 | Frontend | Knowledge Zone integration | ~10 min |

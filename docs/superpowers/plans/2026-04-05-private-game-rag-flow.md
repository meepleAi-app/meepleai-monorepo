# Private Game RAG Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere funzionante il flusso end-to-end: utente normale crea gioco privato → carica PDF rulebook → consulta l'agente RAG via chat.

**Architecture:** Il flusso usa tre BC (UserLibrary, DocumentProcessing, KnowledgeBase). I giochi privati usano `PrivateGameId` al posto di `GameId`; le query di KB devono accettare entrambi via pattern `PrivateGameId ?? GameId`. Il handler di chat mancante è il blocco principale del P0.

**Tech Stack:** .NET 9 / ASP.NET Minimal APIs / MediatR / EF Core + pgvector / Next.js 16 App Router / React 19 / TypeScript

---

## Scope Check

Questo piano copre 4 priorità con dipendenze sequenziali (P0 sblocca P1 che sblocca frontend). Suddivisione suggerita per sprint paralleli:

- **Sprint 1 (P0)** — 3 fix backend bloccanti → flusso funzionante da API
- **Sprint 2 (P1)** — UX asincrona + endpoint KB status → flusso usabile
- **Sprint 3 (P2)** — Architettura + failure handling + testing → flusso robusto
- **Sprint 4 (P3)** — Enhancements → flusso completo

---

## File Map

### P0 — Creati / Modificati

| File | Operazione | Responsabilità |
|------|-----------|----------------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs` | **CREATE** | Handler mancante per creare thread |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs` | **MODIFY** | Aggiungere `PrivateGameId?` |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ChatThreadDto.cs` | **MODIFY** | Aggiungere `PrivateGameId?` a `CreateChatThreadRequest` |
| `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` | **MODIFY** | Passare `PrivateGameId` al comando |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQuery.cs` | **MODIFY** | Aggiungere `IsPrivateGame` flag |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQueryHandler.cs` | **MODIFY** | Supporto `PrivateGameId` nel filtro PDF |
| `apps/api/src/Api/Routing/PrivateGameEndpoints.cs` | **MODIFY** | Nuovo endpoint `GET /private-games/{id}/kb-status` |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs` | **CREATE** | Test handler nuovo |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusPrivateGameTests.cs` | **CREATE** | Test fix query privata |

### P1 — Creati / Modificati

| File | Operazione | Responsabilità |
|------|-----------|----------------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` | **MODIFY** | Auto-enable `IsActiveForRag = true` dopo indexing |
| `apps/web/src/components/private-game/PdfProcessingStatus.tsx` | **CREATE** | Componente polling stato processing |
| `apps/web/src/app/(authenticated)/private-games/[id]/page.tsx` | **CREATE** | Pagina dettaglio gioco privato con PDF + chat |

### P2 — Creati / Modificati

| File | Operazione | Responsabilità |
|------|-----------|----------------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommand.cs` | **CREATE** | Comando retry indexing fallito |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommandHandler.cs` | **CREATE** | Handler retry |
| `apps/api/src/Api/Routing/Pdf/PdfProcessingEndpoints.cs` | **MODIFY** | Endpoint `POST /pdfs/{id}/retry-indexing` |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/PrivateGameRagFlowIntegrationTests.cs` | **CREATE** | Test integrazione flusso completo |

### P3 — Creati / Modificati

| File | Operazione | Responsabilità |
|------|-----------|----------------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/NotifyUserOnPdfReadyHandler.cs` | **CREATE** | Notifica utente completamento |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/IPdfQualityScorer.cs` | **CREATE** | Interfaccia score qualità PDF |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfQualityScorer.cs` | **CREATE** | Implementazione scorer |

---

## SPRINT 1 — P0: Fix Backend Bloccanti

---

### Task 1: Implementare CreateChatThreadCommandHandler (BUG CRITICO)

**Problema:** `POST /api/v1/chat-threads` chiama `mediator.Send(CreateChatThreadCommand)` ma il handler non esiste → MediatR lancia `InvalidOperationException` a runtime.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ChatThreadDto.cs`
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs`

- [ ] **Step 1.1: Scrivi il test che fallisce**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateChatThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CreateChatThreadCommandHandler _handler;

    public CreateChatThreadCommandHandlerTests()
    {
        _mockRepo = new Mock<IChatThreadRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _handler = new CreateChatThreadCommandHandler(_mockRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task Handle_WithGameId_CreatesThreadAndReturnsDto()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, GameId: gameId, Title: "Test");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.Title.Should().Be("Test");
        result.Status.Should().Be("Active");
        result.Messages.Should().BeEmpty();
        _mockRepo.Verify(r => r.AddAsync(It.Is<ChatThread>(t => t.UserId == userId && t.GameId == gameId), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPrivateGameId_UsesPrivateGameIdAsEffectiveGameId()
    {
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, PrivateGameId: privateGameId, Title: "Private Game Chat");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.GameId.Should().Be(privateGameId); // PrivateGameId stored as GameId for RAG search
        _mockRepo.Verify(r => r.AddAsync(It.Is<ChatThread>(t => t.GameId == privateGameId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoGameId_CreatesThreadWithNullGameId()
    {
        var userId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.GameId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAgentId_SetsAgentOnThread()
    {
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var command = new CreateChatThreadCommand(UserId: userId, AgentId: agentId, AgentType: "tutor");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AgentId.Should().Be(agentId);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }
}
```

- [ ] **Step 1.2: Esegui il test — verifica che fallisce**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~CreateChatThreadCommandHandlerTests" -v minimal
```

Atteso: FAIL — `CreateChatThreadCommandHandler` non esiste ancora.

- [ ] **Step 1.3: Aggiungi `PrivateGameId` al command record**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs
// MODIFICA: aggiungi PrivateGameId parametro

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to create a new chat thread.
/// PrivateGameId is used when the thread is for a user-owned private game (not in shared catalog).
/// The handler stores PrivateGameId as the effective GameId so RAG search scopes correctly
/// (VectorDocumentRepository checks both p.PrivateGameId == gameId and p.GameId == gameId).
/// </summary>
internal record CreateChatThreadCommand(
    Guid UserId,
    Guid? GameId = null,
    Guid? PrivateGameId = null,      // Added: private game support
    string? Title = null,
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null,        // Issue #4362
    string? UserRole = null,
    List<Guid>? SelectedKnowledgeBaseIds = null
) : ICommand<ChatThreadDto>;
```

- [ ] **Step 1.4: Aggiungi `PrivateGameId` al request DTO**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ChatThreadDto.cs
// MODIFICA: aggiungi PrivateGameId alla riga 48-55

/// <summary>
/// DTO for creating a chat thread.
/// </summary>
internal record CreateChatThreadRequest(
    Guid? GameId = null,
    Guid? PrivateGameId = null,       // Added: private game support
    string? Title = null,
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null,         // Issue #4362
    List<Guid>? SelectedKnowledgeBaseIds = null
);
```

- [ ] **Step 1.5: Aggiorna l'endpoint per passare `PrivateGameId`**

```csharp
// File: apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs
// MODIFICA: metodo HandleCreateChatThread (riga ~474)
// Sostituisci la costruzione del command con:

var command = new CreateChatThreadCommand(
    UserId: userId,
    GameId: req.GameId,
    PrivateGameId: req.PrivateGameId,     // Added
    Title: req.Title,
    InitialMessage: req.InitialMessage,
    AgentId: req.AgentId,
    AgentType: req.AgentType,
    UserRole: session.User!.Role,
    SelectedKnowledgeBaseIds: req.SelectedKnowledgeBaseIds
);
```

- [ ] **Step 1.6: Implementa il handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles CreateChatThreadCommand.
/// For private games, uses PrivateGameId as the effective GameId stored on the thread
/// so that AskQuestionQueryHandler can scope vector search correctly via
/// VectorDocumentRepository (which checks both PrivateGameId and GameId columns).
/// </summary>
internal sealed class CreateChatThreadCommandHandler : ICommandHandler<CreateChatThreadCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // For private games, PrivateGameId is used as the effective GameId.
        // This enables AskQuestionQueryHandler to find vectors indexed with that PrivateGameId
        // because VectorDocumentRepository uses: WHERE PrivateGameId == gameId OR GameId == gameId.
        var effectiveGameId = command.PrivateGameId ?? command.GameId;

        var thread = new ChatThread(
            id: Guid.NewGuid(),
            userId: command.UserId,
            gameId: effectiveGameId,
            title: command.Title,
            agentId: command.AgentId,
            agentType: command.AgentType);

        if (command.SelectedKnowledgeBaseIds?.Count > 0)
        {
            thread.SetSelectedKnowledgeBases(command.SelectedKnowledgeBaseIds);
        }

        await _threadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(thread);
    }

    private static ChatThreadDto MapToDto(ChatThread thread)
    {
        return new ChatThreadDto(
            Id: thread.Id,
            UserId: thread.UserId,
            GameId: thread.GameId,
            AgentId: thread.AgentId,
            Title: thread.Title,
            Status: thread.Status.Value,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: [],
            AgentType: thread.AgentType);
    }
}
```

- [ ] **Step 1.7: Esegui i test — verifica che passano**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~CreateChatThreadCommandHandlerTests" -v minimal
```

Atteso: 5/5 PASS.

- [ ] **Step 1.8: Esegui build completa**

```bash
cd apps/api
dotnet build --no-incremental -warnaserror
```

Atteso: 0 errori, 0 warning.

- [ ] **Step 1.9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ChatThreadDto.cs
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs
git commit -m "fix(kb): implement CreateChatThreadCommandHandler with PrivateGame support"
```

---

### Task 2: Fix GetKnowledgeBaseStatusQuery per Private Games (BUG)

**Problema:** `GetKnowledgeBaseStatusQueryHandler` filtra i PDF solo per `p.GameId` — i PDF di giochi privati (con `PrivateGameId != null`, `GameId == null`) non vengono mai trovati → lo status ritorna sempre "Pending" per private games.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQueryHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusPrivateGameTests.cs`

- [ ] **Step 2.1: Scrivi il test che fallisce**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusPrivateGameTests.cs

using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKnowledgeBaseStatusPrivateGameTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetKnowledgeBaseStatusQueryHandler _handler;
    private static readonly Guid PrivateGameId = Guid.NewGuid();
    private static readonly Guid OtherGameId = Guid.NewGuid();

    public GetKnowledgeBaseStatusPrivateGameTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetKnowledgeBaseStatusQueryHandler(
            _dbContext,
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_PrivateGame_ReadyPdf_ReturnsCompletedStatus()
    {
        // Arrange: PDF con PrivateGameId (GameId = null)
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = PrivateGameId,
            GameId = null,
            FileName = "rulebook.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024,
            ProcessingProgressJson = null,
            ProcessingError = null
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var query = new GetKnowledgeBaseStatusQuery(PrivateGameId, IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
    }

    [Fact]
    public async Task Handle_PrivateGame_NoPdf_ReturnsPending()
    {
        var query = new GetKnowledgeBaseStatusQuery(Guid.NewGuid(), IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_PrivateGame_DoesNotReturnSharedGamePdf()
    {
        // PDF con GameId (shared), non deve essere trovato dalla query private
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = PrivateGameId,  // stesso Guid ma come GameId (shared), non PrivateGameId
            PrivateGameId = null,
            FileName = "shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024
        });
        await _dbContext.SaveChangesAsync();

        // Query con IsPrivateGame: true deve cercare per PrivateGameId, non GameId
        var query = new GetKnowledgeBaseStatusQuery(PrivateGameId, IsPrivateGame: true);
        var result = await _handler.Handle(query, CancellationToken.None);

        result!.Status.Should().Be("Pending"); // non trova il PDF shared
    }

    [Fact]
    public async Task Handle_SharedGame_IsPrivateGameFalse_ReturnsSharedPdf()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId,
            PrivateGameId = null,
            FileName = "shared.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            FileSize = 1024
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKnowledgeBaseStatusQuery(sharedGameId, IsPrivateGame: false);
        var result = await _handler.Handle(query, CancellationToken.None);

        result!.Status.Should().Be("Completed"); // trova il PDF condiviso
    }
}
```

- [ ] **Step 2.2: Esegui test — verifica che falliscono**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~GetKnowledgeBaseStatusPrivateGameTests" -v minimal
```

Atteso: FAIL — `IsPrivateGame` non esiste ancora.

- [ ] **Step 2.3: Aggiorna il query record**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQuery.cs

using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get the knowledge base (RAG) status for a game.
/// For private games: set IsPrivateGame = true so the handler filters by p.PrivateGameId.
/// For shared games: IsPrivateGame = false (default) — filters by p.GameId (existing behavior).
/// Issue #4065: RAG readiness polling.
/// </summary>
internal sealed record GetKnowledgeBaseStatusQuery(
    Guid GameId,
    bool IsPrivateGame = false
) : IQuery<KnowledgeBaseStatusDto?>;
```

- [ ] **Step 2.4: Aggiorna il query handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQueryHandler.cs
// MODIFICA: sostituisci il metodo Handle (righe 32-91) con la versione qui sotto.
// La firma della classe e il costruttore rimangono invariati.

public async Task<KnowledgeBaseStatusDto?> Handle(
    GetKnowledgeBaseStatusQuery query,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(query);

    try
    {
        // Resolve game name: private games use PrivateGameEntity.Title, shared use Games.Name
        string? gameName;
        if (query.IsPrivateGame)
        {
            gameName = await _dbContext.PrivateGames
                .Where(g => g.Id == query.GameId)
                .Select(g => g.Title)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            gameName = await _dbContext.Games
                .Where(g => g.Id == query.GameId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        // Filter PDFs by correct column depending on game type
        var pdf = await _dbContext.PdfDocuments
            .Where(p => query.IsPrivateGame
                ? p.PrivateGameId == query.GameId
                : p.GameId == query.GameId)
            .OrderByDescending(p => p.UploadedAt)
            .AsNoTracking()
            .Select(p => new
            {
                p.ProcessingState,
                p.ProcessingProgressJson,
                p.ProcessingError,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            return new KnowledgeBaseStatusDto(
                Status: "Pending",
                Progress: 0,
                TotalChunks: 0,
                ProcessedChunks: 0,
                ErrorMessage: null,
                GameName: gameName);
        }

        var (status, progress, totalChunks, processedChunks, errorMessage) =
            MapProcessingState(pdf.ProcessingState, pdf.ProcessingProgressJson, pdf.ProcessingError);

        return new KnowledgeBaseStatusDto(
            Status: status,
            Progress: progress,
            TotalChunks: totalChunks,
            ProcessedChunks: processedChunks,
            ErrorMessage: errorMessage,
            GameName: gameName);
    }
#pragma warning disable CA1031
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving knowledge base status for game {GameId} (isPrivate={IsPrivate})",
            query.GameId, query.IsPrivateGame);
        return null;
    }
#pragma warning restore CA1031
}
```

Nota: il metodo privato `MapProcessingState` rimane invariato.

- [ ] **Step 2.5: Aggiungi using mancante al handler**

Verifica che il file abbia `using Microsoft.EntityFrameworkCore;` nella sezione using (già presente, nessuna modifica).

- [ ] **Step 2.6: Aggiungi endpoint `/private-games/{id}/kb-status` a PrivateGameEndpoints**

```csharp
// File: apps/api/src/Api/Routing/PrivateGameEndpoints.cs
// MODIFICA 1: aggiungi using all'inizio del file
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;

// MODIFICA 2: aggiungi chiamata nel metodo MapPrivateGameEndpoints dopo MapUnlinkAgentEndpoint:
MapKnowledgeBaseStatusEndpoint(group); // Issue #5215 alias

// MODIFICA 3: aggiungi il metodo privato dopo MapUnlinkAgentEndpoint:

/// <summary>
/// GET /api/v1/private-games/{id}/kb-status - RAG readiness status for a private game
/// Issue #5215: Alias for /knowledge-base/{gameId}/status scoped to PrivateGameId.
/// </summary>
private static void MapKnowledgeBaseStatusEndpoint(RouteGroupBuilder group)
{
    group.MapGet("/private-games/{id:guid}/kb-status", async (
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct) =>
    {
        var (authenticated, _, error) = context.TryGetAuthenticatedUser();
        if (!authenticated) return error!;

        var query = new GetKnowledgeBaseStatusQuery(GameId: id, IsPrivateGame: true);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result is null)
            return Results.NotFound(new { error = "Knowledge base status not found" });

        return Results.Ok(result);
    })
    .RequireAuthorization()
    .WithName("GetPrivateGameKbStatus")
    .WithTags("PrivateGames")
    .WithOpenApi(operation =>
    {
        operation.Summary = "Get RAG status for a private game";
        operation.Description = "Returns the PDF processing/indexing status for a private game's rulebook. Poll this endpoint to know when the AI agent can answer questions.";
        return operation;
    })
    .Produces<KnowledgeBaseStatusDto>()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status401Unauthorized);
}
```

- [ ] **Step 2.7: Esegui i test — verifica che passano**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~GetKnowledgeBaseStatusPrivateGameTests" -v minimal
```

Atteso: 4/4 PASS.

- [ ] **Step 2.8: Build**

```bash
cd apps/api
dotnet build --no-incremental -warnaserror
```

- [ ] **Step 2.9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQuery.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusQueryHandler.cs
git add apps/api/src/Api/Routing/PrivateGameEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKnowledgeBaseStatusPrivateGameTests.cs
git commit -m "fix(kb): support PrivateGameId in GetKnowledgeBaseStatus query and add /private-games/{id}/kb-status endpoint"
```

---

## SPRINT 2 — P1: UX Asincrona e Frontend

---

### Task 3: Auto-enable IsActiveForRag dopo indexing completato

**Problema:** Dopo che `IndexPdfCommandHandler` completa con successo, il PDF rimane con `IsActiveForRag = false` — l'utente deve abilitarlo manualmente. Per private games, l'abilitazione deve essere automatica.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs`

- [ ] **Step 3.1: Trova il punto di completamento nel handler**

Nel `IndexPdfCommandHandler`, il processing termina con `pdf.SetProcessingState("Ready")` (o equivalente). Aggiungi la chiamata ad `SetActiveForRag(true)` subito prima o dopo:

```csharp
// Cerca nel file la riga dove viene impostato lo stato "Ready"
// Aggiungi DOPO quella riga:
if (!pdf.IsActiveForRag)
{
    pdf.SetActiveForRag(true);
}
```

Posizione esatta: cerca `"Ready"` o `ProcessingState.Ready` nel metodo Handle. La modifica è 3 righe.

- [ ] **Step 3.2: Build e test esistenti**

```bash
cd apps/api
dotnet build --no-incremental
dotnet test --filter "BoundedContext=DocumentProcessing" -v minimal
```

Atteso: build OK, test esistenti invariati.

- [ ] **Step 3.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs
git commit -m "fix(docs): auto-enable IsActiveForRag after successful PDF indexing"
```

---

### Task 4: Componente Frontend — PdfProcessingStatus (polling)

**Problema:** Non esiste un componente UI che mostri lo stato del processing PDF e abiliti la chat quando il processing è completato.

**Files:**
- Create: `apps/web/src/components/private-game/PdfProcessingStatus.tsx`

- [ ] **Step 4.1: Crea il componente**

```tsx
// File: apps/web/src/components/private-game/PdfProcessingStatus.tsx

'use client';

import { useEffect, useState } from 'react';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';

export type KbStatus = {
  status: 'Pending' | 'Extracting' | 'Chunking' | 'Embedding' | 'Completed' | 'Failed';
  progress: number;
  totalChunks: number;
  processedChunks: number;
  errorMessage: string | null;
  gameName: string | null;
};

type Props = {
  privateGameId: string;
  onReady?: () => void;
  onRetry?: () => void;
  pollingIntervalMs?: number;
};

const STATUS_LABELS: Record<KbStatus['status'], string> = {
  Pending: 'In attesa...',
  Extracting: 'Estrazione testo',
  Chunking: 'Suddivisione in chunk',
  Embedding: 'Generazione embeddings',
  Completed: 'Pronto',
  Failed: 'Errore',
};

export function PdfProcessingStatus({
  privateGameId,
  onReady,
  onRetry,
  pollingIntervalMs = 5000,
}: Props) {
  const [status, setStatus] = useState<KbStatus | null>(null);
  const [isPolling, setIsPolling] = useState(true);

  useEffect(() => {
    if (!isPolling) return;

    const poll = async () => {
      try {
        const res = await fetch(`/api/v1/private-games/${privateGameId}/kb-status`);
        if (!res.ok) return;
        const data: KbStatus = await res.json();
        setStatus(data);

        if (data.status === 'Completed') {
          setIsPolling(false);
          onReady?.();
        } else if (data.status === 'Failed') {
          setIsPolling(false);
        }
      } catch {
        // Network error — continue polling
      }
    };

    poll(); // immediate first call
    const interval = setInterval(poll, pollingIntervalMs);
    return () => clearInterval(interval);
  }, [privateGameId, isPolling, pollingIntervalMs, onReady]);

  if (!status) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        <span>Verifica stato rulebook...</span>
      </div>
    );
  }

  if (status.status === 'Completed') {
    return (
      <div className="flex items-center gap-2 text-sm text-green-600">
        <CheckCircle2 className="h-4 w-4" />
        <span>Rulebook pronto — puoi fare domande all&apos;agente</span>
      </div>
    );
  }

  if (status.status === 'Failed') {
    return (
      <div className="space-y-2">
        <div className="flex items-center gap-2 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{status.errorMessage ?? 'Elaborazione fallita'}</span>
        </div>
        {onRetry && (
          <Button variant="outline" size="sm" onClick={onRetry}>
            Riprova
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-sm">
        <div className="flex items-center gap-2 text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          <span>{STATUS_LABELS[status.status]}</span>
        </div>
        <Badge variant="secondary">{status.progress}%</Badge>
      </div>
      <Progress value={status.progress} className="h-2" />
      <p className="text-xs text-muted-foreground">
        Il rulebook è in elaborazione. La chat sarà disponibile al completamento.
      </p>
    </div>
  );
}
```

- [ ] **Step 4.2: Crea test unitario del componente**

```tsx
// File: apps/web/src/__tests__/components/private-game/PdfProcessingStatus.test.tsx

import { render, screen, waitFor, act } from '@testing-library/react';
import { PdfProcessingStatus } from '@/components/private-game/PdfProcessingStatus';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('PdfProcessingStatus', () => {
  const privateGameId = '00000000-0000-0000-0000-000000000001';

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('mostra spinner iniziale prima del primo poll', () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ status: 'Pending', progress: 0, totalChunks: 0, processedChunks: 0, errorMessage: null, gameName: null }),
    });

    render(<PdfProcessingStatus privateGameId={privateGameId} />);
    expect(screen.getByText('Verifica stato rulebook...')).toBeInTheDocument();
  });

  it('mostra progress bar durante elaborazione', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ status: 'Chunking', progress: 50, totalChunks: 0, processedChunks: 0, errorMessage: null, gameName: null }),
    });

    render(<PdfProcessingStatus privateGameId={privateGameId} pollingIntervalMs={100} />);

    await waitFor(() => {
      expect(screen.getByText('Suddivisione in chunk')).toBeInTheDocument();
      expect(screen.getByText('50%')).toBeInTheDocument();
    });
  });

  it('chiama onReady quando stato è Completed', async () => {
    const onReady = vi.fn();
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ status: 'Completed', progress: 100, totalChunks: 42, processedChunks: 42, errorMessage: null, gameName: null }),
    });

    render(<PdfProcessingStatus privateGameId={privateGameId} onReady={onReady} pollingIntervalMs={100} />);

    await waitFor(() => {
      expect(onReady).toHaveBeenCalledOnce();
      expect(screen.getByText(/Rulebook pronto/)).toBeInTheDocument();
    });
  });

  it('mostra errore e bottone retry quando Failed', async () => {
    const onRetry = vi.fn();
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({ status: 'Failed', progress: 0, totalChunks: 0, processedChunks: 0, errorMessage: 'PDF non leggibile', gameName: null }),
    });

    render(<PdfProcessingStatus privateGameId={privateGameId} onRetry={onRetry} pollingIntervalMs={100} />);

    await waitFor(() => {
      expect(screen.getByText('PDF non leggibile')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 4.3: Esegui test**

```bash
cd apps/web
pnpm test src/__tests__/components/private-game/PdfProcessingStatus.test.tsx
```

Atteso: 4/4 PASS.

- [ ] **Step 4.4: Commit**

```bash
git add apps/web/src/components/private-game/PdfProcessingStatus.tsx
git add apps/web/src/__tests__/components/private-game/PdfProcessingStatus.test.tsx
git commit -m "feat(web): add PdfProcessingStatus polling component for private games"
```

---

### Task 5: Pagina Dettaglio Gioco Privato con sezione PDF + Chat

**Files:**
- Create: `apps/web/src/app/(authenticated)/private-games/[id]/page.tsx`

- [ ] **Step 5.1: Verifica route esistente**

```bash
find /d/Repositories/meepleai-monorepo-dev/apps/web/src/app -name "page.tsx" -path "*/private-games/*" 2>/dev/null
```

Se la pagina esiste già, salta al Step 5.2 e aggiorna il componente aggiungendo la sezione PDF/Chat. Se non esiste, crea la struttura.

- [ ] **Step 5.2: Crea/aggiorna la pagina**

```tsx
// File: apps/web/src/app/(authenticated)/private-games/[id]/page.tsx

import { Suspense } from 'react';
import { notFound } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { MessageSquare, Upload } from 'lucide-react';
import Link from 'next/link';
import { PdfProcessingStatus } from '@/components/private-game/PdfProcessingStatus';
import { PrivateGamePdfUpload } from '@/components/private-game/PrivateGamePdfUpload';

// Server component: fetch gioco privato
async function getPrivateGame(id: string) {
  const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/private-games/${id}`, {
    cache: 'no-store',
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Failed to fetch private game');
  return res.json();
}

type Props = { params: Promise<{ id: string }> };

export default async function PrivateGameDetailPage({ params }: Props) {
  const { id } = await params;
  const game = await getPrivateGame(id);

  if (!game) notFound();

  return (
    <div className="container max-w-3xl py-8 space-y-6">
      <div>
        <h1 className="text-2xl font-bold font-quicksand">{game.title}</h1>
        {game.description && (
          <p className="text-muted-foreground mt-1">{game.description}</p>
        )}
      </div>

      {/* Sezione Rulebook */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Upload className="h-5 w-5" />
            Rulebook
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <PrivateGamePdfUpload privateGameId={id} />
          <Suspense fallback={null}>
            <PdfProcessingStatus
              privateGameId={id}
              pollingIntervalMs={5000}
            />
          </Suspense>
        </CardContent>
      </Card>

      {/* Sezione Chat RAG */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MessageSquare className="h-5 w-5" />
            Chiedi all&apos;agente
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground mb-4">
            Carica il rulebook e chiedi all&apos;agente le regole di setup, meccaniche di gioco e molto altro.
          </p>
          <PrivateGameChatButton privateGameId={id} agentId={game.agentDefinitionId} />
        </CardContent>
      </Card>
    </div>
  );
}

// Client component separato per il bottone chat (necessita conoscere lo stato)
// Importare da un file separato se diventa complesso
function PrivateGameChatButton({
  privateGameId,
  agentId,
}: {
  privateGameId: string;
  agentId: string | null;
}) {
  // Il bottone rimanda alla creazione del thread con privateGameId
  const href = `/chat/new?privateGameId=${privateGameId}${agentId ? `&agentId=${agentId}` : ''}`;

  return (
    <Button asChild disabled={!agentId}>
      <Link href={href}>
        <MessageSquare className="mr-2 h-4 w-4" />
        {agentId ? 'Inizia chat' : 'Carica prima il rulebook'}
      </Link>
    </Button>
  );
}
```

- [ ] **Step 5.3: Crea stub PrivateGamePdfUpload (placeholder da completare in seguito)**

```tsx
// File: apps/web/src/components/private-game/PrivateGamePdfUpload.tsx
// Stub: wrappa PdfUploadStep in modalità private-game

'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Upload } from 'lucide-react';

type Props = { privateGameId: string };

export function PrivateGamePdfUpload({ privateGameId }: Props) {
  const [uploading, setUploading] = useState(false);
  const [uploaded, setUploaded] = useState(false);

  const handleUpload = async (file: File) => {
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('privateGameId', privateGameId);

      const res = await fetch('/api/v1/ingest/pdf', {
        method: 'POST',
        body: formData,
      });

      if (res.ok) setUploaded(true);
    } finally {
      setUploading(false);
    }
  };

  if (uploaded) {
    return <p className="text-sm text-green-600">Rulebook caricato. Elaborazione in corso...</p>;
  }

  return (
    <div>
      <input
        type="file"
        accept=".pdf"
        id="rulebook-upload"
        className="sr-only"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) handleUpload(file);
        }}
      />
      <Button variant="outline" asChild disabled={uploading}>
        <label htmlFor="rulebook-upload" className="cursor-pointer">
          <Upload className="mr-2 h-4 w-4" />
          {uploading ? 'Caricamento...' : 'Carica rulebook PDF'}
        </label>
      </Button>
    </div>
  );
}
```

- [ ] **Step 5.4: Build frontend**

```bash
cd apps/web
pnpm build 2>&1 | tail -20
```

Atteso: 0 errori TypeScript.

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/app/(authenticated)/private-games/
git add apps/web/src/components/private-game/
git commit -m "feat(web): add private game detail page with PDF upload and RAG chat sections"
```

---

## SPRINT 3 — P2: Architettura, Failure Handling, Testing

---

### Task 6: Endpoint Retry Indexing per PDF Falliti

**Problema:** Se `IndexPdfCommandHandler` fallisce, il PDF rimane in stato "Failed" con nessun modo per ritentare l'indicizzazione senza ricaricare il file.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/Pdf/PdfProcessingEndpoints.cs`

- [ ] **Step 6.1: Crea il command**

```csharp
// File: apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommand.cs

using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Retries PDF indexing for a document in Failed state.
/// Only allowed if ProcessingState == "Failed" and the requesting user owns the PDF.
/// </summary>
internal record RetryPdfIndexingCommand(
    Guid DocumentId,
    Guid UserId
) : ICommand<Unit>;
```

- [ ] **Step 6.2: Crea il handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommandHandler.cs

using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Resets PDF state to Pending and re-dispatches IndexPdfCommand.
/// Guard: only "Failed" PDFs can be retried; user must own the PDF (or be admin).
/// </summary>
internal sealed class RetryPdfIndexingCommandHandler : ICommandHandler<RetryPdfIndexingCommand, Unit>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public RetryPdfIndexingCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<Unit> Handle(RetryPdfIndexingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _pdfRepository.GetByIdAsync(command.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
            throw new NotFoundException($"PDF document {command.DocumentId} not found");

        // Only owner can retry their own PDF
        if (pdf.UploadedByUserId != command.UserId)
            throw new UnauthorizedAccessException("You do not own this PDF document");

        if (!string.Equals(pdf.ProcessingState.ToString(), "Failed", StringComparison.Ordinal))
            throw new InvalidOperationException($"PDF is in state '{pdf.ProcessingState}' — only Failed PDFs can be retried");

        // Reset to Pending so IndexPdfCommandHandler can re-process
        pdf.ResetForRetry(); // domain method that sets ProcessingState = Pending and clears ProcessingError

        await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Re-dispatch indexing
        await _mediator.Send(new IndexPdfCommand(command.DocumentId), cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
```

- [ ] **Step 6.3: Verifica metodo `ResetForRetry` nel dominio**

```bash
grep -n "ResetForRetry\|ResetForRe" /d/Repositories/meepleai-monorepo-dev/apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs
```

Se non esiste, aggiungilo alla domain entity:

```csharp
// Aggiungere a PdfDocument.cs nel BC DocumentProcessing/Domain/Entities/
public void ResetForRetry()
{
    if (ProcessingState.ToString() != "Failed")
        throw new InvalidOperationException("Only Failed PDFs can be reset for retry");

    ProcessingState = ProcessingStateEnum.Pending; // usa l'enum corretto del progetto
    ProcessingError = null;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 6.4: Aggiungi endpoint al routing PDF**

```csharp
// File: apps/api/src/Api/Routing/Pdf/PdfProcessingEndpoints.cs
// Aggiungere dopo gli endpoint esistenti:

group.MapPost("/pdfs/{documentId:guid}/retry-indexing", async (
    Guid documentId,
    IMediator mediator,
    HttpContext context,
    CancellationToken ct) =>
{
    var (authenticated, session, error) = context.TryGetAuthenticatedUser();
    if (!authenticated) return error!;

    var command = new RetryPdfIndexingCommand(documentId, session!.User!.Id);

    try
    {
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Accepted();
    }
    catch (NotFoundException)
    {
        return Results.NotFound();
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("RetryPdfIndexing")
.WithTags("PDFs")
.Produces(StatusCodes.Status202Accepted)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
.Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
.Produces<ProblemDetails>(StatusCodes.Status404NotFound);
```

- [ ] **Step 6.5: Build**

```bash
cd apps/api
dotnet build --no-incremental -warnaserror
```

- [ ] **Step 6.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommand.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfIndexingCommandHandler.cs
git add apps/api/src/Api/Routing/Pdf/PdfProcessingEndpoints.cs
git commit -m "feat(docs): add retry indexing endpoint for failed PDF processing"
```

---

### Task 7: Integration Test — Flusso Completo Private Game + RAG

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/PrivateGameRagFlowIntegrationTests.cs`

- [ ] **Step 7.1: Scrivi il test di integrazione**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/PrivateGameRagFlowIntegrationTests.cs

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

/// <summary>
/// Integration test for the private game RAG flow.
/// Verifies: thread creation with privateGameId → KB status returns correct state.
/// Uses InMemory DB (no real vector search — covers the plumbing).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PrivateGameRagFlowIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;

    public PrivateGameRagFlowIntegrationTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetKbStatus_AfterPdfIndexed_ReturnsCompleted_ForPrivateGame()
    {
        // Arrange: private game with a Ready PDF
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            OwnerId = userId,
            Title = "Oath Test",
            MinPlayers = 1,
            MaxPlayers = 6,
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = privateGameId,
            GameId = null,
            FileName = "oath_rulebook.pdf",
            ProcessingState = "Ready",
            IsActiveForRag = true,
            UploadedAt = DateTime.UtcNow,
            FileSize = 4096
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetKnowledgeBaseStatusQueryHandler(
            _dbContext,
            NullLogger<GetKnowledgeBaseStatusQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetKnowledgeBaseStatusQuery(privateGameId, IsPrivateGame: true),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
        result.Progress.Should().Be(100);
        result.GameName.Should().Be("Oath Test");
    }

    [Fact]
    public async Task CreateChatThread_WithPrivateGameId_StoresEffectiveGameId()
    {
        // Arrange
        var privateGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // We need real IChatThreadRepository — use InMemory DB backed repo
        // Skip if infrastructure repo not injectable in unit scope; use mock instead
        var mockRepo = new Moq.Mock<IChatThreadRepository>();
        Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread? captured = null;
        mockRepo.Setup(r => r.AddAsync(Moq.It.IsAny<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var mockUow = new Moq.Mock<IUnitOfWork>();
        mockUow.Setup(u => u.SaveChangesAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateChatThreadCommandHandler(mockRepo.Object, mockUow.Object);

        // Act
        var result = await handler.Handle(
            new CreateChatThreadCommand(UserId: userId, PrivateGameId: privateGameId),
            CancellationToken.None);

        // Assert: PrivateGameId is stored as GameId on the thread (for RAG search scoping)
        result.Should().NotBeNull();
        result.GameId.Should().Be(privateGameId);
        captured.Should().NotBeNull();
        captured!.GameId.Should().Be(privateGameId);
    }
}
```

- [ ] **Step 7.2: Esegui i test**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~PrivateGameRagFlowIntegrationTests" -v minimal
```

Atteso: 2/2 PASS.

- [ ] **Step 7.3: Esegui la suite completa KnowledgeBase**

```bash
cd apps/api
dotnet test --filter "BoundedContext=KnowledgeBase" -v minimal
```

Atteso: tutti i test pre-esistenti continuano a passare.

- [ ] **Step 7.4: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/PrivateGameRagFlowIntegrationTests.cs
git commit -m "test(kb): add integration tests for private game RAG flow"
```

---

### Task 8: BC Decoupling — IKnowledgeBaseIndexingService (P2 Architecture)

**Problema:** `IndexPdfCommandHandler` importa direttamente tipi da `KnowledgeBase.Infrastructure.Entities` — coupling tra BC che rende fragile la modifica del modello KB.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IKnowledgeBaseIndexingService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/KnowledgeBaseIndexingService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs`

- [ ] **Step 8.1: Definisci l'interfaccia nel BC DocumentProcessing**

```csharp
// File: apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IKnowledgeBaseIndexingService.cs

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Service interface for indexing PDF content into the KnowledgeBase vector store.
/// Defined in DocumentProcessing BC to decouple it from KnowledgeBase internals.
/// </summary>
internal interface IKnowledgeBaseIndexingService
{
    /// <summary>
    /// Creates or updates the VectorDocument entry in the KB for a given PDF.
    /// </summary>
    Task UpsertVectorDocumentAsync(
        Guid pdfDocumentId,
        Guid effectiveGameId,
        string language,
        int totalChunks,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 8.2: Implementa il servizio nel BC KnowledgeBase**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/KnowledgeBaseIndexingService.cs

using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// KnowledgeBase implementation of IKnowledgeBaseIndexingService.
/// Registered via DI so DocumentProcessing does not reference KB entities directly.
/// </summary>
internal sealed class KnowledgeBaseIndexingService : IKnowledgeBaseIndexingService
{
    private readonly MeepleAiDbContext _dbContext;

    public KnowledgeBaseIndexingService(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task UpsertVectorDocumentAsync(
        Guid pdfDocumentId,
        Guid effectiveGameId,
        string language,
        int totalChunks,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.VectorDocuments
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _dbContext.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfDocumentId,
                GameId = effectiveGameId,
                Language = language,
                TotalChunks = totalChunks,
                IndexingStatus = "completed",
                IndexedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.TotalChunks = totalChunks;
            existing.Language = language;
            existing.IndexingStatus = "completed";
            existing.IndexedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 8.3: Registra il servizio nella DI**

Nel file di `DependencyInjection` del BC KnowledgeBase (cerca `AddKnowledgeBase` o `ServiceCollectionExtensions`):

```csharp
services.AddScoped<IKnowledgeBaseIndexingService, KnowledgeBaseIndexingService>();
```

- [ ] **Step 8.4: Sostituisci il direct import in IndexPdfCommandHandler**

Inietta `IKnowledgeBaseIndexingService` nel costruttore di `IndexPdfCommandHandler` e sostituisci la chiamata diretta a `UpdateOrCreateVectorDocumentAsync` con `_kbIndexingService.UpsertVectorDocumentAsync(...)`.

Rimuovi i `using` che puntano direttamente a `Api.BoundedContexts.KnowledgeBase.Infrastructure.Entities`.

- [ ] **Step 8.5: Build e test**

```bash
cd apps/api
dotnet build --no-incremental -warnaserror
dotnet test --filter "BoundedContext=DocumentProcessing" -v minimal
```

- [ ] **Step 8.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IKnowledgeBaseIndexingService.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/KnowledgeBaseIndexingService.cs
git commit -m "refactor(docs): decouple IndexPdfCommandHandler from KnowledgeBase entities via IKnowledgeBaseIndexingService"
```

---

## SPRINT 4 — P3: Enhancements

---

### Task 9: Notifica Utente al Completamento Processing

**Obiettivo:** Quando `ProcessingState` diventa `"Ready"`, notificare l'utente (UserNotifications BC).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/NotifyUserOnPdfReadyHandler.cs`

**Skeleton dell'handler:**

```csharp
// Ascolta VectorDocumentReadyIntegrationEvent (già pubblicato da VectorDocumentIndexedEventHandler)
// Dispatcha un comando al BC UserNotifications per creare una notifica in-app
// Payload: "Il rulebook '{fileName}' è stato elaborato. Puoi iniziare la chat."

internal sealed class NotifyUserOnPdfReadyHandler : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    // Inietta IMediator, ILogger
    // Handle: send CreateNotificationCommand(userId, title, message, type: "PdfReady")
}
```

Implementa seguendo il pattern di `RagBackupOnIndexedEventHandler.cs`.

---

### Task 10: PDF Quality Score

**Obiettivo:** Dopo l'indicizzazione, calcolare uno score di qualità del contenuto testuale (es. testo estratto / pagine totali) e salvarlo sul `PdfDocument`.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/IPdfQualityScorer.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfQualityScorer.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs`

**Logica dello scorer:**
- Input: lista chunk, numero pagine totali
- Output: score 0.0–1.0 (ratio testo estratto significativo / pagine)
- Se score < 0.3: aggiungi warning `"LowQuality"` al `ProcessingProgressJson`
- Nessuna modifica allo stato di processing — solo metadata

---

### Task 11: DocumentCollection per Giochi Privati

**Obiettivo:** Permettere di raggruppare più PDF (es. "regolamento base + espansione") in una `DocumentCollection` associata a un gioco privato.

**Files da esplorare prima di implementare:**
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CreateDocumentCollectionCommand.cs`
- Endpoint: `POST /api/v1/games/{gameId}/document-collections`

**Gap attuale:** L'endpoint usa `gameId` (shared game). Aggiungere supporto per `privateGameId` via:
- Nuovo endpoint: `POST /api/v1/private-games/{id}/document-collections`
- Oppure: aggiungere `privateGameId` opzionale all'endpoint esistente

---

### Task 12: Suggerimenti Automatici di Domande

**Obiettivo:** Quando si apre un chat thread su un private game appena indicizzato, suggerire domande predefinite basate sul contenuto (es. "Come si fa il setup?", "Quanti giocatori sono supportati?").

**Approccio suggerito:**
- Aggiungere `SuggestedQuestionsDto[]` a `ChatThreadDto` (popolato al momento della creazione se il gioco ha un PDF indexato)
- In `CreateChatThreadCommandHandler`: se `effectiveGameId` ha VectorDocuments, generare 3 domande standard via `AskQuestionQuery` (o un prompt LLM dedicato) e includerle nella risposta

---

## Self-Review

### Spec Coverage Check

| Requisito Spec | Task che lo implementa |
|----------------|------------------------|
| P0: CreateChatThreadCommandHandler mancante | Task 1 |
| P0: GetKnowledgeBaseStatus non funziona per private games | Task 2 |
| P0: Endpoint `/private-games/{id}/kb-status` | Task 2 (step 2.6) |
| P1: Auto-enable IsActiveForRag | Task 3 |
| P1: Frontend progress bar processing | Task 4 |
| P1: Pagina gioco privato con PDF + chat | Task 5 |
| P2: Retry per PDF falliti | Task 6 |
| P2: Integration test flusso completo | Task 7 |
| P2: BC decoupling IndexPdf → KB | Task 8 |
| P3: Notifica completamento | Task 9 |
| P3: PDF quality score | Task 10 |
| P3: DocumentCollection per private games | Task 11 |
| P3: Auto-suggested questions | Task 12 |

### Placeholder Scan

Nessun "TBD" o "TODO" nei task P0–P2.
Task 9–12 (P3) hanno skeleton code e approach definito — richiedono esplorazione aggiuntiva prima dell'implementazione.

### Type Consistency

- `GetKnowledgeBaseStatusQuery(Guid GameId, bool IsPrivateGame = false)` — usato consistentemente in Task 2 e Task 7
- `CreateChatThreadCommand(... Guid? PrivateGameId = null ...)` — usato in Task 1, step 1.3/1.4/1.5
- `ChatThreadDto(... Guid? GameId ...)` — `GameId` contiene `effectiveGameId = PrivateGameId ?? GameId` nel handler. Il DTO non espone `PrivateGameId` separatamente — questo è intenzionale per retrocompatibilità con i consumer esistenti.
- `PdfDocumentEntity.PrivateGameId` (Guid?) — usato correttamente in Task 2 e Task 7

### Rischi Residui

1. `ResetForRetry()` su `PdfDocument` domain entity (Task 6): se il metodo non esiste, va aggiunto con cura verso l'enum `ProcessingStateEnum` effettivo (verificare prima).
2. `PrivateGameEntity` in DbContext è `PrivateGameEntity` (infra) non `PrivateGame` (domain) — le query nel handler usano correttamente `_dbContext.PrivateGames` (DbSet infra).
3. Task 5 (pagina frontend): la route esatta di `/private-games/[id]` potrebbe già esistere — verificare prima con `find` per non sovrascrivere.

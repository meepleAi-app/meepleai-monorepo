# RemoveRagFromSharedGame Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin-only saga to fully remove a PDF from a shared game with multi-system cleanup (DB + Qdrant + blob storage).

**Architecture:** New `RemoveRagFromSharedGameCommand` saga handler orchestrates two existing commands in sequence: `RemoveDocumentFromSharedGameCommand` (unlink) then `DeletePdfCommand` (full cleanup). Auto-promotes next version if removing active document. Follows the same saga pattern as existing `AddRagToSharedGameCommand`.

**Tech Stack:** .NET 9, MediatR, EF Core, xUnit + Moq

**Spec:** `docs/superpowers/specs/2026-03-12-remove-rag-from-shared-game-design.md`

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommand.cs` | Command + Result records |
| Create | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandler.cs` | Saga orchestration |
| Modify | `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs` | New DELETE endpoint |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandlerTests.cs` | Unit tests |

---

## Chunk 1: Command, Handler & Endpoint

### Task 1: Create Command + Result Records

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommand.cs`

- [ ] **Step 1: Create the command and result records**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

/// <summary>
/// Saga command that removes a PDF from a SharedGame with full multi-system cleanup:
/// 1. Handle active version promotion
/// 2. Remove SharedGameDocument link
/// 3. Delete PdfDocument (cascades VectorDoc, TextChunks, Qdrant, blob)
/// </summary>
internal record RemoveRagFromSharedGameCommand(
    Guid SharedGameId,
    Guid SharedGameDocumentId,
    Guid UserId
) : ICommand<RemoveRagFromSharedGameResult>;

/// <summary>
/// Result of the removal saga. Cleanup flags indicate best-effort external system status.
/// </summary>
internal record RemoveRagFromSharedGameResult(
    Guid RemovedSharedGameDocumentId,
    Guid RemovedPdfDocumentId,
    bool QdrantCleanupSucceeded,
    bool BlobCleanupSucceeded
);
```

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommand.cs
git commit -m "feat(shared-game): add RemoveRagFromSharedGame command and result records"
```

---

### Task 2: Create Saga Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandler.cs`

**Reference patterns:**
- `AddRagToSharedGameCommandHandler.cs` — same saga pattern (IMediator delegation)
- `RemoveDocumentFromSharedGameCommandHandler.cs` — existing unlink logic
- `DeletePdfCommandHandler.cs` — existing full-cleanup logic

- [ ] **Step 1: Write the saga handler**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

/// <summary>
/// Saga handler that orchestrates full PDF removal from a SharedGame:
/// 1. Validate SharedGameDocument exists and belongs to game
/// 2. Resolve PdfDocumentId
/// 3. Auto-promote next version if removing active document
/// 4. Remove SharedGameDocument link (via RemoveDocumentFromSharedGameCommand)
/// 5. Delete PDF with full cleanup (via DeletePdfCommand — cascades VectorDoc, TextChunks, Qdrant, blob)
/// </summary>
internal sealed class RemoveRagFromSharedGameCommandHandler
    : ICommandHandler<RemoveRagFromSharedGameCommand, RemoveRagFromSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly ILogger<RemoveRagFromSharedGameCommandHandler> _logger;

    public RemoveRagFromSharedGameCommandHandler(
        IMediator mediator,
        ISharedGameDocumentRepository documentRepository,
        ILogger<RemoveRagFromSharedGameCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RemoveRagFromSharedGameResult> Handle(
        RemoveRagFromSharedGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting RAG removal saga for SharedGame {SharedGameId}, Document {DocumentId}, User {UserId}",
            command.SharedGameId, command.SharedGameDocumentId, command.UserId);

        // Step 1: Validate document exists and belongs to game
        var document = await _documentRepository.GetByIdAsync(
            command.SharedGameDocumentId, cancellationToken).ConfigureAwait(false);

        if (document is null || document.SharedGameId != command.SharedGameId)
        {
            throw new NotFoundException("SharedGameDocument", command.SharedGameDocumentId.ToString());
        }

        var pdfDocumentId = document.PdfDocumentId;

        // Step 2: Auto-promote next version if removing active document
        if (document.IsActive)
        {
            var sameTypeVersions = await _documentRepository.GetBySharedGameIdAndTypeAsync(
                command.SharedGameId, document.DocumentType, cancellationToken).ConfigureAwait(false);

            var nextVersion = sameTypeVersions
                .Where(d => d.Id != command.SharedGameDocumentId)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

            if (nextVersion is not null)
            {
                nextVersion.SetAsActive();
                _documentRepository.Update(nextVersion);

                _logger.LogInformation(
                    "Auto-promoted document {NextDocId} (v{Version}) to active for {DocType}",
                    nextVersion.Id, nextVersion.Version, document.DocumentType);
            }
        }

        // Step 3: Remove SharedGameDocument link
        await _mediator.Send(
            new RemoveDocumentFromSharedGameCommand(command.SharedGameId, command.SharedGameDocumentId),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "SharedGameDocument {DocumentId} unlinked from game {SharedGameId}",
            command.SharedGameDocumentId, command.SharedGameId);

        // Step 4: Delete PDF with full cleanup (best-effort for external systems)
        bool qdrantOk = true;
        bool blobOk = true;

        try
        {
            var deleteResult = await _mediator.Send(
                new DeletePdfCommand(pdfDocumentId.ToString()),
                cancellationToken).ConfigureAwait(false);

            if (!deleteResult.Success)
            {
                _logger.LogWarning(
                    "PDF deletion reported failure for {PdfId}: {Message}",
                    pdfDocumentId, deleteResult.Message);
                qdrantOk = false;
                blobOk = false;
            }
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete PDF {PdfId} (graceful degradation — SharedGameDocument already removed)",
                pdfDocumentId);
            qdrantOk = false;
            blobOk = false;
        }

        _logger.LogInformation(
            "RAG removal saga completed: SharedGameDocument={DocId}, PDF={PdfId}, Qdrant={Qdrant}, Blob={Blob}",
            command.SharedGameDocumentId, pdfDocumentId, qdrantOk, blobOk);

        return new RemoveRagFromSharedGameResult(
            RemovedSharedGameDocumentId: command.SharedGameDocumentId,
            RemovedPdfDocumentId: pdfDocumentId,
            QdrantCleanupSucceeded: qdrantOk,
            BlobCleanupSucceeded: blobOk);
    }
}
```

**Key design decisions:**
- Auto-promotion happens BEFORE unlink to avoid the `RemoveDocumentFromSharedGameCommandHandler` blocking active version deletion when alternatives exist (its line 54-65 throws if active with >1 versions)
- `DeletePdfCommand` failures are caught and logged — the SharedGameDocument link is already removed, so the orphaned PDF can be cleaned up later
- Uses `NotFoundException` (project convention for 404) not `InvalidOperationException`

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandler.cs
git commit -m "feat(shared-game): add RemoveRagFromSharedGame saga handler"
```

---

### Task 3: Add DELETE Endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`

**Important context:**
- Existing endpoint at line 407: `DELETE /admin/shared-games/{id}/documents/{documentId}` — unlink only (Admin/Editor)
- New endpoint: `DELETE /admin/shared-games/{id}/documents/{documentId}/full` — full cleanup (Admin only)
- The endpoint handler pattern: see `HandleRemoveDocument` at line 1512 and `HandleAddRagToSharedGame` at line 2855

- [ ] **Step 1: Add endpoint registration**

Find the block near line 407 (after `HandleRemoveDocument` registration) and add:

```csharp
        group.MapDelete("/admin/shared-games/{id:guid}/documents/{documentId:guid}/full", HandleRemoveRagFromSharedGame)
            .RequireAuthorization("AdminPolicy")
            .WithName("RemoveRagFromSharedGame")
            .WithSummary("Remove document with full cleanup — PDF, vectors, blob (Admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
```

Insert after the existing `MapDelete` for `HandleRemoveDocument` (line 411) and before the Agent Linking section (line 413).

- [ ] **Step 2: Add handler method**

Add near line 1530 (after `HandleRemoveDocument`):

```csharp
    private static async Task<IResult> HandleRemoveRagFromSharedGame(
        Guid id,
        Guid documentId,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new RemoveRagFromSharedGame.RemoveRagFromSharedGameCommand(
            SharedGameId: id,
            SharedGameDocumentId: documentId,
            UserId: session!.User!.Id);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
```

**Note:** Uses `RequireAdminSession()` (Admin only), NOT `RequireAdminOrEditorSession()`. The NotFoundException thrown by the handler will be caught by the global exception middleware and return 404.

- [ ] **Step 3: Add the using directive**

Add to the top of `SharedGameCatalogEndpoints.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;
```

Only needed if the fully-qualified `RemoveRagFromSharedGame.RemoveRagFromSharedGameCommand` causes ambiguity. Check if other `using` statements already import the parent namespace.

- [ ] **Step 4: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs
git commit -m "feat(shared-game): add DELETE /full endpoint for admin PDF removal"
```

---

## Chunk 2: Unit Tests

### Task 4: Create Unit Tests

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandlerTests.cs`

**Reference pattern:** `AddRagToSharedGameCommandHandlerTests.cs` — same mock-based approach with `IMediator`, repository mocks

- [ ] **Step 1: Write the test file**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

[Trait("Category", TestCategories.Unit)]
public class RemoveRagFromSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ISharedGameDocumentRepository> _documentRepositoryMock;
    private readonly Mock<ILogger<RemoveRagFromSharedGameCommandHandler>> _loggerMock;
    private readonly RemoveRagFromSharedGameCommandHandler _handler;

    private static readonly Guid SharedGameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PdfDocumentId = Guid.NewGuid();

    public RemoveRagFromSharedGameCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _documentRepositoryMock = new Mock<ISharedGameDocumentRepository>();
        _loggerMock = new Mock<ILogger<RemoveRagFromSharedGameCommandHandler>>();
        _handler = new RemoveRagFromSharedGameCommandHandler(
            _mediatorMock.Object,
            _documentRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static SharedGameDocument CreateDocument(
        Guid? id = null,
        Guid? sharedGameId = null,
        Guid? pdfDocumentId = null,
        SharedGameDocumentType type = SharedGameDocumentType.Rulebook,
        string version = "1.0",
        bool isActive = false)
    {
        return new SharedGameDocument(
            id: id ?? Guid.NewGuid(),
            sharedGameId: sharedGameId ?? SharedGameId,
            pdfDocumentId: pdfDocumentId ?? PdfDocumentId,
            documentType: type,
            version: version,
            isActive: isActive,
            tags: null,
            createdAt: DateTime.UtcNow,
            createdBy: UserId);
    }

    private void SetupDeletePdfSuccess()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDeleteResult(true, "Deleted", null));
    }

    private void SetupRemoveDocumentSuccess()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveDocumentFromSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ========== Happy Path ==========

    [Fact]
    public async Task Handle_HappyPath_RemovesDocumentAndDeletesPdf()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId, pdfDocumentId: PdfDocumentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        SetupRemoveDocumentSuccess();
        SetupDeletePdfSuccess();

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.RemovedSharedGameDocumentId.Should().Be(docId);
        result.RemovedPdfDocumentId.Should().Be(PdfDocumentId);
        result.QdrantCleanupSucceeded.Should().BeTrue();
        result.BlobCleanupSucceeded.Should().BeTrue();

        _mediatorMock.Verify(
            m => m.Send(
                It.Is<RemoveDocumentFromSharedGameCommand>(c =>
                    c.SharedGameId == SharedGameId && c.DocumentId == docId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Send(
                It.Is<DeletePdfCommand>(c => c.PdfId == PdfDocumentId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ========== Not Found ==========

    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DocumentBelongsToDifferentGame_ThrowsNotFoundException()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId, sharedGameId: Guid.NewGuid()); // different game

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    // ========== Active Version Promotion ==========

    [Fact]
    public async Task Handle_RemovingActiveVersion_AutoPromotesNextVersion()
    {
        // Arrange
        var activeDocId = Guid.NewGuid();
        var activeDoc = CreateDocument(id: activeDocId, version: "1.0", isActive: true);

        var nextDoc = CreateDocument(version: "2.0", isActive: false);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(activeDocId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDoc);

        _documentRepositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(
                SharedGameId, SharedGameDocumentType.Rulebook, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { activeDoc, nextDoc });

        SetupRemoveDocumentSuccess();
        SetupDeletePdfSuccess();

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, activeDocId, UserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        nextDoc.IsActive.Should().BeTrue();
        _documentRepositoryMock.Verify(r => r.Update(nextDoc), Times.Once);
    }

    [Fact]
    public async Task Handle_RemovingLastActiveVersion_NoPromotion()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId, version: "1.0", isActive: true);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        _documentRepositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(
                SharedGameId, SharedGameDocumentType.Rulebook, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { doc }); // only this one

        SetupRemoveDocumentSuccess();
        SetupDeletePdfSuccess();

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.RemovedSharedGameDocumentId.Should().Be(docId);
        _documentRepositoryMock.Verify(r => r.Update(It.IsAny<SharedGameDocument>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RemovingInactiveVersion_NoPromotion()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId, isActive: false);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        SetupRemoveDocumentSuccess();
        SetupDeletePdfSuccess();

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: no version promotion queries needed
        _documentRepositoryMock.Verify(
            r => r.GetBySharedGameIdAndTypeAsync(It.IsAny<Guid>(), It.IsAny<SharedGameDocumentType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ========== Graceful Degradation ==========

    [Fact]
    public async Task Handle_DeletePdfFails_ReturnsResultWithCleanupFailed()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        SetupRemoveDocumentSuccess();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDeleteResult(false, "Qdrant timeout", null));

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: saga succeeds overall, but reports cleanup failure
        result.RemovedSharedGameDocumentId.Should().Be(docId);
        result.QdrantCleanupSucceeded.Should().BeFalse();
        result.BlobCleanupSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeletePdfThrows_ReturnsResultWithCleanupFailed()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var doc = CreateDocument(id: docId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        SetupRemoveDocumentSuccess();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: graceful degradation — saga still returns result
        result.RemovedSharedGameDocumentId.Should().Be(docId);
        result.QdrantCleanupSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Verify tests compile**

Run: `cd apps/api && dotnet build --no-restore tests/Api.Tests/Api.Tests.csproj 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Run the tests**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RemoveRagFromSharedGame" --no-build -v minimal`
Expected: 7 tests passed

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/
git commit -m "test(shared-game): add RemoveRagFromSharedGame saga handler tests"
```

---

## Chunk 3: Integration Verification

### Task 5: Verify Existing Tests Still Pass

- [ ] **Step 1: Run all SharedGameCatalog tests**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SharedGameCatalog" --no-build -v minimal`
Expected: All tests pass (including existing AddRag, RemoveDocument tests)

- [ ] **Step 2: Run DocumentProcessing tests (DeletePdf)**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePdf" --no-build -v minimal`
Expected: All tests pass

- [ ] **Step 3: Final full build check**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | tail -3`
Expected: Build succeeded with 0 errors

---

## Notes for Implementer

### Active Version Race Condition

The handler does auto-promotion BEFORE calling `RemoveDocumentFromSharedGameCommand`. This is because the existing `RemoveDocumentFromSharedGameCommandHandler` (line 54-65) throws `InvalidOperationException("Cannot delete active version. Set another version as active first.")` when the document is active AND other versions exist. By deactivating the removed doc and promoting the next one first, we avoid this blocker.

However, the existing handler's check at line 61 is `if (otherVersions.Count > 1)` — it only blocks if there are OTHER versions. If there's only one version (the active one being removed), it allows deletion. So for the "last document" case, no pre-promotion is needed.

### RemoveDocumentFromSharedGameCommand Re-Use

The saga delegates to this existing command for the actual unlink. This means:
- The `SharedGameDocumentRemovedEvent` domain event is automatically published
- The repository's `Remove()` + `SaveChangesAsync()` is handled
- We don't duplicate the validation/removal logic

### DeletePdfCommand Re-Use

The saga delegates to this existing command for PDF cleanup. This means:
- VectorDocument is deleted from DB
- Qdrant vectors are deleted (best-effort)
- Blob file is deleted (best-effort)
- TextChunks are cascade-deleted by EF Core
- Cache is invalidated

### FK Constraint: Restrict on SharedGameDocument → PdfDocument

The EF Core configuration uses `DeleteBehavior.Restrict` on the PdfDocument FK in SharedGameDocumentEntityConfiguration. This means we MUST remove the SharedGameDocument BEFORE deleting the PdfDocument, otherwise the delete will fail with a FK violation. The saga enforces this order.

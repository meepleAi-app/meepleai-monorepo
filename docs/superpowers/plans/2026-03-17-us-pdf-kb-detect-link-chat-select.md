# Upload PDF → Detect Existing KB → Link → Chat with KB Selection

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user uploads a PDF for their game, detect if the same content already exists (in their own uploads OR in SharedGameCatalog) and offer to link the existing KB instead of re-processing. Additionally, allow users to select which KB(s) to use when creating a chat.

**Architecture:** Modify `UploadPdfCommandHandler` duplicate detection from blocking (409) to informational (200 + metadata). Add `LinkExistingKbToGameCommand` to create VectorDocument clones. Extend `CreateChatThreadCommand` with optional KB selection. All changes follow existing CQRS + MediatR patterns.

**Tech Stack:** .NET 9, EF Core, PostgreSQL, MediatR, FluentValidation

---

## Scope

Three independent subsystems, each producing working software:

1. **Detect & Inform** — Modify PDF upload to detect existing KB and return metadata instead of 409
2. **Link KB to Game** — New command to create VectorDocument clone for a different GameId
3. **Chat KB Selection** — Extend chat thread creation with optional VectorDocument ID list

---

## File Structure

### Subsystem 1: Detect & Inform

| File | Action | Responsibility |
|------|--------|---------------|
| `DocumentProcessing/Application/DTOs/PdfUploadResult.cs` | Modify | Add `ExistingKbInfo` to result |
| `DocumentProcessing/Application/DTOs/ExistingKbInfoDto.cs` | Create | DTO for existing KB metadata |
| `DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs` | Modify | Change duplicate check from 409 to informational |
| `tests/Api.Tests/DocumentProcessing/UploadPdfDuplicateDetectionTests.cs` | Create | Unit tests for new detection logic |

### Subsystem 2: Link KB to Game

| File | Action | Responsibility |
|------|--------|---------------|
| `KnowledgeBase/Application/Commands/LinkExistingKbToGameCommand.cs` | Create | Command record |
| `KnowledgeBase/Application/Handlers/LinkExistingKbToGameCommandHandler.cs` | Create | Handler: validate + create VectorDocument clone |
| `KnowledgeBase/Application/Validators/LinkExistingKbToGameCommandValidator.cs` | Create | FluentValidation rules |
| `Routing/KnowledgeBaseEndpoints.cs` | Modify | Add POST endpoint |
| `tests/Api.Tests/KnowledgeBase/LinkExistingKbToGameTests.cs` | Create | Unit + integration tests |

### Subsystem 3: Chat KB Selection

| File | Action | Responsibility |
|------|--------|---------------|
| `KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs` | Modify | Add `SelectedKnowledgeBaseIds` |
| `KnowledgeBase/Application/Handlers/CreateChatThreadCommandHandler.cs` | Modify | Wire KB selection into ChatThread |
| `KnowledgeBase/Domain/Entities/ChatThread.cs` | Modify | Add `SelectedKnowledgeBaseIdsJson` property |
| `KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs` | Modify | Update MapToDomain + MapToPersistence mappers |
| `Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs` | Modify | Add `SelectedKnowledgeBaseIdsJson` column |
| `KnowledgeBase/Application/Services/IRagAccessService.cs` | Modify | Add overload with selectedIds filter |
| `KnowledgeBase/Infrastructure/Services/RagAccessService.cs` | Modify | Implement filtered method |
| `tests/Api.Tests/KnowledgeBase/ChatThreadKbSelectionTests.cs` | Create | Unit tests for KB selection |

### Key Property Names (Entity Reference)

| Entity | Property | Type | Notes |
|--------|----------|------|-------|
| `PdfDocumentEntity` | `UploadedByUserId` | `Guid` | NOT `UserId` |
| `PdfDocumentEntity` | `GameId` | `Guid?` | NOT `string` |
| `PdfDocumentEntity` | `ProcessingState` | `string` | No `ProgressPercentage` — derive from state |
| `VectorDocumentEntity` | `ChunkCount` | `int` | Domain uses `TotalChunks` |
| `VectorDocumentEntity` | `IndexingStatus` | `string` | "pending"/"completed"/"failed" |
| `ForbiddenException` | namespace | `Api.Middleware.Exceptions` | NOT `Api.SharedKernel.Exceptions` |

---

## Task 1: ExistingKbInfoDto

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/ExistingKbInfoDto.cs`

- [ ] **Step 1: Create the DTO**

```csharp
namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Metadata returned when a PDF with matching content hash already exists.
/// </summary>
internal record ExistingKbInfoDto(
    Guid PdfDocumentId,
    string Source,              // "user" | "shared"
    string FileName,
    string ProcessingState,     // "Ready" | "Embedding" | etc.
    int? TotalChunks,           // null if still processing
    string? OriginalGameName,
    Guid? SharedGameId
);
```

- [ ] **Step 2: Run build to verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/ExistingKbInfoDto.cs
git commit -m "feat(doc-processing): add ExistingKbInfoDto for duplicate detection response"
```

---

## Task 2: Extend PdfUploadResult

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PdfUploadResult.cs`

- [ ] **Step 1: Write the failing test**

Create: `tests/Api.Tests/DocumentProcessing/PdfUploadResultTests.cs`

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;

namespace Api.Tests.DocumentProcessing;

[Category("Unit")]
public class PdfUploadResultTests
{
    [Fact]
    public void PdfUploadResult_WithExistingKbInfo_ShouldHaveKbData()
    {
        var kbInfo = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "catan-rules.pdf",
            ProcessingState: "Ready",
            TotalChunks: 108,
            OriginalGameName: "Catan",
            SharedGameId: Guid.NewGuid());

        var result = new PdfUploadResult(true, "Existing KB found", null, kbInfo);

        Assert.True(result.Success);
        Assert.NotNull(result.ExistingKb);
        Assert.Equal("shared", result.ExistingKb.Source);
        Assert.Equal(108, result.ExistingKb.TotalChunks);
    }

    [Fact]
    public void PdfUploadResult_WithoutExistingKb_ShouldHaveNullKbInfo()
    {
        var result = new PdfUploadResult(true, "Upload OK", null);

        Assert.Null(result.ExistingKb);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~PdfUploadResultTests" -v minimal`
Expected: FAIL — `PdfUploadResult` doesn't accept 4 params yet

- [ ] **Step 3: Modify PdfUploadResult to add optional ExistingKbInfo**

File: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PdfUploadResult.cs`

```csharp
namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF upload operation.
/// </summary>
internal record PdfUploadResult(
    bool Success,
    string Message,
    PdfDocumentDto? Document,
    ExistingKbInfoDto? ExistingKb = null);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~PdfUploadResultTests" -v minimal`
Expected: PASS (2 tests)

- [ ] **Step 5: Run full build to check no regressions**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded (existing callers use 3-param constructor, backward compatible)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PdfUploadResult.cs \
       tests/Api.Tests/DocumentProcessing/PdfUploadResultTests.cs
git commit -m "feat(doc-processing): extend PdfUploadResult with optional ExistingKbInfo"
```

---

## Task 3: Modify Duplicate Detection in UploadPdfCommandHandler

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`

**Context:** The current handler at lines 159-166 does a global `AnyAsync` on ContentHash and returns 409 on match. We need to:
1. Check user-scoped first (`p.UserId == userId`)
2. Check shared-game-scoped (`p.SharedGameId != null`)
3. Return 200 with `ExistingKbInfo` instead of 409

- [ ] **Step 1: Write the failing test**

Create: `tests/Api.Tests/DocumentProcessing/UploadPdfDuplicateDetectionTests.cs`

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;

namespace Api.Tests.DocumentProcessing;

[Category("Unit")]
public class UploadPdfDuplicateDetectionTests
{
    [Fact]
    public void ExistingKbInfoDto_UserSource_ShouldBeValid()
    {
        var dto = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "user",
            FileName: "rules.pdf",
            ProcessingState: "Ready",
            TotalChunks: 50,
            OriginalGameName: "My Game",
            SharedGameId: null);

        Assert.Equal("user", dto.Source);
        Assert.Null(dto.SharedGameId);
    }

    [Fact]
    public void ExistingKbInfoDto_SharedSource_ShouldHaveSharedGameId()
    {
        var sharedId = Guid.NewGuid();
        var dto = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "shared-rules.pdf",
            ProcessingState: "Embedding",
            TotalChunks: null,
            OriginalGameName: "Community Game",
            SharedGameId: sharedId);

        Assert.Equal("shared", dto.Source);
        Assert.Equal(sharedId, dto.SharedGameId);
        Assert.Null(dto.TotalChunks);
    }
}
```

- [ ] **Step 2: Run test to verify it passes** (DTO already created in Task 1)

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~UploadPdfDuplicateDetectionTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Modify the handler — replace global check with scoped check**

File: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`

Replace the duplicate check block (around lines 159-166) with:

```csharp
// Compute content hash
var contentHash = await ComputeContentHashAsync(file, cancellationToken).ConfigureAwait(false);

// Check 1: User's own PDFs with same content (any game)
// NOTE: PdfDocumentEntity uses UploadedByUserId (Guid), NOT UserId
// NOTE: GameId is Guid?, NOT string — direct comparison, no .ToString()
// NOTE: No ProgressPercentage on entity — derive from ProcessingState
var userMatch = await _db.PdfDocuments
    .AsNoTracking()
    .Where(p => p.ContentHash == contentHash && p.UploadedByUserId == userId)
    .Select(p => new { p.Id, p.FileName, p.ProcessingState, p.SharedGameId,
        GameName = _db.Games.Where(g => g.Id == p.GameId).Select(g => g.Title).FirstOrDefault(),
        TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
    })
    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

if (userMatch != null)
{
    var kbInfo = new ExistingKbInfoDto(
        PdfDocumentId: userMatch.Id,
        Source: "user",
        FileName: userMatch.FileName,
        ProcessingState: userMatch.ProcessingState ?? "Pending",
        TotalChunks: userMatch.TotalChunks,
        OriginalGameName: userMatch.GameName,
        SharedGameId: null);
    return new PdfUploadResult(true, "Existing KB found in your uploads", null, kbInfo);
}

// Check 2: SharedGameCatalog PDFs with same content
var sharedMatch = await _db.PdfDocuments
    .AsNoTracking()
    .Where(p => p.ContentHash == contentHash && p.SharedGameId != null)
    .Select(p => new { p.Id, p.FileName, p.ProcessingState, p.SharedGameId,
        GameName = _db.SharedGames.Where(sg => sg.Id == p.SharedGameId).Select(sg => sg.Title).FirstOrDefault(),
        TotalChunks = _db.VectorDocuments.Where(vd => vd.PdfDocumentId == p.Id).Select(vd => (int?)vd.ChunkCount).FirstOrDefault()
    })
    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

if (sharedMatch != null)
{
    var kbInfo = new ExistingKbInfoDto(
        PdfDocumentId: sharedMatch.Id,
        Source: "shared",
        FileName: sharedMatch.FileName,
        ProcessingState: sharedMatch.ProcessingState ?? "Pending",
        TotalChunks: sharedMatch.TotalChunks,
        OriginalGameName: sharedMatch.GameName,
        SharedGameId: sharedMatch.SharedGameId);
    return new PdfUploadResult(true, "Existing KB found in shared catalog", null, kbInfo);
}

// No match — proceed with standard upload
```

**Important:** Also apply the same pattern to the private game PDF path (around line 137-142). Replace the 409 block there too.

- [ ] **Step 4: Run build to verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Run existing upload tests to check no regressions**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~UploadPdf" -v minimal`
Expected: Any existing tests that expected 409 for duplicates will now fail — update them to expect `Success=true` with `ExistingKb != null`

- [ ] **Step 6: Fix broken tests**

Update any test asserting `result.Success == false` for duplicate content to instead assert `result.ExistingKb != null`.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs \
       tests/Api.Tests/DocumentProcessing/UploadPdfDuplicateDetectionTests.cs
git commit -m "feat(doc-processing): replace 409 duplicate block with informational ExistingKbInfo response"
```

---

## Task 4: Modify PdfEndpoints response for ExistingKb

**Files:**
- Modify: `apps/api/src/Api/Routing/PdfEndpoints.cs`

**Context:** The `HandleStandardUpload` method needs to return different HTTP status/body when `ExistingKb` is populated.

- [ ] **Step 1: Find and modify the upload response handling**

In `PdfEndpoints.cs`, locate the `HandleStandardUpload` response mapping. When `result.ExistingKb != null`, return:

```csharp
if (result.ExistingKb != null)
{
    return Results.Ok(new
    {
        existingKbFound = true,
        existingKb = result.ExistingKb,
        message = result.Message
    });
}
```

This ensures the frontend receives a 200 with KB metadata instead of 409.

- [ ] **Step 2: Run build**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/PdfEndpoints.cs
git commit -m "feat(routing): return existingKbFound response on duplicate PDF upload"
```

---

## Task 5: LinkExistingKbToGameCommand + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/LinkExistingKbToGameCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/LinkExistingKbToGameCommandValidator.cs`

- [ ] **Step 1: Create the command**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Links an existing KB (VectorDocument) to a different game by cloning the VectorDocument metadata.
/// The underlying pgvector embeddings are shared (same PdfDocumentId).
/// </summary>
internal record LinkExistingKbToGameCommand(
    Guid UserId,
    Guid TargetGameId,
    Guid SourcePdfDocumentId
) : ICommand<LinkKbResultDto>;
```

- [ ] **Step 2: Create the result DTO**

Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/LinkKbResultDto.cs`

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

internal record LinkKbResultDto(
    Guid VectorDocumentId,
    Guid GameId,
    Guid PdfDocumentId,
    string Status  // "linked" | "pending" (if source PDF still processing)
);
```

- [ ] **Step 3: Create the validator**

```csharp
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

internal class LinkExistingKbToGameCommandValidator
    : AbstractValidator<Commands.LinkExistingKbToGameCommand>
{
    public LinkExistingKbToGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.TargetGameId)
            .NotEmpty().WithMessage("TargetGameId is required");

        RuleFor(x => x.SourcePdfDocumentId)
            .NotEmpty().WithMessage("SourcePdfDocumentId is required");
    }
}
```

- [ ] **Step 4: Run build**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/LinkExistingKbToGameCommand.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/LinkKbResultDto.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/LinkExistingKbToGameCommandValidator.cs
git commit -m "feat(kb): add LinkExistingKbToGameCommand with validator"
```

---

## Task 6: LinkExistingKbToGameCommandHandler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/LinkExistingKbToGameCommandHandler.cs`
- Create: `tests/Api.Tests/KnowledgeBase/LinkExistingKbToGameTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;

namespace Api.Tests.KnowledgeBase;

[Category("Unit")]
public class LinkExistingKbToGameTests
{
    [Fact]
    public void LinkKbResultDto_Linked_ShouldHaveCorrectStatus()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "linked");

        Assert.Equal("linked", result.Status);
    }

    [Fact]
    public void LinkKbResultDto_Pending_ShouldIndicateProcessing()
    {
        var result = new LinkKbResultDto(
            VectorDocumentId: Guid.Empty,  // No VectorDocument yet
            GameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            Status: "pending");

        Assert.Equal("pending", result.Status);
        Assert.Equal(Guid.Empty, result.VectorDocumentId);
    }
}
```

- [ ] **Step 2: Run test to verify it passes** (DTO created in Task 5)

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~LinkExistingKbToGameTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Create the handler**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;  // ForbiddenException lives here, NOT SharedKernel
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler uses MeepleAiDbContext for read-only cross-BC queries (same pattern as RagAccessService).
/// Write operations go through IVectorDocumentRepository.
/// </summary>
internal class LinkExistingKbToGameCommandHandler
    : ICommandHandler<LinkExistingKbToGameCommand, LinkKbResultDto>
{
    private readonly MeepleAiDbContext _db;  // Read-only cross-BC queries (acceptable per RagAccessService pattern)
    private readonly IVectorDocumentRepository _vectorDocumentRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LinkExistingKbToGameCommandHandler> _logger;

    public LinkExistingKbToGameCommandHandler(
        MeepleAiDbContext db,
        IVectorDocumentRepository vectorDocumentRepo,
        IUnitOfWork unitOfWork,
        ILogger<LinkExistingKbToGameCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _vectorDocumentRepo = vectorDocumentRepo ?? throw new ArgumentNullException(nameof(vectorDocumentRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LinkKbResultDto> Handle(
        LinkExistingKbToGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validate source PDF exists and is accessible
        // NOTE: PdfDocumentEntity uses UploadedByUserId (Guid), NOT UserId
        var sourcePdf = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == command.SourcePdfDocumentId)
            .Select(p => new { p.Id, p.UploadedByUserId, p.SharedGameId, p.ProcessingState, p.FileName })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (sourcePdf == null)
            throw new NotFoundException($"PdfDocument {command.SourcePdfDocumentId} not found");

        // 2. Access check: user owns the PDF OR it's a SharedGame PDF
        // NOTE: UploadedByUserId is Guid — direct comparison, no .ToString()
        var isOwner = sourcePdf.UploadedByUserId == command.UserId;
        var isShared = sourcePdf.SharedGameId != null;
        if (!isOwner && !isShared)
            throw new ForbiddenException("Cannot link a PDF you don't own");

        // 3. Resolve target GameId (may be SharedGame ID → games.Id)
        var targetGameId = await _db.Games
            .AsNoTracking()
            .Where(g => g.Id == command.TargetGameId || g.SharedGameId == command.TargetGameId)
            .Select(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (targetGameId == Guid.Empty)
            throw new NotFoundException($"Game {command.TargetGameId} not found");

        // 4. Check if already linked (idempotency)
        var alreadyLinked = await _db.VectorDocuments
            .AnyAsync(vd => vd.GameId == targetGameId
                         && vd.PdfDocumentId == command.SourcePdfDocumentId,
                cancellationToken).ConfigureAwait(false);

        if (alreadyLinked)
        {
            var existingVd = await _db.VectorDocuments
                .Where(vd => vd.GameId == targetGameId && vd.PdfDocumentId == command.SourcePdfDocumentId)
                .Select(vd => vd.Id)
                .FirstAsync(cancellationToken).ConfigureAwait(false);

            return new LinkKbResultDto(existingVd, targetGameId, command.SourcePdfDocumentId, "linked");
        }

        // 5. Find source VectorDocument (if PDF is Ready)
        // NOTE: VectorDocumentEntity uses ChunkCount (int), domain uses TotalChunks
        var sourceVd = await _db.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.PdfDocumentId == command.SourcePdfDocumentId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (sourceVd == null)
        {
            // PDF exists but not yet indexed → return "pending"
            _logger.LogInformation(
                "PDF {PdfId} not yet indexed, link will be pending for game {GameId}",
                command.SourcePdfDocumentId, targetGameId);

            // Future: create a PendingKbLink event so that when processing completes,
            // the VectorDocument is auto-created for this game. For now, return pending.
            return new LinkKbResultDto(Guid.Empty, targetGameId, command.SourcePdfDocumentId, "pending");
        }

        // 6. Create VectorDocument clone for the target game
        // ChunkCount (infra) → totalChunks (domain constructor param)
        var clonedVd = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: targetGameId,
            pdfDocumentId: command.SourcePdfDocumentId,
            language: sourceVd.Language ?? "en",
            totalChunks: sourceVd.ChunkCount,
            sharedGameId: sourcePdf.SharedGameId);

        await _vectorDocumentRepo.AddAsync(clonedVd, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Linked KB {SourceVdId} → cloned as {ClonedVdId} for game {TargetGameId}",
            sourceVd.Id, clonedVd.Id, targetGameId);

        return new LinkKbResultDto(clonedVd.Id, targetGameId, command.SourcePdfDocumentId, "linked");
    }
}
```

- [ ] **Step 4: Run build**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded (fix property names if needed)

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/LinkExistingKbToGameCommandHandler.cs \
       tests/Api.Tests/KnowledgeBase/LinkExistingKbToGameTests.cs
git commit -m "feat(kb): add LinkExistingKbToGameCommandHandler with VectorDocument cloning"
```

---

## Task 7: Add Link KB Endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`

- [ ] **Step 1: Add the endpoint registration**

In `MapKnowledgeBaseEndpoints`, add a new call:

```csharp
MapLinkKbEndpoint(group);
```

Then add the method:

```csharp
private static void MapLinkKbEndpoint(RouteGroupBuilder group)
{
    group.MapPost("/games/{gameId:guid}/knowledge-base/link", HandleLinkKb)
        .WithName("LinkExistingKbToGame")
        .RequireSession()
        .WithTags("KnowledgeBase")
        .WithSummary("Link an existing KB to a game")
        .WithDescription("Creates a VectorDocument clone linking an existing processed PDF to a different game. The pgvector embeddings are shared.");
}

private static async Task<IResult> HandleLinkKb(
    Guid gameId,
    LinkKbRequest request,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct)
{
    var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

    var result = await mediator.Send(new LinkExistingKbToGameCommand(
        UserId: session.User!.Id,
        TargetGameId: gameId,
        SourcePdfDocumentId: request.PdfDocumentId), ct).ConfigureAwait(false);

    return result.Status == "linked"
        ? Results.Ok(result)
        : Results.Accepted(value: result);
}
```

Also add the request record (in same file or a separate DTO):

```csharp
internal record LinkKbRequest(Guid PdfDocumentId);
```

- [ ] **Step 2: Add the necessary using statements**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
```

- [ ] **Step 3: Run build**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs
git commit -m "feat(routing): add POST /games/{gameId}/knowledge-base/link endpoint"
```

---

## Task 8: Extend ChatThread with KB Selection

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThread.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs`

- [ ] **Step 1: Write the failing test**

Create: `tests/Api.Tests/KnowledgeBase/ChatThreadKbSelectionTests.cs`

```csharp
namespace Api.Tests.KnowledgeBase;

[Category("Unit")]
public class ChatThreadKbSelectionTests
{
    [Fact]
    public void CreateChatThreadCommand_WithSelectedKbs_ShouldContainIds()
    {
        var kbIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var command = new CreateChatThreadCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            SelectedKnowledgeBaseIds: kbIds);

        Assert.Equal(2, command.SelectedKnowledgeBaseIds!.Count);
    }

    [Fact]
    public void CreateChatThreadCommand_WithoutSelectedKbs_ShouldBeNull()
    {
        var command = new CreateChatThreadCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid());

        Assert.Null(command.SelectedKnowledgeBaseIds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ChatThreadKbSelectionTests" -v minimal`
Expected: FAIL — `CreateChatThreadCommand` doesn't have `SelectedKnowledgeBaseIds`

- [ ] **Step 3: Add SelectedKnowledgeBaseIds to CreateChatThreadCommand**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs`

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

internal record CreateChatThreadCommand(
    Guid UserId,
    Guid? GameId = null,
    string? Title = null,
    string? InitialMessage = null,
    Guid? AgentId = null,
    string? AgentType = null,
    string? UserRole = null,
    List<Guid>? SelectedKnowledgeBaseIds = null  // VectorDocument IDs to use for RAG
) : ICommand<ChatThreadDto>;
```

- [ ] **Step 4: Add SelectedKnowledgeBaseIdsJson to ChatThread domain entity**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThread.cs`

Add property after existing properties:

```csharp
/// <summary>
/// JSON-serialized list of VectorDocument IDs selected for RAG.
/// Null = use all VectorDocuments for the game (default behavior).
/// </summary>
public string? SelectedKnowledgeBaseIdsJson { get; private set; }
```

Add method:

```csharp
/// <summary>
/// Sets the selected knowledge base IDs for this chat thread.
/// </summary>
public void SetSelectedKnowledgeBases(List<Guid>? knowledgeBaseIds)
{
    if (knowledgeBaseIds == null || knowledgeBaseIds.Count == 0)
    {
        SelectedKnowledgeBaseIdsJson = null;
        return;
    }

    SelectedKnowledgeBaseIdsJson = System.Text.Json.JsonSerializer.Serialize(knowledgeBaseIds);
}

/// <summary>
/// Gets the selected knowledge base IDs, or null if all should be used.
/// </summary>
public List<Guid>? GetSelectedKnowledgeBaseIds()
{
    if (string.IsNullOrEmpty(SelectedKnowledgeBaseIdsJson))
        return null;

    return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(SelectedKnowledgeBaseIdsJson);
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ChatThreadKbSelectionTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommand.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThread.cs \
       tests/Api.Tests/KnowledgeBase/ChatThreadKbSelectionTests.cs
git commit -m "feat(kb): add SelectedKnowledgeBaseIds to ChatThread and CreateChatThreadCommand"
```

---

## Task 9: Wire KB Selection into CreateChatThreadCommandHandler

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/CreateChatThreadCommandHandler.cs`

- [ ] **Step 1: Add KB selection after ChatThread creation**

In `CreateChatThreadCommandHandler.Handle()`, after line 97 (`agentType: command.AgentType`), add:

```csharp
// Set selected KB IDs (null = use all for game, as before)
if (command.SelectedKnowledgeBaseIds is { Count: > 0 })
{
    thread.SetSelectedKnowledgeBases(command.SelectedKnowledgeBaseIds);
}
```

- [ ] **Step 2: Run build + existing chat tests**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ChatThread" -v minimal`
Expected: Build + tests pass

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/CreateChatThreadCommandHandler.cs
git commit -m "feat(kb): wire SelectedKnowledgeBaseIds into CreateChatThreadCommandHandler"
```

---

## Task 10: Extend RagAccessService to filter by selected KBs

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagAccessService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/RagAccessService.cs`

- [ ] **Step 1: Add method signature to IRagAccessService interface**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagAccessService.cs`

Add below existing `GetAccessibleKbCardsAsync` method:

```csharp
/// <summary>
/// Gets accessible KB card IDs, filtered by a user selection list.
/// If selectedIds is null/empty, returns all accessible KBs (backward compatible).
/// </summary>
Task<List<Guid>> GetAccessibleKbCardsFilteredAsync(
    Guid userId, Guid gameId, UserRole role,
    List<Guid>? selectedIds,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Add implementation to RagAccessService**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/RagAccessService.cs`

```csharp
/// <inheritdoc />
public async Task<List<Guid>> GetAccessibleKbCardsFilteredAsync(
    Guid userId, Guid gameId, UserRole role,
    List<Guid>? selectedIds,
    CancellationToken cancellationToken = default)
{
    var allAccessible = await GetAccessibleKbCardsAsync(userId, gameId, role, cancellationToken)
        .ConfigureAwait(false);

    if (selectedIds is not { Count: > 0 })
        return allAccessible;

    // Intersect: only return IDs that are both accessible AND selected
    var selectedSet = new HashSet<Guid>(selectedIds);
    return allAccessible.Where(id => selectedSet.Contains(id)).ToList();
}
```

- [ ] **Step 3: Run build**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/RagAccessService.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagAccessService.cs
git commit -m "feat(kb): extend IRagAccessService + RagAccessService with filtered KB method"
```

---

## Task 11: ChatThreadEntity + Repository Mappers + Migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs`
- Create: new migration file (auto-generated)

- [ ] **Step 1: Add property to ChatThreadEntity**

File: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs`

Add:
```csharp
/// <summary>
/// JSON-serialized list of VectorDocument IDs selected for RAG.
/// Null = use all VectorDocuments for the game (default).
/// </summary>
public string? SelectedKnowledgeBaseIdsJson { get; set; }
```

- [ ] **Step 2: Update MapToPersistence in ChatThreadRepository**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs`

In `MapToPersistence()` (around line 385-399), add to the return object:

```csharp
SelectedKnowledgeBaseIdsJson = domainEntity.SelectedKnowledgeBaseIdsJson
```

After the existing `LastSummarizedMessageCount = domainEntity.LastSummarizedMessageCount` line.

- [ ] **Step 3: Update MapToDomain in ChatThreadRepository**

In `MapToDomain()` (around line 344-357), add after the `LastSummarizedMessageCount` hydration block:

```csharp
// Hydrate selected KB IDs
if (!string.IsNullOrEmpty(entity.SelectedKnowledgeBaseIdsJson))
{
    var kbIdsProp = typeof(ChatThread).GetProperty("SelectedKnowledgeBaseIdsJson");
    kbIdsProp?.SetValue(thread, entity.SelectedKnowledgeBaseIdsJson);
}
```

This follows the same reflection-based hydration pattern used for `ConversationSummary` (line 345-349).

- [ ] **Step 4: Run build to verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Create migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddSelectedKnowledgeBaseIdsToChatThread`
Expected: Migration file created

- [ ] **Step 6: Review migration SQL**

Read the generated migration file, verify it only adds one nullable text column to `chat_threads`.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/ChatThreadEntity.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/ChatThreadRepository.cs \
       apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(db): add SelectedKnowledgeBaseIdsJson to ChatThread entity, mappers, and migration"
```

---

## Task 12: Integration Test — Full Flow

**Files:**
- Create: `tests/Api.Tests/Integration/PdfKbLinkFlowTests.cs`

- [ ] **Step 1: Write integration test**

```csharp
namespace Api.Tests.Integration;

[Category("Integration")]
public class PdfKbLinkFlowTests
{
    // Test: Upload PDF → get ExistingKbInfo → link to another game → verify VectorDocument created
    // This test requires Testcontainers (PostgreSQL) to run

    [Fact]
    public void ExistingKbInfoDto_RoundTrip_ShouldPreserveData()
    {
        // Verify the DTO serialization round-trip works
        var original = new ExistingKbInfoDto(
            PdfDocumentId: Guid.NewGuid(),
            Source: "shared",
            FileName: "test.pdf",
            ProcessingState: "Ready",
            TotalChunks: 42,
            OriginalGameName: "Test Game",
            SharedGameId: Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ExistingKbInfoDto>(json);

        Assert.Equal(original.PdfDocumentId, deserialized!.PdfDocumentId);
        Assert.Equal(original.Source, deserialized.Source);
        Assert.Equal(original.TotalChunks, deserialized.TotalChunks);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~PdfKbLinkFlowTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/Integration/PdfKbLinkFlowTests.cs
git commit -m "test(integration): add PDF-KB link flow integration tests"
```

---

## API Reference

| Step | Method | Endpoint | Request Body | Response |
|------|--------|----------|-------------|----------|
| Upload PDF (detect) | POST | `/api/v1/ingest/pdf` | `multipart/form-data` | `{ existingKbFound, existingKb: { pdfDocumentId, source, ... } }` |
| Link KB to game | POST | `/api/v1/games/{gameId}/knowledge-base/link` | `{ pdfDocumentId }` | `{ vectorDocumentId, gameId, status }` |
| Create chat (with KB selection) | POST | `/api/v1/chat-threads` | `{ gameId, agentType, selectedKnowledgeBaseIds: [guid, ...] }` | `ChatThreadDto` |
| List game KBs | GET | `/api/v1/games/{gameId}/pdfs` | — | `[GameDocumentDto, ...]` |

---

## Dependency Graph

```
Task 1 (ExistingKbInfoDto)
  └→ Task 2 (Extend PdfUploadResult) — depends on Task 1
      └→ Task 3 (Modify UploadPdfCommandHandler) — depends on Task 2
          └→ Task 4 (Modify PdfEndpoints) — depends on Task 3

Task 5 (LinkExistingKbToGameCommand + Validator) — independent
  └→ Task 6 (Handler) — depends on Task 5
      └→ Task 7 (Endpoint) — depends on Task 6

Task 8 (ChatThread KB selection) — independent
  └→ Task 9 (Wire into handler) — depends on Task 8
      └→ Task 10 (RagAccessService filter) — depends on Task 8

Task 11 (ChatThreadEntity + Mappers + Migration) — depends on Task 8
Task 12 (Integration test) — depends on Tasks 1-11
```

**Parallelization:** Tasks 1-4, Tasks 5-7, and Tasks 8-10 can be executed as three parallel streams.

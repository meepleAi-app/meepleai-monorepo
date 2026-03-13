# Remove RAG from Shared Game — Design Specification

**Date**: 2026-03-12
**Status**: Approved
**Context**: Admin PDF removal with full multi-system cleanup

## Problem

Admins cannot fully remove a PDF from a shared game through a single operation. Two separate commands exist:

1. `RemoveDocumentFromSharedGameCommand` — unlinks `SharedGameDocument` only
2. `DeletePdfCommand` — deletes PDF + VectorDocument + Qdrant + blob + TextChunks (cascade)

But `DeletePdfCommand` cannot execute while a `SharedGameDocument` references the PDF (`DeleteBehavior.Restrict` FK). There is no orchestrated flow.

## Solution

A new saga command `RemoveRagFromSharedGameCommand` that orchestrates full cleanup in the correct order, following the same pattern as the existing `AddRagToSharedGameCommand` saga.

### Endpoint

```
DELETE /api/v1/admin/shared-games/{id}/documents/{docId}/full
```

- **Auth**: Admin only (not Editor — Editors can only unlink, not destroy data)
- **Response**: 204 No Content on success

### Saga Flow

```
RemoveRagFromSharedGameCommandHandler
  1. Validate: SharedGameDocument exists and belongs to the specified SharedGame
  2. Resolve PdfDocumentId from the SharedGameDocument
  3. Handle active version:
     - If document is active AND other versions of same type exist → auto-promote next version
     - If document is active AND no other versions → clear active (game has empty KB for that type)
  4. Remove SharedGameDocument link (reuse existing RemoveDocumentFromSharedGameCommand)
  5. Delete PDF with full cleanup (reuse existing DeletePdfCommand — cascades VectorDoc, TextChunks, Qdrant, blob)
  6. Return result
```

### Transaction Boundary

- **Atomic (single DB transaction)**: Steps 3-4 (SharedGameDocument removal + active version handling)
- **Best-effort**: Step 5 — `DeletePdfCommand` already handles Qdrant/blob failures gracefully (logs warning, does not throw)
- **If Step 5 fails**: SharedGameDocument is already removed. The orphaned PDF can be cleaned up via existing `CleanupOrphansCommand` or admin bulk delete.

### Data Flow

```
SharedGameDocument (link)
  └─ PdfDocument
       ├─ VectorDocument (cascade delete)
       │    └─ Qdrant vectors (best-effort delete via QdrantService)
       ├─ TextChunk[] (cascade delete)
       └─ Blob file (best-effort delete via BlobStorageService)
```

### Edge Cases

| Scenario | Behavior |
|----------|----------|
| Document not found | 404 Not Found |
| Document belongs to different game | 404 Not Found |
| Active version with alternatives | Auto-promote next version of same type |
| Last/only document | Remove without promotion |
| Qdrant cleanup fails | Log warning, continue (best-effort) |
| Blob cleanup fails | Log warning, continue (best-effort) |
| PDF already being processed | Allow removal (cancels implicitly) |
| Concurrent removal | First wins, second gets 404 |

### Command & Result

```csharp
internal record RemoveRagFromSharedGameCommand(
    Guid SharedGameId,
    Guid SharedGameDocumentId,
    Guid UserId,
    bool IsAdmin
) : ICommand<RemoveRagFromSharedGameResult>;

internal record RemoveRagFromSharedGameResult(
    Guid RemovedSharedGameDocumentId,
    Guid RemovedPdfDocumentId,
    bool QdrantCleanupSucceeded,
    bool BlobCleanupSucceeded
);
```

### Files

| Action | Path |
|--------|------|
| Create | `SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommand.cs` |
| Create | `SharedGameCatalog/Application/Commands/RemoveRagFromSharedGame/RemoveRagFromSharedGameCommandHandler.cs` |
| Modify | `Routing/SharedGameCatalogEndpoints.cs` — add DELETE endpoint |
| Create | Tests for saga handler |

### Testing Strategy

1. **Happy path**: Full cleanup succeeds (SharedGameDoc + PDF + VectorDoc + TextChunks removed)
2. **Active version promotion**: Removing active v1.0 auto-promotes v2.0
3. **Last document**: Removing only document leaves game with no documents
4. **Not found**: 404 for non-existent or wrong-game document
5. **Authorization**: Only admin, not editor
6. **Qdrant failure**: Graceful degradation — DB cleanup succeeds anyway
7. **Blob failure**: Same as Qdrant — best-effort

### Audit

Existing `SharedGameDocumentRemovedEvent` is published by `RemoveDocumentFromSharedGameCommand`. The saga reuses this command, so audit events fire automatically.

### Not In Scope

- Frontend UI changes (admin will use existing document list with new "Delete with cleanup" action — separate issue)
- Bulk removal (can be added later by iterating over this saga)
- Soft delete (project uses hard delete for all PDF-related entities)

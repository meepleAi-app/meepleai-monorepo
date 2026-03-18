# Rulebook Upload → KB → Chat Flow

**Date**: 2026-03-17
**Status**: Draft
**Scope**: DocumentProcessing, KnowledgeBase, EntityRelationships, GameManagement (frontend + backend)

## Overview

Enable users to upload game rulebooks with automatic deduplication, link them as KB cards to games, and start AI-assisted chat sessions using the indexed knowledge base. Three user flows:

1. **PDF already exists**: Hash match detected → automatic EntityLink → zero re-upload
2. **New PDF**: Upload → process → index → EntityLink → KB card ready
3. **Chat with KB**: User selects a game with KB ready → starts chat with RAG agent

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Duplicate behavior | Auto-link (not 409 block) | Zero friction; same PDF serves multiple games |
| Processing-in-progress | Link immediately, show "in elaborazione" | Responsive UX, no polling needed |
| Chat KB selection | Filter games with KB ready | Natural UX: "ask about Catan's rules" |
| Entry points | Game detail page + post add-game flow | Maximum discoverability |
| Approach | Dedicated endpoint (not modified existing) | Clean semantics, testable, no side effects on existing flows |
| Failed PDF reuse | Treat as new (re-upload) | Failed PDFs are unreliable; fresh upload is safer |
| Multiple rulebooks per game | Supported naturally | Base + expansions + FAQ; agent uses all VectorDocuments |

> **Important**: The deduplication behavior in `AddRulebookCommand` is **intentionally different** from `UploadPdfCommandHandler`, which returns 409 on hash match. Do not use `UploadPdfCommandHandler` as a template for the new handler. The new endpoint treats hash matches as successful deduplication (auto-link), not as errors.

## API Design

### `POST /api/v1/games/{gameId}/rulebook`

**Auth**: Required (game must belong to user)
**Content-Type**: `multipart/form-data`
**Body**: PDF file
**Routing file**: `RulebookEndpoints.cs` (new file in `apps/api/src/Api/Routing/`)

**Handler logic** (`AddRulebookCommand`):

```
1. Validate: PDF file, tier quota, game ownership
2. Compute SHA-256 hash
3. Query: IPdfDocumentRepository.FindByContentHashAsync(hash)
   ├─ FOUND + state Ready (PdfProcessingState.Ready):
   │   → CreateEntityLink(Game, gameId, KbCard, pdfDocumentId, LinkType: RelatedTo)
   │   → Return { isNew: false, status: "ready" }
   │
   ├─ FOUND + state In-Progress (Pending/Uploading/Extracting/Chunking/Embedding/Indexing):
   │   → CreateEntityLink(Game, gameId, KbCard, pdfDocumentId, LinkType: RelatedTo)
   │   → Return { isNew: false, status: "pending" if Pending/Uploading, "processing" otherwise }
   │
   ├─ FOUND + state Failed:
   │   → Delete existing EntityLink(s) from this game to the failed PdfDocument (cleanup stale link)
   │   → Treat as new: upload new PdfDocument + process + create fresh EntityLink
   │   → Return { isNew: true, status: "pending" }
   │
   └─ NOT FOUND:
       → Upload file → Create PdfDocument → Start processing
       → CreateEntityLink(Game, gameId, KbCard, pdfDocumentId, LinkType: RelatedTo)
       → Return { isNew: true, status: "pending" }
```

**Response** (all cases):

```json
{
  "pdfDocumentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isNew": true,
  "status": "pending",
  "message": "Regolamento caricato con successo"
}
```

| Field | Type | Values |
|-------|------|--------|
| `pdfDocumentId` | `Guid` | ID of the PDF (new or existing) |
| `isNew` | `bool` | `true` if newly uploaded, `false` if reused |
| `status` | `string` | `pending` \| `processing` \| `ready` \| `failed` |
| `message` | `string` | Human-readable Italian message |

> **Status mapping**: `PdfProcessingState.Pending` and `PdfProcessingState.Uploading` both map to client-facing `"pending"`. `Extracting` through `Indexing` map to `"processing"`. `Ready` maps to `"ready"`. `Failed` maps to `"failed"`.

**Error responses**:

| Code | Condition |
|------|-----------|
| 400 | Invalid file (not PDF, corrupt structure) |
| 403 | Game not owned by user, or tier quota exceeded |
| 404 | Game not found |

### `GET /api/v1/users/{userId}/games/with-kb`

**Auth**: Required (userId must match authenticated user)

**Response**:

```json
[
  {
    "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Catan",
    "imageUrl": "https://...",
    "overallKbStatus": "ready",
    "rulebooks": [
      {
        "pdfDocumentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "fileName": "regolamento-base.pdf",
        "kbStatus": "ready",
        "indexedAt": "2026-03-15T10:30:00Z"
      },
      {
        "pdfDocumentId": "7ba85f64-1234-4562-b3fc-2c963f66bbb1",
        "fileName": "espansione-mare.pdf",
        "kbStatus": "processing",
        "indexedAt": null
      }
    ]
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `gameId` | `Guid` | Game ID |
| `title` | `string` | Game name |
| `imageUrl` | `string?` | Game cover image |
| `overallKbStatus` | `string` | `ready` if any rulebook is ready, else `processing` if any processing, else `failed` |
| `rulebooks` | `array` | All linked PDF documents for this game |
| `rulebooks[].pdfDocumentId` | `Guid` | PDF document ID |
| `rulebooks[].fileName` | `string` | Original filename |
| `rulebooks[].kbStatus` | `string` | `ready` \| `processing` \| `failed` |
| `rulebooks[].indexedAt` | `DateTime?` | null if not ready |

**Query logic**:

```
User's library games
  JOIN EntityLink WHERE SourceEntityType=Game AND TargetEntityType=KbCard
  JOIN PdfDocument ON EntityLink.TargetEntityId = PdfDocument.Id
  MAP kbStatus per rulebook:
    PdfDocument.ProcessingState == PdfProcessingState.Ready → "ready"
    PdfDocument.ProcessingState IN [Extracting..Indexing] → "processing"
    PdfDocument.ProcessingState == PdfProcessingState.Failed → "failed"
  MAP overallKbStatus per game:
    ANY rulebook "ready" → "ready"
    ELSE ANY rulebook "processing" → "processing"
    ELSE → "failed"
```

> **Implementation note**: This handler uses direct EF Core joins on `MeepleAiDbContext` to access `EntityLink`, `PdfDocumentEntity` tables. Do not dispatch cross-BC mediator commands — this is a read-only query that benefits from a single DB round-trip.

> **Join key**: Use `EntityLink.TargetEntityId = PdfDocument.Id` to enumerate linked PDFs. Do NOT use `PdfDocument.GameId` to find linked games, as it only reflects the original uploader's game.

## Data Model

### Required repository addition

Add to `IPdfDocumentRepository`:

```csharp
/// <summary>
/// Finds a PdfDocument by its SHA-256 content hash (global, cross-game lookup).
/// Returns the full entity to inspect ProcessingState for dedup branching.
/// </summary>
Task<PdfDocument?> FindByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);
```

This is required by the `AddRulebookCommandHandler` to branch on `ProcessingState` (Ready → reuse, Processing → link + wait, Failed → re-upload). The existing `ExistsByContentHashAsync` is a boolean scoped by `gameId` and is insufficient.

### No new entities required

| Entity | Role | Changes |
|--------|------|---------|
| `PdfDocument` | Physical PDF with hash, processing state | None — `GameId` stays as original uploader's game |
| `VectorDocument` | Indexed KB for RAG | None |
| `EntityLink` | Game→KbCard relationship | None — existing aggregate handles multi-game linking |
| `Agent` | AI agent per game | None |
| `ChatThread` | Conversation with GameId + AgentId | None |

### Shared PDF model

One PdfDocument can serve multiple games via EntityLink:

```
Game A ──EntityLink(RelatedTo)──┐
                                ├──→ KbCard (PdfDocument X) ──→ VectorDocument(s)
Game B ──EntityLink(RelatedTo)──┘
```

`PdfDocument.GameId` = the game that originally triggered the upload. Other games link via EntityLink only. The `GetGamesWithKbQuery` must use `EntityLink.TargetEntityId = PdfDocument.Id` as the join key, not `PdfDocument.GameId`, since `GameId` only reflects the original uploader.

## Frontend Design

### Component: `RulebookSection`

Placed in game detail page. Renders based on KB state:

**State: No rulebook**
```
📄 Regolamento
[Carica regolamento]
```

**State: Processing**
```
📄 Regolamento
⏳ In elaborazione...
Sarà disponibile a breve
```

**State: Ready (single)**
```
📄 Regolamento                    ✅ Pronto
regolamento-catan.pdf
[Chatta con l'agente]
```

**State: Ready (multiple)**
```
📄 Regolamenti
• regolamento-base.pdf            ✅ Pronto
• espansione-mare.pdf             ⏳ Elab.
[Carica altro]  [Chatta con agente]
```

**State: Failed**
```
📄 Regolamento
❌ Elaborazione fallita
[Riprova]           [Rimuovi]
```

**Upload interaction**: Click "Carica regolamento" → file picker → `POST /api/v1/games/{gameId}/rulebook` → update UI based on response.

**Reuse toast**: When `isNew: false`, show toast: "Questo regolamento è già nel sistema — collegato al tuo gioco!"

### Post add-game flow

After game is added (PR #511 flow), show optional step:

```
Game aggiunto ✓
┌─────────────────────────────────┐
│ Vuoi caricare il regolamento?   │
│ [Carica PDF]    [Salta per ora] │
└─────────────────────────────────┘
```

- "Carica PDF" → same file picker + same endpoint
- "Salta per ora" → continue to existing chat flow

### Chat KB selection: `GameWithKbList`

In new chat creation / agent selection:

```
Scegli un gioco con regolamento:
┌─────────────────────────────────┐
│ 🎲 Catan              ✅ Pronto │
│ 🎲 Wingspan           ⏳ Elab.  │  ← disabled
│ 🎲 Terraforming Mars  ✅ Pronto │
└─────────────────────────────────┘
```

- Source: `GET /api/v1/users/{userId}/games/with-kb`
- Only games with `overallKbStatus: "ready"` are selectable; `processing` visible but disabled
- Selection → create ChatThread with GameId + AgentId → open SSE chat

## Error Handling & Edge Cases

| Case | Behavior |
|------|----------|
| Game not owned by user | 403 Forbidden |
| Game already has KB (same PDF) | EntityLink idempotent (same `LinkType: RelatedTo`) → return `{ isNew: false, status }` |
| Game already has KB (different PDF) | New EntityLink created → game has 2+ KB cards. Agent uses all VectorDocuments for GameId |
| File is not a PDF | 400 Bad Request (pre-hash validation) |
| File exceeds tier limit | 403 with quota message |
| PDF found by hash but state Failed | Cleanup: delete existing EntityLink(s) from this game to the failed PDF. Then treat as new: re-upload and re-process with fresh EntityLink |
| Upload fails after hash check | Rollback: no PdfDocument, no EntityLink |
| Processing fails after upload | PdfDocument → Failed. EntityLink exists but KB card shows "Errore". Retry available |
| Private games | Not supported by this endpoint. Use existing `POST /users/{userId}/library/entries/{entryId}/pdf` |
| Two users upload same PDF simultaneously | First creates PdfDocument. Second finds hash match → reuse. Race condition safe: `FindByContentHashAsync` is read-only, EntityLink creation is idempotent |
| User uploads while processing in progress (same hash) | Hash match → EntityLink created → status "processing" |
| Quota exhausted during upload | Fail-fast before blob upload. No cleanup needed |

### Retry for failed PDFs

KB card in "failed" state shows:
- "Riprova": calls existing retry mechanism (`PdfDocument.Retry()` — max 3 attempts)
- "Rimuovi": uses existing `DeleteEntityLinkCommand` (soft delete). PdfDocument stays for other linked games.

## Concurrency

| Scenario | Handling |
|----------|----------|
| Two users upload same PDF concurrently | First creates PdfDocument. Second finds hash match → reuse via EntityLink. Safe: hash check is read-only, EntityLink creation is idempotent |
| User uploads while same hash is processing | Hash match → EntityLink → status "processing" |
| Quota reservation race | Two-phase quota (existing pattern): reserve → confirm. Expiry handles abandoned uploads |

## Testing Strategy

### Backend Unit Tests

| Test | Verifies |
|------|----------|
| `AddRulebookCommand_WhenHashMatch_ReturnsReused` | Hash found → `isNew: false`, EntityLink created with `RelatedTo` |
| `AddRulebookCommand_WhenHashMatch_Processing_ReturnsProcessingStatus` | PDF processing → status "processing" |
| `AddRulebookCommand_WhenHashMatch_Failed_TreatsAsNew` | Failed PDF → re-upload as new |
| `AddRulebookCommand_WhenNewPdf_UploadsAndLinks` | New PDF → upload + EntityLink + processing started |
| `AddRulebookCommand_WhenGameNotOwned_Returns403` | Ownership check |
| `AddRulebookCommand_WhenDuplicateLink_IsIdempotent` | Double link → no error |
| `AddRulebookCommand_WhenQuotaExceeded_Returns403` | Tier enforcement |

### Backend Integration Tests

| Test | Verifies |
|------|----------|
| `Rulebook_FullFlow_NewPdf_CreatesAllEntities` | PdfDocument + EntityLink in DB |
| `Rulebook_FullFlow_ReusedPdf_OnlyCreatesLink` | No new PdfDocument, only EntityLink |
| `Rulebook_ConcurrentUploads_SameHash_NoRace` | Parallel uploads same hash → one creates, other reuses |
| `GamesWithKb_ReturnsCorrectStatus` | Query endpoint filters and maps states correctly |
| `GamesWithKb_ExcludesGamesWithoutKb` | Games without EntityLink→KbCard excluded |
| `GamesWithKb_ReturnsMultipleRulebooksPerGame` | Game with 2+ PDFs returns all in `rulebooks` array |

### Backend E2E Tests

| Test | Verifies |
|------|----------|
| `POST /games/{id}/rulebook` new PDF → 200 + `isNew: true` | Happy path new |
| `POST /games/{id}/rulebook` duplicate PDF → 200 + `isNew: false` | Happy path reuse |
| `GET /users/{id}/games/with-kb` → correct list with rulebooks array | Query endpoint |
| `POST /agents/{id}/chat` on game with KB → RAG response | Chat flow end-to-end |

### Frontend Unit Tests (Vitest)

| Test | Verifies |
|------|----------|
| `RulebookSection` renders correctly for each state (none/processing/ready/failed/multiple) | UI states |
| `RulebookSection` upload flow → calls endpoint → shows result | Upload interaction |
| `GameWithKbList` shows only games with KB, disables "processing" | Chat selection filter |
| Toast "già disponibile" appears when `isNew: false` | Reuse message |

### Frontend E2E Tests (Playwright)

| Test | Verifies |
|------|----------|
| Game detail → upload PDF → KB card appears | Full flow entry point 1 |
| Add game → upload prompt → upload → KB card | Full flow entry point 2 |
| Select game with KB → start chat → agent response | Full flow entry point 3 |

## CQRS Structure

### Commands

| Command | Handler | Bounded Context |
|---------|---------|-----------------|
| `AddRulebookCommand` | `AddRulebookCommandHandler` | DocumentProcessing |

> **Note**: For "Rimuovi" (remove rulebook link), reuse the existing `DeleteEntityLinkCommand` in the EntityRelationships BC. No new command needed.

### Queries

| Query | Handler | Bounded Context |
|-------|---------|-----------------|
| `GetGamesWithKbQuery` | `GetGamesWithKbQueryHandler` | GameManagement (cross-context read query via direct EF joins) |

### Endpoints

| Method | Route | Handler | Routing File |
|--------|-------|---------|-------------|
| `POST` | `/api/v1/games/{gameId}/rulebook` | `AddRulebookCommand` via MediatR | `RulebookEndpoints.cs` (new) |
| `GET` | `/api/v1/users/{userId}/games/with-kb` | `GetGamesWithKbQuery` via MediatR | `RulebookEndpoints.cs` (new) |

## Out of Scope

- Private game rulebook upload (existing flow unchanged)
- PDF similarity detection (only exact hash match)
- Per-document KB selection in chat (agent uses all VectorDocuments for the game)
- Admin bulk upload flow (existing `/ingest/pdf` unchanged)
- Real-time status updates via SSE/WebSocket for processing progress

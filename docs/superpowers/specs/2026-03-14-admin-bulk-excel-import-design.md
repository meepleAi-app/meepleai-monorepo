# Admin Bulk Excel Import & BGG Enrichment Pipeline

**Date**: 2026-03-14
**Status**: Draft (v3 — post expert panel review)
**Approach**: Hybrid lightweight (C) — extend existing BGG queue infrastructure + persistent enrichment tracking

## Problem Statement

As an admin, I want to load games into the SharedGameCatalog in bulk from an Excel spreadsheet, enrich them with BGG data on demand, and auto-download rulebook PDFs where available. BGG search must be admin-only and not exposed to regular users.

## Workflow

```
Excel upload → Parse → Check duplicates → Create Skeletons
                                              ↓
                            Admin selects → Enqueue BGG enrichment
                                              ↓
                  Existing BggImportQueue processes (rate-limited)
                                              ↓
                            BGG data → update SharedGame → save raw JSON (infra layer)
                                              ↓
                            If rulebook link → auto-download PDF
                            If no link       → admin marks complete or uploads manually
                                              ↓
                            In-app notification on batch completion
                                              ↓
                            Export enriched Excel at any time
```

---

## 1. Excel Import

### Endpoint

`POST /api/v1/admin/games/excel-import` — AdminOrEditorPolicy

Input: `.xlsx` file (multipart/form-data)

### Excel Format

| Column | Type | Required |
|--------|------|:--------:|
| Name | string | Yes |
| BggId | int | No |

One row per game/expansion. Parse the **first worksheet**. Expect a **header row** (column names). Extra columns are **ignored**. Non-ASCII characters preserved as-is.

### Processing Logic

1. Parse Excel with **ClosedXML >= 0.104** (MIT license)
2. **Intra-file deduplication**: Track seen names+BggIds within the batch. If a row duplicates an earlier row in the same file → skip, report as "intra-file duplicate"
3. Per row:
   - Trim name, validate non-empty (whitespace-only after trim → error)
   - Validate name length ≤ 500 characters
   - If BggId present: validate > 0 (reject 0 and negative values)
   - Duplicate check vs DB: search SharedGameCatalog by `BggId` (if present) or `Title` (exact match, case-insensitive)
   - If duplicate → skip, add to report
   - If new → create SharedGame skeleton via `SharedGame.CreateSkeleton(title, createdBy, timeProvider, bggId?)`
4. Return `ExcelImportResult`: total, created, duplicates, errors (per row)

**Transaction boundary**: Each row is processed in its own transaction (partial success). If row 301 fails, rows 1-300 are already committed. The result body reports per-row status.

### Response Semantics

- **200 OK**: Result body contains full breakdown (works for all cases: some created, all duplicates, mixed)
- **400 Bad Request**: File validation failures (size, format, empty, corrupted/not-a-real-xlsx)

### Error DTO

```csharp
record ExcelRowError(int RowNumber, string? ColumnName, string ErrorMessage);
record ExcelImportResult(
    int Total, int Created, int Duplicates, int Errors,
    IReadOnlyList<ExcelRowError> RowErrors);
```

### Validations

- Max 500 rows per upload
- Max 5MB file size
- `.xlsx` extension only + validate file magic bytes (ZIP signature `PK`)
- Rate limit: dedicated `ExcelImportAdmin` policy — 1 request per 5 minutes per user (separate from `BulkImportAdmin` to avoid cross-throttling with JSON bulk import)

### DDD: Skeleton Factory Method

New factory method on `SharedGame` using the **private constructor directly** (bypasses existing `Create()` validation):

```csharp
public static SharedGame CreateSkeleton(string title, Guid createdBy, TimeProvider timeProvider, int? bggId = null)
```

- Uses private constructor directly — does NOT call `Create()` or run `ValidateYear`, `ValidatePlayers`, etc.
- Only validates: `title` is non-empty and ≤ 500 chars
- Sets defaults:
  - `yearPublished = 0`, `description = ""`, `minPlayers = 0`, `maxPlayers = 0`
  - `playingTimeMinutes = 0`, `minAge = 0`
  - `imageUrl = null`, `thumbnailUrl = null` (nullable — no placeholder URLs)
  - `GameDataStatus = Skeleton`, `GameStatus = Draft`
  - `CreatedAt` / `UpdatedAt` via `timeProvider.GetUtcNow()`
- Full domain validation runs only in `EnrichFromBgg()` before transitioning to `Enriched`

### New Field on SharedGame: `GameDataStatus`

```
Skeleton → EnrichmentQueued → Enriching → Enriched → PdfDownloading → Complete
                                              ↓                           ↑
                                         (admin marks complete) ──────────┘
                                              ↓
Failed ← (any step can fail; Failed can be re-enqueued → EnrichmentQueued)
```

**State transitions**:
- `Skeleton` → `EnrichmentQueued` (admin enqueues for BGG enrichment)
- `EnrichmentQueued` → `Enriching` (background service picks up)
- `Enriching` → `Enriched` (BGG data applied successfully)
- `Enriching` → `Failed` (BGG fetch/match failed)
- `Enriched` → `PdfDownloading` (auto-download started)
- `Enriched` → `Complete` (admin manually marks complete — no PDF needed)
- `PdfDownloading` → `Complete` (PDF blob stored and enqueued for processing)
- `PdfDownloading` → `Enriched` (download failed — reverts, admin can retry or mark complete)
- `Failed` → `EnrichmentQueued` (admin re-enqueues after fixing BggId)

**Admin action**: `POST /api/v1/admin/bgg-queue/mark-complete` — accepts list of SharedGameIds in `Enriched` state, transitions to `Complete`. For games that don't need a PDF.

**Invalid transitions**: Any transition not listed above is rejected by the domain. Tests must cover all invalid paths.

**Relationship with existing `GameStatus`**:
- `GameStatus` tracks the **publication lifecycle**: Draft → PendingApproval → Published → Archived
- `GameDataStatus` tracks the **data completeness lifecycle**: Skeleton → ... → Complete
- **Constraint**: A game cannot transition to `GameStatus.PendingApproval` unless `GameDataStatus >= Enriched`
- **Dashboard**: Admin sees both statuses. Enrichment queue filters by `GameDataStatus`. Publication workflows filter by `GameStatus`.
- Skeleton and Enriched games always have `GameStatus = Draft` until admin explicitly submits for approval

### New Field on SharedGame: `BggRawData`

`jsonb` nullable column. **Stored at infrastructure/handler level, NOT in domain aggregate** (see Section 2). Write-only, no index. Max 1MB — truncate if BGG response exceeds this. Must **never** be included in any API response DTO. Only read during manual re-enrichment scenarios via direct DB query.

### Command/Handler

- `ImportGamesFromExcelCommand(IFormFile File, Guid UserId)` → `ExcelImportResult`
- Handler: parse, deduplicate (intra-file + vs DB), create SharedGames — each row in its own SaveChanges call
- Validator: file size, extension, magic bytes, row count

---

## 2. BGG Enrichment via Existing Queue Infrastructure

### Extending Existing `BggImportQueueEntity`

**No new table.** Extend the existing `BggImportQueueEntity` (at `Infrastructure/Entities/BggImportQueueEntity.cs`) and `IBggImportQueueService` with:

- **New column: `JobType`** (enum: `Import = 0`, `Enrichment = 1`) — explicit discriminator for queue item purpose. Default `Import` for existing rows. The background service branches on `JobType`, not on nullable field inference.
- New optional column: `SharedGameId` (Guid?, FK → SharedGame) — set when `JobType = Enrichment`
- New column: `BatchId` (Guid?, nullable) — groups items enqueued in a single admin request. `null` for legacy/import items. Batch notification logic filters out null BatchIds.
- **`BggId` remains `int?`** — change from `required int` to `int?` (nullable). Required for enrichment jobs where BggId is unknown (auto-match). **All existing callers must be updated**:

### Interface Changes on `IBggImportQueueService`

```csharp
// Existing method — update signature:
Task EnqueueAsync(int bggId, string gameName, Guid requestedBy, ...);
// Becomes:
Task EnqueueImportAsync(int bggId, string gameName, Guid requestedBy, ...);

// New method for enrichment:
Task EnqueueEnrichmentAsync(Guid sharedGameId, int? bggId, string gameName, Guid requestedBy, Guid batchId, ...);

// New batch method:
Task EnqueueEnrichmentBatchAsync(IEnumerable<(Guid SharedGameId, int? BggId, string GameName)> items, Guid requestedBy);

// Existing query methods — add null guard:
Task<BggImportQueueEntity?> GetByBggIdAsync(int bggId); // unchanged, only queries non-null BggIds
```

All existing callers of `EnqueueAsync` must be updated to use `EnqueueImportAsync`. This is a **compile-time breaking change** — search all usages.

### BGG Auto-Match (when BggId is absent)

When a skeleton has no BggId:
1. `IBggApiService.SearchGamesAsync(gameName, exact: true)` — counts as 1 rate-limited API call
2. **Zero results** → mark as `Failed` with message: "No BGG match found. Please provide BggId manually."
3. **One result** → use it, store matched BggId on queue item and on SharedGame
4. **Multiple results** → compare titles (case-insensitive, trimmed). If exact title match found → use it. Otherwise → mark as `Failed` with message: "Ambiguous BGG match (N results). Please provide BggId manually." Admin can then update the skeleton with a BggId and re-enqueue.

**Rate limiting note**: Auto-match items consume 2 BGG API calls (search + details) vs 1 for items with BggId. Both calls go through the existing `IRateLimitService`. This halves throughput for auto-match items — acceptable given auto-match is the fallback, not the default.

### Enrichment Processing

When processing an enrichment job (`JobType = Enrichment`):
1. Fetch BGG details via `IBggApiService.GetGameDetailsAsync(bggId)`
2. Update SharedGame via new domain method `SharedGame.EnrichFromBgg(...)`:
   - Accepts strongly-typed parameters: description, minPlayers, maxPlayers, playingTimeMinutes, yearPublished, complexityRating, averageRating, imageUrl, thumbnailUrl, categories, mechanics, designers, publishers, rulebookUrl
   - Sets all fields and transitions `GameDataStatus = Enriched`
   - Runs **full domain validation** (ValidateYear, ValidatePlayers, etc.) — if BGG data is invalid, throws and item is marked `Failed`
   - Does NOT set `BggRawData` (that's infrastructure)
3. **At handler/infrastructure level** (not in domain): serialize BGG API response to JSON, truncate to 1MB, store in `BggRawData` column via EF entity directly
4. Save changes

### Concurrency & Reliability

- **Claiming items**: Use **raw SQL** for atomic claim — `UPDATE bgg_import_queue SET status = 'Processing', updated_at = @now WHERE id = @id AND status = 'Queued' RETURNING *` (via `ExecuteSqlRawAsync` with row-count check). If 0 rows affected, another instance claimed it → skip. **Note**: This is a gap in the existing code (current implementation loads then mutates without atomic guard) — must be fixed as part of this feature.
- **Stale recovery**: On service startup and every 5 minutes, atomic SQL: `UPDATE bgg_import_queue SET status = 'Queued', retry_count = retry_count + 1, updated_at = @now WHERE status = 'Processing' AND updated_at < @threshold RETURNING id`. Log each recovered item at Warning level.
- **Circuit breaker**: Add Polly circuit breaker on the BGG `HttpClient` — trips after 5 consecutive failures, half-opens after 60 seconds. Prevents burning retries during systemic BGG outages.
- **Singleton**: The background service runs as a singleton per app instance (existing pattern).

### Endpoints

Extend existing `BggImportQueueEndpoints.cs` routes under `/api/v1/admin/bgg-queue/`:

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/admin/bgg-queue/enqueue-enrichment` | Enqueue selected SharedGameIds for enrichment |
| POST | `/api/v1/admin/bgg-queue/enqueue-all-skeletons` | Enqueue all Skeleton games |
| POST | `/api/v1/admin/bgg-queue/mark-complete` | Mark Enriched games as Complete (no PDF needed) |
| GET | `/api/v1/admin/bgg-queue/status` | Queue status (existing, extended with enrichment items) |
| DELETE | `/api/v1/admin/bgg-queue/{id}` | Remove from queue (existing) |

All endpoints: AdminOrEditorPolicy.

**Idempotency for enqueue endpoints**:
- `enqueue-enrichment`: Only games with `GameDataStatus` in (`Skeleton`, `Failed`) are eligible. Games already in `EnrichmentQueued`, `Enriching`, `Enriched`, or `Complete` are silently skipped. Response includes `enqueued` and `skipped` counts.
- `enqueue-all-skeletons`: Same filter — only `Skeleton` and `Failed` games.
- `mark-complete`: Only games in `Enriched` state. Others are skipped with per-item status in response.

**Input validation for `enqueue-enrichment`**: All submitted SharedGameIds must exist. Non-existent IDs return per-item error in response.

### Batch Notification

Each enqueue request generates a `BatchId` (Guid) stored on all items in that batch. When the last item in a batch completes (or fails), send an in-app notification via `UserNotifications` bounded context:
- "BGG enrichment batch completed: N succeeded, M failed"
- Notification includes link to enrichment queue filtered by BatchId
- `BatchId = null` items (legacy) never trigger batch notifications

---

## 3. PDF Auto-Download & Manual Upload

### Auto-Download

After BGG enrichment completes successfully and BGG provides a rulebook URL:

1. Extract rulebook URL from SharedGame. **New property on `GameRules` value object**: add `ExternalUrl` (string?, nullable) alongside existing `Content` and `Language` properties. This requires:
   - Update `GameRules` record: add `ExternalUrl` property
   - **New factory method**: `GameRules.CreateFromUrl(string externalUrl)` — creates a GameRules with only ExternalUrl set, Content and Language empty/null. For when BGG provides a URL but no inline content.
   - Update existing `GameRules.Create()` factory to accept optional `externalUrl` parameter
   - Update EF configuration for the owned type (new column on SharedGame table)
   - Update DTO mappings to include `ExternalUrl`
   - Include column in migration
   - During BGG enrichment, populate `ExternalUrl` from BGG API `rules` field
2. Update `GameDataStatus = PdfDownloading`
3. Dispatch `AutoDownloadPdfCommand(SharedGameId, PdfUrl)` via MediatR

**Handler security validations (SSRF prevention)**:
- URL must use HTTPS scheme
- **Resolve DNS at request time** and validate the resolved IP address (not just hostname):
  - Reject private ranges: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `127.0.0.0/8`
  - Reject link-local: `169.254.0.0/16` (includes AWS metadata `169.254.169.254`)
  - Reject loopback: `0.0.0.0`
  - Reject IPv6 private: `::1`, `fe80::/10`, `fc00::/7`
  - Reject IPv6-mapped IPv4: `::ffff:10.x.x.x`, `::ffff:127.x.x.x`, etc.
- **Disable automatic HTTP redirects** on the `HttpClient`. If a redirect is received, validate the new URL's resolved IP before following it manually (max 3 redirects).
- Response `Content-Type` must be `application/pdf`
- First 5 bytes must match PDF magic signature (`%PDF-`)
- Max 50MB, timeout 60s

**Processing**:
4. Download via named `HttpClient` ("PdfDownloader") with Polly retry (NOT the circuit-breaker client used for BGG)
5. Save via `IBlobStorageService.StoreAsync()` → returns `blobId`
6. Create `PdfDocument` entity for the SharedGame (same pattern as `UploadPdfCommandHandler`: create entity with SharedGameId, filename, blobId, set status to Pending)
7. Publish `PdfReadyForProcessingEvent(PdfDocumentId, SharedGameId, UserId)` — DocumentProcessing subscribes and triggers the processing pipeline (extraction, chunking, indexing). This decouples SharedGameCatalog from DocumentProcessing via domain event instead of direct command dispatch.
8. Update `GameDataStatus = Complete`

Note: `UserId` is the admin user who requested the enrichment batch (from `BatchId` → `RequestedBy`).

**Failure handling**:
- On download failure (404, timeout, non-PDF, SSRF block) → `GameDataStatus = Enriched` (enrichment OK, PDF missing)
- No automatic retry — admin uploads manually or marks complete
- If blob storage succeeds but PdfDocument creation fails → log error, delete orphaned blob via `IBlobStorageService.DeleteAsync()`, set `GameDataStatus = Enriched`

### Manual Upload

Existing endpoint: `POST /api/v1/admin/shared-games/{gameId}/documents/bulk-upload`

Admin sees which games are "Enriched" without PDF in the dashboard → uploads manually.
After manual upload → `GameDataStatus = Complete`.

No new endpoints needed for PDF.

---

## 4. BGG Access Restriction

1. **Audit existing endpoints**: Verify `SearchGamesAsync` is not exposed in any public endpoint. If a public BGG search endpoint exists, remove it or protect with `AdminOrEditorPolicy`
2. **Admin Game Wizard** (`/api/v1/admin/games/wizard/*`): Already protected, no changes
3. **UserLibrary**: No endpoint must call BGG. Users search only the local SharedGame catalog

---

## 5. Excel Export

### Endpoint

`GET /api/v1/admin/games/excel-export` — AdminOrEditorPolicy

Max 10,000 rows (ClosedXML streams — handles this size within ~5s). If catalog exceeds 10K, return 400 with message suggesting status filters to reduce scope.

### Output Columns

| Column | Source |
|--------|--------|
| Name | SharedGame.Title |
| BggId | SharedGame.BggId |
| Publisher | SharedGame.Publishers (comma-separated names) |
| Year | SharedGame.YearPublished |
| MinPlayers | SharedGame.MinPlayers |
| MaxPlayers | SharedGame.MaxPlayers |
| PlayingTimeMinutes | SharedGame.PlayingTimeMinutes |
| ComplexityRating | SharedGame.ComplexityRating |
| AverageRating | SharedGame.AverageRating |
| Categories | SharedGame.Categories (comma-separated names) |
| Mechanics | SharedGame.Mechanics (comma-separated names) |
| ImageUrl | SharedGame.ImageUrl |
| RulebookUrl | SharedGame.Rules?.ExternalUrl |
| DataStatus | SharedGame.GameDataStatus |
| PdfUploaded | bool — cached flag on SharedGame (`HasUploadedPdf`) |
| SharedGameId | Guid (for reference) |

### `HasUploadedPdf` Cached Flag

Instead of cross-context joins, add a `bool HasUploadedPdf` column on SharedGame. Updated by:
- **Domain event**: `SharedGamePdfUploadedEvent(Guid SharedGameId)` — published by the `AutoDownloadPdfCommand` handler on success and by the existing bulk-upload endpoint handler. An event handler in SharedGameCatalog subscribes and sets `HasUploadedPdf = true`.
- **Implementation note**: During implementation, check if DocumentProcessing already publishes a `PdfDocumentStatusChangedEvent` or similar. If so, subscribe to that and filter by SharedGameId instead of creating a new event. The spec requires the flag to be updated — the exact event mechanism is an implementation detail.

### Filters (query string)

- `?status=Skeleton,Enriched` — filter by GameDataStatus (comma-separated)
- `?hasPdf=false` — only games without PDF

---

## 6. Admin Dashboard (Frontend)

New page: `/admin/catalog-ingestion`

### Tab 1: Import

**Acceptance criteria**:
- Drag & drop or click-to-upload for `.xlsx` files
- GIVEN an uploaded Excel with N rows, WHEN the admin clicks "Preview", THEN all rows are displayed in a table with columns: Row#, Name, BggId, Status (pending)
- A "Confirm Import" button triggers the import
- GIVEN import completes, THEN results table shows per-row status: Created (green), Duplicate (yellow), Error (red with message)
- Error rows show `ExcelRowError.ErrorMessage`

### Tab 2: Enrichment Queue

**Acceptance criteria**:
- Game list with columns: Name, BggId, GameDataStatus, GameStatus, Actions
- Status filter dropdown (Skeleton / EnrichmentQueued / Enriching / Enriched / Complete / Failed)
- Multi-select checkbox → "Enrich Selected" button (enqueues selected, disabled if none selected)
- "Enqueue All Skeletons" button with confirmation dialog ("This will enqueue N games")
- "Mark Selected Complete" button (for Enriched games that don't need PDF)
- Per-row actions: Remove from queue (if queued), Retry (if Failed)
- Queue summary bar: N queued, N processing, N completed today, N failed

### Tab 3: Export

**Acceptance criteria**:
- Filter controls for status (multi-select) and hasPdf (checkbox)
- "Preview" shows first 20 rows of filtered data in a table
- "Download .xlsx" generates and downloads the file
- If filtered result > 10,000 rows, show warning and disable download

---

## 7. Testing Strategy

### Unit Tests

**Excel parser**:
- Valid file with 2 columns, header row
- Empty file (0 data rows) → error
- Missing "Name" column → error
- Max rows exceeded (501 rows) → error
- Invalid BggId: negative, zero, non-integer → per-row error
- Whitespace-only names after trim → per-row error
- Name exceeding 500 chars → per-row error
- File with formulas in cells (ClosedXML reads computed values)
- Unicode/non-ASCII game names → preserved correctly
- Multiple sheets → only first sheet parsed
- Corrupted file (valid .xlsx extension but invalid content) → 400 error
- Intra-file duplicate detection (same name or same BggId in multiple rows)

**Duplicate detection**:
- By BggId (exact match)
- By title (case-insensitive: "catan" matches "Catan")
- Mixed: BggId match takes priority over title match
- Existing skeleton with same title → skip (not update)

**SharedGame.CreateSkeleton()**:
- Produces valid entity with zero defaults
- Title validation: non-empty, ≤ 500 chars
- Uses TimeProvider (not DateTime.UtcNow)
- `GameDataStatus = Skeleton`, `GameStatus = Draft`

**SharedGame.EnrichFromBgg()**:
- All fields updated correctly
- Full domain validation runs on enriched data
- Partial BGG data (some fields missing) → validation fails, exception thrown
- Cannot enrich a game already in `Complete` state

**GameDataStatus state machine**:
- All valid transitions succeed
- All invalid transitions throw (e.g., Skeleton → Complete, Complete → EnrichmentQueued, Enriched → Enriching)
- `PendingApproval` rejected when `GameDataStatus < Enriched`
- `Failed` → `EnrichmentQueued` (re-enqueue) works

**BGG auto-match logic**:
- Zero results → Failed
- One result → use it
- Multiple results with exact title match → use matched
- Multiple results with no exact match → Failed with ambiguous message

**PDF URL validation**:
- HTTPS required (HTTP rejected)
- Private IPs rejected: 10.x, 172.16-31.x, 192.168.x, 127.x
- Link-local rejected: 169.254.x.x
- IPv6 loopback rejected: ::1
- IPv6-mapped IPv4 rejected: ::ffff:10.0.0.1
- Redirect to private IP rejected
- Content-Type not application/pdf → rejected
- Invalid PDF magic bytes → rejected

**GameRules value object**:
- `CreateFromUrl(url)` works with only ExternalUrl
- `Create(content, language, externalUrl)` works with all fields
- Existing `Create(content, language)` still works (backward compatible)

### Integration Tests

**Excel import endpoint** (real DB via Testcontainers, mock nothing):
- Upload valid file → verify SharedGames created with `GameDataStatus = Skeleton`
- Upload with duplicates → verify skip behavior, correct counts in response
- Concurrent upload by two admins → no race condition on duplicate detection

**Enrichment queue** (real DB, mock `IBggApiService`):
- Enqueue → background service processes → verify SharedGame updated with BGG data
- Enqueue with `BggId = null` → auto-match flow → verify search called
- Failed enrichment → verify `GameDataStatus = Failed`, error message stored
- Atomic claim: simulate two workers → only one claims the item
- Stale recovery: item stuck in Processing > 5min → reset to Queued
- Batch completion → notification sent to correct admin user

**PDF auto-download** (real DB, mock `HttpClient` and `IBlobStorageService`):
- Successful download → verify blob stored, PdfDocument created, event published
- SSRF blocked → verify `GameDataStatus` reverts to `Enriched`
- Download timeout → verify graceful failure

**Export endpoint** (real DB):
- Verify Excel generated with correct column count, order, data types
- Verify comma-separated categories/mechanics formatting
- Filter by status → correct subset returned
- Combined filters (`?status=Skeleton&hasPdf=false`) → correct intersection
- Empty result set → valid empty Excel with headers only

**HasUploadedPdf event handler**:
- Event published → flag set to true on SharedGame
- Event for non-existent SharedGameId → handled gracefully (logged, not thrown)

### E2E Tests

- Full workflow: Upload Excel → enqueue enrichment → verify games enriched → export Excel
- Full workflow with PDF: Enrich → auto-download PDF → verify `Complete` status

---

## 8. Observability

### Structured Logging

Each operation gets a distinct `EventId` for filtering:

| EventId | Operation | Level |
|---------|-----------|-------|
| 5001 | Excel import started | Information |
| 5002 | Excel row processed (created/duplicate/error) | Debug |
| 5003 | Excel import completed | Information |
| 5010 | Enrichment job claimed | Information |
| 5011 | BGG auto-match attempted | Information |
| 5012 | Enrichment completed | Information |
| 5013 | Enrichment failed | Warning |
| 5014 | Stale item recovered | Warning |
| 5015 | Circuit breaker state change | Warning |
| 5020 | PDF auto-download started | Information |
| 5021 | PDF auto-download completed | Information |
| 5022 | PDF auto-download failed (SSRF/timeout/invalid) | Warning |
| 5023 | Orphaned blob cleaned up | Warning |
| 5030 | Batch notification sent | Information |

### Metrics

Expose via existing monitoring infrastructure:

- `enrichment_queue_depth` (gauge): items in Queued state
- `enrichment_queue_processing` (gauge): items in Processing state
- `enrichment_completed_total` (counter): successful enrichments
- `enrichment_failed_total` (counter): failed enrichments
- `enrichment_duration_seconds` (histogram): time per enrichment job
- `pdf_download_total` (counter, labels: success/failure)
- `excel_import_rows_total` (counter, labels: created/duplicate/error)

### Health Check

Extend existing `SharedGameCatalogHealthCheck`:
- **Degraded** if enrichment queue depth > 500
- **Degraded** if > 20% failure rate in last hour
- **Unhealthy** if circuit breaker is open

### Alerting Thresholds

- Queue depth > 1000 → alert (possible stuck processing)
- Failure rate > 30% in 1 hour → alert (possible BGG API issue)
- Stale recovery fires > 10 items → alert (possible background service issue)

---

## 9. Database Changes

### Migration 1: `AlterBggImportQueueForEnrichment`

- **Add** `JobType` (int, default 0 = Import) on BggImportQueueEntity
- **Add** `SharedGameId` (Guid?, nullable, FK → SharedGame) on BggImportQueueEntity
- **Add** `BatchId` (Guid?, nullable) on BggImportQueueEntity
- **Alter** `BggId` from `int NOT NULL` to `int NULL` on BggImportQueueEntity (remove `required` keyword)
- Backfill: all existing rows get `JobType = 0` (Import), `BatchId = null`

### Migration 2: `AddGameDataStatusAndSkeletonSupport`

- **Add** `GameDataStatus` (int, default 7 = Complete) on SharedGame — backfill all existing rows to `Complete`
- **Add** `BggRawData` (jsonb, nullable) on SharedGame — no index
- **Add** `HasUploadedPdf` (bool, default false) on SharedGame
- **Add** `ExternalUrl` (string?, nullable) on GameRules owned type

### Migration Safety

- All new columns are nullable or have defaults → **backward compatible**
- The `BggId` nullability change requires deploying updated application code FIRST (null guards on all consumers), THEN running the migration. In a rolling deployment, the old code must handle `BggId = null` gracefully.
- **Rollback SQL** (for emergency):
  ```sql
  -- Migration 1 rollback
  ALTER TABLE bgg_import_queue DROP COLUMN job_type, shared_game_id, batch_id;
  ALTER TABLE bgg_import_queue ALTER COLUMN bgg_id SET NOT NULL;
  -- Migration 2 rollback
  ALTER TABLE shared_games DROP COLUMN game_data_status, bgg_raw_data, has_uploaded_pdf;
  -- GameRules.ExternalUrl rollback depends on EF owned type column naming
  ```

---

## Dependencies

### NuGet Packages (new)
- **ClosedXML >= 0.104** (MIT) — Excel parsing and generation

### Existing Services Reused
- `IBggApiService` — BGG search and details
- `IBggImportQueueService` — existing queue infrastructure (extended with `JobType`)
- `BggImportQueueBackgroundService` — existing background processor (extended)
- `IRateLimitService` — rate limiting
- `IBlobStorageService` — PDF storage
- `UserNotifications` bounded context — in-app notifications
- `DocumentProcessing` bounded context — subscribes to `PdfReadyForProcessingEvent`
- `TimeProvider` — for all time-dependent operations (no `DateTime.UtcNow`)

---

## Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| Excel import (500 rows) | ≤ 30 seconds response time |
| Enrichment throughput | ~120 games/hour (1 per 30s, BGG rate limit) |
| Auto-match throughput | ~60 games/hour (2 API calls per game) |
| Excel export (10K rows) | ≤ 10 seconds |
| Queue depth capacity | Up to 10,000 items without degradation |
| PDF download | 60s timeout per file, max 50MB |

---

## Out of Scope

- BGG search for regular users
- Automatic scheduled re-enrichment
- Excel template download endpoint (can be added later)
- Bulk PDF upload from ZIP archive
- DNS rebinding prevention via purpose-built SSRF library (current IP validation is sufficient for admin-only internal tool; can be upgraded later)

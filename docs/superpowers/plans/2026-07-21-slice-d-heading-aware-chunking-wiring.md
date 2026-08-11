# Slice D — Heading-Aware Chunking Wiring (parent/child) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Populate `text_chunks.Heading` / `Level` / `ParentChunkId` / `ElementType` (and the pgvector-embedded corpus) on every ingest path by routing chunking through the already-built `ExtractedDocumentFactory` → `AdvancedChunkingService` (parent section + child sentence hierarchy), so the retrieval heading-path CTE resolves.

**Architecture:** PR #3264 (`a6bd3df91`) already ships the *producer* half: the Unstructured extractor emits `IReadOnlyList<ExtractedElement>? StructuredElements` on `PagedTextExtractionResult`, reaching production through the orchestrator. `ExtractedDocumentFactory.FromExtraction(...)` (groups elements by `"Title"` into heading sections) and `AdvancedChunkingService.ChunkDocumentAsync(ExtractedDocument)` (emits parent Level-0 section chunks + child Level-2 sentence chunks with `ChunkMetadata.Heading`) are built + DI-registered but have **zero production callers**. The persistence sink (`TextChunkEntity` columns + both save paths copying `chunk.Heading` etc.) is wired and waiting for non-null values. This plan connects the two halves: a new `HierarchicalChunk → DocumentChunk` adapter, a stable chunk `Id` on the chunk DTOs so `ParentChunkId` linkage persists, a persisted structured-elements column so the decoupled `IndexPdf` re-index path can rebuild the document, and the wiring itself in a single shared helper reused by all four ingest paths. **Decision locked (user):** persist AND embed BOTH parents (Level 0) and children (Level 2) — a ~2x corpus, retrieval-semantics change is accepted.

**Tech Stack:** .NET 9, EF Core (PostgreSQL 16 + pgvector), MediatR, xUnit + Moq (unit) + Testcontainers (integration). Python unstructured-service unchanged.

## Status (2026-07-21) — SHIPPED (partial) + DESCOPED

**Heading-aware chunking is LIVE on the re-index path.** `IndexPdfCommandHandler` routes chunking through `HeadingAwareChunker.BuildAsync` → `AdvancedChunkingService` (Level-0 parent sections + Level-2 child sentences), populating `text_chunks.Heading`/`Level`/`ParentChunkId`/`ElementType`. Latent until re-EXTRACTION + re-index (~2x corpus).

**⚠️ Ops recipe (verified 2026-07-21) — NO single command materialises headings on the EXISTING corpus.** Existing rows have `StructuredElementsJson = NULL` (extracted before the column existed), and heading-aware chunking lives ONLY in `IndexPdfCommandHandler` which READS that column — it does not extract. So it is a **two-step** flow per PDF:
1. **Re-extract** to repopulate `StructuredElementsJson`: `POST /ingest/pdf/{pdfId}/extract` (`ExtractPdfTextCommand`, extract-only, cheapest) — or the heavier `POST /admin/pdfs/{id}/reindex` (full pipeline; also repopulates the column but wastes a flat chunk/embed pass).
2. **Heading-aware re-chunk/index**: bulk per game `POST /games/{gameId}/kb/reindex` (`ReindexGameKbCommand` → `KbReindexProcessorService` → `IndexPdfCommand` per PDF) — or per PDF `POST /ingest/pdf/{pdfId}/index`.

What does NOT produce headings alone: `make seed-index` (skips unchanged-hash corpus entirely — idempotent no-op), `/admin/pdfs/{id}/reindex` (re-extracts but flat-chunks via the descoped `PdfProcessingPipelineService`), `/games/{id}/kb/reindex` run before step 1 (column still NULL → NullPathDocument flat fallback). Single-pass extract+heading-chunk is exactly what follow-up #3281 adds by wiring the pipeline.

- **Shipped:** Task 1 (stable chunk `Id`), Task 2 (`HierarchicalChunkMapper`), Task 3 (`HeadingAwareChunker`), Task 4 (`PdfDocumentEntity.StructuredElementsJson` jsonb), Task 5 (`IndexPdfCommandHandler` wiring + handler-driven test), **Task G** (embedding-input cap `ChunkingConstants.MaxEmbeddingChars=1800` so oversized parent chunks never fail a re-index; full text still persisted).
- **DESCOPED → follow-up [#3281](https://github.com/meepleAi-app/meepleai-monorepo/issues/3281):** Task 6 (fresh-upload parity — `PdfProcessingPipelineService` / `UploadPdfCommandHandler.Processing` / `CompleteChunkedUploadCommandHandler`, whose PRIMARY chunker is `PrepareForEmbedding`, not the `.ChunkText` fallback this plan assumed — replacing it is a broad fresh-ingestion behaviour change) + the translation-field copy + the Testcontainers HeadingPath CTE round-trip (needs reviving the excluded/stale `IndexPdfIntegrationTests.cs`). Repo rule #1555 is satisfied for the re-index path by Task 5's in-memory handler-driven test.

Tasks 6–7 below are retained for the follow-up's reference; they are NOT part of the shipped branch.

## Global Constraints

- Backend test path: `apps/api/tests/Api.Tests` (NOT `tests/Api.Tests`).
- `TreatWarningsAsErrors=true` + `AnalysisLevel=latest-Recommended` — no new CA/MA/S/IDE warnings. Use `Regex.Count(s)` not `Regex.Matches(s).Count` (CA1875); `StringComparison`/`CultureInfo` on all string ops (MA0002/CA1305).
- CQRS: endpoints use only `IMediator.Send()`.
- Every code change is TDD (failing test first). Repo lesson #1555: ≥1 handler-driven test that exercises the real extract→chunk→persist pipeline and asserts non-null `text_chunks.Heading` in the DB.
- Feature is **latent until re-index** — existing rows keep `Heading=null`; only re-ingestion repopulates. A corpus re-index (`make seed-index` / per-doc `ReindexDocumentCommand`) is an ops follow-up, NOT part of this plan.
- Null/degradation safety: when `StructuredElements` is null/empty (SmolDocling/Docnet/OCR fallback), `ExtractedDocumentFactory.FromExtraction` returns a `NullPathDocument` (single `Heading=null` section) → behaviour is the pre-slice flat chunk, text never lost.

---

## Key existing types (read before starting)

- `ExtractedElement(string Text, int PageNumber, string ElementType)` — `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/ExtractedElement.cs`.
- `PagedTextExtractionResult { ...; IReadOnlyList<ExtractedElement>? StructuredElements }` — `.../Infrastructure/External/IPdfTextExtractor.cs:89`.
- `ExtractedDocument { Guid Id; Guid? GameId; string Content; List<DocumentSection> Sections; int PageCount }` and `DocumentSection { string? Heading; string Content; int Page; string ElementType; int CharStart; int CharEnd; BoundingBox? BBox }` — `.../KnowledgeBase/Application/Services/Chunking/IAdvancedChunkingService.cs:43,74`.
- `ExtractedDocumentFactory.FromExtraction(Guid documentId, Guid? gameId, IReadOnlyList<ExtractedElement>? structuredElements, string flatText) : ExtractedDocument` — `.../Chunking/ExtractedDocumentFactory.cs:18` (internal static).
- `IAdvancedChunkingService.ChunkDocumentAsync(ExtractedDocument, ChunkingConfiguration?, CancellationToken) : Task<List<HierarchicalChunk>>` — `.../Chunking/AdvancedChunkingService.cs:28`. DI at `KnowledgeBaseServiceExtensions.cs:542`.
- `HierarchicalChunk { string Id ("N" guid); string? ParentId; int Level (0 section / 2 sentence); ChunkMetadata Metadata; string Content }`; `ChunkMetadata { string? Heading; string ElementType; int Page; int CharStart; int CharEnd }` — `.../Domain/Chunking/HierarchicalChunk.cs`, `ChunkMetadata.cs`.
- `DocumentChunk { string Text; float[] Embedding; int Page; int CharStart; int CharEnd; string? Heading; short Level=1; Guid? ParentChunkId; string ElementType="NarrativeText" }` — `apps/api/src/Api/Services/VectorSearchModels.cs:9` (**NO Id field today**).
- `DocumentChunkInput { string Text; int Page; int CharStart; int CharEnd; string? Heading; short Level=1; Guid? ParentChunkId; string ElementType }` — `apps/api/src/Api/Services/TextChunkingService.cs:391` (**NO Id field today**).
- `TextChunkEntity { Guid Id; ... string? Heading; Guid? ParentChunkId; short Level=1; string ElementType="NarrativeText" }` — `.../Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs:33-38`. Save sites set `Id = Guid.NewGuid()`.
- Retrieval heading-path CTE walks `text_chunks.ParentChunkId → text_chunks.Heading` (`GetKbChunksHandler.cs:48-71`) — so populating `Heading` + `ParentChunkId` on `text_chunks` is sufficient; **no pgvector schema change is required for headings**.

## File Structure

- **Create** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/HierarchicalChunkMapper.cs` — pure static adapter `HierarchicalChunk[] → DocumentChunk[]` (Id/ParentId parse, Level cast, Heading/ElementType copy). One responsibility: type-bridge + linkage.
- **Create** `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/HeadingAwareChunker.cs` — shared ingest helper `BuildDocumentChunksAsync(structuredElements, flatText, docId, gameId, adv, textChunkingService, ct) → List<DocumentChunk>` (no embeddings) used by all four paths, so the flat-vs-advanced branch + factory + mapper live in ONE place.
- **Modify** `apps/api/src/Api/Services/VectorSearchModels.cs` — add `Guid Id` to `DocumentChunk`.
- **Modify** `apps/api/src/Api/Services/TextChunkingService.cs` — add `Guid Id` to `DocumentChunkInput`.
- **Modify** `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` + its EF config — add `string? StructuredElementsJson` (jsonb) column.
- **New migration** (`dotnet ef migrations add AddPdfStructuredElementsJson`).
- **Modify** `ExtractPdfTextCommandHandler.cs` + `PdfProcessingPipelineService.cs` (extraction writers) — serialize `StructuredElements` into `StructuredElementsJson`.
- **Modify** the 4 ingest chunk steps to call `HeadingAwareChunker` + use the stable `Id` as `TextChunkEntity.Id`: `IndexPdfCommandHandler.cs`, `PdfProcessingPipelineService.cs`, `UploadPdfCommandHandler.Processing.cs`, `CompleteChunkedUploadCommandHandler.cs`.
- **Modify** the translation branch in `PdfProcessingPipelineService.cs:224-237` to copy hierarchy fields.
- **Tests**: `HierarchicalChunkMapperTests.cs`, `HeadingAwareChunkerTests.cs` (unit); extend `IndexPdfCommandHandlerTests.cs` (handler-driven, in-memory DB) to assert parent+child rows with `Heading`/`Level`/`ParentChunkId`; extend `IndexPdfIntegrationTests.cs` (Testcontainers) for the persisted-column round-trip.

---

## Task 1: Stable `Id` on chunk DTOs

**Files:**
- Modify: `apps/api/src/Api/Services/VectorSearchModels.cs` (`DocumentChunk` record)
- Modify: `apps/api/src/Api/Services/TextChunkingService.cs` (`DocumentChunkInput` record)
- Modify: `IndexPdfCommandHandler.SaveTextChunksToPostgresAsync` + `IndexChunksInVectorStoreAsync`, and the same save sites in `PdfProcessingPipelineService`, `UploadPdfCommandHandler.Processing`, `CompleteChunkedUploadCommandHandler` — use `chunk.Id` as `TextChunkEntity.Id` (fallback `chunk.Id == default ? Guid.NewGuid() : chunk.Id`).
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfCommandHandlerTests.cs`

**Interfaces:**
- Produces: `DocumentChunk.Id : Guid` (default `Guid.Empty` — back-compat: existing flat paths that don't set it fall back to a fresh guid at save), `DocumentChunkInput.Id : Guid`.

- [ ] **Step 1: Add `public Guid Id { get; init; }` to `DocumentChunk` and `DocumentChunkInput`.** (No behaviour change yet — default `Guid.Empty`.)
- [ ] **Step 2: In each of the 4 save sites, change `Id = Guid.NewGuid()` → `Id = chunk.Id == default ? Guid.NewGuid() : chunk.Id`.** Grep `Id = Guid.NewGuid()` inside the `TextChunkEntity`/`PgVectorEmbeddingEntity` creation blocks. IMPORTANT: pgvector and text_chunks rows for the SAME chunk must use the SAME `Id`-derived identity only where already aligned by `ChunkIndex`; this task only touches `TextChunkEntity.Id`.
- [ ] **Step 3: Build + run existing IndexPdf handler tests** — `dotnet test --filter "FullyQualifiedName~IndexPdfCommandHandlerTests"` — Expected: all green (fallback preserves current behaviour).
- [ ] **Step 4: Commit** `feat(rag): add stable Id to chunk DTOs for parent/child linkage`.

## Task 2: `HierarchicalChunk → DocumentChunk` adapter (pure)

**Files:**
- Create: `.../KnowledgeBase/Application/Services/Chunking/HierarchicalChunkMapper.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/HierarchicalChunkMapperTests.cs`

**Interfaces:**
- Produces: `internal static class HierarchicalChunkMapper { public static List<DocumentChunk> ToDocumentChunks(IReadOnlyList<HierarchicalChunk> chunks) }`. Each output: `Id = Guid.ParseExact(hc.Id, "N")`, `ParentChunkId = string.IsNullOrEmpty(hc.ParentId) ? null : Guid.ParseExact(hc.ParentId, "N")`, `Level = (short)hc.Level`, `Heading = hc.Metadata.Heading`, `ElementType = string.IsNullOrEmpty(hc.Metadata.ElementType) ? "NarrativeText" : hc.Metadata.ElementType`, `Text = hc.Content`, `Page = hc.Metadata.Page`, `CharStart/CharEnd = hc.Metadata.CharStart/End`, `Embedding = []` (filled later).

- [ ] **Step 1: Write failing tests** covering: parent (ParentId null → ParentChunkId null, Level 0), child (ParentId set → parsed Guid matches parent Id, Level 2, Heading inherited), ElementType default when empty, `Guid.ParseExact("N")` round-trips the `Guid.NewGuid().ToString("N")` ids `AdvancedChunkingService` emits, and a child's `ParentChunkId` equals its parent's `Id` in the same list. Use real `HierarchicalChunk`/`ChunkMetadata` constructed inline.
- [ ] **Step 2: Run — Expected FAIL** (mapper missing).
- [ ] **Step 3: Implement `HierarchicalChunkMapper.ToDocumentChunks`** per the Interfaces block. `internal static`; `using System.Globalization` not needed (`ParseExact` culture-invariant).
- [ ] **Step 4: Run — Expected PASS.**
- [ ] **Step 5: Commit** `feat(rag): HierarchicalChunk -> DocumentChunk adapter`.

## Task 3: Shared `HeadingAwareChunker` helper

**Files:**
- Create: `.../DocumentProcessing/Application/Services/HeadingAwareChunker.cs`
- Test: `apps/api/tests/.../HeadingAwareChunkerTests.cs`

**Interfaces:**
- Consumes: `ExtractedDocumentFactory.FromExtraction`, `IAdvancedChunkingService.ChunkDocumentAsync`, `HierarchicalChunkMapper.ToDocumentChunks`.
- Produces: `internal static class HeadingAwareChunker { public static async Task<List<DocumentChunk>> BuildAsync(IReadOnlyList<ExtractedElement>? structuredElements, string flatText, Guid documentId, Guid? gameId, IAdvancedChunkingService advancedChunking, CancellationToken ct) }` — builds `ExtractedDocument` via the factory (null elements → NullPathDocument safe fallback), runs `ChunkDocumentAsync`, maps via the adapter, returns `DocumentChunk`s WITHOUT embeddings (caller batch-embeds `chunk.Text`).

- [ ] **Step 1: Write failing tests** with a mock `IAdvancedChunkingService` returning a known parent+child `HierarchicalChunk` list: assert `BuildAsync` returns the mapped `DocumentChunk`s (parent Level 0 Heading set, child Level 2 ParentChunkId=parent.Id), and that null `structuredElements` still yields ≥1 chunk (fallback, Heading null) — assert `ChunkDocumentAsync` is called with an `ExtractedDocument` whose `Sections` come from `FromExtraction`.
- [ ] **Step 2: Run — Expected FAIL.**
- [ ] **Step 3: Implement `HeadingAwareChunker.BuildAsync`.**
- [ ] **Step 4: Run — Expected PASS.**
- [ ] **Step 5: Commit** `feat(rag): shared heading-aware chunker helper`.

## Task 4: Persist `StructuredElements` (JSONB column + migration)

**Files:**
- Modify: `.../Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` — add `public string? StructuredElementsJson { get; set; }`.
- Modify: its EF config — `builder.Property(e => e.StructuredElementsJson).HasColumnType("jsonb");`.
- Migration: `dotnet ef migrations add AddPdfStructuredElementsJson` (run from `apps/api/src/Api`). Review the generated `AddColumn` (nullable jsonb, no default).
- Modify: `ExtractPdfTextCommandHandler.cs:126-134` and `PdfProcessingPipelineService.cs:495` (the two extraction writers) — after computing `fullText`, serialize `System.Text.Json.JsonSerializer.Serialize(extractResult.StructuredElements)` into `pdf.StructuredElementsJson` (null when `StructuredElements` is null).
- Test: `IndexPdfIntegrationTests.cs` (Testcontainers) round-trip; unit test for the serialize/deserialize helper if extracted.

**Interfaces:**
- Produces: `PdfDocumentEntity.StructuredElementsJson : string?` (jsonb) holding `List<ExtractedElement>` JSON.

- [ ] **Step 1: Add the property + EF config.**
- [ ] **Step 2: Generate migration; verify `dotnet build` + `dotnet ef migrations add` succeed and the SQL is a single nullable `jsonb` add** (per repo lesson [[feedback_migration_flatten_drops_raw_sql]] this is plain DDL, safe).
- [ ] **Step 3: Serialize in both extraction writers** (guard null).
- [ ] **Step 4: Commit** `feat(rag): persist StructuredElements as jsonb for re-index heading path`.

## Task 5: Wire `IndexPdfCommandHandler` (re-index path)

**Files:**
- Modify: `IndexPdfCommandHandler.cs` — inject `IAdvancedChunkingService`; in `ChunkAndEmbedTextAsync` (line ~332) deserialize `pdf.StructuredElementsJson` → `List<ExtractedElement>?`, call `HeadingAwareChunker.BuildAsync(...)` for the chunk list (replacing the flat `_chunkingService.ChunkText`), then batch-embed each `DocumentChunk.Text` and set `Embedding` (keep the embedded list == persisted list, aligned by index). Remove the forward-wire placeholder comment (`:384-386`).
- Test: `IndexPdfCommandHandlerTests.cs` — handler-driven (in-memory DB), mock `IAdvancedChunkingService`.

**Interfaces:**
- Consumes: `HeadingAwareChunker.BuildAsync`, Task-1 `DocumentChunk.Id`.

- [ ] **Step 1: Write failing handler-driven test** (extend the pattern already in `IndexPdfCommandHandlerTests` that seeds a PDF + mocks chunker/embedder): seed a PDF with `StructuredElementsJson` for a Title+body doc, mock `IAdvancedChunkingService` to return one parent (Heading "Setup", Level 0) + two children (Level 2, ParentChunkId=parent.Id), run `Handle`, assert persisted `text_chunks` (ordered by ChunkIndex) have the parent with `Heading="Setup"`/`Level=0` and children with `Level=2`/`ParentChunkId==parent.Id`; assert `pgvector_embeddings` count == `text_chunks` count (both levels embedded).
- [ ] **Step 2: Run — Expected FAIL** (still flat chunker, Heading null).
- [ ] **Step 3: Implement the wiring** (inject service, deserialize, `BuildAsync`, embed loop, thread `chunk.Id`).
- [ ] **Step 4: Run — Expected PASS**; run full `IndexPdfCommandHandlerTests` for no regression.
- [ ] **Step 5: Commit** `feat(rag): route IndexPdf chunking through AdvancedChunkingService`.

## Task 6: Parity for pipeline + upload paths + translation

**Files:**
- Modify: `PdfProcessingPipelineService.cs` (`ChunkText` ~655 → `HeadingAwareChunker.BuildAsync` using in-scope `extractResult.StructuredElements`; inject `IAdvancedChunkingService`; translation branch `:224-237` copy `Heading/Level/ParentChunkId/ElementType`), `UploadPdfCommandHandler.Processing.cs` (~337), `CompleteChunkedUploadCommandHandler.cs` (~682).
- Test: one handler/integration test per path (or a shared parametrized one) asserting non-null Heading.

- [ ] **Step 1..N (per path):** failing test → wire `BuildAsync` + `chunk.Id` → pass → commit `feat(rag): heading-aware chunking on <path>`. Do the translation-field copy in the pipeline task.

## Task 7: Handler-driven integration test (repo rule #1555) + docs

**Files:**
- Modify: `IndexPdfIntegrationTests.cs` (Testcontainers) — a real extract→persist→index run asserting non-null `text_chunks.Heading` + resolvable `ParentChunkId`; and a `HeadingPath` CTE assertion via the retrieval handler.
- Modify: `CLAUDE.md` "AI Assistant Rules" / a topic doc — note heading-aware chunking is live, ~2x corpus, re-index required.

- [ ] Failing integration test → (already implemented by Tasks 5–6) → pass → commit `test(rag): integration heading-path round-trip`.

---

## Ops follow-up (NOT in this plan, needs user go)
- **Re-index the corpus** (`make seed-index` or per-doc `ReindexDocumentCommand`) — `text_chunks.Heading` backfills only on re-ingestion; ~2x row growth (parents+children).
- Monitor pgvector row count / retrieval quality after re-index (2x corpus is a ranking change).

## Self-review notes
- **Spec coverage:** producer (#3264 done) → persist (T4) → factory+advanced (existing) → adapter (T2) → helper (T3) → Id linkage (T1) → wiring 4 paths (T5,T6) → translation (T6) → tests (T5,T7). All hops covered.
- **Open decision surfaced to user & locked:** embed BOTH parents+children (2x corpus).
- **Type consistency:** `HierarchicalChunk.Id`("N" guid string) ↔ `Guid.ParseExact(x,"N")` ↔ `DocumentChunk.Id`/`TextChunkEntity.Id`(Guid); `Level` int→short; `ParentId` string?→`ParentChunkId` Guid?. Consistent across T1/T2/T5.
- **Risk:** the pgvector arm has no Heading column — retrieval reconstructs HeadingPath via the `text_chunks` CTE, so no pgvector migration is needed; if a future need arises to boost by heading on the vector arm directly, that is a separate schema addition (out of scope).

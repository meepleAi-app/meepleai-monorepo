# Libro Game Architecture

Integration patterns for the Libro Game AI Assistant MVP Phase 1 feature.

## SharedGameCatalog Integration

Photo upload flow leverages `SharedGameCatalog` BC for game matching and deduplication:

- **Lookup by name**: `CheckDuplicateGameQuery` (line 13, `BoundedContexts/SharedGameCatalog/Application/Queries/CheckDuplicateGameQuery.cs`) accepts a `Title` parameter and uses fuzzy title matching via `FindFuzzyDuplicatesByTitleAsync` (line 114-167 of `CheckDuplicateGameQueryHandler.cs`). Levenshtein similarity ≥70% threshold flags games for review.
  
- **Lookup by BGG ID**: `ISharedGameRepository.GetByBggIdAsync` (line 53, `BoundedContexts/SharedGameCatalog/Domain/Repositories/ISharedGameRepository.cs`) provides exact BGG ID matching. Used by `CheckExactDuplicateByBggIdAsync` (line 84-108 of `CheckDuplicateGameQueryHandler.cs`).

- **Recommended action routing**: `ProposalApprovalAction` enum (used at line 179-189 of `CheckDuplicateGameQueryHandler.cs`) determines flow:
  - `MergeKnowledgeBase` — exact BggId match found, merge PDFs into existing game
  - `ApproveAsVariant` — fuzzy title match found, create as variant
  - `ApproveAsNew` — no duplicates, safe to create new game entry

- **New game flow**: when `CheckDuplicateGameQuery` returns `ProposalApprovalAction.ApproveAsNew` (no match found), the app uses `CreateSharedGameCommand` or `CreateSharedGameFromPdfCommand` to create the catalog entry. (Plan referenced `CreateGameCommand` — naming drift, see Audit Verdict.)

## Content Hash Deduplication

Photo-derived manual content deduplication via existing `DocumentProcessing` BC:

- **Interface**: `IPdfDocumentRepository.FindByContentHashAsync` (line 15, `BoundedContexts/DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs`)

> **Note**: `IPdfDocumentRepository` is `internal` — it must be consumed within the `DocumentProcessing` BC; cross-BC access goes through MediatR commands (`UploadPdfCommand`, etc.).
  
- **Implementation**: SHA-256 hash lookup at line 152-161 (`BoundedContexts/DocumentProcessing/Infrastructure/Persistence/PdfDocumentRepository.cs`). Query pattern:
  ```csharp
  var entity = await DbContext.PdfDocuments
      .AsNoTracking()
      .Where(p => p.ContentHash == contentHash)
      .OrderByDescending(p => p.UploadedAt)
      .FirstOrDefaultAsync(cancellationToken);
  ```
  
- **Scope**: Global lookup — `FindByContentHashAsync` does not filter by game context. For scoped dedup (per-game), the sibling `ExistsByContentHashAsync(contentHash, gameId, privateGameId)` at line 163 accepts optional context parameters. Task 1.2 should determine which method fits the libro-game dedup requirement.

- **Cost savings**: Prevents re-indexing of identical PDF content; if hash exists, reuse existing `PdfDocument.Id` and skip embedding pipeline.

## Audit Verdict

✅ **No blockers identified.** Both game lookup mechanisms (`CheckDuplicateGameQuery` for name/BGG matching + `GetByBggIdAsync` for direct BGG lookup) and content hash deduplication (`FindByContentHashAsync`) are production-ready and can be integrated into Phase 1 photo upload flow immediately.

**Architectural alignment**: 
- Plan v2 assumption ✅ verified: `IPdfDocumentRepository` exists (not `IBlobStorage`)
- Pattern audit reference ✅ verified: All expected services located at specified paths
- No extensions required: All needed repository methods already implemented
- **Naming drift**: Plan `FindByNameQuery`/`FindByBggIdQuery` → actual `CheckDuplicateGameQuery`/`GetByBggIdAsync`
- **Naming drift**: Plan `CreateGameCommand` → actual `CreateSharedGameCommand` / `CreateSharedGameFromPdfCommand`

**Integration readiness for Phase 1**:
- Task 1.2 can directly use `IPdfDocumentRepository.FindByContentHashAsync` for dedup check
- Task 1.3 can route `UploadPhotoBatchCommand` result through `CheckDuplicateGameQuery` for game matching
- Task 1.5 (`UploadPhotoBatchCommandHandler`) will create a new ingestion path alongside existing `UploadPdfCommand` / `UploadPrivatePdfCommand` — no existing "AddDocumentAsync" hook exists; the handler must store results via `IPhotoBatchUploadRepository` (to be created in Task 1.2).

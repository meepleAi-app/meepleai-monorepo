# Fix SharedGame RAG Pipeline + Legacy Cleanup

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the broken hybrid search for SharedGame PDFs, update frontend to allow system-agent chat on shared games, and remove dead code.

**Architecture:** The RAG pipeline has two search paths: pgvector semantic search (working — already resolves SharedGameId via COALESCE in `PgVectorStoreAdapter.IndexBatchAsync:222-226`) and PostgreSQL full-text search via `text_chunks` table (broken — SharedGame PDFs indexed with `GameId = Guid.Empty`). The fix adds `SharedGameId` to `TextChunkEntity`, propagates it during indexing in **both** indexing paths (`IndexPdfCommandHandler` and `PdfProcessingPipelineService`), and updates the FTS query. Frontend changes allow system agents (tutor/arbitro) for shared games without requiring custom agent creation.

**Tech Stack:** .NET 9, EF Core, PostgreSQL (tsvector + pgvector), Next.js 16, React 19, TypeScript

**Parent branch:** `main-dev`

**Review notes (2026-04-07):** Plan reviewed and corrected for 7 issues found:
- **[Fixed]** Issue 1: `KnowledgeBase.AgentConfiguration` deletion requires deeper audit (domain events). Task 5 now audit-only.
- **[Fixed]** Issue 2: Second indexing path in `PdfProcessingPipelineService.SaveTextChunksAsync` was missing. Added Task 2b.
- **[Fixed]** Issue 3: `SearchRaptorSummariesAsync` references non-existent `SharedGameId` on `RaptorSummaryEntity`. Removed from scope.
- **[Fixed]** Issue 4: `setStep('agent-selection')` does not exist in `NewChatView`. Corrected to actual component pattern.
- **[Fixed]** Issue 5: Backfill SQL column quoting verified against EF conventions.
- **[Fixed]** Issue 6: Integration test now uses Testcontainers pattern instead of hardcoded connection string.
- **[Fixed]** Issue 7: Test helper methods verified against existing test file before use.

---

## Task 1: Add SharedGameId to TextChunkEntity + EF Migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs:14`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs:15-39`
- Create: Migration via `dotnet ef migrations add`

- [ ] **Step 1: Add SharedGameId property to TextChunkEntity**

In `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs`, add after line 14 (`GameId`):

```csharp
/// <summary>
/// Cross-BC reference to SharedGameCatalog for hybrid search on shared games.
/// When set, FTS queries match on this ID in addition to GameId.
/// </summary>
public Guid? SharedGameId { get; set; }
```

- [ ] **Step 2: Update TextChunkEntityConfiguration**

In `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs`, add after line 15 (`GameId` config):

```csharp
builder.Property(e => e.SharedGameId).IsRequired(false);
```

Add index after line 39 (existing indexes):

```csharp
builder.HasIndex(e => e.SharedGameId);
```

No FK constraint — SharedGameId is a cross-BC reference (same pattern as `VectorDocumentEntity.SharedGameId`).

- [ ] **Step 3: Generate EF migration**

Run:
```bash
cd apps/api/src/Api
dotnet ef migrations add AddSharedGameIdToTextChunks
```

Expected: New migration file created in `Infrastructure/Migrations/`.

- [ ] **Step 4: Verify migration SQL**

Read the generated migration `.cs` file and verify it contains:
- `AddColumn("shared_game_id", table: "text_chunks", type: "uuid", nullable: true)`
- `CreateIndex("ix_text_chunks_shared_game_id", table: "text_chunks", column: "shared_game_id")`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs
git add apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(rag): add SharedGameId column to text_chunks for hybrid search"
```

---

## Task 2: Fix IndexPdfCommandHandler to propagate SharedGameId

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs:87,97,366-401`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfCommandHandlerTests.cs`

- [ ] **Step 1: Read existing test file to verify helper methods**

**Review Issue #7**: Before writing the test, read `IndexPdfCommandHandlerTests.cs` to confirm that `CreatePdfEntity(pdfId)` and `SetupMocksForSuccessfulIndexing(pdf)` helpers exist with compatible signatures. Adjust test code below if method names or signatures differ.

- [ ] **Step 2: Write failing test — SharedGame PDF stores SharedGameId on text chunks**

In `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfCommandHandlerTests.cs`, add:

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public async Task Handle_SharedGamePdf_StoresSharedGameIdOnTextChunks()
{
    // Arrange
    var sharedGameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid().ToString("N");

    var pdf = CreatePdfEntity(pdfId);
    pdf.GameId = null;           // SharedGame PDFs have null GameId
    pdf.PrivateGameId = null;    // Not a private game
    pdf.SharedGameId = sharedGameId;
    pdf.ExtractedText = "Sample game rules text for testing.";

    SetupMocksForSuccessfulIndexing(pdf);

    var command = new IndexPdfCommand(pdfId);

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert — text chunks should have SharedGameId set
    var savedChunks = _dbContext.TextChunks.Where(tc => tc.PdfDocumentId == Guid.Parse(pdfId)).ToList();
    Assert.All(savedChunks, chunk =>
    {
        Assert.Equal(sharedGameId, chunk.SharedGameId);
    });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "Handle_SharedGamePdf_StoresSharedGameIdOnTextChunks" --no-build
```

Expected: FAIL — `SharedGameId` is null on all chunks because current code doesn't set it.

- [ ] **Step 3: Fix effectiveGameId logic and SaveTextChunksToPostgresAsync**

In `IndexPdfCommandHandler.cs`, change line 87:

```csharp
// BEFORE:
var effectiveGameId = pdf.PrivateGameId ?? pdf.GameId ?? Guid.Empty;

// AFTER:
var effectiveGameId = pdf.PrivateGameId ?? pdf.GameId ?? pdf.SharedGameId ?? Guid.Empty;
```

Update `SaveTextChunksToPostgresAsync` signature (line 366) to accept SharedGameId:

```csharp
private async Task SaveTextChunksToPostgresAsync(
    string pdfId,
    Guid gameId,
    Guid? sharedGameId,
    List<DocumentChunk> documentChunks,
    CancellationToken cancellationToken)
```

Update the TextChunkEntity creation (line 384-396) to include SharedGameId:

```csharp
var textChunkEntities = documentChunks
    .Select((chunk, index) => new TextChunkEntity
    {
        Id = Guid.NewGuid(),
        GameId = gameId,
        SharedGameId = sharedGameId,
        PdfDocumentId = pdfGuid,
        Content = chunk.Text,
        ChunkIndex = index,
        PageNumber = chunk.Page,
        CharacterCount = chunk.Text.Length,
        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
    })
    .ToList();
```

Update the call site (line 97):

```csharp
await SaveTextChunksToPostgresAsync(pdfId, effectiveGameId, pdf.SharedGameId, documentChunks!, cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "Handle_SharedGamePdf_StoresSharedGameIdOnTextChunks"
```

Expected: PASS

- [ ] **Step 5: Run all IndexPdfCommandHandler tests**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "IndexPdf"
```

Expected: All existing tests still pass (effectiveGameId fallback chain is backwards-compatible).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/
git commit -m "fix(rag): propagate SharedGameId to text_chunks during PDF indexing"
```

---

## Task 2b: Fix PdfProcessingPipelineService (second indexing path)

**Review Issue #2**: There is a second text chunk saving path that also lacks SharedGameId propagation.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:579-616`

- [ ] **Step 1: Read PdfProcessingPipelineService.SaveTextChunksAsync**

Read lines 579-616 of `PdfProcessingPipelineService.cs`. Find the `TextChunkEntity` creation block where `GameId = pdfDoc.GameId` is set (around line 601).

- [ ] **Step 2: Fix SharedGameId propagation**

Update the TextChunkEntity creation in `SaveTextChunksAsync`:

```csharp
// BEFORE:
GameId = pdfDoc.GameId,

// AFTER:
GameId = pdfDoc.SharedGameId ?? pdfDoc.GameId,
SharedGameId = pdfDoc.SharedGameId,
```

This ensures chunks saved via this pipeline path also have the correct game ID for FTS.

- [ ] **Step 3: Build and run existing pipeline tests**

```bash
cd apps/api/src/Api
dotnet build && dotnet test --filter "PdfProcessingPipeline"
```

Expected: Build succeeds, all existing tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "fix(rag): propagate SharedGameId in PdfProcessingPipelineService text chunk path"
```

---

## Task 3: Update TextChunkSearchService to query by SharedGameId

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchService.cs:44,127`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchServiceTests.cs`

- [ ] **Step 1: Write failing test — FTS finds chunks by SharedGameId**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchServiceTests.cs`:

**Review Issue #6**: Use the project's existing Testcontainers pattern instead of hardcoded connection strings. Search for existing integration test base classes (e.g., `IntegrationTestBase`, `DatabaseFixture`, or `IClassFixture<>` patterns) and follow the same approach.

```csharp
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// NOTE: Before implementing, search for existing Testcontainers PostgreSQL fixture in the test project:
///   grep -r "PostgreSqlContainer\|Testcontainers\|IntegrationTestBase" tests/ --include="*.cs" -l
/// Use the same pattern found. FTS requires real PostgreSQL (not in-memory).
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class TextChunkSearchServiceTests : IClassFixture</* use project's existing PostgreSQL fixture */>
{
    private readonly MeepleAiDbContext _db;
    private readonly TextChunkSearchService _service;

    public TextChunkSearchServiceTests(/* inject fixture */)
    {
        // Initialize _db and _service from the fixture's connection
        _service = new TextChunkSearchService(_db, NullLogger<TextChunkSearchService>.Instance);
    }

    [Fact]
    public async Task FullTextSearchAsync_FindsChunksBySharedGameId()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var chunk = new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId, // effectiveGameId resolved to SharedGameId
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfId,
            Content = "Roll two dice and move your piece forward",
            ChunkIndex = 0,
            PageNumber = 1,
            CharacterCount = 42,
            CreatedAt = DateTime.UtcNow
        };
        _db.TextChunks.Add(chunk);
        await _db.SaveChangesAsync();

        // Act
        var results = await _service.FullTextSearchAsync(sharedGameId, "dice move", 10, CancellationToken.None);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.PdfDocumentId == pdfId);
    }
}
```

- [ ] **Step 2: Run test to verify it passes (baseline — GameId already matches)**

This test should pass even before the SQL change because `GameId = sharedGameId`. This is the baseline.

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "FullTextSearchAsync_FindsChunksBySharedGameId"
```

Expected: PASS (baseline — confirms FTS works when GameId is set correctly).

- [ ] **Step 3: Write test for SharedGameId-only match (GameId is different)**

Add to the same test file:

```csharp
[Fact]
public async Task FullTextSearchAsync_FindsChunksBySharedGameId_WhenGameIdDiffers()
{
    // Arrange — chunk has a different GameId but matching SharedGameId
    var sharedGameId = Guid.NewGuid();
    var differentGameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();
    var chunk = new TextChunkEntity
    {
        Id = Guid.NewGuid(),
        GameId = differentGameId,     // Different from search ID
        SharedGameId = sharedGameId,  // Matches search ID
        PdfDocumentId = pdfId,
        Content = "Each player draws five cards from the deck",
        ChunkIndex = 0,
        PageNumber = 1,
        CharacterCount = 44,
        CreatedAt = DateTime.UtcNow
    };
    _db.TextChunks.Add(chunk);
    await _db.SaveChangesAsync();

    // Act — search by sharedGameId
    var results = await _service.FullTextSearchAsync(sharedGameId, "player draws cards", 10, CancellationToken.None);

    // Assert
    Assert.NotEmpty(results);
}
```

- [ ] **Step 4: Run test to verify it fails**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "FindsChunksBySharedGameId_WhenGameIdDiffers"
```

Expected: FAIL — current SQL only checks `GameId`, not `SharedGameId`.

- [ ] **Step 5: Update FullTextSearchAsync SQL**

In `TextChunkSearchService.cs`, update the SQL in `FullTextSearchAsync` (line 44):

```csharp
// BEFORE:
WHERE tc.""GameId"" = {gameId}

// AFTER:
WHERE (tc.""GameId"" = {gameId} OR tc.""SharedGameId"" = {gameId})
```

Full updated query:

```csharp
var results = await _dbContext.TextChunks
    .FromSqlInterpolated($@"
        SELECT tc.*
        FROM text_chunks tc
        WHERE (tc.""GameId"" = {gameId} OR tc.""SharedGameId"" = {gameId})
          AND tc.search_vector @@ plainto_tsquery('english', {sanitizedQuery})
        ORDER BY ts_rank(tc.search_vector, plainto_tsquery('english', {sanitizedQuery})) DESC
        LIMIT {limit}")
    .AsNoTracking()
    .Select(tc => new TextChunkMatch(
        tc.PdfDocumentId,
        tc.Content,
        tc.ChunkIndex,
        tc.PageNumber,
        0f))
    .ToListAsync(ct)
    .ConfigureAwait(false);
```

- [ ] **Step 6: DO NOT update SearchRaptorSummariesAsync**

**Review Issue #3**: `RaptorSummaryEntity` does NOT have a `SharedGameId` column. Adding `OR rs."SharedGameId"` would cause a runtime PostgreSQL error. The RAPTOR summaries fix requires its own schema migration (out of scope for this PR). Leave `SearchRaptorSummariesAsync` unchanged — it will return empty for shared games but the hybrid search still works via text chunks + pgvector.

- [ ] **Step 7: Run both tests to verify they pass**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "TextChunkSearchServiceTests"
```

Expected: Both tests PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchService.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchServiceTests.cs
git commit -m "fix(rag): hybrid FTS now queries SharedGameId for shared game documents"
```

---

## Task 4: Frontend — Allow system agents for shared games without custom agents

**Files:**
- Modify: `apps/web/src/components/chat-unified/NewChatView.tsx` (lines ~522-550, direct game mode)

- [ ] **Step 1: Read current direct game mode logic**

In `NewChatView.tsx`, the current logic when `directGameId` is set (lines ~522-550):
- 0 custom agents → redirect to `/chat/agents/create?gameId=...`
- 1 custom agent → auto-create thread
- 2+ custom agents → show picker

The problem: for shared games with no custom agent, the user gets redirected to agent creation instead of being able to use system agents.

- [ ] **Step 2: Read the actual component state and flow**

**Review Issue #4**: `NewChatView.tsx` does NOT use `setStep()` — there is no step state machine. The component uses `autoStartedRef`, `customAgents`, and render guards. Read lines 517-560 (useEffect) and lines 652-674 (render guard) to understand the actual pattern.

- [ ] **Step 3: Update the useEffect to NOT redirect on 0 agents**

In the `useEffect` at ~line 522, find the 0-agents case that calls `router.push('/chat/agents/create?gameId=...')`. Replace with a no-op that just marks auto-start as done:

```typescript
// BEFORE:
if (customAgents.length === 0) {
  router.push(`/chat/agents/create?gameId=${directGameId}`);
  return;
}

// AFTER:
if (customAgents.length === 0) {
  // No custom agents — fall through to show system agent selection UI.
  // System agents (tutor, arbitro, strategist) work with shared game KB.
  autoStartedRef.current = true;
  return;
}
```

- [ ] **Step 4: Update the render guard to not spin forever on 0 agents**

At ~line 654, the render guard shows a loading spinner when `customAgents.length <= 1`. Change to `=== 1` so 0-agents falls through to the main selection UI:

```typescript
// BEFORE:
if (directGameId && isLoadingCustomAgents && customAgents.length <= 1 && !error) {

// AFTER:
if (directGameId && isLoadingCustomAgents && customAgents.length === 1 && !error) {
```

This allows the 0-agents case to reach the main return block (~line 676) which already renders system agent selection.

- [ ] **Step 5: Verify the system agent list is shown**

The main return block at ~line 676 already renders both custom and system agent options. With the render guard change, the 0-agents case now falls through to this UI instead of being trapped in a loading spinner or redirect.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/chat-unified/NewChatView.tsx
git commit -m "fix(chat): show system agents for shared games without custom agents"
```

---

## Task 5: Audit KnowledgeBase.AgentConfiguration (DO NOT delete yet)

**Review Issue #1**: The entity has associated domain events (`AgentConfigurationCreatedEvent`, `AgentConfigurationActivatedEvent`) and is referenced by 60+ files via namespace import. Deletion requires a thorough audit that is out of scope for this bug-fix PR.

**Files:**
- Read-only audit: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentConfiguration.cs`

- [ ] **Step 1: Audit all references (broader grep)**

Run:
```bash
cd apps/api/src/Api
grep -rn "AgentConfiguration\b" --include="*.cs" | grep -v "UserLibrary" | grep -v "\.Designer\.cs" | grep -v "Migrations/"
```

Document ALL references found. Pay attention to:
- `AgentConfigurationCreatedEvent` / `AgentConfigurationActivatedEvent` consumers
- Any handler that subscribes to these events
- Any file that uses unqualified `AgentConfiguration` with KB namespace import

- [ ] **Step 2: Create tracking issue for dead code removal**

If audit confirms the entity is safe to remove, create a separate GitHub issue:

```bash
gh issue create --title "chore: remove orphaned KnowledgeBase.AgentConfiguration entity" \
  --body "Audit found no runtime consumers. Safe to remove entity, tests, and domain events. Separate from RAG fix PR." \
  --label "tech-debt"
```

- [ ] **Step 3: Document audit results in this PR description**

Include audit findings in the PR body so reviewers understand why removal was deferred.

---

## Task 6: Audit duplicate AutoCreateAgentOnPdfReadyHandler (DO NOT delete yet)

**Review Issue #6 (from reviewer)**: The two handlers handle DIFFERENT events and DIFFERENT cases:
- **KnowledgeBase version**: Handles `PdfStateChangedEvent` for admin-priority SharedGame PDFs
- **GameManagement version**: Handles `VectorDocumentReadyIntegrationEvent` for PrivateGame PDFs

They are NOT truly duplicates. Consolidation requires careful analysis of event sourcing.

**Files:**
- Read-only: Both handler files

- [ ] **Step 1: Verify both handlers' event subscriptions**

```bash
cd apps/api/src/Api
grep -rn "INotificationHandler\|IRequestHandler" --include="AutoCreateAgentOnPdfReadyHandler.cs"
```

Document which event each handler subscribes to.

- [ ] **Step 2: Trace both event publications**

```bash
cd apps/api/src/Api
grep -rn "Publish.*PdfStateChangedEvent\|Publish.*VectorDocumentReadyIntegrationEvent" --include="*.cs"
```

Map which pipeline publishes which event.

- [ ] **Step 3: Create tracking issue if consolidation is beneficial**

```bash
gh issue create --title "chore: analyze AutoCreateAgentOnPdfReadyHandler consolidation" \
  --body "Two handlers exist for different events. Analyze if they can be merged or if both are needed." \
  --label "tech-debt"
```

---

## Task 7: Data migration — backfill SharedGameId on existing text_chunks

**Files:**
- Create: SQL migration script or EF migration with raw SQL

- [ ] **Step 1: Write backfill SQL**

Create a data migration that backfills `shared_game_id` on existing `text_chunks` rows where the PDF belongs to a shared game:

**Review Issue #5**: Column names must match EF Core naming convention. Verify actual column names from the Task 1 migration output before writing SQL. EF Core with Npgsql uses snake_case by default for new columns.

```sql
-- Step 1: Backfill shared_game_id on text_chunks from pdf_documents
-- Column names: verify from Task 1 migration output (likely snake_case)
UPDATE text_chunks tc
SET shared_game_id = pd.shared_game_id
FROM pdf_documents pd
WHERE tc.pdf_document_id = pd."Id"
  AND pd.shared_game_id IS NOT NULL
  AND tc.shared_game_id IS NULL;

-- Step 2: Fix GameId for text_chunks that have Guid.Empty (the original bug)
-- NOTE: "GameId" uses PascalCase (FK to games table, set in TextChunkEntityConfiguration)
UPDATE text_chunks tc
SET "GameId" = pd.shared_game_id
FROM pdf_documents pd
WHERE tc.pdf_document_id = pd."Id"
  AND pd.shared_game_id IS NOT NULL
  AND (tc."GameId" IS NULL OR tc."GameId" = '00000000-0000-0000-0000-000000000000');
```

- [ ] **Step 2: Verify column names from Task 1 migration**

Before writing the backfill migration, read the Task 1 generated migration to confirm:
- `text_chunks.shared_game_id` (new column name)
- `pdf_documents.shared_game_id` (source column name)
- `pdf_documents."Id"` vs `pdf_documents.id` (PK column casing)

Adjust the SQL accordingly.

- [ ] **Step 3: Add as EF migration with raw SQL**

Run:
```bash
cd apps/api/src/Api
dotnet ef migrations add BackfillSharedGameIdOnTextChunks
```

Edit the generated migration to include the verified SQL:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Backfill shared_game_id from pdf_documents onto text_chunks
    migrationBuilder.Sql("""
        UPDATE text_chunks tc
        SET shared_game_id = pd.shared_game_id
        FROM pdf_documents pd
        WHERE tc.pdf_document_id = pd."Id"
          AND pd.shared_game_id IS NOT NULL
          AND tc.shared_game_id IS NULL;

        UPDATE text_chunks tc
        SET "GameId" = pd.shared_game_id
        FROM pdf_documents pd
        WHERE tc.pdf_document_id = pd."Id"
          AND pd.shared_game_id IS NOT NULL
          AND (tc."GameId" IS NULL OR tc."GameId" = '00000000-0000-0000-0000-000000000000');
    """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Intentionally empty — data backfill is not reversible
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "fix(rag): backfill SharedGameId on existing text_chunks for shared games"
```

---

## Task 8: Integration test — end-to-end SharedGame RAG hybrid search

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/SharedGameRagIntegrationTests.cs`

- [ ] **Step 1: Write E2E test**

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public async Task SharedGame_RagHybridSearch_ReturnsResults()
{
    // Arrange — seed a shared game with indexed text chunks
    var sharedGameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();

    // Seed text chunks with SharedGameId
    var chunks = Enumerable.Range(0, 3).Select(i => new TextChunkEntity
    {
        Id = Guid.NewGuid(),
        GameId = sharedGameId,
        SharedGameId = sharedGameId,
        PdfDocumentId = pdfId,
        Content = $"Game rule chunk {i}: Players take turns rolling dice and moving pieces on the board.",
        ChunkIndex = i,
        PageNumber = i + 1,
        CharacterCount = 70,
        CreatedAt = DateTime.UtcNow
    }).ToList();

    _dbContext.TextChunks.AddRange(chunks);
    await _dbContext.SaveChangesAsync();

    // Act — search using the shared game ID
    var results = await _textSearchService.FullTextSearchAsync(
        sharedGameId, "rolling dice", 10, CancellationToken.None);

    // Assert
    Assert.NotEmpty(results);
    Assert.All(results, r => Assert.Equal(pdfId, r.PdfDocumentId));
}
```

- [ ] **Step 2: Run integration test**

Run:
```bash
cd apps/api/src/Api
dotnet test --filter "SharedGame_RagHybridSearch_ReturnsResults"
```

Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/SharedGameRagIntegrationTests.cs
git commit -m "test(rag): add integration test for SharedGame hybrid search"
```

---

## Task 9: PR + Review

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api/src/Api
dotnet test
```

Expected: All tests pass, zero failures.

- [ ] **Step 2: Run frontend build**

```bash
cd apps/web
pnpm build
```

Expected: Build succeeds.

- [ ] **Step 3: Create PR to `main-dev`**

```bash
git push -u origin feature/fix-shared-game-rag-pipeline
gh pr create --base main-dev --title "fix(rag): SharedGame hybrid search + dead code cleanup" --body-file /tmp/pr-body.md
```

PR body should include:
- Summary of bug (SharedGame PDFs indexed with GameId=Guid.Empty)
- Fix description (SharedGameId propagation + FTS query update)
- Dead code removed (KnowledgeBase.AgentConfiguration entity)
- Frontend change (system agents shown for shared games)
- Test coverage added

- [ ] **Step 4: Request code review**

Use `superpowers:requesting-code-review` skill.

---

## Verification Checklist

| Check | Command | Expected |
|-------|---------|----------|
| Backend builds | `dotnet build` | Success |
| Backend tests | `dotnet test` | All pass |
| Frontend builds | `pnpm build` | Success |
| Migration applies | `dotnet ef database update` | No errors |
| Existing RAG still works | Manual test with private game | Results returned |
| SharedGame RAG works | Manual test with shared game | FTS + vector results |

## Files Changed Summary

| Action | File | Reason |
|--------|------|--------|
| Modify | `TextChunkEntity.cs` | Add SharedGameId property |
| Modify | `TextChunkEntityConfiguration.cs` | Configure SharedGameId column + index |
| Modify | `IndexPdfCommandHandler.cs` | Propagate SharedGameId during indexing |
| Modify | `PdfProcessingPipelineService.cs` | Propagate SharedGameId in second indexing path |
| Modify | `TextChunkSearchService.cs` | Query by SharedGameId in FTS (text_chunks only, NOT raptor_summaries) |
| Modify | `NewChatView.tsx` | Show system agents for 0-custom-agent shared games |
| Audit | `KnowledgeBase/Domain/Entities/AgentConfiguration.cs` | Audit for future removal (separate issue) |
| Audit | Both `AutoCreateAgentOnPdfReadyHandler.cs` | Audit for consolidation (separate issue) |
| Create | EF migration (schema + backfill) | Add column + backfill existing data |
| Create | `TextChunkSearchServiceTests.cs` | New test coverage for FTS |
| Create | `SharedGameRagIntegrationTests.cs` | E2E integration test |

## Out of Scope (tracked as separate issues)

| Item | Reason | Tracking |
|------|--------|----------|
| Delete `KnowledgeBase.AgentConfiguration` | Has domain events, needs deeper audit | GitHub issue |
| Consolidate `AutoCreateAgentOnPdfReadyHandler` | Two handlers serve different events/cases | GitHub issue |
| Fix `SearchRaptorSummariesAsync` for SharedGames | `RaptorSummaryEntity` needs own SharedGameId migration | Future PR |
| Add SharedGameId to `RaptorSummaryEntity` | Schema change + RAPTOR indexing fix | Future PR |

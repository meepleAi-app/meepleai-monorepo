# KB Doc-Panel Actions + Chunks Similarity-Search (F3-FU-4 #1653) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Re-index / Download / Delete / Export-chunks-JSON actions + per-document similarity-search to the admin KB explorer's `KbDocDetailPanel`, with actions reachable for `failed`/`processing` docs.

**Architecture:** Backend adds 3 admin endpoints (delete-with-cascade, chunks-export, per-doc scored search) under the existing admin-gated groups, reusing `DeletePdfCommand` mechanics + the `IEmbeddingRepository` per-doc filter. Frontend adds an action-bar + search box to `KbDocDetailPanel`, threading the selected doc's `{title, gameId}` from `KbExplorer` so the locked-state restructure can render actions without a 423-contract change.

**Tech Stack:** .NET 9 Minimal APIs + MediatR + EF Core (pgvector) + xUnit/Testcontainers · Next.js 16 + React 19 + TanStack Query + Tailwind + Vitest + Playwright.

**Spec:** `docs/superpowers/specs/2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md` (read it + its Addendum first).

**Branch:** `feature/issue-1653-kb-doc-actions` (already created, parent `main-dev`).

---

## File Structure

**Backend (create):**
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeleteKbDocumentCommand.cs` — admin delete w/ agent-cascade + audit
- `…/DeleteKbDocumentCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/ExportDocumentChunks/ExportDocumentChunksQuery.cs` (+ Handler, + DTO)
- `…/KnowledgeBase/Application/Queries/SearchDocumentChunks/SearchDocumentChunksByVectorQuery.cs` (+ Handler, + DTOs)

**Backend (modify):**
- `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs` — add `DELETE /{pdfId:guid}`
- `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` — add export + chunk-search endpoints
- `…/KnowledgeBase/Domain/Repositories/IEmbeddingRepository.cs` — add `SearchByVectorWithScoresAsync`
- `…/KnowledgeBase/Infrastructure/Persistence/EmbeddingRepository.cs` + `PgVectorStoreAdapter.cs` — implement score-returning search (additive)

**Frontend (create):**
- `apps/web/src/components/admin/knowledge-base/explorer/actions/KbDocActions.tsx` — hero action-bar
- `apps/web/src/components/admin/knowledge-base/explorer/search/KbChunkSearch.tsx` — in-panel similarity-search
- `apps/web/src/hooks/queries/useKbDocActions.ts` — `useDeleteKbDoc`, `useDocChunkSearch`
- test files alongside each

**Frontend (modify):**
- `apps/web/src/lib/api/clients/pdfClient.ts` (+ a kb client) — new methods
- `apps/web/src/components/ui/admin/admin-confirmation-dialog.tsx` — optional `confirmPhrase`
- `apps/web/src/components/admin/knowledge-base/explorer/KbExplorer.tsx` — thread `{title, gameId}` on select
- `apps/web/src/components/admin/knowledge-base/explorer/KbTree.tsx` — `onSelectDoc(doc)` carries metadata
- `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` — locked-restructure + mount action-bar + search

---

## BACKEND

### Task 1: Admin delete command with agent-cascade + audit

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeleteKbDocumentCommand.cs`
- Create: `…/Commands/DeleteKbDocumentCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeleteKbDocumentCommandHandlerIntegrationTests.cs`

> Read first: `DeletePdfCommandHandler.cs` (blob delete + cache-invalidate body to reuse), `AdminPdfManagementEndpoints.cs` (group + filter + reindex pattern), an existing `[AuditableAction]`+`[AtomicAudit]` command (e.g. a destructive admin command in Administration BC) for the exact attribute args, and `IAgentDefinitionRepository` (`GetByConsumedDocumentAsync`, `UpdateAsync`) + `AgentDefinition.UpdateKbCardIds`.

- [ ] **Step 1: Write the failing integration test**

```csharp
// DeleteKbDocumentCommandHandlerIntegrationTests.cs (Testcontainers Postgres, follow sibling Integration test setup)
[Fact]
[Trait("Category", "Integration")]
public async Task Handle_RemovesDoc_ChunksVectors_AndDetachesFromConsumingAgents()
{
    // Arrange: seed a SharedGame + PdfDocument + VectorDocument + 2 TextChunks,
    // and 2 AgentDefinitions whose KbCardIds contain the pdfDocId.
    var pdfId = await SeedIndexedDocWithAgentsAsync(agentCount: 2);

    // Act
    await _mediator.Send(new DeleteKbDocumentCommand(pdfId));

    // Assert
    (await _db.PdfDocuments.FindAsync(pdfId)).Should().BeNull();
    (await _db.TextChunks.CountAsync(c => c.PdfDocumentId == pdfId)).Should().Be(0);
    (await _db.VectorDocuments.CountAsync(v => v.PdfDocumentId == pdfId)).Should().Be(0);
    var agents = await _db.AgentDefinitions.ToListAsync();
    agents.Should().OnlyContain(a => !a.KbCardIds.Contains(pdfId));
}

[Fact]
[Trait("Category", "Integration")]
public async Task Handle_Throws_NotFound_WhenDocMissing()
{
    var act = async () => await _mediator.Send(new DeleteKbDocumentCommand(Guid.NewGuid()));
    await act.Should().ThrowAsync<NotFoundException>();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~DeleteKbDocumentCommandHandlerIntegrationTests"`
Expected: FAIL — `DeleteKbDocumentCommand` does not exist.

- [ ] **Step 3: Create the command**

```csharp
// DeleteKbDocumentCommand.cs
using Api.SharedKernel.Application.Interfaces;
using Api.BoundedContexts.Administration.Application.Attributes;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

[AuditableAction("KbDocumentDelete", "Document", Level = 2)]
[AtomicAudit]
public sealed record DeleteKbDocumentCommand(Guid Id) : ICommand;
```
> The audit behavior auto-extracts `resourceId` from the command's `Id` property — name it `Id`.

- [ ] **Step 4: Implement the handler**

```csharp
// DeleteKbDocumentCommandHandler.cs
internal sealed class DeleteKbDocumentCommandHandler : ICommandHandler<DeleteKbDocumentCommand>
{
    private readonly MeepleAiDbContext _db;
    private readonly IAgentDefinitionRepository _agents;
    private readonly IVectorStore _vectorStore;          // PgVectorStoreAdapter — confirm exact iface name
    private readonly IBlobStorageService _blob;
    private readonly IAiResponseCacheService _cache;
    private readonly ILogger<DeleteKbDocumentCommandHandler> _logger;
    // ctor: assign + null-guard all (mirror DeletePdfCommandHandler)

    public async Task Handle(DeleteKbDocumentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var doc = await _db.PdfDocuments.FirstOrDefaultAsync(p => p.Id == command.Id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException($"PDF document {command.Id} not found");

        // 1) Detach from consuming agents (D-1 = cascade)
        var consuming = await _agents.GetByConsumedDocumentAsync(command.Id, ct).ConfigureAwait(false);
        foreach (var agent in consuming)
        {
            agent.UpdateKbCardIds(agent.KbCardIds.Where(id => id != command.Id));
            await _agents.UpdateAsync(agent, ct).ConfigureAwait(false);
        }

        // 2) Explicitly delete pgvector embeddings (NO FK cascade — see spec Addendum)
        var vectorDocId = await _db.VectorDocuments
            .Where(v => v.PdfDocumentId == command.Id).Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (vectorDocId is Guid vId)
            await _vectorStore.DeleteByVectorDocumentIdAsync(vId, ct).ConfigureAwait(false);

        // 3) Remove the doc — EF cascade removes TextChunks + VectorDocument
        var storageGameId = (doc.PrivateGameId ?? doc.SharedGameId)?.ToString() ?? string.Empty;
        _db.PdfDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);   // single commit: agent updates + delete

        // 4) Best-effort blob + cache cleanup (reuse DeletePdfCommandHandler helpers verbatim)
        await DeletePhysicalFileAsync(command.Id.ToString(), storageGameId, ct).ConfigureAwait(false);
        await InvalidateCacheSafelyAsync(storageGameId, "KB doc deletion", ct).ConfigureAwait(false);
    }
    // copy DeletePhysicalFileAsync + InvalidateCacheSafelyAsync from DeletePdfCommandHandler
}
```
> Verify exact `IVectorStore` interface name + `DeleteByVectorDocumentIdAsync` signature in `PgVectorStoreAdapter.cs`. Register the handler if the BC doesn't auto-scan (check how `DeletePdfCommandHandler` is registered).

- [ ] **Step 5: Add the endpoint**

```csharp
// AdminPdfManagementEndpoints.cs — inside the /admin/pdfs group, next to reindex
group.MapDelete("/{pdfId:guid}", DeleteKbDocument)
    .WithName("DeleteKbDocument")
    .WithSummary("Delete a KB document (cascade: detach from agents, remove chunks/vectors/blob, audited)");

private static async Task<IResult> DeleteKbDocument(
    Guid pdfId, IMediator mediator, CancellationToken ct)
{
    await mediator.Send(new DeleteKbDocumentCommand(pdfId), ct).ConfigureAwait(false);
    return Results.NoContent();
}
```
> `NotFoundException` → 404 is handled by the global exception mapper (#2568). Confirm the group already has `RequireAdminSessionFilter`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~DeleteKbDocumentCommandHandlerIntegrationTests"`
Expected: PASS (both tests).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeleteKbDocument*.cs apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeleteKbDocumentCommandHandlerIntegrationTests.cs
git commit -m "feat(admin-kb): #1653 admin delete KB doc with agent-cascade + audit"
```

---

### Task 2: Export document chunks (full content) endpoint

**Files:**
- Create: `…/KnowledgeBase/Application/Queries/ExportDocumentChunks/ExportDocumentChunksQuery.cs` (record `ExportDocumentChunksQuery(Guid PdfDocumentId) : IQuery<IReadOnlyList<ExportedChunkDto>>`)
- Create: `…/ExportDocumentChunksQueryHandler.cs`, `…/ExportedChunkDto.cs`
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/ExportDocumentChunksQueryHandlerIntegrationTests.cs`

> Read first: `TextChunkEntity.cs` (fields: `Content`, `ChunkIndex`, `PageNumber`, `Heading`), `GetKbChunks` handler for the fetch idiom, and `AdminKnowledgeBaseEndpoints.cs:75-86` (the `docs/{docId}/ingestion-log` endpoint) for the endpoint shape.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Handle_ReturnsAllChunks_FullContent_OrderedByIndex()
{
    var pdfId = await SeedDocWithChunksAsync(count: 3); // contents "A","B","C" at indexes 0,1,2
    var result = await _mediator.Send(new ExportDocumentChunksQuery(pdfId));
    result.Select(c => c.Content).Should().ContainInOrder("A", "B", "C");
    result.Should().HaveCount(3);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ExportDocumentChunksQueryHandlerIntegrationTests"`
Expected: FAIL — query not defined.

- [ ] **Step 3: Implement DTO + query + handler**

```csharp
public sealed record ExportedChunkDto(Guid Id, int ChunkIndex, int? PageNumber, string? Heading, string Content);

internal sealed class ExportDocumentChunksQueryHandler
    : IQueryHandler<ExportDocumentChunksQuery, IReadOnlyList<ExportedChunkDto>>
{
    private readonly MeepleAiDbContext _db; // ctor inject + null-guard
    public async Task<IReadOnlyList<ExportedChunkDto>> Handle(ExportDocumentChunksQuery q, CancellationToken ct)
        => await _db.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == q.PdfDocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new ExportedChunkDto(c.Id, c.ChunkIndex, c.PageNumber, c.Heading, c.Content))
            .ToListAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 4: Add endpoint**

```csharp
// AdminKnowledgeBaseEndpoints.cs (kbGroup)
kbGroup.MapGet("/docs/{docId:guid}/chunks/export", async (Guid docId, IMediator m, CancellationToken ct) =>
{
    var chunks = await m.Send(new ExportDocumentChunksQuery(docId), ct).ConfigureAwait(false);
    return Results.Ok(chunks);
}).WithName("ExportKbDocChunks").WithSummary("Export all chunks (full content) for a document as JSON.");
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ExportDocumentChunksQueryHandlerIntegrationTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/ExportDocumentChunks/ apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/KnowledgeBase/ExportDocumentChunksQueryHandlerIntegrationTests.cs
git commit -m "feat(admin-kb): #1653 export document chunks (full content) endpoint"
```

---

### Task 3: Per-document scored similarity-search

**Files:**
- Modify: `…/KnowledgeBase/Domain/Repositories/IEmbeddingRepository.cs` (+ `EmbeddingRepository.cs`, `PgVectorStoreAdapter.cs`)
- Create: `…/KnowledgeBase/Application/Queries/SearchDocumentChunks/SearchDocumentChunksByVectorQuery.cs` (+ Handler + DTOs)
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Test: `…/Api.Tests/Integration/KnowledgeBase/SearchDocumentChunksQueryHandlerIntegrationTests.cs`

> Read first: `PgVectorStoreAdapter.SearchAsync` (it computes `1-(vec<=>q) AS similarity` and discards it — you will surface it additively), `IEmbeddingService.GenerateEmbeddingAsync` + `Vector` ctor, `VectorDocumentEntity` (`Id`,`PdfDocumentId`,`GameId`), and `VectorSemanticSearchQueryHandler` for the embed→search→map idiom.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Handle_ReturnsScoredHits_ForDoc_OrderedByScoreDesc()
{
    var pdfId = await SeedIndexedDocAsync(); // a doc with embedded chunks in pgvector
    var result = await _mediator.Send(
        new SearchDocumentChunksByVectorQuery(pdfId, "predator activation", TopK: 5, MinScore: 0.0));
    result.ErrorMessage.Should().BeNull();
    result.Results.Should().NotBeEmpty();
    result.Results.Select(r => r.Score).Should().BeInDescendingOrder();
}

[Fact]
[Trait("Category", "Integration")]
public async Task Handle_ReturnsError_WhenDocNotIndexed()
{
    var result = await _mediator.Send(
        new SearchDocumentChunksByVectorQuery(Guid.NewGuid(), "x", 5, 0.0));
    result.Results.Should().BeEmpty();
    result.ErrorMessage.Should().NotBeNull();
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test --filter "FullyQualifiedName~SearchDocumentChunksQueryHandlerIntegrationTests"`
Expected: FAIL — query + repo method missing.

- [ ] **Step 3: Add the score-returning repo method (additive)**

```csharp
// IEmbeddingRepository.cs — new method (do NOT change existing SearchByVectorAsync)
Task<IReadOnlyList<ScoredEmbedding>> SearchByVectorWithScoresAsync(
    Guid gameId, Vector queryVector, int topK, double minScore,
    IReadOnlyList<Guid>? documentIds = null, CancellationToken ct = default);
```
```csharp
// new value record (Domain or Application)
public sealed record ScoredEmbedding(Embedding Embedding, double Score);
```
Implement in `EmbeddingRepository.cs` by delegating to a new adapter method that reads the similarity column the existing `SearchAsync` already SELECTs (`1-(vector<=>q) AS similarity`) but currently drops. In `PgVectorStoreAdapter`, copy `SearchAsync` to `SearchWithScoresAsync` and project the similarity value (column 7) into `ScoredEmbedding` instead of discarding it. Keep `SearchAsync` unchanged (existing RAG callers).

- [ ] **Step 4: Add DTOs + query + handler**

```csharp
public sealed record DocChunkSearchHit(int ChunkIndex, int? PageNumber, double Score, string Snippet);
public sealed record SearchDocumentChunksResultDto(IReadOnlyList<DocChunkSearchHit> Results, string? ErrorMessage);

internal sealed record SearchDocumentChunksByVectorQuery(
    Guid PdfDocumentId, string Query, int TopK, double MinScore)
    : IQuery<SearchDocumentChunksResultDto>;

internal sealed class SearchDocumentChunksByVectorQueryHandler
    : IQueryHandler<SearchDocumentChunksByVectorQuery, SearchDocumentChunksResultDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly IEmbeddingService _embeddings;
    private readonly IEmbeddingRepository _repo; // ctor inject + null-guard

    public async Task<SearchDocumentChunksResultDto> Handle(SearchDocumentChunksByVectorQuery q, CancellationToken ct)
    {
        var vd = await _db.VectorDocuments.AsNoTracking()
            .Where(v => v.PdfDocumentId == q.PdfDocumentId)
            .Select(v => new { v.Id, v.GameId })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (vd is null)
            return new SearchDocumentChunksResultDto(Array.Empty<DocChunkSearchHit>(), "Document not indexed");

        var emb = await _embeddings.GenerateEmbeddingAsync(q.Query, ct).ConfigureAwait(false);
        if (!emb.Success || emb.Embeddings.Count == 0)
            return new SearchDocumentChunksResultDto(Array.Empty<DocChunkSearchHit>(), "Embedding failed");

        var hits = await _repo.SearchByVectorWithScoresAsync(
            vd.GameId, new Vector(emb.Embeddings[0]),
            Math.Clamp(q.TopK, 1, 100), q.MinScore,
            documentIds: new[] { vd.Id }, ct).ConfigureAwait(false);

        var results = hits
            .OrderByDescending(h => h.Score)
            .Select(h => new DocChunkSearchHit(
                h.Embedding.ChunkIndex, h.Embedding.PageNumber, h.Score,
                Truncate(h.Embedding.TextContent, 240)))
            .ToList();
        return new SearchDocumentChunksResultDto(results, null);
    }
    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
```
> Confirm `EmbeddingResult` property names (`Success`, `Embeddings`) against `IEmbeddingService.cs`. Confirm `VectorDocumentEntity.GameId` is non-null (else resolve game via PdfDocument).

- [ ] **Step 5: Add endpoint**

```csharp
// AdminKnowledgeBaseEndpoints.cs (kbGroup)
kbGroup.MapPost("/docs/{docId:guid}/chunks/search", async (
    Guid docId, [FromBody] DocChunkSearchRequest req, IMediator m, CancellationToken ct) =>
{
    var r = await m.Send(new SearchDocumentChunksByVectorQuery(
        docId, req.Query, req.TopK ?? 10, req.MinScore ?? 0.0), ct).ConfigureAwait(false);
    return Results.Ok(r);
}).WithName("SearchKbDocChunks").WithSummary("Per-document semantic chunk search (scored).");
// record DocChunkSearchRequest(string Query, int? TopK, double? MinScore);
```

- [ ] **Step 6: Run to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~SearchDocumentChunksQueryHandlerIntegrationTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/ apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/KnowledgeBase/SearchDocumentChunksQueryHandlerIntegrationTests.cs
git commit -m "feat(admin-kb): #1653 per-document scored similarity-search endpoint"
```

---

## FRONTEND

### Task 4: API client methods + action/search hooks

**Files:**
- Modify: `apps/web/src/lib/api/clients/pdfClient.ts` (+ kb client if export/search belong there)
- Create: `apps/web/src/hooks/queries/useKbDocActions.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useKbDocActions.test.ts`

> Read first: `pdfClient.ts` (mirror `reindexDocument`/`getPdfDownloadUrl`), `client.ts` (`post`/`delete`/`get` signatures), `useKbHub.ts` (`useMutation`+invalidate pattern), and the key factories `kbDocDetailKeys`, `kbChunksListKeys`, `kbGameDocumentKeys`.

- [ ] **Step 1: Write failing hook tests** (Vitest, mock `api.pdf`/`api.kb` + `useQueryClient`)

```ts
it('useDeleteKbDoc calls DELETE /admin/pdfs/{id} and invalidates tree + detail', async () => {
  const del = vi.fn().mockResolvedValue(undefined);
  // arrange mock api.pdf.adminDeleteKbDoc = del; spy invalidateQueries
  const { result } = renderHook(() => useDeleteKbDoc(gameId), { wrapper });
  await act(() => result.current.mutateAsync(docId));
  expect(del).toHaveBeenCalledWith(docId);
  expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: kbGameDocumentKeys.byGame(gameId) });
});
```

- [ ] **Step 2: Run to verify fail** — Run: `pnpm test src/hooks/queries/__tests__/useKbDocActions.test.ts` — Expected: FAIL.

- [ ] **Step 3: Add client methods**

```ts
// pdfClient.ts
adminDeleteKbDoc: (pdfId: string): Promise<void> =>
  httpClient.delete(`/api/v1/admin/pdfs/${pdfId}`),
exportDocChunks: (pdfId: string) =>
  httpClient.get<ExportedChunk[]>(`/api/v1/admin/kb/docs/${pdfId}/chunks/export`),
searchDocChunks: (pdfId: string, body: { query: string; topK?: number; minScore?: number }) =>
  httpClient.post<DocChunkSearchResult>(`/api/v1/admin/kb/docs/${pdfId}/chunks/search`, body),
// getPdfDownloadUrl already exists → reuse for FR-2
```

- [ ] **Step 4: Add hooks**

```ts
// useKbDocActions.ts
export function useDeleteKbDoc(gameId: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (docId: string) => api.pdf.adminDeleteKbDoc(docId),
    onSuccess: () => {
      if (gameId) qc.invalidateQueries({ queryKey: kbGameDocumentKeys.byGame(gameId) });
      qc.invalidateQueries({ queryKey: kbDocDetailKeys.all });
    },
  });
}
export function useDocChunkSearch(docId: string) {
  return useMutation({
    mutationFn: (body: { query: string; topK?: number; minScore?: number }) =>
      api.pdf.searchDocChunks(docId, body),
  });
}
```
> Re-index uses the existing `api.pdf.reindexDocument`; wrap with a small `useReindexDoc(docId, gameId)` invalidating `kbDocDetailKeys.byId(docId)` + `kbChunksListKeys.all`.

- [ ] **Step 5: Run to verify pass** — Expected: PASS.
- [ ] **Step 6: Commit** — `git commit -m "feat(admin-kb): #1653 FE api client methods + doc-action hooks"`

---

### Task 5: Extend AdminConfirmationDialog with `confirmPhrase`

**Files:**
- Modify: `apps/web/src/components/ui/admin/admin-confirmation-dialog.tsx`
- Test: its existing test file (or create one)

> Read first: the component — it hardcodes `typedConfirmation !== 'CONFIRM'`.

- [ ] **Step 1: Failing test** — typing the filename (not "CONFIRM") enables Confirm when `confirmPhrase={filename}`.

```tsx
it('enables confirm when the custom confirmPhrase is typed', () => {
  render(<AdminConfirmationDialog isOpen level={AdminConfirmationLevel.Level2}
    confirmPhrase="Wingspan.pdf" title="t" message="m" onClose={()=>{}} onConfirm={()=>{}} />);
  fireEvent.change(screen.getByRole('textbox'), { target: { value: 'Wingspan.pdf' } });
  expect(screen.getByRole('button', { name: /confirm|conferma/i })).toBeEnabled();
});
```

- [ ] **Step 2: Run fail.**
- [ ] **Step 3: Implement** — add optional `confirmPhrase?: string` prop; `const phrase = confirmPhrase ?? 'CONFIRM'; const isConfirmDisabled = isLevel2 && typedConfirmation !== phrase;` Render the phrase in the prompt label.
- [ ] **Step 4: Run pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat(admin): #1653 AdminConfirmationDialog optional confirmPhrase"`

---

### Task 6: KbDocActions action-bar component

**Files:**
- Create: `…/explorer/actions/KbDocActions.tsx`
- Test: `…/explorer/actions/__tests__/KbDocActions.test.tsx`

> Props: `{ docId: string; fileName: string; gameId: string | null; processingStatus: KbProcessingStatus; usedByCount?: number }`. Toast = `sonner`. Download = `<a href={api.pdf.getPdfDownloadUrl(docId)} download>` (cookie auth). Export = `downloadAsFile(JSON.stringify(chunks,null,2), `${fileName}-chunks.json`)` (helper from `IngestionActions.tsx` — extract to a shared util `explorer/utils/downloadAsFile.ts` and import in both). Used-by = `<Link href={`/admin/knowledge-base?docId=${docId}&tab=used-by`}>`.

- [ ] **Step 1: Failing tests**

```tsx
it('disables Re-index while processing', () => {
  render(<KbDocActions {...base} processingStatus="processing" />);
  expect(screen.getByRole('button', { name: /re-index/i })).toBeDisabled();
});
it('Delete opens typed-confirm showing agent count and deletes on confirm', async () => {
  // mock useDeleteKbDoc; render with usedByCount={3}; click Delete;
  // expect dialog text /3 agent/i; type fileName; confirm; expect mutate called with docId
});
it('Export downloads JSON via exportDocChunks', async () => { /* mock exportDocChunks + downloadAsFile */ });
```

- [ ] **Step 2: Run fail.**
- [ ] **Step 3: Implement** the action-bar: 5 buttons with `type="button"` + `focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2`; destructive Delete uses `bg-destructive text-destructive-foreground`; Re-index `disabled={processingStatus==='processing'||processingStatus==='queued'||isPending}` + `toast.success/error`; Delete renders `AdminConfirmationDialog` with `confirmPhrase={fileName}` and `warningMessage={`Referenced by ${usedByCount} agent(s) — they will be detached.`}`.
- [ ] **Step 4: Run pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat(admin-kb): #1653 KbDocActions action-bar (reindex/download/delete/export/used-by)"`

---

### Task 7: Thread selected doc `{title, gameId}` from KbExplorer → panel

**Files:**
- Modify: `…/explorer/KbTree.tsx` (`onSelectDoc` carries `{ id, title, gameId }`), `…/explorer/KbExplorer.tsx` (store `selectedDoc`), `…/explorer/KbDocDetailPanel.tsx` (accept optional `selectedDocMeta`)
- Test: update `KbTree`/`KbExplorer` tests

> Read first: `KbTree.tsx` `onSelectDoc` call site + `KbExplorer.tsx` selection state.

- [ ] **Step 1: Failing test** — selecting a doc passes `{id,title,gameId}` to the panel.
- [ ] **Step 2: Run fail.**
- [ ] **Step 3: Implement** — change `onSelectDoc: (doc: { id: string; title: string; gameId: string }) => void`; `KbExplorer` keeps `selectedDoc` and passes `selectedDocMeta={selectedDoc}` to `KbDocDetailPanel`. Keep URL `?docId=` as the source of truth for `docId`; metadata is the in-memory companion.
- [ ] **Step 4: Run pass.**
- [ ] **Step 5: Commit** — `git commit -m "refactor(admin-kb): #1653 thread selected doc title+gameId to detail panel"`

---

### Task 8: Locked-restructure + mount action-bar in KbDocDetailPanel

**Files:**
- Modify: `…/explorer/KbDocDetailPanel.tsx`
- Test: `…/explorer/__tests__/KbDocDetailPanel.test.tsx`

> Read first: current locked early-return (lines ~92-115) + the Used-by-through-locked branch.

- [ ] **Step 1: Failing tests**

```tsx
it('renders the action-bar for a failed/locked doc (actions reachable)', () => {
  mockUseKbDocDetail.mockReturnValue({ data: { status: 'locked', processingStatus: 'failed', doc: null } });
  render(<KbDocDetailPanel docId="d1" selectedDocMeta={{ id:'d1', title:'X.pdf', gameId:'g1' }} />, { wrapper });
  expect(screen.getByRole('button', { name: /re-index/i })).toBeInTheDocument();
  expect(screen.getByText(/in elaborazione/i)).toBeInTheDocument();
});
it('ready doc still renders full hero + chunks (unchanged)', () => { /* status:'ready' */ });
```

- [ ] **Step 2: Run fail.**
- [ ] **Step 3: Implement** — restructure: when `locked`, render `<KbDocDetailTabs>` + a slim hero (`selectedDocMeta.title` + `processingStatus` chip) + `<KbDocActions docId fileName={selectedDocMeta.title} gameId={selectedDocMeta.gameId} processingStatus={envelope.processingStatus} />` + the existing amber notice (Export/search disabled when not ready). When `ready`, render `<KbDocActions>` in the hero header (the `<div style="display:flex;gap:6px">` slot from the mockup) + existing chunks. Preserve the Used-by-through-locked branch.
- [ ] **Step 4: Run pass** (+ full panel suite green).
- [ ] **Step 5: Commit** — `git commit -m "feat(admin-kb): #1653 reachable actions on failed docs (locked-restructure)"`

---

### Task 9: In-panel chunk similarity-search

**Files:**
- Create: `…/explorer/search/KbChunkSearch.tsx`
- Modify: `…/explorer/KbDocDetailPanel.tsx` (mount in Overview, ready only)
- Test: `…/explorer/search/__tests__/KbChunkSearch.test.tsx`

> Uses `useDocChunkSearch(docId)`. Mockup L168-221: search box, score-threshold filter (default ≥0.7), results rows (chunk index, page, score badge, snippet).

- [ ] **Step 1: Failing tests** — submitting a query renders scored rows; threshold filter hides low scores; empty result shows empty state; renders disabled when `chunkCount===0`.
- [ ] **Step 2: Run fail.**
- [ ] **Step 3: Implement** — debounced (300ms) search box; on submit call mutation; render `results.filter(r => r.score >= threshold)` sorted desc; score badge color by tier (hue utilities, not neutral). Empty/loading/error states + `toast.error` on failure.
- [ ] **Step 4: Run pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat(admin-kb): #1653 in-panel chunk similarity-search"`

---

### Task 10: E2E smoke

**Files:**
- Create: `apps/web/e2e/admin/kb-doc-actions.spec.ts`

> Follow existing explorer E2E smoke (`thread-view-smoke`-style). Keep minimal.

- [ ] **Step 1: Write smoke** — login admin → open `/admin/knowledge-base` → select a doc → assert action-bar visible; click Re-index → assert toast/processing; (optionally) select a failed doc → assert actions reachable.
- [ ] **Step 2: Run** `pnpm test:e2e admin/kb-doc-actions.spec.ts` (or note CI-only if local infra absent).
- [ ] **Step 3: Commit** — `git commit -m "test(admin-kb): #1653 E2E smoke for doc actions"`

---

## Self-Review (run before execution)

- **Spec coverage:** FR-1 reindex→T4/T6/T8 · FR-2 download→T6 · FR-3 delete+cascade+typed-confirm→T1/T5/T6 · FR-4 export→T2/T6 · FR-5 search→T3/T9 · B5 used-by link→T6 · O-1 locked-restructure→T7/T8. ✅ all covered.
- **Type consistency:** `DeleteKbDocumentCommand(Guid Id)` (Id name required for audit) · `ScoredEmbedding(Embedding, double Score)` · `DocChunkSearchHit(ChunkIndex, PageNumber, Score, Snippet)` · FE `selectedDocMeta {id,title,gameId}` used identically in T7/T8.
- **Open items to confirm at execution** (read-and-confirm, not placeholders): exact `IVectorStore` iface + `DeleteByVectorDocumentIdAsync` signature; `EmbeddingResult` property names; `AdminConfirmationDialog` exact prop/textbox role; handler DI registration idiom in each BC; whether `VectorDocumentEntity.GameId` is non-null.
- **D-1** = cascade (option b) implemented in T1 step 4. **Score gap** fixed additively in T3 step 3 (existing RAG `SearchAsync` untouched).
- **Test baseline:** must not grow unit-fail count above zero (CLAUDE.md policy). Backend integration tests need Docker (Testcontainers).

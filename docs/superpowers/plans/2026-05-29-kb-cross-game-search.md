# KB Cross-Game Search + SSE Ask (#1661) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:test-driven-development` per ogni task (.NET xUnit + Testcontainers). Steps con checkbox `- [ ]`. Backend .NET 9, KnowledgeBase BC, CQRS via `IMediator` (regola 🔴: endpoint usano SOLO `m.Send()`, zero service injection diretta).

**Goal:** Sbloccare #1482 (`/knowledge-base/global` FE) fornendo gli endpoint BE cross-game che non esistono in forma user-facing: (1) ricerca KB cross-game RBAC-filtered, (2) ask cross-game via SSE. Issue #1661, Epic #1475.

**Architecture (post spec-panel + verifica D2/D7):**
- **NON modificare** `SearchQuery`/`AskQuestionQuery`/`SearchQueryHandler` esistenti (single-game) → backward compat by-design (D5).
- **Endpoint separati** cross-game con DTO ricco dedicato (D1 = B-refined: nuovo endpoint, ma riuso engine retrieval).
- **`IMultiGameVectorSearchService`** (nuovo) astrae hybrid+RRF cross-game riusando `IVectorStoreAdapter.SearchByMultipleGameIdsAsync` (esiste) + `RrfFusionDomainService.FuseResults` (isolato, puro) + keyword search (D7 opzione a).
- **`GetAccessibleGameIdsAsync`** in `RagAccessService` = fondamento RBAC condiviso search+ask (D3): Admin→all; non-admin→ public(`SharedGame.IsRagPublic`) ∪ owned(`UserLibraryEntry.OwnershipDeclaredAt != null`). Cache breve (D8).
- **SSE ask** riusa pattern `AdminDebugChatEndpoints` (`mediator.CreateStream` → `IAsyncEnumerable<RagStreamingEvent>`).

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs + MediatR · EF Core + pgvector · xUnit + Testcontainers · FluentValidation · HybridCache.

**Depends on (verificato nel codice):**
- `IVectorStoreAdapter.SearchByMultipleGameIdsAsync(gameIds[], ...)` — esiste (Infrastructure/External/IVectorStoreAdapter.cs:29), vector-only multi-game ✅
- `RrfFusionDomainService.FuseResults(...)` — isolato/puro (Domain/.../RrfFusionDomainService.cs:22) ✅
- `IRagAccessService.CanAccessRagAsync` + `GetAccessibleKbCardsAsync` (Application/Services/IRagAccessService.cs) ✅
- `UserLibraryEntryEntity {UserId, SharedGameId, OwnershipDeclaredAt}` (Infrastructure/Entities/UserLibrary/) ✅
- `PdfDocument {FileName, DocumentType, GameId, SharedGameId}` (DocumentProcessing) ✅
- `SharedGame {Title, IsRagPublic, IsDeleted}` (SharedGameCatalog) ✅
- FE contract Phase 0.5 §5: `docs/for-developers/frontend/contracts/kb-globale-hooks.md`

**Spec di riferimento:** Issue #1661; spec-panel review (D1-D8 + EC-1..EC-8) — vedi commento di apertura issue.

**Known limitations (da spec-panel D2):**
- `headingPath`: solo `ChunkMetadata.Heading` (heading semplice), **non materializzato nel result pgvector** → esposto best-effort (null se non disponibile). Contract FE relax: `headingPath: string | null`.
- `chunkId`: oggi `SearchResultDto.VectorDocumentId` (string). Cross-game DTO usa il vector embedding id come `chunkId`; se il chunk-level id reale non è queryable, fallback a embedding id (documentato).

**Non-goals:**
- ❌ Modifica degli endpoint per-game esistenti (`/knowledge-base/search`, `/ask`).
- ❌ Materializzazione del chunk metadata (headingPath full-path) nel vector store — separate concern, tracked se il FE lo richiede hard.
- ❌ Reranker cross-encoder cross-game (riuso solo RRF; cross-encoder rerank = follow-up perf).
- ❌ FE implementation di #1482 (questo è solo BE; sblocca il FE).

---

## PR strategy (split 2 PR — Nygard, riduce blast radius)

- **PR-1** = cross-game search + RBAC foundation (Task 1-7). Mergeabile da solo (sblocca FE Foundation Phase 1 di #1482).
- **PR-2** = SSE ask cross-game (Task 8-11). Dipende da Task 1 (GetAccessibleGameIds). Sblocca FE Interactions Phase 2.

---

## AC trasversali (da spec-panel — applicare a tutti i task pertinenti)

- **EC-1**: utente con 0 giochi accessibili → `200` `{ results: [], totalCount: 0 }` (o stream con `Complete` vuoto), NON error.
- **EC-2**: gioco public ma non indicizzato (no VectorDocument "completed") → escluso silenziosamente.
- **EC-3**: SSE client disconnect → `CancellationToken` honored, no resource leak.
- **EC-4**: pagination cross-game → ordering deterministico `score DESC, chunkId ASC`; cursor opaco; `hasMore` boolean (D6 — evita `totalCount` esatto costoso cross-game, usa `hasMore`).
- **EC-5**: **RBAC leak** → utente A NON vede chunk di gioco owned-only di B (test esplicito).
- **EC-6**: `headingPath`/`chunkId` best-effort (null/fallback documentato).
- **EC-7**: perf — `accessibleGameIds` filtrati a monte (NON caricare tutti i VectorDocuments completed in memoria); hard cap `Limit`; timeout.
- **EC-8**: gioco in library ma `OwnershipDeclaredAt == null` → escluso.
- **D8 cache**: `GetAccessibleGameIdsAsync` cached per-user (HybridCache TTL ~5min, tag-invalidate su library change — best-effort, bounded-stale accettabile).

---

# PR-1 — Cross-game search + RBAC

## Task 1: `GetAccessibleGameIdsAsync` in RagAccessService (RBAC foundation)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagAccessService.cs` (+ method signature)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/RagAccessService.cs` (impl)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/RagAccessServiceTests.cs` (o nuovo file)

- [ ] **Step 1: Test (TDD)** — casi: Admin→tutti i giochi non-deleted; non-admin→ union(public, owned); owned con `OwnershipDeclaredAt == null` escluso (EC-8); utente 0 giochi → lista vuota (EC-1); deleted games esclusi.
- [ ] **Step 2: Run, confirm FAIL**.
- [ ] **Step 3: Impl** (bozza verificata):

```csharp
public async Task<IReadOnlyList<Guid>> GetAccessibleGameIdsAsync(
    Guid userId, UserRole role, CancellationToken ct = default)
{
    if (role is UserRole.Admin or UserRole.SuperAdmin)
        return await _dbContext.SharedGames
            .Where(sg => !sg.IsDeleted)
            .Select(sg => sg.Id).ToListAsync(ct);

    var publicGames = _dbContext.SharedGames
        .Where(sg => !sg.IsDeleted && sg.IsRagPublic).Select(sg => sg.Id);
    var ownedGames = _dbContext.UserLibraryEntries
        .Where(e => e.UserId == userId && e.OwnershipDeclaredAt != null && e.SharedGameId != null)
        .Select(e => e.SharedGameId!.Value);

    return await publicGames.Union(ownedGames).Distinct().ToListAsync(ct);
}
```

- [ ] **Step 4: Cache (D8)** — wrappa in `IHybridCacheService` con key `rag:accessible-games:{userId}:{role}`, TTL 5min. (Se HybridCache pattern complesso, MVP senza cache + TODO tracking — ma preferibile includerla.)
- [ ] **Step 5: Run, confirm PASS**. Commit `feat(kb): #1661 GetAccessibleGameIdsAsync for cross-game RBAC`.

## Task 2: Espose `SearchByMultipleGameIdsAsync` su `IEmbeddingRepository`

**Gap** (D7): il metodo esiste su `IVectorStoreAdapter` ma non sul repository domain.

**Files:**
- Modify: `.../Domain/Repositories/IEmbeddingRepository.cs` (+ `SearchByMultipleGameIdsAsync`)
- Modify: impl `EmbeddingRepository` (delega all'adapter)
- Test: handler/repo test

- [ ] Step 1: Test (TDD) — repo delega all'adapter con i gameIds, ritorna embeddings multi-game.
- [ ] Step 2-4: FAIL → impl (delega) → PASS. Commit.

## Task 3: `IMultiGameVectorSearchService` (hybrid + RRF cross-game)

**Files:**
- Create: `.../Application/Services/IMultiGameVectorSearchService.cs` + impl
- Create: test `MultiGameVectorSearchServiceTests.cs`

- [ ] **Step 1: Test (TDD)** — input: `query, queryVector, gameIds[], topK, minScore, mode`. Verifica: vector search multi-game (via repo Task 2) + keyword + RRF fusion (`RrfFusionDomainService.FuseResults`); ordering `score DESC, chunkId ASC` (EC-4); empty gameIds → empty (EC-1); minScore filtra.
- [ ] **Step 2-3: FAIL → impl**: orchestrazione = (a) vector multi-game, (b) keyword multi-game, (c) RRF fuse, (d) sort deterministico, (e) take topK. NON caricare tutti i completed docs (EC-7: filtra per gameIds a monte).
- [ ] **Step 4: PASS**. Commit `feat(kb): #1661 IMultiGameVectorSearchService (hybrid+RRF cross-game)`.

## Task 4: `GlobalKbSearchQuery` + Handler + DTO enrichment

**Files:**
- Create: `.../Application/Queries/GlobalKbSearchQuery.cs` (+ Validator)
- Create: `.../Application/Queries/GlobalKbSearchQueryHandler.cs`
- Create: DTO `GlobalKbSearchResultDto` + `GlobalKbSearchResponseDto` (results + nextCursor + hasMore)
- Test: `GlobalKbSearchQueryHandlerTests.cs` (unit)

- [ ] **Step 1: Test (TDD)** — handler: chiama `GetAccessibleGameIdsAsync` → `IMultiGameVectorSearchService` → enrichment join. Casi: RBAC filtra (EC-5 leak: gameIds non accessibili esclusi); empty accessible → empty (EC-1); enrichment popola docTitle/gameName/docType; headingPath best-effort null (EC-6); pagination cursor (EC-4).
- [ ] **Step 2-3: FAIL → impl**:
  - `IQuery<GlobalKbSearchResponseDto>` (SharedKernel `IQuery<T>`, NON raw MediatR — convenzione progetto).
  - Handler: `accessibleGameIds = GetAccessibleGameIdsAsync(userId, role)` → `MultiGameVectorSearch(query, accessibleGameIds, ...)` → **enrichment batch** (single EF query join `VectorDocument→PdfDocument` per docTitle/docType + `→SharedGame` per gameName, sui docId/gameId del result-set — NO N+1, EC-7).
  - DTO `GlobalKbSearchResultDto { chunkId, docId, docTitle, gameId, gameName, docType, headingPath?, snippet, pageNumber, score }` (mirror contract §5).
- [ ] **Step 4: PASS**. Commit.

## Task 5: Endpoint `POST /api/v1/knowledge-base/search/global` (CQRS)

**Files:**
- Modify: `.../KnowledgeBaseEndpoints.cs` (+ `HandleGlobalSearch`, `RequireSession()`)
- Test: integration Testcontainers `GlobalKbSearchEndpointTests.cs`

- [ ] **Step 1: Integration test (TDD, Testcontainers Postgres+pgvector)** — AC:
  - 200 con results cross-game per utente con giochi accessibili
  - **EC-5 RBAC leak**: utente A non riceve chunk di gioco owned-only di B
  - EC-1 empty (0 giochi) → 200 results []
  - EC-4 pagination: cursor → seconda pagina, no overlap
  - 401 senza sessione
  - 422 validation (query vuota)
- [ ] **Step 2-3: FAIL → impl**: endpoint usa SOLO `IMediator.Send(GlobalKbSearchQuery)` (regola CQRS), userId/role da session, `.Produces<GlobalKbSearchResponseDto>(200).Produces(422)`.
- [ ] **Step 4: PASS**. Commit.

## Task 6: OpenAPI/Scalar doc

- [ ] Verifica che il nuovo endpoint appaia in `/scalar/v1` con schema corretto + esempi. Commit se servono annotazioni.

## Task 7: PR-1 self-review + open PR

- [ ] `dotnet test --filter "BoundedContext=KnowledgeBase"` verde
- [ ] PR base **main-dev** (parent), titolo `feat(kb): #1661 cross-game KB search + RBAC (PR-1/2)`, body con AC + link #1482/#1475 + nota "PR-2 SSE ask follows".

---

# PR-2 — Cross-game SSE ask

## Task 8: `GlobalKbAskStreamQuery` (IAsyncEnumerable<RagStreamingEvent>)

**Files:**
- Create: `.../Application/Queries/GlobalKbAskStreamQuery.cs` + streaming handler
- Test: `GlobalKbAskStreamQueryHandlerTests.cs`

- [ ] **Step 1: Test (TDD)** — handler: cross-game retrieval (`GetAccessibleGameIds` + MultiGameVectorSearch) → LLM stream → emette `RagStreamingEvent` sequence: `StateUpdate → Citations → Token* → Complete`; error → `Error` event; citations portano `documentId, chunkId, chunkPosition, page?, snippet, score` (deep-link). EC-1 empty → `Complete` con answer "nessun contesto".
- [ ] **Step 2-3: FAIL → impl**: riusa il RAG streaming generator esistente (quello dietro agent-chat/`StreamDebugQaQuery`) passando il context cross-game retrieved. `mediator.CreateStream` pattern.
- [ ] **Step 4: PASS**. Commit.

## Task 9: Endpoint SSE `POST /api/v1/knowledge-base/ask/global`

**Files:**
- Modify: `.../KnowledgeBaseEndpoints.cs` (+ `HandleGlobalAskStream`)
- Test: integration `GlobalKbAskStreamEndpointTests.cs`

- [ ] **Step 1: Integration test (TDD)** — AC:
  - SSE `text/event-stream`, eventi `data: {json}\n\n` parsabili nella sequence attesa
  - **EC-3 cancellation**: client disconnect → `CancellationToken` propagato, stream termina, no leak
  - EC-5 RBAC: solo context da giochi accessibili
  - 401 senza sessione
- [ ] **Step 2-3: FAIL → impl**: pattern `AdminDebugChatEndpoints.cs:26` (headers SSE + `mediator.CreateStream` + `WriteAsync + FlushAsync`), `RequireSession()`, honor `ct`.
- [ ] **Step 4: PASS**. Commit.

## Task 10: OpenAPI + PR-2 self-review + open PR

- [ ] Doc SSE endpoint. `dotnet test` KB verde. PR base **main-dev**, titolo `feat(kb): #1661 cross-game SSE ask (PR-2/2)`, body + link.

## Task 11: Close-out

- [ ] Update FE Phase 0.5 contract `kb-globale-hooks.md` §7: riclassifica Q1/Q2 da "FE client" a "BE done" + nota `headingPath` best-effort.
- [ ] Commenta #1482 (BE sbloccato, endpoint disponibili) + #1661 (chiuso al merge PR-2).

---

## Self-Review

**1. Spec coverage**: D1 (endpoint separato+engine reuse) ✅ · D2 (DTO enrichment batch join, headingPath best-effort) ✅ · D3 (GetAccessibleGameIds + cache) ✅ · D4 (SSE pattern reuse) ✅ · D5 (no touch single-game = backward compat) ✅ · D6 (cursor + hasMore) ✅ · D7 (IMultiGameVectorSearchService opzione a) ✅ · D8 (cache) ✅. EC-1..EC-8 mappate ai test.

**2. Placeholder scan**: Task 1 impl concreta (verificata). Task 3/8 dipendono dal riuso engine — se l'estrazione keyword multi-game è più complessa del previsto, fallback documentato: vector-only multi-game + RRF su vector+keyword-per-game-loop. Task 8 dipende dal RAG streaming generator esistente — verificare la sua firma in impl.

**3. Type consistency**: `IQuery<T>`/`IQueryHandler<,>` da SharedKernel (NON raw MediatR). DTO mirror contract §5. `UserRole` enum esistente.

**4. Rischi**:
- 🔴 **R1 headingPath/chunkId** (D2): non materializzati nel result pgvector → best-effort null. Se il FE li richiede hard, è un follow-up di materializzazione metadata (fuori scope). Documentato nel contract relax.
- 🟡 **R2 perf cross-game** (EC-7): `SearchByMultipleGameIdsAsync` su molti gameIds. Mitigazione: filtro accessibleGameIds a monte + hard cap Limit. Monitorare.
- 🟡 **R3 keyword multi-game**: il keyword/BM25 search attuale (`_hybridSearchService.SearchAsync`) è per-game; estenderlo a multi-game o loop. Verificare in Task 3.
- 🟡 **R4 RAG streaming reuse** (Task 8): il generator dietro agent-chat potrebbe essere accoppiato all'agent context. Verificare riusabilità per kb-ask cross-game in impl; fallback = nuovo streaming handler.

**5. Test strategy**: unit (handler + service + RBAC) + integration Testcontainers (endpoint, RBAC leak, cancellation, pagination). Priorità EC-5 (leak) + EC-3 (cancellation).

---

## Execution Handoff

**Ordine**: PR-1 Task 1→7 sequenziale (Task 1 RBAC è fondamento; Task 2→3→4 catena retrieval; Task 5 endpoint; 6-7 close). Poi PR-2 Task 8→11 (dipende da Task 1 + Task 3).

**Effort stimato**: PR-1 ~10-14h, PR-2 ~6-8h (BE .NET TDD).

**Dispatch**: subagent-driven-development; ogni task è TDD red-green-commit. Mix: la maggior parte richiede giudizio (Sonnet) — è BE con integrazione, non mechanical.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)

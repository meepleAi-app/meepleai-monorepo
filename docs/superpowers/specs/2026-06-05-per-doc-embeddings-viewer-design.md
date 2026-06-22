# Per-Doc Embeddings Viewer — Design Spec

**Issue**: [#1674](https://github.com/meepleAi-app/meepleai-monorepo/issues/1674) (P3, area/backend + area/frontend, admin)
**Date**: 2026-06-05
**Status**: DESIGNED (brainstorming sessione 2026-06-05) — ready for plan
**Parent**: #1653 (F3-FU-4) spin-out per spec-panel 2026-05-29 (Newman corpus-reconstruction risk)
**Predecessor spec**: [`2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`](./2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md) §3 (spun out), §4 (foundation)
**Mockups**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` (L236 trigger button) + `sp5-admin-kb-vectors.html` (riusato come riferimento estetico per search panel + result-row + vec-thumb gradient)

---

## 1. Context & Goal

`KbDocDetailPanel` (right pane di `/admin/knowledge-base` explorer) ha 5 bottoni hero nel mockup (`sp5-admin-kb.html:234-240`):

```
⟳ Re-index    📋 View embeddings    ⤓ Export chunks JSON    🔬 Quality eval    🤖 Used by N agents
```

I bottoni A1 (re-index), B3 (export), B5 (used-by) sono già wirati post-FU-4 #1653 (PR #1649 merged 2026-05-28). I bottoni B2 (View embeddings) e B4 (Quality eval) sono stati spun-out come #1674 e #1675 perché net-new backend con design implication non triviali — in particolare B2 ha un security concern flagged da Newman durante lo spec-panel review:

> "Exposing raw per-doc vectors can enable corpus reconstruction / data leak. Design the API with access control (and possibly aggregated/approximate views) BEFORE building UI."

**Goal**: implementare il bottone "📋 View embeddings" come **drawer scoped al documento selezionato**, mostrando i metadati embeddings (model, dimensions, total chunks, indexed timestamp) + ricerca semantica intra-doc + export, **senza esporre raw vector values** in nessun payload API.

## 2. Current state (evidence)

- `kbGroup` (admin-gated) in `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` ospita già:
  - `GET /api/v1/admin/kb/docs/{docId}/chunks/export` (linea 101, da FU-4)
  - `POST /api/v1/admin/kb/docs/{docId}/chunks/search` (linea 113, da FU-4)
  - Entrambi audit-loggati Level 1 via `AuditLoggingBehavior` pipeline.
- `PgVectorStoreAdapter.SearchWithScoresAsync` (riga 127) ritorna `List<ScoredEmbedding>` con similarity score; usato da `SearchDocumentChunksByVectorQueryHandler` (per-doc filter via `documentIds=[vectorDocId]`).
- `VectorDocumentEntity` (aggregate root) ha: `Id`, `GameId`, `PdfDocumentId`, `Language`, `TotalChunks`, `IndexedAt`, `LastSearchedAt`, `SearchCount`, `SharedGameId`, `Metadata`.
- `EmbeddingEntity` (in `pgvector_embeddings`) ha: `Id`, `VectorDocumentId`, `GameId`, `TextContent`, `Vector` (768d), `Model`, `ChunkIndex`, `PageNumber`, `Language`, `IsTranslation`, `RoleTags`, `SourceChunkId`.
- `/admin/embedding/{info,metrics}` service-level endpoints esistono (`AdminEmbeddingEndpoints.cs`), espongono model + dim + throughput aggregati cross-corpus.
- Frontend admin embedding tab (`embedding-tab.tsx`) consuma `/info`+`/metrics` via TanStack Query polling 30s. Nessuna UI per per-doc embeddings esiste.
- `apps/web/src/components/ui/drawer/drawer.tsx` primitive disponibile (post DS-15 de-versioning); pattern usato in `upload-for-game-drawer.tsx` (KB-admin domain-consistent).
- `AuditLoggingBehavior` MediatR pipeline intercetta `IRequest` con `[AuditableAction]` → auto-scrive `audit_logs` row.
- **Step-up 2FA NON implementato** nel progetto: `X-StepUp-Token` non riconosciuto da nessun middleware. Gap noto, tracked in #1859 (rotate-key). Per questa feature read-only Level 1 audit non richiesto.

## 3. Decisions (lockate durante brainstorming 2026-06-05)

| ID | Decision | Razionale |
|---|---|---|
| **DEC-1** | Surface = **drawer side-right** sopra `KbDocDetailPanel` (NO route dedicata, NO navigate a vectors-hub) | Mantiene contesto KbDocDetail visibile sotto. Evita duplicazione UI tra route dedicate e hub. |
| **DEC-2** | **1 NEW endpoint** `GET /admin/kb/docs/{docId}/embeddings/meta` per meta-strip header (Model · Dim · Total chunks · Indexed at) | Confirma visivamente all'admin che sta guardando il modello + dim corretti per il doc. Low-cost pure-read (2 SELECT). |
| **DEC-3** | **Authorization**: Admin guard (group-level) + `[AuditableAction("EmbeddingsMetaView", "Document", Level=1)]` | Newman risk pre-mitigato by design (no raw vectors esposti, vedi §7). Level 1 audit proportionato per read-only forensic trail. Step-up 2FA non applicabile (non implementato). |
| **DEC-4** | **Export action** = bottone footer drawer che riusa endpoint esistente `GET /admin/kb/docs/{docId}/chunks/export` (no nuovo endpoint) | Comodo per debug ML pipeline (carica chunks in notebook). Audit Level 1 già emesso dal handler esistente. |
| **DEC-5** | **Vec-thumb visualization** = gradient **client-side** deterministic da `seed = hash(chunkIndex)` | Pure visual, zero info leak, stable per chunk. Backend NON serializza mai i raw float[768] in JSON. |
| **DEC-6** | **Mobile fallback** (< 880px): trigger button disabled con tooltip "Solo desktop ≥ 880px" | Allineato al pattern `admin-mobile-fallback` esistente nei mockup admin. |

## 4. Scope

### In scope (#1674)

- **NEW backend**: `GetDocumentEmbeddingsMetaQuery` + handler + DTO + endpoint
- **NEW frontend**: `DocumentEmbeddingsDrawer` + 5 sub-components (`EmbeddingsMetaStrip`, `EmbeddingsSearchPanel`, `EmbeddingsResultRow`, `VecThumb`, drawer footer) + `useDocumentEmbeddingsMeta` hook
- **EDIT** `KbDocDetailPanel`: wire-up bottone "📋 View embeddings" → setOpenEmbeddingsForDocId
- **Riuso** 2 endpoint FU-4 (`/chunks/search` + `/chunks/export`) per search panel + export footer
- **Test**: ~32-34 test (BE 11-12 + FE 17-18 + E2E 4)

### Out of scope (esplicito, deferred ad altre issue / future)

- ❌ Endpoint per inspect raw vector values (es. `GET /admin/kb/chunks/{chunkId}/embedding/raw`) — incompatibile con Newman risk
- ❌ Export `.npy` / `.csv` di coordinate vettoriali
- ❌ Aggregati statistici per-doc (chunk distribution histogram, lang chips %, size MB) — overkill per P3
- ❌ Cross-doc embeddings comparison / clustering visualization
- ❌ Per-doc quality eval (tracked in #1675)
- ❌ Re-index versionato (tracked in #1673)
- ❌ Hero metadata enrichment (tracked in #1676)

## 5. Architecture

### Bounded context

`KnowledgeBase` (Application + Infrastructure layers). CQRS via MediatR.

### Components backend

| Componente | Tipo | Path |
|---|---|---|
| `GetDocumentEmbeddingsMetaQuery(Guid DocId)` | NEW Query | `BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/` |
| `GetDocumentEmbeddingsMetaQueryHandler` | NEW Handler | stessa cartella |
| `GetDocumentEmbeddingsMetaQueryValidator` | NEW FluentValidator | stessa cartella |
| `DocumentEmbeddingsMetaDto` | NEW DTO record | stessa cartella |
| Endpoint route | NEW MapGet | `Routing/AdminKnowledgeBaseEndpoints.cs` (stesso `kbGroup`) |
| `SearchDocumentChunksByVectorQueryHandler` | ✅ RIUSO | `Application/Queries/SearchDocumentChunks/` |
| `ExportDocumentChunksQueryHandler` | ✅ RIUSO | `Application/Queries/ExportDocumentChunks/` |
| `PgVectorStoreAdapter.SearchWithScoresAsync` | ✅ RIUSO | `Infrastructure/Persistence/` |
| `[AuditableAction]` attribute | ✅ PATTERN | `AuditLoggingBehavior` MediatR pipeline |

### Resolver pattern (consistency con SearchDocumentChunks)

```
GetDocumentEmbeddingsMetaQueryHandler:
  1. dbContext.VectorDocuments
       .Where(v => v.PdfDocumentId == request.DocId)
       .Select(v => new { v.Id, v.Language, v.IndexedAt, v.TotalChunks })
       .SingleOrDefaultAsync()
     → if null: throw NotFoundException("Document not indexed")

  2. dbContext.PgVectorEmbeddings
       .Where(e => e.VectorDocumentId == vectorDoc.Id)
       .Select(e => e.Model)
       .Take(1)
       .SingleOrDefaultAsync()
     → if null: throw NotFoundException("No embeddings found for this document")

  3. return new DocumentEmbeddingsMetaDto(
       DocId: request.DocId,
       Model: model,
       Dimensions: 768,  // schema-locked al modello bge-base-en-v1.5
       TotalChunks: vectorDoc.TotalChunks,
       IndexedAt: vectorDoc.IndexedAt,
       Language: vectorDoc.Language
     );
```

### Components frontend

| Componente | Tipo | Path |
|---|---|---|
| `DocumentEmbeddingsDrawer` | NEW orchestrator | `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/index.tsx` |
| `EmbeddingsMetaStrip` | NEW (4 KPI) | sub-folder |
| `EmbeddingsSearchPanel` | NEW (form + results) | sub-folder |
| `EmbeddingsResultRow` | NEW (collapse/expand) | sub-folder |
| `VecThumb` | NEW (pure visual) | sub-folder |
| `useDocumentEmbeddingsMeta(docId, enabled)` | NEW TanStack hook | `apps/web/src/hooks/admin/use-document-embeddings-meta.ts` |
| `useSearchDocumentChunks(docId)` | ✅ RIUSO | esistente da FU-4 |
| `api.admin.kb.getDocumentEmbeddingsMeta(docId)` | NEW fetcher | `apps/web/src/lib/api/admin/kb.ts` |
| Wire-up trigger button | EDIT | `KbDocDetailPanel.tsx` (hero actions row) |

### Sequence diagram

```
Admin click [📋 View embeddings] in KbDocDetailPanel
  └→ setOpenEmbeddingsForDocId(currentDocId) state lifted in panel
     └→ <DocumentEmbeddingsDrawer open={true} docId={...} docFileName={...} />
        └→ useDocumentEmbeddingsMeta(docId, enabled=open)
           └→ GET /admin/kb/docs/{docId}/embeddings/meta
              └→ AdminKnowledgeBaseEndpoints → MediatR Send(GetDocumentEmbeddingsMetaQuery)
                 └→ FluentValidation: DocId not empty
                 └→ AuditLoggingBehavior pre-pipeline (acquire userId)
                 └→ Handler executes (resolver 2-step)
                 └→ AuditLoggingBehavior post-pipeline writes audit_logs row
                 └→ Returns DocumentEmbeddingsMetaDto
           └→ EmbeddingsMetaStrip renders 4 KPI cards
        └→ user types "predator activation" → click Cerca
           └→ useSearchDocumentChunks.mutate({ query, limit }) ✅ riuso FU-4
              └→ POST /admin/kb/docs/{docId}/chunks/search
                 └→ Returns List<ScoredChunkDto>
           └→ EmbeddingsSearchPanel renders result-rows
              └→ VecThumb gradient client-side da seed=chunkIdx
        └→ user click [⤓ Export chunks JSON] footer
           └→ <a href={getExportUrl(docId)} download> → browser download (cookie auth)
              └→ GET /admin/kb/docs/{docId}/chunks/export
                 └→ Returns application/json full chunks (FU-4 handler emette audit Level 1)
```

### Boundary check (interface segregation)

Ogni nuovo componente FE ha responsabilità singola:
- `DocumentEmbeddingsDrawer` = orchestrazione open/close + layout shell
- `EmbeddingsMetaStrip` = render 4 KPI da DTO
- `EmbeddingsSearchPanel` = state ricerca + delegate al mutation hook
- `EmbeddingsResultRow` = render singola riga con expand/collapse
- `VecThumb` = pure visual da seed

Zero coupling con KnowledgeBaseHub o vectors-hub: il drawer è auto-contained.

## 6. Endpoint contract

### NEW endpoint

**Route**: `GET /api/v1/admin/kb/docs/{docId}/embeddings/meta`
**Auth**: `RequireAdminSessionFilter` (group-level su `kbGroup`)
**Audit**: `[AuditableAction("EmbeddingsMetaView", "Document", Level=1, UserIdSource=Caller)]`

#### Request

```http
GET /api/v1/admin/kb/docs/a3f7c218-4d11-4b9e-9d2a-7e5f1c8a0b6e/embeddings/meta
Cookie: meepleai_session=...
Accept: application/json
```

Path param: `docId` (Guid) = `PdfDocumentId`.
Query param: NONE.

#### Response 200 OK

```json
{
  "docId": "a3f7c218-4d11-4b9e-9d2a-7e5f1c8a0b6e",
  "model": "bge-base-en-v1.5",
  "dimensions": 768,
  "totalChunks": 412,
  "indexedAt": "2026-05-28T14:22:14Z",
  "language": "en"
}
```

DTO record `DocumentEmbeddingsMetaDto(Guid DocId, string Model, int Dimensions, int TotalChunks, DateTimeOffset IndexedAt, string? Language)`.

#### Status codes

| Code | Causa | Body |
|---|---|---|
| 200 | Doc indicizzato | `DocumentEmbeddingsMetaDto` |
| 400 | `docId` malformato (non-Guid) | ASP.NET RouteConstraint default |
| 401 | Non autenticato | `ProblemDetails` standard |
| 403 | Autenticato non-admin | `ProblemDetails` standard |
| 404 | `VectorDocument` non esiste per `docId` (doc pending / failed / mai indicizzato) | `ProblemDetails { type: "NotFound", detail: "Document not indexed" }` |
| 404 | `PgVectorEmbeddings` 0 row per `vectorDocId` (stato corrotto) | `ProblemDetails { type: "NotFound", detail: "No embeddings found for this document" }` |
| 500 | DB/infra failure | `ProblemDetails` standard |

> **Idempotenza**: GET pure-read, safe da retry. No rate-limit specifico (admin trusted + audit log per forensic).

### Endpoint riusati (no change)

```
POST /api/v1/admin/kb/docs/{docId}/chunks/search
Body: { "query": string, "limit": int (1..50, default 10) }
→ 200: List<{ chunkIndex, page, snippet, score, vectorDocumentId, language }>
→ 404 se doc non indicizzato
```

```
GET /api/v1/admin/kb/docs/{docId}/chunks/export
→ 200 application/json: { docId, exportedAt, chunks: [{ id, chunkIndex, page, headingPath, content }] }
→ 404 se doc non indicizzato
```

### Audit log row emessa (esempio per meta endpoint)

```
id          | uuid auto
actor_id    | <admin userId from session>
action      | "EmbeddingsMetaView"
resource    | "Document"
resource_id | <docId from path>
level       | 1
created_at  | <utc now>
```

## 7. Security & threat model

### Newman risk (origin)

Spec-panel review 2026-05-29 ha flagged:
> "Exposing raw per-doc vectors can enable corpus reconstruction / data leak."

### Mitigation by design (zero raw vector exposure)

**Nessuno dei 3 endpoint coinvolti espone valori del `Vector` ValueObject in JSON response.**

| Endpoint | Payload | Raw vector leak? |
|---|---|---|
| `GET /embeddings/meta` (NEW) | 6 fields: docId, model, dimensions, totalChunks, indexedAt, language | ❌ NO |
| `POST /chunks/search` (riuso) | text snippet + score (scalar) + page + chunkIdx + language | ❌ NO (score derivato, non vector) |
| `GET /chunks/export` (riuso) | text chunks full content | ❌ NO (text, non vector) |

**Vec-thumb gradient** (UI) = computed **client-side** da `seed = hash(chunkIndex)`:
- Backend NON invia mai i raw float[768]
- Backend NON invia mai una versione quantizzata o approssimata
- Il gradient è puramente decorativo (3 hue stop derivati da seed) → ZERO informazione vettoriale, non reverse-engineerable a vector

### Threat model

| Attore | Capacità | Mitigation |
|---|---|---|
| **Insider admin malizioso** | Legitimate access, può chiamare gli endpoint | Audit Level 1 → forensic trail. NO prevention by design (admin trust model). |
| **External actor con credenziali admin compromesse** | Session valida da credentials stolen | Admin gate first line. Audit per anomaly detection. |
| **External actor senza credenziali** | Tentativo accesso diretto | `RequireAdminSessionFilter` → 401/403. |
| **Information aggregation attack** | Combina meta endpoint multipli → mappa modello+dim+chunks per N doc | Low-sensitivity: model+dim già visibili in `/admin/embedding/info`. Per-doc chunks count ricavabile da `GET /kb-docs/{id}` esistente. NO incremental leak. |

### Defense in depth

1. **Admin gate** — `RequireAdminSessionFilter` su `kbGroup` (cookie session + role check)
2. **DTO whitelist** — handler ritorna `DocumentEmbeddingsMetaDto` statico (record), no anonymous projection da entity
3. **Audit Level 1** — `[AuditableAction]` su query handler, ogni invocation tracciata
4. **No vector serialization** — il DTO non include `Vector` field; mapper `KnowledgeBaseMappers` non mappa Vector verso DTO admin

### Esplicito OUT OF SCOPE (no implementation paths)

- ❌ `GET /admin/kb/chunks/{chunkId}/embedding/raw` (no endpoint per single vector inspection)
- ❌ Export vector raw `.npy` / `.csv` di coordinate
- ❌ GraphQL field selection (REST DTO statici, no evasion path)
- ❌ Step-up 2FA enforcement (non implementato nel progetto; Level 1 proportionato per read-only no-leak)

### Rate limit

NESSUN rate limit specifico — meta è low-cost (1 SELECT VectorDocuments + 1 SELECT PgVectorEmbeddings Take(1)). Search/export riusano rate limit esistente se presente nel BC.

### Compliance posture

- **GDPR**: PDF content può contenere PII (es. nomi giocatori in house rules). Già coperto da export endpoint preesistente con admin gate + audit.
- **Newman audit pass**: design rispetta "design the API with access control BEFORE building UI" — 0 raw values esposti via API.

## 8. UI composition & accessibility

### Layout & drawer primitive

```
side="right" · width=720px desktop · scroll-y inner · backdrop dim 50%
header sticky (title + close X)
body scroll
footer sticky (export action + close)
```

Riusa `<Drawer />` primitive da `apps/web/src/components/ui/drawer/drawer.tsx`.

### Component tree

```
<DocumentEmbeddingsDrawer open onOpenChange docId docFileName />
├─ <DrawerHeader>
│    ├─ <DrawerTitle>Embeddings · {docFileName}</DrawerTitle>
│    └─ <DrawerCloseButton aria-label="Chiudi viewer embeddings" />
├─ <DrawerBody>
│    ├─ <EmbeddingsMetaStrip metaQuery={useDocumentEmbeddingsMeta(docId, open)} />
│    │    ├─ loading: 4× <Skeleton h-[88px] />
│    │    ├─ error 404: <EmptyState title="Documento non indicizzato" />
│    │    └─ data: 4× <MetaKpiCard label value entity="kb" />
│    │           Model · Dimensions · Total chunks · Indexed at
│    └─ <EmbeddingsSearchPanel docId={docId} />
│         ├─ <SearchHeading icon="🔬" title="Ricerca semantica" subtitle="cosine · top-k" />
│         ├─ <SearchForm> input + limit-select + Cerca-button
│         │    └─ on submit → searchMutation.mutate({ query, limit })
│         ├─ <SearchResultsTable>
│         │    ├─ <SearchResultsTableHead> Page · Chunk# · Snippet · Score
│         │    └─ <EmbeddingsResultRow chunk={c} onExpand /> × N
│         │         ├─ collapsed: page, chunkIdx, snippet (1 line ellipsis), score-pill
│         │         └─ expanded: snippet-full (with <mark>), <VecThumb seed={chunkIdx} />, meta-table
│         └─ <SearchEmptyState /> when results=[] post-search
└─ <DrawerFooter sticky>
     ├─ <a href={getExportUrl(docId)} download="{docId}-chunks.json" class="btn-admin">⤓ Export chunks JSON</a>
     └─ <CloseSecondaryButton />
```

### State machine (lifted in `KbDocDetailPanel`)

```
state: openEmbeddingsForDocId: Guid | null

closed:
  → click "📋 View embeddings" → setOpenEmbeddingsForDocId(currentDocId) → open

open:
  → meta query: idle → loading → success | error(404)
  → search panel: idle | searching | results | empty | error
  → close trigger: backdrop click, X button, Escape key, programmatic onOpenChange(false)
  → setOpenEmbeddingsForDocId(null) → closed
```

**Guard**: drawer NOT mountato se `docId === null`. `enabled: open && !!docId` su TanStack query.

### TanStack Query keys

```typescript
export const documentEmbeddingsKeys = {
  meta: (docId: string) => ['admin', 'kb', 'docs', docId, 'embeddings', 'meta'] as const,
} as const;

useQuery({
  queryKey: documentEmbeddingsKeys.meta(docId),
  queryFn: () => api.admin.kb.getDocumentEmbeddingsMeta(docId),
  enabled: open && !!docId,
  staleTime: 5 * 60 * 1000,
  gcTime: 10 * 60 * 1000,
});
```

Invalidation cross-feature: `reindexMutation.onSuccess` (esistente FU-4) deve invalidare anche `documentEmbeddingsKeys.meta(docId)`.

### Accessibility (WCAG AA)

- **Focus trap**: drawer primitive già gestisce focus trap on open + return focus al trigger
- **Escape key**: close handler bound
- **ARIA**: `role="dialog"`, `aria-labelledby` → DrawerTitle, `aria-live="polite"` su results count
- **Heading hierarchy**: drawer h2 (title) → MetaStrip h3 → SearchPanel h3 "Ricerca semantica"
- **Color contrast**: token-only (`text-foreground`, `text-muted-foreground`, `bg-card`, `bg-muted`, `border-border`); zero hardcoded colors → ESLint `local/no-hardcoded-color-utility` clean
- **Entity utility**: viewer è KB-domain → `bg-entity-kb`, `text-entity-kb`, `ring-entity-kb/30` per accent (score-pill alto, vec-thumb seed-color)
- **Skeleton states**: 4 skeleton cards meta + 5 row skeletons search
- **Tab order**: header X → search input → limit select → Cerca button → result rows (each expandable, button role) → export → close secondary
- **Screen reader**: `aria-live` su results count ("8 risultati trovati"), `aria-hidden` su VecThumb (decorative)

### VecThumb implementation

```typescript
function VecThumb({ seed }: { seed: number | string }) {
  const hash = simpleHash(String(seed));
  const hue1 = hash % 360;
  const hue2 = (hash * 7) % 360;
  const hue3 = (hash * 13) % 360;
  return (
    <div
      className="h-7 rounded-md mt-1.5 relative overflow-hidden"
      style={{
        background: `linear-gradient(90deg, hsl(${hue1} 60% 50% / .35), hsl(${hue2} 60% 50% / .05), hsl(${hue3} 60% 50% / .25))`,
      }}
      aria-hidden="true"
    >
      <span className="text-[9px] font-mono opacity-75 absolute right-1.5 top-1/2 -translate-y-1/2">
        768d · float32
      </span>
    </div>
  );
}
```

### Mobile fallback

`< 880px` → trigger button "📋 View embeddings" disabled con tooltip "Solo desktop ≥ 880px" (allineato a `admin-mobile-fallback` esistente nei mockup).

## 9. Error handling

### Meta endpoint failure modes

| Stato | Trigger | UX behavior |
|---|---|---|
| Loading | TanStack `isPending` | 4× `<Skeleton />` shimmer; SearchPanel disabled |
| 404 Doc not indexed | `VectorDocument` missing | `<EmptyState>` "Documento non indicizzato" + suggerimento re-index. SearchPanel hidden. Export disabled. |
| 404 No embeddings (corrotto) | `VectorDocument` esiste, `PgVectorEmbeddings` 0 row | `<EmptyState>` "Stato inconsistente" + button "Re-index ora" |
| 401 | Session scaduta | Toast → redirect `/login?returnUrl=/admin/knowledge-base`, drawer chiuso |
| 403 | User downgraded | Toast "Permessi insufficienti", drawer chiuso |
| 500 / Network | Backend/infra | `<ErrorBanner>` + button "Riprova" (refetch) |

### Search endpoint failure modes (riusa FU-4)

| Stato | Trigger | UX behavior |
|---|---|---|
| Searching | mutation pending | Spinner button + skeleton × 5 result rows |
| Empty results | response `[]` | `<SearchEmptyState>` "Nessun chunk corrisponde a «{query}»" + suggerimento |
| 404 race (doc de-indicizzato durante drawer) | response 404 | Toast warning → drawer auto-close → refetch tree |
| 400 Empty query (client-side) | input vuoto | button "Cerca" disabled |
| 400 Query too long (>1000 char) | input > 1000 char | `aria-invalid="true"` + helper text |
| 500 Embedding service down | timeout/unavailable | Toast error, search box resta utilizzabile |
| Concurrent search | rapid user input | AbortController su mutation cancella precedente |

### Export endpoint failure modes (riusa FU-4)

| Stato | Trigger | UX behavior |
|---|---|---|
| 404 race | doc de-indicizzato | Browser error page; best-effort toast post-attempt |
| 500 / Network | backend fail | Browser default download error; user può retry |

Plain `<a href download>` (cookie auth) — no fetch+blob, no custom error UI.

### Edge cases UX

| Caso | Behavior |
|---|---|
| User chiude drawer durante meta fetch | TanStack `enabled` flip → AbortSignal cancellation |
| User chiude drawer durante search | `mutation.reset()` su `onClose` → cancel + clear |
| User cambia doc selezionato in tree | `KbDocDetailPanel` chiama `setOpenEmbeddingsForDocId(null)` su `onSelectChange` → drawer chiude (no auto-reopen) |
| Re-index del doc completato | `reindexMutation.onSuccess` invalida `documentEmbeddingsKeys.meta(docId)` → MetaStrip refetch auto |
| Delete del doc completato | drawer ascolta `selectedDocId` null da panel → auto-close; cleanup TanStack cache |
| Multiple drawer (2 tab browser) | staleTime 5min permette cross-tab cache; no conflict (read-only) |
| Search debounce | NO debounce su typing; submit via Enter/button (1 search = 1 embedding call, costo non-trivial) |

### Audit error logging

`AuditLoggingBehavior` scrive audit row solo su success. Failure (handler exception) skipped — acceptable per Level 1 read-only. Per Level 2+ andrebbero auditati anche i failure; non applicabile qui.

### Server-side logging

Handler usa `ILogger<GetDocumentEmbeddingsMetaQueryHandler>` esistente:
- `LogInformation` su success post-DB
- `LogWarning` su 404 (doc not indexed) — distinguibile per future alerting
- Exception propagate a `ProblemDetails` middleware (no custom catch)

## 10. Testing strategy

### Backend xUnit (target 90%+ nuovo codice)

**Unit** — `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

| Test | Scenario |
|---|---|
| `Returns_Meta_When_Document_Indexed` | VectorDoc + ≥1 Embedding → DTO popolata |
| `Throws_NotFound_When_VectorDocument_Missing` | PdfDocument esiste, VectorDocument no → `NotFoundException("Document not indexed")` |
| `Throws_NotFound_When_VectorDocument_Has_Zero_Embeddings` | VectorDocument esiste, 0 PgVectorEmbeddings → `NotFoundException("No embeddings found")` |
| `Audit_Attribute_Applied` | Riflessione sulla query class → `[AuditableAction("EmbeddingsMetaView", "Document", Level=1, UserIdSource=Caller)]` presente |
| `Validator_Rejects_Empty_DocId` | Query con `DocId=Guid.Empty` → `IsValid=false` |
| `Returns_Language_Null_When_VectorDocument_Language_Null` | edge case lang null |

**Integration** — `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs` (Testcontainers Postgres + pgvector)

| Test | Scenario |
|---|---|
| `GET_Returns_200_For_Indexed_Doc` | Seed PdfDoc + VectorDoc + 412 embeddings → 200 + DTO esatto |
| `GET_Returns_404_For_Not_Indexed_Doc` | Solo PdfDoc seed → 404 ProblemDetails |
| `GET_Returns_401_When_No_Session` | No cookie → 401 |
| `GET_Returns_403_When_Not_Admin` | User role session → 403 |
| `GET_Writes_Audit_Row` | Success → assert `audit_logs` row con action="EmbeddingsMetaView", resource_id=docId |
| `GET_DTO_Has_No_Vector_Field` | Response JSON deserialize → assert nessun field "vector", "embedding", "values" presente |

Trait `[Trait("BoundedContext","KnowledgeBase")]` per filtering.

### Frontend Vitest (target 85%+)

**Component** — `apps/web/src/components/admin/knowledge-base/__tests__/document-embeddings-drawer.test.tsx`

| Test | Scenario |
|---|---|
| `Drawer_Opens_On_Trigger_Click` | Mock KbDocDetailPanel → click trigger → `role="dialog"` visible |
| `MetaStrip_Renders_4_KPIs_On_Success` | QueryClient pre-loaded → MetaStrip mostra Model + Dim + Chunks + IndexedAt |
| `MetaStrip_Shows_Skeleton_While_Loading` | Pending → 4× skeleton |
| `MetaStrip_Shows_NotIndexed_EmptyState_On_404` | Error 404 → EmptyState "Documento non indicizzato" |
| `Search_Submits_On_Enter_And_Button_Click` | Type query + Enter → mutation called `{ query, limit: 10 }` |
| `Search_Button_Disabled_When_Query_Empty` | Empty input → button disabled |
| `Search_Helper_Shown_When_Query_Too_Long` | Type > 1000 char → `aria-invalid` + helper text |
| `ResultRow_Expands_On_Click` | Click row → snippet-full + VecThumb + meta-table visible |
| `VecThumb_Renders_Deterministic_Gradient` | Same seed 2 instances → identical `style.background` |
| `Export_Button_Has_Correct_Href` | Render → `<a href download>` con URL corretto |
| `Drawer_Closes_On_Escape_Key` | Open → Escape → onOpenChange(false) |
| `Drawer_Auto_Closes_When_DocId_Becomes_Null` | Re-render `docId=null` → state cleared |
| `Mobile_Trigger_Button_Disabled` | matchMedia `< 880px` → trigger disabled + tooltip |

**Hook** — `apps/web/src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts`

| Test | Scenario |
|---|---|
| `useDocumentEmbeddingsMeta_Disabled_When_Closed` | `enabled=false` → no fetch |
| `useDocumentEmbeddingsMeta_Fires_When_Open` | `enabled=true` → URL corretto |
| `useDocumentEmbeddingsMeta_StaleTime_5min` | Verify query options |

### E2E Playwright (smoke)

`apps/web/e2e/admin-kb-embeddings-viewer.spec.ts`

| Spec | Steps |
|---|---|
| **EM-01 happy path** | Login admin → /admin/knowledge-base → select doc indicizzato → click 📋 View embeddings → drawer opens → MetaStrip visible → search "predator" → results table populates → click row → snippet expands |
| **EM-02 not-indexed doc** | Select failed doc → click trigger → drawer opens → EmptyState "Documento non indicizzato" → close |
| **EM-03 export** | EM-01 setup → click Export footer → download `{docId}-chunks.json` triggered |
| **EM-04 a11y axe** | EM-01 setup → `injectAxe()` + `checkA11y()` su drawer → 0 violations AA |

### Acceptance criteria (Adzic Given/When/Then)

```
AC-1 Drawer apre dal trigger
  Given doc selezionato in /admin/knowledge-base con status="ready" e VectorDocument esistente
  When admin clicca "📋 View embeddings" nel hero-actions di KbDocDetailPanel
  Then DocumentEmbeddingsDrawer apre con role="dialog"
  And MetaStrip mostra 4 KPI cards (Model, Dimensions, Total chunks, Indexed at)
  And i valori provengono da GET /admin/kb/docs/{docId}/embeddings/meta
  And una audit row viene scritta con action="EmbeddingsMetaView", resource="Document", resource_id={docId}, level=1
```

```
AC-2 Ricerca semantica scoped
  Given drawer aperto su doc Wingspan con 412 chunks
  When admin digita "predator activation" e clicca Cerca
  Then POST /admin/kb/docs/{wingspanDocId}/chunks/search invocato con { query: "predator activation", limit: 10 }
  And results mostrate in result-table con score badge + snippet + page + chunkIdx
  And VecThumb gradient rendered deterministic per ogni row
  And nessun raw vector value presente nel payload network response
```

```
AC-3 Documento non indicizzato
  Given doc selezionato con status="failed" (nessun VectorDocument)
  When admin clicca "📋 View embeddings"
  Then drawer apre con EmptyState "Documento non indicizzato"
  And SearchPanel non visibile
  And Export button disabled
```

```
AC-4 Audit forensic trail
  Given admin alice@example.com apre drawer su 3 doc differenti in sequenza
  When query audit_logs WHERE action="EmbeddingsMetaView" AND actor_id=alice
  Then 3 row tracciate con resource_id distinti e ts crescenti
```

```
AC-5 Zero raw vector leak (security)
  Given drawer aperto + ricerca eseguita
  When DevTools network inspect dei 2 response (meta + search)
  Then nessun field "vector", "embedding", "coordinates", "values" presente nei JSON payload
  And i campi sono limitati alla whitelist DTO documentata
```

### Test count summary

| Layer | Count atteso |
|---|---|
| BE Unit | ~6 |
| BE Integration | ~6 |
| FE Unit (component + hook) | ~16 |
| FE Integration (TanStack + MSW) | ~3-4 |
| E2E Playwright | 4 |
| **Total** | **~35-36 test** |

## 11. Effort & phasing

| Layer | Effort | Note |
|---|---|---|
| BE Query/Handler/DTO/Validator/Endpoint | ~3-4h | Pattern consolidato, riusa pipeline AuditLoggingBehavior |
| BE Test (unit + integration) | ~3h | Riusa Testcontainers fixture esistente |
| FE Drawer + 5 sub-components | ~7-9h | Componenti nuovi ma estetica già design-locked nel mockup |
| FE Hook + fetcher + wire-up | ~2h | Pattern consolidato (TanStack + api client) |
| FE Test (component + hook) | ~3-4h | Vitest standard |
| E2E Playwright | ~1-2h | 4 spec, riusa fixture admin login |
| **Total** | **~20-24h** | P3 (single feature branch, 1-2 PR) |

### Phasing

**Single PR** (feature branch `feature/issue-1674-embeddings-viewer`):

1. BE: Query + Handler + Validator + DTO + Endpoint + 6 unit + 6 integration test
2. FE: api client + hook + drawer + 5 sub-components + 16 unit + 4 hook test
3. Wire-up: edit KbDocDetailPanel + invalidation in reindexMutation.onSuccess
4. E2E: 4 Playwright spec

Se review size warrants, split in 2 PR:
- PR 1: BE only (endpoint + test) — mergiable standalone (FE può consumarlo successivamente)
- PR 2: FE wire-up (drawer + test + E2E)

## 12. Open decisions for plan three-amigos

- **D-EV-1**: Vec-thumb hash function — `djb2` vs `fnv1a` vs `cyrb53` vs trivial `chunkIdx % 1000`. Decisione: usare util esistente se presente (cercare `simpleHash` in `apps/web/src/lib/`), altrimenti `cyrb53` inline (deterministic + buona distribuzione, ~10 LOC).
- **D-EV-2**: Drawer width 720px su desktop — verificare con altri drawer admin esistenti (`upload-for-game-drawer.tsx`, `CanaliDrawer.tsx`) per consistency. Adattare se altro standard.
- **D-EV-3**: Endpoint URL `embeddings/meta` vs `embeddings-meta` vs `embeddings/summary` — verificare convenzioni endpoint admin esistenti. Raccomandato `embeddings/meta` per coerenza REST con `/chunks/search`, `/chunks/export`.
- **D-EV-4**: Skeleton durante meta loading — usare componente `<Skeleton>` esistente o inline `animate-pulse bg-muted`. Verificare presenza primitive condivisa.
- **D-EV-5**: **Dimensions constant resolution** — discrepanza nota tra `/admin/embedding/info` (espone `dimension: 1024`) e schema DB (`vector(768)` in `pgvector_embeddings`). Il mockup `sp5-admin-kb-vectors.html` mostra 768d coerente con DB. Decisione raccomandata: usare `768` hardcoded nel DTO (allineato a schema + mockup), tracciare follow-up issue per audit `/admin/embedding/info` endpoint (likely stale config). Alternativa: in plan three-amigos, esporre `Dimensions` come `int?` e ricavarlo runtime da `IEmbeddingService.GetDimensions()` se metodo esiste, fallback 768.

## 13. Cross-references

- Parent spec: [`2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`](./2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md)
- Sibling spin-outs: #1673 (B1 versioned reindex), #1675 (B4 quality eval), #1676 (D hero metadata)
- Mockups: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` (trigger button L236), `sp5-admin-kb-vectors.html` (estetica riferimento per result-row + vec-thumb)
- ADR potenzialmente impattati: nessuno (read-only feature, no architectural change)
- CLAUDE.md sezione applicabile: § Architecture (CQRS Pattern), § AI Assistant Rules (DDD Rules: no direct service injection in endpoints)

## 14. Appendix — brainstorming session provenance

Sessione 2026-06-05 (Claude Opus 4.7, /sc:spec-panel + superpowers:brainstorming).

**4 clarifying questions risolte** → 6 decisioni lockate (DEC-1..DEC-6).
**Sezioni design presentate**: Architettura, Endpoint contract, Security, UI, Error handling, Testing — ciascuna approvata utente sequenzialmente.

**Convergent findings (Wiegers/Adzic/Fowler/Newman/Crispin)**:
- Mockup vectors-hub espone già il pattern "vec-thumb gradient + meta-table senza raw values" → Newman risk pre-mitigato by design
- 2 endpoint FU-4 esistenti coprono search + export → 1 solo NEW endpoint richiesto (meta)
- Drawer scoped al doc evita duplicazione UI con vectors-hub cross-doc
- Audit Level 1 proportionato (read-only no-leak) — Level 2+ overkill

**Productive tension**:
- Surface alternative (navigate vs drawer vs route) → risolto in favore drawer per contesto preservation
- Meta endpoint vs zero-endpoint baseline → risolto per confirmation visiva model/dim

**Out of scope esplicito** (deferred):
- Aggregati statistici (chunk distribution, lang chips %, size MB) — overkill P3
- Raw vector inspection — incompatibile con Newman risk
- Step-up 2FA enforcement — gap noto (#1859), non implementato in progetto

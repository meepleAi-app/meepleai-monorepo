# KB Detail Page (G4) — Design Spec

**Date**: 2026-05-10
**Author**: Brainstorming session post-G1+G5 (visual companion)
**Issue**: [#921](https://github.com/meepleAi-app/meepleai-monorepo/issues/921)
**Mockup riferimento**: [`admin-mockups/design_files/sp4-kb-detail.jsx`](../../../admin-mockups/design_files/sp4-kb-detail.jsx)
**Parent spec**: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G4
**Sblocca**: [`2026-05-09-game-chat-tab-v1-g5-design.md`](./2026-05-09-game-chat-tab-v1-g5-design.md) §4.3 (footer "Apri nella KB" del CitationModal — oggi gated in PR #918)
**Status**: DRAFT — pending user review

---

## 1. Contesto

Implementa il **goal G4** del flusso "serata di gioco": una pagina che permette all'utente Alpha di consultare il regolamento (Knowledge Base) atterrando da una citazione cliccata nella chat in-game. Sblocca il footer "Apri nella KB" del `CitationModal` (PR #918) che oggi è `undefined`-gated.

### 1.1 Scope-frame

**In scope:**
- Nuova route Next.js `/kb/[documentId]`
- 9 componenti V2 in `apps/web/src/components/v2/kb-detail/` (6 stub esistenti + 3 nuovi)
- 3 hook query (probabilmente già esistenti — verifica Step 0)
- Redirect 307 da `/knowledge-base/[id]` → `/kb/[documentId]` per backward compat
- Wiring del footer "Apri nella KB" del `CitationModal` in `GameChatTabV2.tsx`

**Out of scope:**
- Lista docs per gioco (CTA "Vedi tutti" punta a route futura, fuori scope qui)
- Backend modifiche (endpoint già documentati in PR #732 §6.3)
- E2E Playwright (rinviato a plan G2 fast-resume)
- Editing chunks (read-only)

### 1.2 Decisioni di design (validate via brainstorming visual companion)

| # | Domanda | Scelta |
|---|---|---|
| 1 | Layout responsive | C — Hybrid (split desktop + drawer mobile) |
| 2 | Comportamento deep-link | D — TOC + highlight + preview, NO drawer auto-aperto |
| 3 | Search dentro KB | A — Client-side filter su `snippet` + `headingPath` |
| 4 | Processing states | C+D — 2 stati distinti (pending/failed) + auto-refresh polling 5s |
| 5 | Routing | D — `/kb/[documentId]` + breadcrumb back + redirect da V1 |
| 6 | Markdown rendering | A — `react-markdown` + `remark-gfm` (verifica reuse chat-unified) |

### 1.3 Vincoli SMART (da spec G4 parent)

| Goal | Vincolo | Misurabile |
|---|---|---|
| **G4.1** | Click chip citation → KB page con chunk evidenziato in ≤2s | Test integrazione + smoke manual |
| **G4.2** | Search box filtra chunks in <100ms (client-side) | Vitest performance check |
| **G4.3** | Polling auto-refresh 5s quando `processingStatus === 'queued' \| 'processing'` | Mock test + manual smoke |
| **G4.4** | 100% delle citazioni del CitationModal possono ora essere aperte (footer KB visibile) | Audit visivo + integration test |

### 1.4 OQ risolte durante brainstorm

- **OQ1 (Citation chunkId)**: `Citation` type (`apps/web/src/types/domain.ts:134-145`) ha `documentId` + `pageNumber` ma **NON `chunkId`**. Deep-link userà `?page=12` come query param invece di `?chunk=<id>`. La pagina cercherà il primo chunk con `pageNumber === 12` per evidenziarlo.

---

## 2. Architecture

```
apps/web/src/
├── app/(authenticated)/kb/[documentId]/
│   └── page.tsx                          ← NEW — Server component, parse params, render <KbDetailView/>
├── app/(authenticated)/knowledge-base/[id]/
│   └── page.tsx                          ← MODIFY — sostituisci body con redirect 307 a /kb/[id]
├── components/v2/kb-detail/              ← stub esistenti, da implementare
│   ├── KbHeader.tsx                      ← STUB → impl (puro)
│   ├── KbChunkListPanel.tsx              ← STUB → impl (puro)
│   ├── KbChunkPreview.tsx                ← STUB → impl (puro)
│   ├── ChunkSearchBox.tsx                ← STUB → impl (controlled)
│   ├── MarkdownRenderBlock.tsx           ← STUB → impl (puro, wrapper react-markdown)
│   ├── KbProcessingState.tsx             ← STUB → impl (puro)
│   ├── KbToc.tsx                         ← NEW (puro, TOC dai headingPath)
│   ├── KbChunkPreviewDrawer.tsx          ← NEW (controlled, mobile bottom-sheet)
│   ├── KbDetailView.tsx                  ← NEW (orchestrator)
│   └── index.ts                          ← NEW (barrel)
└── hooks/queries/
    ├── useKbDocDetail.ts                 ← VERIFY (Step 0): già esiste? altrimenti CREATE
    ├── useKbChunks.ts                    ← VERIFY: già esiste? altrimenti CREATE
    └── useKbChunkDetail.ts               ← VERIFY: già esiste? altrimenti CREATE
```

**Rendering tree (desktop ≥1024px)**:

```
KbDetailView (orchestrator)
├── KbHeader (top: title + meta + breadcrumb back se from=chat)
├── ChunkSearchBox (sticky)
└── flex(2-col):
    ├── left (300px): KbToc + KbChunkListPanel (filtered by search)
    └── right (flex-1): KbChunkPreview con MarkdownRenderBlock
```

**Rendering tree (mobile <1024px)**:

```
KbDetailView (orchestrator)
├── KbHeader (compact)
├── ChunkSearchBox (sticky)
└── KbChunkListPanel (full width)
└── KbChunkPreviewDrawer (controlled, opens on chunk tap)
    └── KbChunkPreview con MarkdownRenderBlock
```

**Stati FSM (orchestrator)**:

```
on mount:
  useKbDocDetail(documentId)
    .processingStatus === 'failed'   → <KbProcessingState status="failed" onRetry={...} />
    .processingStatus === 'queued'   → <KbProcessingState status="queued" /> + START polling 5s
    .processingStatus === 'processing' → <KbProcessingState status="processing" /> + START polling 5s
    .processingStatus === 'ready'    → render full layout (continue below)
    HTTP 423 Locked                  → handle as 'queued' (semantica spec §6.3.1)

  useKbChunks(documentId, { limit: 200 })
    isLoading → skeleton list
    isError   → error inline
    data      → render KbToc + KbChunkListPanel

  active chunk: searchParams.get('page') → first chunk where chunk.pageNumber === pageNum
                 OR searchParams.get('chunk') (futuro) → chunk.id
                 OR chunks[0]?.id (default)

  useKbChunkDetail(documentId, activeChunkId) — lazy fetch del body completo
    isLoading → preview skeleton
    data      → MarkdownRenderBlock con chunk.body
```

---

## 3. Component Contracts

### 3.1 Pure components

| Component | Props essenziali |
|---|---|
| `KbHeader` | `title: string, docType: KbDocType, gameName?: string, uploaderName: string, uploadedAt: string, lastIngestedAt: string, chunkCount: number, pageCount: number \| null, language: string, backHref?: string, backLabel?: string` |
| `MarkdownRenderBlock` | `content: string, className?: string` (wrapper su `react-markdown` + `remark-gfm`) |
| `KbToc` | `entries: TocEntry[], activeChunkId?: string, onSelect: (chunkId: string) => void` dove `TocEntry = { id, headingPath: string[], pageNumber: number \| null }` |
| `KbChunkListPanel` | `chunks: readonly KbChunkSummary[], activeChunkId?: string, onChunkSelect: (id: string) => void` |
| `KbChunkPreview` | `chunk: KbChunkDetail \| null, isLoading?: boolean` |
| `KbProcessingState` | `status: 'queued' \| 'processing' \| 'failed', errorMessage?: string, onRetry?: () => void` |

### 3.2 Controlled components

| Component | Props essenziali |
|---|---|
| `ChunkSearchBox` | `value: string, onChange: (next: string) => void, placeholder?: string, resultCount?: number, totalCount?: number` |
| `KbChunkPreviewDrawer` | `chunk: KbChunkDetail \| null, open: boolean, onClose: () => void` (mobile bottom-sheet wrapper di `KbChunkPreview`) |

### 3.3 Orchestrator

```ts
interface KbDetailViewProps {
  readonly documentId: string;
  readonly initialPageNumber?: number;  // da ?page=N
  readonly fromChat?: boolean;          // da ?from=chat
  readonly backGameId?: string;         // da ?gameId=X (per breadcrumb back)
}
```

### 3.4 Hook contracts

```ts
interface UseKbDocDetailResult {
  readonly data: KbDocDetail | null;
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly isLocked: boolean;  // HTTP 423 semantically (status !== 'ready')
}

interface UseKbChunksResult {
  readonly data: readonly KbChunkSummary[];
  readonly isLoading: boolean;
  readonly isError: boolean;
  // Note: paginazione cursor disponibile backend ma in scope G4 fetch first page (limit=200) — sufficiente per Alpha
}

interface UseKbChunkDetailResult {
  readonly data: KbChunkDetail | null;
  readonly isLoading: boolean;
  readonly isError: boolean;
}
```

`useKbDocDetail` integra il **polling auto-refresh 5s** quando `data?.processingStatus` è `'queued'` o `'processing'`. Stop polling quando diventa `'ready'` o `'failed'`.

---

## 4. Logic / Decisions

### 4.1 Search filter (client-side)

```ts
const filtered = useMemo<readonly KbChunkSummary[]>(() => {
  const q = query.trim().toLowerCase();
  if (q.length === 0) return chunks;
  return chunks.filter(c =>
    c.snippet.toLowerCase().includes(q) ||
    c.headingPath.some(h => h.toLowerCase().includes(q))
  );
}, [chunks, query]);
```

Quando `filtered.length === 0` → empty state inline ("Nessun chunk corrisponde a '<query>'") — NON nasconde search box, l'utente può clear.

### 4.2 Polling auto-refresh

```ts
const POLL_INTERVAL_MS = 5_000;

const isPending = data?.processingStatus === 'queued' || data?.processingStatus === 'processing';

useEffect(() => {
  if (!isPending) return;
  const id = setInterval(() => {
    queryClient.invalidateQueries({ queryKey: ['kb-doc', documentId] });
  }, POLL_INTERVAL_MS);
  return () => clearInterval(id);
}, [isPending, documentId, queryClient]);
```

Quando il documento diventa `ready`, l'orchestrator re-renderizza automaticamente al layout completo (no refresh manuale).

### 4.3 Citation deep-link flow

URL atteso: `/kb/<documentId>?page=<N>&from=chat&gameId=<gameId>`

```
1. Page mount → parse searchParams
2. useKbChunks loads → trova first chunk dove chunk.pageNumber === N
3. setActiveChunkId(matchedChunk.id) (oppure chunks[0].id se nessun match)
4. Scroll KbChunkListPanel allo chunk attivo (smooth, scrollIntoView)
5. Highlight chunk attivo: classe data-highlight="entering" per 2s, fade-out via CSS
6. KbToc evidenzia il path attivo
7. useKbChunkDetail fetcha body chunk → MarkdownRenderBlock renderizza
8. Mobile: NO drawer auto-aperto (Q2-D scelta) — utente vede contesto attorno
9. KbHeader mostra breadcrumb "← Wingspan" se fromChat=true (link a /library/games/<gameId>?tab=aiChat)
```

### 4.4 Sblocco G1+G5 (PR #918) — `onOpenInKb`

In `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`, sostituire:

```tsx
// PRIMA (PR #918)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  onOpenInKb={undefined /* TODO: enable when G4 lands */}
/>

// DOPO (G4)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  onOpenInKb={selectedCitation ? () => router.push(
    `/kb/${selectedCitation.documentId}?page=${selectedCitation.pageNumber}&from=chat&gameId=${gameId}`
  ) : undefined}
/>
```

### 4.5 Markdown rendering

`MarkdownRenderBlock` wraps `react-markdown` + `remark-gfm`. Configurazione minima:

```tsx
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

<ReactMarkdown
  remarkPlugins={[remarkGfm]}
  components={{
    table: props => <table className="my-2 border-collapse text-sm" {...props} />,
    th: props => <th className="border border-border px-2 py-1 bg-muted text-left" {...props} />,
    td: props => <td className="border border-border px-2 py-1" {...props} />,
    code: ({ inline, ...props }) =>
      inline
        ? <code className="rounded bg-muted px-1 py-0.5 text-xs font-mono" {...props} />
        : <pre className="my-2 overflow-x-auto rounded bg-muted p-3 text-xs font-mono"><code {...props} /></pre>,
    blockquote: props => <blockquote className="border-l-4 border-[hsl(var(--c-kb))] pl-4 italic my-2" {...props} />,
  }}
>
  {content}
</ReactMarkdown>
```

> **Verifica Step 0**: `react-markdown` è già nel bundle? Probabilmente sì da chat-unified V1 — riusare. Se NO, aggiungere a `package.json` (pnpm add).

### 4.6 TOC build

```ts
interface TocEntry {
  readonly id: string;          // chunk id
  readonly headingPath: readonly string[];  // ["Setup", "Numero di factory display"]
  readonly pageNumber: number | null;
}

function buildToc(chunks: readonly KbChunkSummary[]): readonly TocEntry[] {
  return chunks.map(c => ({
    id: c.id,
    headingPath: c.headingPath,
    pageNumber: c.pageNumber,
  }));
}
```

Il `KbToc` rendera la gerarchia raggruppando per primo elemento di `headingPath` (sezione root).

---

## 5. Routing & redirect

### 5.1 Nuova route

`apps/web/src/app/(authenticated)/kb/[documentId]/page.tsx`:

```tsx
// Server component, parse params, delega a client orchestrator
export default async function KbDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ documentId: string }>;
  searchParams: Promise<{ page?: string; from?: string; gameId?: string }>;
}) {
  const { documentId } = await params;
  const sp = await searchParams;
  const initialPageNumber = sp.page ? Number(sp.page) : undefined;
  return (
    <KbDetailView
      documentId={documentId}
      initialPageNumber={initialPageNumber}
      fromChat={sp.from === 'chat'}
      backGameId={sp.gameId}
    />
  );
}
```

### 5.2 Redirect V1

`apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx`:

```tsx
import { redirect } from 'next/navigation';

export default async function KnowledgeBaseLegacyRedirect({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/kb/${id}`);
}
```

Lo stub minimal precedente (~160 LOC) viene sostituito interamente.

---

## 6. Testing strategy

### 6.1 Unit (Vitest)

| Component | Scenari |
|---|---|
| `KbHeader` | render con/senza gameName, con backHref, con tutti i campi meta |
| `ChunkSearchBox` | controlled, count display "X di Y", placeholder default |
| `KbToc` | build da headingPath nested, active highlight, click → callback, empty entries |
| `KbChunkListPanel` | render N chunks, active highlight, scroll into view via ref |
| `KbChunkPreview` | loading skeleton, render chunk, null state |
| `KbChunkPreviewDrawer` | open/close, ESC chiude, swipe-down opzionale |
| `MarkdownRenderBlock` | snapshot su contenuto ricco (tabella, code inline, code block, blockquote, lista nested) |
| `KbProcessingState` | 3 stati visivi (queued, processing, failed), retry callback |
| `useKbDocDetail` | data success, isLoading, isError, isLocked (423), polling start/stop quando pending → ready |
| `useKbChunks` | data success, isLoading, isError, paginazione cursor (eventuale) |

### 6.2 Integration (Vitest + MSW)

| Scenario | Setup | Asserzione |
|---|---|---|
| Happy path desktop | docDetail=ready, chunks=10 | KbToc + KbChunkListPanel + KbChunkPreview rendered |
| Pending state | docDetail.processingStatus=processing | KbProcessingState rendered, polling started |
| Failed state | docDetail.processingStatus=failed | KbProcessingState con retry CTA |
| Deep-link | searchParams.page=12 | activeChunkId = first chunk con pageNumber=12, highlight applied |
| Search filter | type "score" | chunks filtered, count display aggiornato |
| Mobile drawer | tap chunk | KbChunkPreviewDrawer open=true |
| 423 Locked | API ritorna 423 | KbProcessingState renderizzato come pending |
| Back to chat | searchParams.from=chat&gameId=X | KbHeader breadcrumb "← Game X" link a /library/games/X?tab=aiChat |

### 6.3 G1+G5 reciprocal sblocco

Modifica `GameChatTabV2.test.tsx` (PR #918) per cambiare aspettativa:
- Test "citation chip click opens CitationModal" → ora il footer "Apri nella KB" DEVE essere visibile (non più hidden)
- Aggiungi test "click 'Apri nella KB' naviga a /kb/[documentId]?page=N&from=chat&gameId=X"

### 6.4 Acceptance criteria

- [ ] G4.1: 10/10 fixture deep-link landa con chunk highlight in <2s
- [ ] G4.2: search filter su 200 chunks <100ms (vitest perf check)
- [ ] G4.3: polling 5s start/stop testato
- [ ] G4.4: footer "Apri nella KB" del CitationModal visibile in tutti i test integration di GameChatTabV2

---

## 7. Open questions (residue, da risolvere in plan)

- **OQ2**: paginazione chunks oltre 200? Per Alpha (manuali tipici <100 chunks) limit=200 basta. Se servirà infinite scroll, plan separato.
- **OQ3**: `MarkdownRenderBlock` deve sanitizzare HTML? Default `react-markdown` blocca raw HTML, ma se backend invia `<script>` nei chunks (improbabile da PDF) serve `rehype-sanitize`. Decisione delegata al plan.
- **OQ4**: drawer mobile usa primitive v2 esistente (`apps/web/src/components/ui/v2/drawer/`) o componente custom? Decisione delegata al plan dopo aver verificato API drawer primitive.
- **OQ5**: search box ha debounce? Il filtro è O(N) con N≤200 — istantaneo a JS speed. Probabilmente NO debounce necessario. Decisione delegata al plan.

---

## 8. Riferimenti

- Mockup: [`admin-mockups/design_files/sp4-kb-detail.jsx`](../../../admin-mockups/design_files/sp4-kb-detail.jsx) (split-view desktop reference)
- Spec parent: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G4
- Sblocca: [`2026-05-09-game-chat-tab-v1-g5-design.md`](./2026-05-09-game-chat-tab-v1-g5-design.md) §4.3
- API contract: PR #732 §6.3 (backend già implementato)
- Schema TS: `apps/web/src/lib/api/schemas/kb-chunks.schemas.ts`
- Citation type: `apps/web/src/types/domain.ts:134-145`
- Issue: [#921](https://github.com/meepleAi-app/meepleai-monorepo/issues/921)
- v2-migration-matrix: route `/kb/[id]` Tier M, status `pending` → diventerà `done #YYY` post-merge

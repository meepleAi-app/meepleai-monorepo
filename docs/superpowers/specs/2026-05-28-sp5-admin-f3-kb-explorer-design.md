# SP5 Admin F3.1 — KB Explorer + sub-nav (Design)

**Date:** 2026-05-28
**Status:** draft (pending user review)
**Scope:** Prima ondata KB del re-skin SP5 (F3, gruppo A5). Trasforma la landing `/admin/knowledge-base` da hub-a-griglia a **esploratore master-detail** (KBTree + pannello documento) e introduce una **sub-nav** KB condivisa. Incremento conservativo: consegna il pezzo net-new (KbTree) + l'IA consolidata; differisce il re-skin interno delle tool-page e i tab doc-detail senza backend.

**Predecessors:**
- `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` — design di consolidamento (§5 A5 → `/admin/knowledge-base`, §9 F3 = ondata KB/AI). Principio guida: *l'estetica prima della struttura* (re-skin fedele al look, IA libera di consolidare).
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` — mockup SP5 dell'esploratore (tree + doc-detail).
- `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-subnav.html` — mockup della sub-nav KB (colma il buco IA: l'handoff SP5 non posizionava le tool-page KB una volta rimosso l'hub-a-griglia).

---

## 1. Goal

La landing `/admin/knowledge-base` oggi è un **hub a griglia di 10 card** che linkano a sotto-pagine. Il mockup SP5 la vuole come **esploratore master-detail**: KBTree a sinistra (gioco → documenti), pannello documento a destra (hero + tab Preview/Chunks). Inoltre introduce una **sub-nav** che dà accesso alle altre tool-page KB.

F3.1 realizza, con blast radius minimo:

1. Una **sub-nav KB** (`layout.tsx`) che wrappa tutte le route `/admin/knowledge-base/*` e ne evidenzia la sezione attiva.
2. Il **rebuild della landing** in esploratore: componente `KbTree` (net-new) + pannello documento che riusa i componenti `features/kb-detail/*` esistenti.

Valore: estetica SP5 sull'area KB + un esploratore gerarchico che oggi manca, senza toccare le 7 tool-page esistenti né inventare backend.

## 2. Non-goals (differiti → follow-up)

- ❌ **Tab Ingestion-log & Used-by** del pannello documento: nessun endpoint backend li alimenta → **non renderizzati** in F3.1 (niente tab rotti o dati finti). Follow-up dedicato quando/se il backend esiste.
- ❌ **Re-skin interno delle 7 tool-page** (vectors, queue, pipeline, embedding, feedback, settings, snapshots): in F3.1 ereditano **solo** la sub-nav dal layout; il loro contenuto resta invariato. Re-skin in incrementi successivi.
- ❌ **Azioni esotiche** del pannello (Quality eval, Export chunks JSON, View embeddings): nessun backend → fuori scope.
- ❌ **Similarity-search dentro i chunk** (il mockup mostra "similarity match"): in F3.1 la ricerca chunk è un **filtro testo** sulla lista esistente.
- ❌ **Tab Preview (rendering PDF) del pannello documento**: richiede un PDF viewer (react-pdf, iframe, …) → fuori scope F3.1. Il pannello dx ha **una sola vista** (hero + lista chunk), senza tab. Preview = follow-up dedicato.
- ❌ **Riuso `features/kb-detail/*`**: i 4 componenti (`KbHeader`, `KbChunkListPanel`, `KbChunkPreview`, `KbProcessingState`) sono **stub `return null`** esplicitamente DEFERRED post-pivot 2026-05-10 ("NON IMPORTARE questo componente"). F3.1 costruisce un componente nuovo `KbDocDetailPanel` minimale (vedi §5).
- ❌ **A5b (upload), A4 (AI/RAG), D1 (mechanic-extractor)**: parte del cluster F3 ma incrementi separati (F3.2+).
- ❌ **Header KB condiviso nel layout** (crumbs + stat globali `247 docs · 18.487 chunks`): in F3.1 il layout rende solo la sub-nav; spostare l'header nel layout richiederebbe editare le 7 sub-page (rimosso il loro `<h1>`) → rimandato all'incremento di re-skin sub-page.

## 3. Information Architecture — sub-nav come tab-link a route reali

Il mockup `sp5-admin-kb-subnav.html` risolve la sub-nav come una `.admin-tabs` di **8 link a route reali** (non tab JavaScript), con la sezione attiva derivata dal path:

| Tab | Route | Contenuto |
|-----|-------|-----------|
| **Explorer** (default) | `/admin/knowledge-base` | master-detail (rebuild, questo doc) |
| Vector Collections | `/admin/knowledge-base/vectors` | sub-page esistente, invariata |
| Processing Queue | `/admin/knowledge-base/queue` | sub-page esistente, invariata |
| RAG Pipeline | `/admin/knowledge-base/pipeline` | sub-page esistente, invariata |
| Embedding | `/admin/knowledge-base/embedding` | sub-page esistente, invariata |
| Feedback | `/admin/knowledge-base/feedback` | sub-page esistente, invariata |
| Settings | `/admin/knowledge-base/settings` | sub-page esistente, invariata |
| Snapshots | `/admin/knowledge-base/snapshots` | sub-page esistente, invariata |

(`documents` e `games`/KB-per-Gioco vengono **assorbite** funzionalmente dall'Explorer; `upload` resta il CTA header "+ Upload PDF". Le route file `documents/` e `games/` **restano** in F3.1 — non più linkate dalla sub-nav, raggiungibili solo via URL diretto; la loro rimozione fisica è un follow-up, non in scope qui.)

## 4. Architettura & file

| File | Azione | Tipo |
|------|--------|------|
| `apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx` | **nuovo** — server component; rende `<KbSubNav/>` sopra `{children}`. Wrappa **tutte** le route KB → le 7 sub-page ottengono la sub-nav senza modifiche. | net-new (piccolo) |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx` | **rebuild** — da hub-a-card a `<KbExplorer/>`. | rebuild landing |
| `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx` | **nuovo** — client, sub-nav 8 tab, active da `usePathname()`. | net-new |
| `apps/web/src/components/admin/knowledge-base/explorer/KbTree.tsx` | **nuovo** — client, tree gioco→documenti (cuore, TDD). | **net-new** |
| `apps/web/src/components/admin/knowledge-base/explorer/KbExplorer.tsx` | **nuovo** — client, orchestra master-detail. | net-new |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` | **nuovo** — client, pannello dx minimale (hero + lista chunk). | net-new |
| 7 tool-page sotto `knowledge-base/*` | **invariate** | riuso |

Blast radius F3.1 = 1 layout + 1 page riscritta + 3 componenti nuovi. **Zero edit** alle 7 tool-page.

## 5. Componenti nuovi

`components/admin/knowledge-base/explorer/`

### `KbSubNav.tsx` (client)
8 tab-link (`<Link>`) con `active` derivato da `usePathname()`. Badge `count` su Queue/Feedback **solo se** una sorgente leggera già esiste (es. `useKbHub`); altrimenti badge omesso (niente numeri hardcoded). Pattern visivo `.admin-tabs`/`.admin-tab`.

### `KbTree.tsx` (client) — net-new, TDD
Il cuore dell'ondata. Niente tree gerarchico esiste oggi nel codebase.

- **Props**: `games: { id, title, chunkCount }[]`, `docsByGame: Record<gameId, KbDoc[]>`, `expandedGameIds`, `selectedDocId`, `filter`, `onToggleGame`, `onSelectDoc`, `onFilterChange`.
- **Render**: game-node espandibile (▾/▸) con emoji + titolo + badge chunk-count; alla espansione, i doc-leaf del gioco con classe stato `indexed | pending | failed`, chunk-count (`347ch` / `indexing…` / `⚠ failed`) e marcatura `selected`.
- **Search**: input filtra game + doc per titolo (match case-insensitive); un gioco con almeno un doc match resta visibile ed espanso.
- **Lazy-load**: i doc di un gioco si caricano all'espansione (vedi §6) — non si scaricano tutti i 247 doc upfront.
- **A11y**: `role="tree"`/`treeitem`, `aria-expanded` sui game-node, `aria-selected` sul doc attivo, focus visibile, target ≥28px.

### `KbExplorer.tsx` (client)
Orchestra il master-detail in grid `300px | 1fr`:
- **sinistra**: `<KbTree/>` con stato espansione + filtro locali.
- **destra**: `<KbDocDetailPanel docId={selectedDocId}/>` (o placeholder se nessuno selezionato).
- **Selezione via URL**: `?doc=<id>` (deep-link condivisibile/bookmarkabile) con `useSearchParams` + `router.replace`.

### `KbDocDetailPanel.tsx` (client) — pannello documento minimale
Costruito da zero (i componenti `features/kb-detail/*` sono stub DEFERRED, non riusabili).
- **Hero** dal `useKbDocDetail({docId})`: titolo, gameName, language, docType, uploadedAt, lastIngestedAt, processingStatus chip, chunkCount, pageCount.
- **Stato 423 (locked)**: `useKbDocDetail` ritorna `{status:'locked', processingStatus}` quando il doc è queued/processing/failed → pannello renderizza banner "Documento in elaborazione" + status chip, niente lista chunk.
- **Stato 200 (ready)**: sotto l'hero, lista chunk via `useKbChunksList({docId, limit:50})` con cursor "Carica altri" (`fetchNextPage`). Ogni riga = `position` · `pageNumber` (se presente) · snippet (1-2 righe troncate) · `headingPath` come breadcrumb mono.
- Placeholder "Seleziona un documento" se `docId` è null/undefined.

## 6. Data flow (hook esistenti — nessun nuovo endpoint)

```
KbTree (top-level)   adminClient.getGameKbStatuses()    → GameKbStatusItem[]   (gameId, gameName, kbStatus 'complete'|'partial'|'none', totalChunks, latestIndexedAt)
KbTree (on-expand)   useKbGameDocuments(gameId)         → GameDocument[]       (KB-indexed docs della game, hook esistente)
Detail (hero)        useKbDocDetail({docId})            → KbDocEnvelope        ({status:'ready', doc:KbDocDetail} | {status:'locked', processingStatus})
Detail (chunks)      useKbChunksList({docId, limit:50}) → useInfiniteQuery     (items:KbChunkSummary[] = id/position/headingPath/snippet/pageNumber, nextCursor)
```

Azioni core del pannello (Re-index / Delete / Download) wired **solo alle mutation già esistenti** (es. la `removeMutation` di `game-kb-documents`); se una mutation non esiste, l'azione è omessa in F3.1 (no stub). La conferma esatta degli endpoint/mutation avviene in fase `writing-plans` (API discovery via Serena prima dei test, da CLAUDE.md).

## 7. Stati

- **Tree**: loading skeleton · empty ("nessun gioco con KB") · error shell.
- **Detail**: nessuna selezione → placeholder · loading/error per il doc selezionato.
- **Sub-nav**: sempre presente; tab attivo coerente col path anche su sub-page.

## 8. Testing (TDD)

- `KbTree.test.tsx`: render game; expand/collapse mostra/nasconde i doc; classi stato `indexed/pending/failed`; `selected` evidenziato; search filtra game+doc; chunk-count mostrato; aria-expanded/selected.
- `KbSubNav.test.tsx`: 8 tab; tab attivo derivato da `usePathname()` (mockato) per ogni route; href corretti.
- `KbExplorer.test.tsx`: selezione doc → carica detail (Chunks/hero); deep-link `?doc=` idrata la selezione al mount; stati empty/error.
- **E2E smoke** (`knowledge-base-explorer.spec.ts`): `/admin/knowledge-base` rende sub-nav + Explorer; navigando a una sub-page la sub-nav resta col tab attivo corretto.

## 9. Criteri di accettazione

1. `/admin/knowledge-base` rende l'esploratore master-detail (non più la griglia di card).
2. La sub-nav è visibile su `/admin/knowledge-base` **e** su tutte le 7 sub-page, col tab corretto in stato `active`.
3. KbTree lista i giochi con KB; espandendo un gioco compaiono i suoi doc con stato e chunk-count; i doc si caricano all'espansione.
4. Selezionando un doc, il pannello destro mostra hero-stats + lista chunk (cursor paginata, "Carica altri" funzionante); l'URL diventa `?doc=<id>` e ricaricando la pagina la selezione si idrata. Se il doc è in stato locked (423), pannello mostra banner "in elaborazione" senza lista chunk.
5. Le 7 tool-page restano **identiche** nel contenuto (diff = solo la sub-nav ereditata dal layout).
6. Nessun tab Ingestion-log/Used-by, nessun dato finto, nessuna azione senza backend.
7. Token-only (nessun colore hardcoded, ESLint `local/no-hardcoded-color-utility` verde); a11y tree (role/aria) presente.

## 10. Follow-up (issue da aprire)

- **F3-FU-1**: tab Ingestion-log del pannello documento (richiede endpoint log di ingestione).
- **F3-FU-2**: tab Used-by (agenti che usano il doc) (richiede endpoint).
- **F3-FU-3**: re-skin interno delle 7 tool-page KB verso il look SP5 + header KB condiviso nel layout.
- **F3-FU-4**: azioni avanzate pannello (Quality eval, Export chunks JSON, View embeddings) + similarity-search nei chunk.
- **F3-FU-5**: tab Preview del pannello documento (richiede integrazione PDF viewer: react-pdf o iframe gated).
- **F3.2+**: re-skin A5b (upload), A4 (AI/RAG), D1 (mechanic-extractor).

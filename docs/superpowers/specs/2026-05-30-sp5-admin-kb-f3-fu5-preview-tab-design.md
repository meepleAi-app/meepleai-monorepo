# SP5 Admin KB — F3-FU-5: Preview tab in KbDocDetailPanel (PDF viewer)

> **Status**: design — review pending
> **Issue**: [#1654](https://github.com/meepleAi-app/meepleai-monorepo/issues/1654) (P3, area/frontend)
> **Parent**: SP5 admin console consolidation — wave F3-FU (post-#1649 KB Explorer)
> **Spec parents**: `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5.A5 · `docs/superpowers/specs/2026-05-28-sp5-admin-kb-toolpages-reskin-design.md` (F3-FU sub-nav contract) · `docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md` (PdfRenderer pattern reused)
> **Predecessors in wave**: F3-FU-1 ingestion-log #1650 (PR #1668) · F3-FU-2 used-by #1651 (PR #1671) · F3-FU-4 doc actions + chunk similarity #1653 (commit `23b17f6a5`, PR #1679)

## 1. Context & Goal

Estendere `KbDocDetailPanel` con un quarto tab `?tab=preview` che renderizza inline il PDF originale del documento KB, eliminando il context-switch verso il download per ispezionare il contenuto. Operazione FE-only: riusa l'endpoint esistente `/api/v1/pdfs/{id}/download`.

L'attuale wave F3-FU ha aggiunto a `KbDocDetailPanel` i tab Overview (default), Ingestion log, Used by, e (dentro Overview) action-bar + chunk similarity-search. Manca un'anteprima visuale del file sorgente — l'admin che vuole verificare il rendering, la qualità OCR, o controllare a quale pagina cade un chunk deve oggi scaricare il PDF o aprire una citation in altra parte dell'app. Il tab Preview chiude questo gap.

## 2. Current state (evidence)

- **Tab routing** in `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`: union `KbDocTabKey = 'overview' | 'ingestion' | 'used-by'`; array `TABS` consumato dalla nav; URL-driven via `?doc={id}&tab={key}` (overview = assenza param).
- **Panel branching** in `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`: 4 stati (null/loading/locked/ready); su `locked` post-#1653 rende `KbDocDetailTabs + KbDocActions + banner` (Used-by è speciale: rende `UsedByPanel` invece del banner durante locked).
- **PDF inline rendering pattern già nel codebase**: `apps/web/src/components/features/game-chat/CitationPdfTab.tsx` (post-PR di `2026-05-10-citation-pdf-viewer-design.md`) usa react-pdf (`Document` + `Page` + `pdfjs.GlobalWorkerOptions.workerSrc`), fetch del blob con credentials, prev/next pagination, anti-leak (no contextmenu, no download). Spec di riferimento citato nel header del file.
- **Endpoint download** `api.pdf.getPdfDownloadUrl(docId)` già esposto e usato sia da `CitationPdfTab` (fetch blob) sia da `KbDocActions` (`<a href={...} download>`). Session-cookie auth.
- **Bundle**: react-pdf + pdfjs worker già nel chunk admin (CitationPdfTab attivo nel game-chat sotto `(library)` route group). Per il tab admin sotto `(dashboard)` il viewer aggiunge zero deps; il bundle admin assorbe la stessa libreria.
- **Test infra**: react-pdf mockato in `apps/web/vitest.setup.ts` (riusabile per nuovi test); `KbDocDetailPanel.test.tsx` + `KbDocDetailTabs.test.tsx` esistono come baseline.

## 3. Scope

### In scope (FU-5)

1. Aggiungere `'preview'` a `KbDocTabKey` e a `TABS` in `KbDocDetailTabs.tsx`.
2. Estrarre `PdfInlineViewer` (net-new shared component, `apps/web/src/components/pdf/PdfInlineViewer.tsx`) con props-driven feature flags. Pattern base = il `PdfRenderer` interno di `CitationPdfTab`.
3. Refactor `CitationPdfTab.tsx` per consumare `PdfInlineViewer` con variant citation (`{ antiLeak: true }`). Output visuale + comportamento esterno invariati.
4. Net-new `KbDocPreviewPanel` (`apps/web/src/components/admin/knowledge-base/explorer/preview/KbDocPreviewPanel.tsx`) che consuma `PdfInlineViewer` con variant admin (`{ download: true, openInTab: true, jumpToPage: true, zoom: true }`).
5. Estendere il branching in `KbDocDetailPanel.tsx`:
   - su `ready`: aggiungere `{activeTab === 'preview' && <KbDocPreviewPanel docId={doc.id} />}` accanto a Ingestion/Used-by/Overview.
   - su `locked`: aggiungere lo stesso special-case di `used-by` per renderizzare il viewer (assunto: file blob disponibile anche su queued/processing/failed). **Verifica vincolante in T0 spike**: se l'endpoint non risponde su `locked`, il scope si restringe a "solo `ready`" (banner standard su locked per Preview come Overview/Ingestion) — la decisione Q2 utente diventa best-effort. Documentare il fallback in `audits/2026-05-30-pdf-download-locked-spike.md`.
6. Toolbar admin: prev/next + page counter (esistente in CitationPdfTab) + jump-to-page input + zoom 50/100/150/fit-width (default fit-width) + open-in-tab + download.
7. Error handling: 404/403/500 fetch errors, pdf.js parsing failure, PDF vuoto, AbortError ignorato, input invalido jump-to-page clampato.
8. Test: unit `PdfInlineViewer` (feature-flag matrix), unit `KbDocPreviewPanel` (admin variant), update `KbDocDetailTabs.test.tsx` (`activeTab='preview'`), update `KbDocDetailPanel.test.tsx` (`?tab=preview` su ready + locked), regression `CitationPdfTab.test.tsx` (deve passare invariato), 2 E2E smoke.

### Out of scope (deferred — track separately if needed)

- ❌ Highlight chunks sovrapposti alla pagina (overlay rectangles).
- ❌ Text-search dentro PDF (richiede pdf.js text-layer + UI dedicata).
- ❌ Annotation rendering (`renderAnnotationLayer={false}` preservato).
- ❌ Multi-page thumbnail grid.
- ❌ Rotation controls.
- ❌ Print button (open-in-tab + print nativo browser è escape-hatch).
- ❌ Performance optimization per PDF >100 pagine (lazy page rendering).
- ❌ Hero metadata enrichment (FileSizeBytes, OCR flag, indexer version) — tracked in spin-out #1676.
- ❌ Visual regression test del viewer rendering (gate rimosso 2026-05-20, CLAUDE.md "Visual Gate REMOVED").
- ❌ Modifiche backend (zero impact: endpoint download già esistente).

## 4. Architecture & components

```
apps/web/src/components/pdf/
  PdfInlineViewer.tsx                      [NEW — extracted shared component]
  __tests__/PdfInlineViewer.test.tsx       [NEW]

apps/web/src/components/features/game-chat/
  CitationPdfTab.tsx                       [MODIFY — consumes PdfInlineViewer w/ citation variant]
  __tests__/CitationPdfTab.test.tsx        [MUST pass invariato]

apps/web/src/components/admin/knowledge-base/explorer/
  KbDocDetailTabs.tsx                      [MODIFY — add 'preview' to KbDocTabKey + TABS entry]
  KbDocDetailPanel.tsx                     [MODIFY — render <KbDocPreviewPanel> for preview tab in BOTH locked + ready branches]
  __tests__/KbDocDetailTabs.test.tsx       [MODIFY — extend test matrix]
  __tests__/KbDocDetailPanel.test.tsx      [MODIFY — extend test matrix]
  preview/
    KbDocPreviewPanel.tsx                  [NEW — admin variant of PdfInlineViewer]
    __tests__/KbDocPreviewPanel.test.tsx   [NEW]

apps/web/e2e/admin/
  kb-doc-preview-tab.spec.ts               [NEW — 2 smoke scenarios]
```

### `PdfInlineViewer` interface (single source of truth)

```typescript
interface PdfInlineViewerProps {
  readonly documentId: string;
  readonly initialPage?: number;
  readonly defaultZoom?: 'fit-width' | number;     // default: 'fit-width'
  readonly features?: {
    readonly antiLeak?: boolean;      // disable contextmenu + select-none; default false
    readonly download?: boolean;      // show download button in toolbar; default false
    readonly openInTab?: boolean;     // show open-in-tab anchor; default false
    readonly jumpToPage?: boolean;    // show jump-to-page input; default false
    readonly zoom?: boolean;          // show zoom controls; default false
  };
  readonly className?: string;
}
```

- **Citation variant** (CitationPdfTab consumer): `features={{ antiLeak: true }}` — resto resta a default (false). Output visuale invariato → test esistenti passano.
- **Admin Preview variant** (KbDocPreviewPanel consumer): `features={{ download: true, openInTab: true, jumpToPage: true, zoom: true }}` — toolbar full.

### Boundary intenzionale

- Ownership check (`useCanViewPdf`) + upsell (`CitationOwnershipUpsell`) restano in `CitationPdfTab`, NON scendono dentro `PdfInlineViewer`. Il viewer riceve solo "rendere o no" — separation of concerns.
- `pdfjs.GlobalWorkerOptions.workerSrc` setup viene spostato dentro `PdfInlineViewer` (mount-time idempotent) → CitationPdfTab non lo dichiara più. Verifica in Task 0 spike: il worker setup multiplo causa problemi? (Risposta attesa: no, è idempotent — react-pdf detecta già-impostato.)

## 5. Data flow

### Fetch PDF blob (riuso pattern CitationPdfTab)

```
PdfInlineViewer mounts
  → useEffect[documentId, initialPage]:
      AbortController(); setLoading(true); setLoadError(null); setNumPages(0); setPdfBlob(null);
      fetch(api.pdf.getPdfDownloadUrl(documentId), { credentials: 'include', signal })
        .then(res => res.ok ? res.blob() : throw new Error(`HTTP ${res.status}: ${res.statusText}`))
        .then(blob => setPdfBlob(blob); setLoading(false))
        .catch(err => if (err.name !== 'AbortError') setLoadError(err.message); setLoading(false))
  → cleanup: controller.abort() su unmount/depChange
```

### Lazy loading (admin variant)

`KbDocPreviewPanel` montato solo quando `activeTab === 'preview'` (pattern analogo a `IngestionPanel`/`UsedByPanel`). Switch a Overview → unmount → controller.abort(). Costo di rete zero se l'utente non apre il tab.

### State interno

| State | Owner | Trigger |
|-------|-------|---------|
| `pdfBlob: Blob \| null` | viewer | fetch success |
| `numPages: number` | viewer | onDocumentLoadSuccess |
| `currentPage: number` | viewer | prev/next/jump-to-page |
| `zoomLevel: number` | viewer | zoom buttons / fit-width recalc on resize |
| `loadError: string \| null` | viewer | fetch failure / pdf.js error |
| `loading: boolean` | viewer | true durante fetch o pdf parsing |

### Toolbar interactions (admin variant)

- **Prev/Next**: `setCurrentPage(p => clamp(p ± 1, 1, numPages))` — esistente in CitationPdfTab.
- **Jump-to-page**: controlled input numerico (`type="number" min="1" max={numPages}`); on submit → `setCurrentPage(clamp(parseInt(input), 1, numPages))`; blur clampa silenziosamente input invalido (NaN → currentPage invariato).
- **Zoom**: 4 preset (50% / 100% / 150% / fit-width) via dropdown o button group. `fit-width` = `ResizeObserver` sul container → calcola `scale = container.width / viewport.width` (passato a `<Page scale={...}>`). Default = `'fit-width'`.
- **Open-in-tab**: `<a href={api.pdf.getPdfDownloadUrl(docId)} target="_blank" rel="noopener noreferrer">` — apre viewer browser-nativo in nuovo tab (preserva session cookie).
- **Download**: stesso pattern di `KbDocActions` Download button: `<a href={...} download>` con session-cookie.

### Integration con tab routing

`KbDocDetailPanel.tsx` legge `searchParams.get('tab')` (pattern esistente, righe 57-63):

```typescript
const activeTab: KbDocTabKey = (() => {
  const tab = searchParams?.get('tab');
  if (tab === 'ingestion') return 'ingestion';
  if (tab === 'used-by') return 'used-by';
  if (tab === 'preview') return 'preview';     // NEW
  return 'overview';
})();
```

URL canonico: `/admin/knowledge-base?doc={uuid}&tab=preview`.

## 6. Error handling

| Failure | Where | Contromisura |
|---------|-------|--------------|
| `/api/v1/pdfs/{id}/download` 404 | fetch | `setLoadError("Documento non trovato")` → banner inline rosa nel viewer body. Toolbar disabilitata. |
| 403 (auth scaduta) | fetch | `setLoadError("Sessione scaduta — ricarica la pagina")` + suggerimento refresh. |
| 500 / network error | fetch | `setLoadError(err.message)` + retry button. |
| pdf.js parsing failure | `<Document onLoadError>` | `setLoadError("PDF non leggibile")`. |
| `numPages === 0` (PDF vuoto) | `<Document onLoadSuccess>` | banner "Documento vuoto" + toolbar nascosta. |
| AbortError (tab-switch rapido) | fetch catch | ignorato (`err.name === 'AbortError'`) — pattern esistente in CitationPdfTab. |
| File non ancora caricato (`locked` con upload incompleto) | fetch 423/404 | banner "File non ancora caricato — riprova al completamento upload". |
| Browser senza pdf.js worker support | Document error | banner + fallback Open-in-tab CTA. |
| Jump-to-page input invalido | onSubmit | clamp `[1, numPages]`, blur silenzioso, NaN ignorato. |
| Zoom out-of-range | controls | preset disabilitati ai bordi (la lista è chiusa: 50/100/150/fit-width). |
| `documentId` cambia mid-fetch | useEffect dep array | cleanup abort → nuovo fetch. |

**Failure isolation**: errori nel viewer locali a `PdfInlineViewer` — torna a Overview ricarica doc detail invariato.

**Telemetria**: nessun nuovo metric — gli errori di download sono già coperti da monitoring esistente su `/api/v1/pdfs/{id}/download` (PDF service-level).

**Toast vs inline**: errori = banner inline (viewer occupa la viewport del pannello, errore già visibile). Toast solo per azioni utente discrete (es. download fallito da `<a download>` — ma in pratica `<a download>` non emette errori React-catchable, lo skip).

## 7. UX & states

### Tab nav

`KbDocDetailTabs` rende 4 voci in ordine: **Overview · Ingestion log · Used by · Preview**. Tab attivo con `aria-current="page"` + bordo amber-500 (pattern esistente).

### Stati del documento

| Stato doc | Comportamento Preview tab |
|-----------|----------------------------|
| `null` (no doc selected) | placeholder "Seleziona documento" (esistente, fuori dal viewer) |
| `loading` (detail fetch in corso) | skeleton (esistente, fuori dal viewer) |
| `locked` queued/processing/failed | `KbDocDetailTabs` + slim hero + `KbDocActions` + `<KbDocPreviewPanel>` (file blob disponibile pre-indexing — da verificare in Task 0 spike) |
| `ready` | `KbDocDetailTabs` + full hero + `<KbDocPreviewPanel>` |

Su `locked`, il viewer rimpiazza il banner "in elaborazione" SOLO per il tab Preview (analogo al pattern Used-by). Per Overview e Ingestion il banner resta come oggi.

### Viewer layout (admin variant)

```
┌─────────────────────────────────────────────────────────┐
│ ⟵ Prev   Pagina 5 / 47   Next ⟶  │ [⌖ Vai a:  __5__]  │
│                                    │ Zoom: [fit-width▾] │
│                                    │ ↗ Apri in tab      │
│                                    │ ⤓ Download         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│                                                         │
│            [ PDF page canvas, scaled ]                  │
│                                                         │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

Layout responsive: toolbar `flex-wrap` su pannello stretto; canvas `aspect-ratio` A4 (210/297) come fallback durante loading (CitationPdfTab pattern).

### Loading skeleton

Durante fetch del blob: spinner centrato `border-[hsl(var(--c-kb))]` (pattern CitationPdfTab). Toolbar visibile ma controlli disabilitati.

### Error banner

Banner inline rosa nel viewer body: `border-rose-500/30 bg-rose-500/5 text-rose-700 dark:text-rose-300 rounded-lg p-4` + CTA retry se applicabile.

## 8. Functional requirements (testable)

| ID | Requirement | Verification |
|----|-------------|--------------|
| FR-1 | `?tab=preview` su `ready` doc → `<KbDocPreviewPanel>` rende; `<Overview chunks>` NON rende. | Unit `KbDocDetailPanel.test.tsx` |
| FR-2 | `?tab=preview` su `locked` doc → `<KbDocPreviewPanel>` rende; banner "in elaborazione" NON rende (special-case come Used-by). | Unit `KbDocDetailPanel.test.tsx` |
| FR-3 | `KbDocDetailTabs` mostra 4 voci nell'ordine `overview · ingestion · used-by · preview`; tab attivo ha `aria-current="page"`. | Unit `KbDocDetailTabs.test.tsx` |
| FR-4 | `PdfInlineViewer` con `features.antiLeak=true` previene `contextmenu` (event default prevented) e applica `select-none` al container canvas. | Unit `PdfInlineViewer.test.tsx` |
| FR-5 | `PdfInlineViewer` con `features.download=true` rende `<a href={api.pdf.getPdfDownloadUrl(docId)} download>` nella toolbar. | Unit `PdfInlineViewer.test.tsx` |
| FR-6 | `PdfInlineViewer` con `features.openInTab=true` rende `<a target="_blank" rel="noopener noreferrer">` nella toolbar. | Unit `PdfInlineViewer.test.tsx` |
| FR-7 | `PdfInlineViewer` con `features.jumpToPage=true` rende input numerico; submit clampato a `[1, numPages]`; NaN ignorato. | Unit `PdfInlineViewer.test.tsx` |
| FR-8 | `PdfInlineViewer` con `features.zoom=true` rende preset 50/100/150/fit-width; switch ricalcola `scale` su Page. `defaultZoom='fit-width'` recalc su resize. | Unit `PdfInlineViewer.test.tsx` |
| FR-9 | Fetch failure → banner inline rosa + toolbar disabilitata; AbortError ignorato silenziosamente. | Unit `PdfInlineViewer.test.tsx` |
| FR-10 | `CitationPdfTab` post-refactor produce identico output e comportamento (test esistenti passano invariati). | Regression `CitationPdfTab.test.tsx` |
| FR-11 | Tab `Preview` switch via click sub-nav aggiorna URL a `?doc={id}&tab=preview`; tornare a `Overview` rimuove il param. | E2E smoke `kb-doc-preview-tab.spec.ts` |
| FR-12 | Toolbar admin visibile in viewport: prev/next + page counter `1 / N` + tutti i controlli admin. | E2E smoke `kb-doc-preview-tab.spec.ts` |

## 9. Test plan

### Layer 1 — Unit (Vitest + React Testing Library)

- **`apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx`** (NEW)
  - Loading skeleton durante fetch
  - Render success con N pagine (mock react-pdf)
  - Prev/Next clamp
  - Jump-to-page valid + clamp + NaN ignore
  - Zoom preset switching → ricalcola `scale`
  - Fit-width recalc su window resize
  - Errore fetch (HTTP 500) → banner inline; toolbar disabled
  - Errore pdf.js → banner inline
  - AbortController cleanup su unmount
  - `features.antiLeak=true` → `onContextMenu` preventDefault + `select-none`
  - `features.download=true` → `<a download>` rendered
  - `features.openInTab=true` → `<a target="_blank" rel="noopener noreferrer">`
  - `features.jumpToPage=false` → input non renderizzato
  - `features.zoom=false` → controls non renderizzati

- **`apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`** (MODIFY — extend matrix)
  - Aggiungi voce `preview` nell'array di test; verifica `aria-current="page"` quando `activeTab='preview'`.

- **`apps/web/src/components/admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx`** (NEW)
  - Mount con `docId` valido → `PdfInlineViewer` rende con admin feature flags
  - Verifica props passate: `{ download, openInTab, jumpToPage, zoom } = true`, `antiLeak = false (default)`
  - `docId` null → componente non crash (early-return o `null`)

- **`apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`** (MODIFY — extend matrix)
  - `?tab=preview` su `ready` doc → `KbDocPreviewPanel` rende, chunks list NON renderizzata
  - `?tab=preview` su `locked` doc → viewer rende, banner "in elaborazione" NON renderizzato
  - Slim hero + action-bar coesistono con viewer in locked

- **`apps/web/src/components/features/game-chat/__tests__/CitationPdfTab.test.tsx`** (MUST keep passing invariato)
  - Refactor a `PdfInlineViewer` invisible dall'esterno; ownership check + upsell + behavior invariati.

### Layer 2 — E2E smoke (Playwright)

- **`apps/web/e2e/admin/kb-doc-preview-tab.spec.ts`** (NEW)
  - Scenario 1: Admin naviga `/admin/knowledge-base?doc={fixtureId}&tab=preview` → toolbar viewer visibile + page counter `1 / 3` (fixture PDF 3-page).
  - Scenario 2: Tab routing — click `Preview` sub-nav → URL aggiornato a `?tab=preview`; click `Overview` → URL torna a `?doc={fixtureId}`.

Mock react-pdf esistente in `apps/web/vitest.setup.ts` riusato per i nuovi unit test; E2E smoke usa fixture reale del backend admin.

### Layer 3 — Verification gate (pre-PR)

```bash
cd apps/web
pnpm test pdf/__tests__/PdfInlineViewer.test.tsx
pnpm test admin/knowledge-base/explorer/__tests__/
pnpm test:admin-dashboard              # suite admin 558+
pnpm test features/game-chat           # regression CitationPdfTab
pnpm typecheck
pnpm lint
pnpm lint:tokens
```

## 10. Non-functional & constraints

### Hard constraints

- **DDD/CQRS backend zero-touch**: riusa `/api/v1/pdfs/{id}/download` esistente. No migration, no nuovo handler, no nuovo DTO.
- **Token canonicalization (DS-15)**: no neutral palette hardcoded (slate/gray/zinc/neutral/stone/white/black). Semantic tokens (`bg-card`, `border-border/60`, `text-muted-foreground`) + entity tokens già attivi in CitationPdfTab (`hsl(var(--c-kb))`, `hsl(var(--c-success))`).
- **Eslint admin convention (DS-13c)**: `KbDocPreviewPanel.tsx` può portare il file-level disable `local/no-hardcoded-color-utility` per amber/zinc come gli altri file dell'explorer (`KbDocDetailPanel.tsx`). `PdfInlineViewer.tsx` evita la palette neutral → no disable necessario.
- **A11y**: bare `<button>` → `type="button"` + `focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2` (convention #1664).
- **Re-skin invariant CitationPdfTab**: output visuale + comportamento esterno identici dopo refactor — test esistenti come gate.

### Soft constraints

- **Bundle**: react-pdf + pdfjs worker già nel chunk admin via altro consumer; il nuovo viewer aggiunge zero deps.
- **Network**: lazy fetch (solo quando tab attivo); AbortController su unmount.
- **Memory**: blob revocato implicitamente quando `pdfBlob` viene sovrascritto (Blob garbage-collected da React state turnover).

## 11. Phasing

L'implementazione segue 6 task incrementali (TDD per ciascuno). I dettagli vivono nel plan associato.

| Task | Focus | Files | Gate |
|------|-------|-------|------|
| T0 | Spike: (a) verifica che `/api/v1/pdfs/{id}/download` risponde 200 su doc `queued`/`processing`/`failed`; (b) verifica setup multiple di `pdfjs.GlobalWorkerOptions.workerSrc` non causa side-effect. **Outcome decisorio**: se (a) fallisce, T5 branching su `locked` resta come Overview/Ingestion (banner standard), test FR-2 rimosso; (b) deve passare prima di T1 (se fallisce, isolare il workerSrc setup a un singleton module). | doc-only `audits/2026-05-30-pdf-download-locked-spike.md` | report committed + (a) outcome riflesso in T5 implementation + (b) outcome riflesso in T1 module structure |
| T1 | Estrai `PdfInlineViewer` (mirror PdfRenderer di CitationPdfTab, props-driven features, default toolbar minimal = solo prev/next + counter) | `components/pdf/PdfInlineViewer.tsx` + test | unit verdi |
| T2 | Refactor `CitationPdfTab` per consumare `PdfInlineViewer` con `{antiLeak:true}` | `features/game-chat/CitationPdfTab.tsx` | regression test verdi |
| T3 | Aggiungi `'preview'` a `KbDocTabKey` + `TABS` + test | `explorer/KbDocDetailTabs.tsx` + test | unit verdi |
| T4 | Crea `KbDocPreviewPanel` (admin variant: full toolbar) | `explorer/preview/KbDocPreviewPanel.tsx` + test | unit verdi |
| T5 | Branching in `KbDocDetailPanel` (ready + locked special-case) | `explorer/KbDocDetailPanel.tsx` + test | unit verdi |
| T6 | E2E smoke (2 scenari) + verification gate completo | `e2e/admin/kb-doc-preview-tab.spec.ts` | typecheck + lint + tutte le suite verdi |

Ordine forzato (dipendenze): T0 → T1 → T2 (CitationPdfTab regression gate prima di toccare la nuova feature) → T3 → T4 → T5 → T6.

## 12. Open questions — status

| ID | Question | Status |
|----|----------|--------|
| O-1 | `/api/v1/pdfs/{id}/download` risponde su doc in stato `queued`/`processing`/`failed`? | OPEN — T0 spike |
| O-2 | `pdfjs.GlobalWorkerOptions.workerSrc` setup multiplo da `PdfInlineViewer` + `CitationPdfTab` pre-refactor causa side-effects? | OPEN — T0 spike. Spostare il setup dentro `PdfInlineViewer` mount-time mitiga (idempotent). |
| O-3 | Zoom UI: dropdown preset o button-group (− 100% +)? | DEFERRED — implementatore sceglie il più ergonomico (entrambi accettabili dal design). |
| O-4 | Fit-width: `ResizeObserver` sul container o `window.addEventListener('resize')`? | DEFERRED — `ResizeObserver` più preciso (container può cambiare width per ragioni non-window, es. drawer collapse). |
| O-5 | Hero metadata enrichment (FileSize, OCR flag) nel viewer panel? | OUT-OF-SCOPE — tracked in spin-out #1676. |

## Appendix — brainstorming provenance (2026-05-30)

Design derivato da brainstorming session via `superpowers:brainstorming` skill. Decisioni chiave (Q1-Q4):

- **Q1 Scope viewer**: `Full PDF (pattern CitationPdfTab)` — react-pdf con Document+Page+prev/next, riuso del pattern già nel codebase.
- **Q2 Stati doc abilitati**: `Tutti gli stati (queued/processing/ready/failed)` — il file blob è disponibile pre-indexing, valore diagnostico anche su `failed`.
- **Q3 Code reuse**: `Estrarre PdfInlineViewer condiviso (refactor)` — refactor pulito vs DRY-debt; rischio CitationPdfTab regression mitigato da test esistenti.
- **Q4 Toolbar admin**: tutte le 4 opzioni selezionate (jump-to-page + zoom + open-in-tab + download).

Visual companion skipped (decisioni concettuali, no materiale visivo SP5 di riferimento — F3-FU-5 è follow-up senza mockup).

Panel sintetico applicato (Wiegers/Cockburn/Nygard/Fowler/Newman/Crispin) durante la scelta del task: Fowler "finisci una wave prima di aprirne un'altra" → conferma scope F3-FU residuo. Nygard sull'isolamento error → banner inline locale, no impact su altri tab.

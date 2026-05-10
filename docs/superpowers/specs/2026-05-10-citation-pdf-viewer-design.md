# Citation PDF Viewer (G4 v3) — Design Spec

**Date**: 2026-05-10
**Author**: Brainstorming session (visual + socratic) post-G1+G5
**Issue**: [#921](https://github.com/meepleAi-app/meepleai-monorepo/issues/921) (riformulato)
**Sblocca**: [`2026-05-09-game-chat-tab-v1-g5-design.md`](./2026-05-09-game-chat-tab-v1-g5-design.md) §4.3 (footer del CitationModal — oggi `onOpenInKb=undefined` in PR #918)
**Spec parent**: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G4
**Status**: DRAFT — pending user review

---

## 1. Contesto e cambio di scope (storia)

Lo spec G4 originale prevedeva una **nuova pagina** `/kb/[documentId]` con lista chunks Markdown navigabili. Durante il brainstorming socratico l'utente ha rivelato il vero modello legale e tecnico:

- I PDF dei manuali sono **protetti da copyright**: l'utente vede il PDF originale **solo se l'ha caricato lui** (per-documentId, non per-gameId)
- **Anti-duplicazione** backend: rifiuta upload di PDF con hash già indicizzato
- **Anti-leak frontend**: niente download button, niente save-as
- **BGG**: solo per metadati pubblici (titolo, anno, copertina) — mai per scaricare PDF
- L'apertura del PDF dalla citazione = **drawer/modal sopra la chat**, NON navigazione a una pagina esterna

→ G4 è ridefinito come **estensione del `CitationModal`** (PR #918) con tab `Snippet | PDF originale` + ownership gate + upload upsell. NON è una nuova pagina.

### 1.1 Scope-frame finale

**In scope:**
- Estensione del `CitationModal` esistente con 2 tab: `Snippet` (default) | `PDF originale` (lazy)
- Ownership check: `Citation.isPublic` primario + fallback `getGameDocuments(gameId)` per cercare `documentId`
- PDF viewer riusa `PdfViewerModal.tsx` esistente (logica embed nel tab)
- Upsell card per non-owner con CTA `/upload?gameId=<X>`
- Sblocco di `onOpenInKb` in `GameChatTabV2.tsx` (PR #918) → diventa `onOpenPdf`
- Rinomina prop in `CitationModal`: `onOpenInKb` → rimosso (la logica vive dentro il modal)
- Cancellazione: vecchio spec G4, issue #921 commento di riformulazione

**Out of scope:**
- Nuova pagina `/kb/[documentId]` — NON la facciamo (cancello dalla matrix v2)
- 9 componenti V2 KB-detail (KbHeader, KbToc, ecc.) — restano stub `return null` per progetto futuro autonomo
- Backend modifiche (tutti gli endpoint esistono: `pdfClient.getPdfDownloadUrl`, `library.declareOwnership`, `getGameDocuments`)
- E2E Playwright (G2 separato)

### 1.2 Decisioni di design (validate via brainstorming)

| # | Domanda | Scelta |
|---|---|---|
| Soc-1 | JTBD reale | Tutti e 4 driver (trust + context + drill-down + learning) |
| Soc-2 | Failure mode (chunk non trovato) | Fuzzy match + warning soft (poi non più rilevante post-pivot) |
| Soc-3 | Mobile UX | Modal/drawer in-place sopra la chat (NO navigate) |
| v3-1 | Verifica ownership client | C primario (`Citation.isPublic`) + A fallback (`getGameDocuments`) |
| v3-2 | Tab default | `Snippet` (lazy load PDF su user action) |

### 1.3 Vincoli SMART

| Goal | Vincolo | Misurabile |
|---|---|---|
| **G4.1** | Click chip citation → modal aperto in <300ms | Già rispettato in PR #918 |
| **G4.2** | Tab "PDF originale" sempre visibile (anche se non-owner) | Test rendering |
| **G4.3** | Se non-owner: upsell card visibile <100ms (no fetch) | Unit test |
| **G4.4** | Se owner: PDF caricato + page seek to `pageNumber` < 3s su 4G | Manual smoke / integration |
| **G4.5** | Niente download button visibile, no link diretto a `/api/v1/pdfs/{id}/download` | Visual audit + a11y test |

### 1.4 OQ residue risolte/dismissed

- ❌ ~~OQ2 paginazione chunks~~ — non più rilevante (no pagina KB)
- ❌ ~~OQ3 sanitize markdown~~ — non più rilevante
- ✅ OQ4 drawer mobile primitive — non serve, il modal è desktop+mobile via `Dialog` shadcn
- ❌ ~~OQ5 search debounce~~ — non più rilevante

---

## 2. Architecture

```
apps/web/src/
├── components/v2/game-chat/
│   ├── CitationModal.tsx                ← MODIFY (2 tab: Snippet | PDF)
│   ├── CitationPdfTab.tsx               ← NEW (gated tab body, embed PdfViewer)
│   ├── CitationOwnershipUpsell.tsx      ← NEW (upsell card per non-owner)
│   └── __tests__/
│       ├── CitationModal.test.tsx       ← MODIFY (aggiungi test tab + ownership)
│       ├── CitationPdfTab.test.tsx      ← NEW
│       └── CitationOwnershipUpsell.test.tsx ← NEW
├── components/v2/game-chat/GameChatTabV2.tsx
│                                         ← MODIFY (passa gameId al CitationModal)
├── hooks/queries/
│   └── useCanViewPdf.ts                 ← NEW (ownership check: isPublic || getGameDocuments)
└── components/pdf/PdfViewerModal.tsx    ← REUSE (estrai parte renderer per embed inline)
```

**Cancellazioni** (post-pivot):
- ❌ Nuova route `/kb/[documentId]` — NON creata
- ❌ Cancellazione `/knowledge-base/[id]/page.tsx` — NON fatta (resta com'è, è ortogonale)
- ❌ 9 componenti KB-detail in `apps/web/src/components/v2/kb-detail/` — restano stub orphan (deferred plan futuro per "browse KB" feature)
- ❌ v2-migration-matrix riga `/kb/[id]` Tier M — riformulata come "deferred — out of scope game-night flow"

**Rendering tree del CitationModal v3**:

```
CitationModal (PR #918 base)
├── header: "📖 p.{pageNumber}"
├── tabs: [ Snippet (default) | PDF originale ]
├── tab=Snippet (current PR #918 body):
│   ├── blockquote snippet (or paraphrasedSnippet se copyrightTier='protected')
│   └── footer: [ Chiudi ]
├── tab=PDF (new, lazy on click):
│   ├── case useCanViewPdf:
│   │   - canView=true → <CitationPdfTab pdfUrl={...} initialPage={citation.pageNumber} />
│   │   - canView=false → <CitationOwnershipUpsell gameId={...} />
│   │   - loading → spinner
│   │   - error → error inline
│   └── footer: [ Chiudi ]
```

---

## 3. Component Contracts

### 3.1 `CitationModal` (modifiche)

```ts
interface CitationModalProps {
  readonly citation: Citation | null;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly gameId?: string;  // NEW: passato da GameChatTabV2 — necessario per upsell + ownership check
  // RIMOSSO: onOpenInKb (logica vive dentro il modal ora)
}
```

Stato interno:
```ts
const [activeTab, setActiveTab] = useState<'snippet' | 'pdf'>('snippet');
```

Cambia tab attivo via `setActiveTab`. Tab `pdf` triggera lazy mount di `<CitationPdfTab>` (no fetch finché non visibile).

### 3.2 `CitationPdfTab` (new, lazy mounted)

```ts
interface CitationPdfTabProps {
  readonly documentId: string;
  readonly gameId?: string;  // necessario per upsell upload link
  readonly initialPage: number;  // citation.pageNumber
  readonly isPublic?: boolean;   // citation.isPublic (shortcut)
}
```

Logica:
1. Se `isPublic === true` → render direttamente PDF viewer
2. Altrimenti → `useCanViewPdf({ documentId, gameId })` → 3 stati:
   - `loading` → spinner compatto
   - `canView: true` → render PDF viewer
   - `canView: false` → `<CitationOwnershipUpsell gameId={gameId} />`
3. PDF viewer: estrae il renderer da `PdfViewerModal.tsx` (Document + Page) embed inline (NO Dialog wrapper, NO download button)
4. Page navigation: bottoni `← Prev / Next →` per scorrere pagine, default page = `initialPage`

### 3.3 `CitationOwnershipUpsell` (new, pure)

```ts
interface CitationOwnershipUpsellProps {
  readonly gameId?: string;  // se undefined, CTA generica /upload
  readonly className?: string;
}
```

Renderizza card:
- Icona 🔒
- Titolo: "PDF originale protetto da copyright"
- Subtitle: "Per visualizzare il PDF, devi caricare la tua copia del manuale che possiedi."
- CTA: `<Button asChild><Link href={`/upload${gameId ? `?gameId=${gameId}` : ''}`}>Carica il mio PDF</Link></Button>`
- Footnote piccolo: "Il backend rifiuta upload di PDF già indicizzati con hash identico (anti-duplicazione)."

### 3.4 `useCanViewPdf` (new hook)

```ts
interface UseCanViewPdfArgs {
  readonly documentId: string;
  readonly gameId?: string;
  readonly enabled?: boolean;  // default true
}

interface UseCanViewPdfResult {
  readonly canView: boolean;
  readonly isLoading: boolean;
  readonly isError: boolean;
}
```

Logica:
1. Se `!enabled` → `{ canView: false, isLoading: false, isError: false }` (no fetch)
2. Se `gameId === undefined` → `{ canView: false, ... }` (impossibile decidere — fallback safe)
3. `useGameDocuments(gameId)` (esistente in `knowledgeBaseClient.getGameDocuments`)
4. `canView = data?.some(d => d.id === documentId) ?? false`
5. Polling/refetch: standard TanStack stale time 5min

> **Nota**: `Citation.isPublic` viene già controllato a monte in `CitationPdfTab` (shortcut). L'hook copre il caso non-public.

### 3.5 `GameChatTabV2` (modifica wiring)

```tsx
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  gameId={gameId}  // NEW
/>
```

Niente più `onOpenInKb` (rimosso dal contract).

---

## 4. Logic / Decisions

### 4.1 PDF viewer embed inline

Riusa `PdfViewerModal.tsx` MA estrae solo il body (Document + Page navigation), NO Dialog wrapper. Render compatto adatto al modal di citazione (height ~60vh, max-width ~700px).

Pseudo-implementazione (factoring):

```tsx
// Riusa il body esistente di PdfViewerModal.tsx, estrai in un sub-componente
// PdfRenderer (interno, non esportato) — copy-paste delle ~100 LOC core.
// CitationPdfTab importa solo PdfRenderer, NO PdfViewerModal Dialog.
```

> Alternativa: refactor `PdfViewerModal` per separare `<PdfRenderer>` interno riusabile + il Dialog wrapper. Decisione delegata al plan (preferisce refactor se safe; altrimenti copy-paste accettato).

### 4.2 Anti-leak

Misure attive:
- `onContextMenu={(e) => e.preventDefault()}` sul container PDF → blocca right-click "Salva immagine"
- NO bottone "Download" o "Apri in nuova tab" nel UI
- NO link diretto al `/api/v1/pdfs/{id}/download` esposto al click utente
- `react-pdf` renderizza tramite `<canvas>` → text selection limitata (default)

Misure NON in scope (impossibili lato frontend):
- Screenshot OS-level (sempre possibile)
- DevTools blob inspection (sempre possibile)
- Browser Print → PDF (mitigabile via `window.matchMedia('print')` CSS hide ma overkill)

> Disclaimer legale chiaro nell'upsell card: "L'app non permette download diretti del PDF di altri utenti."

### 4.3 Sblocco PR #918

Modifica `GameChatTabV2.tsx`:

```tsx
// PRIMA (PR #918)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  onOpenInKb={undefined /* TODO: enable when G4 lands */}
/>

// DOPO (G4 v3)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  gameId={gameId}
/>
```

Modifica `CitationModal.tsx`: rimuovi `onOpenInKb` prop, rimuovi footer button condizionale "Apri nella KB". Aggiungi tab system + tab body.

Test esistenti `CitationModal.test.tsx` da aggiornare:
- Test "hides 'Apri nella KB' footer when onOpenInKb is undefined" → DELETE (prop rimossa)
- Test "shows 'Apri nella KB' when onOpenInKb provided" → DELETE
- Aggiungere: test tab switching, test ownership upsell render, test PDF viewer mount lazy

### 4.4 Tab default + lazy mount

```tsx
const [activeTab, setActiveTab] = useState<'snippet' | 'pdf'>('snippet');
const [pdfTabEverOpened, setPdfTabEverOpened] = useState(false);

const handleTabChange = (tab: 'snippet' | 'pdf') => {
  setActiveTab(tab);
  if (tab === 'pdf') setPdfTabEverOpened(true);
};

return (
  <Dialog>
    <Tabs value={activeTab} onValueChange={handleTabChange}>
      <TabsList>
        <TabsTrigger value="snippet">Snippet</TabsTrigger>
        <TabsTrigger value="pdf">PDF originale</TabsTrigger>
      </TabsList>
      <TabsContent value="snippet">
        {/* PR #918 body invariato */}
      </TabsContent>
      <TabsContent value="pdf">
        {pdfTabEverOpened && (
          <CitationPdfTab
            documentId={citation.documentId}
            gameId={gameId}
            initialPage={citation.pageNumber}
            isPublic={citation.isPublic}
          />
        )}
      </TabsContent>
    </Tabs>
  </Dialog>
);
```

`pdfTabEverOpened` evita unmount/refetch quando l'utente switcha avanti e indietro tra i tab.

---

## 5. Cancellazioni e cleanup

### 5.1 v2-migration-matrix update

Modifica `docs/for-developers/frontend/v2-migration-matrix.md`, sezione `/kb/[id]`:
- Cambia status da `pending` a `deferred — out of scope game-night flow (vedi G4 v3)`
- Aggiungi nota inline sui 6 stub residui

### 5.2 Stub orphan in `apps/web/src/components/v2/kb-detail/`

I 6 componenti stub (KbHeader, KbChunkListPanel, KbChunkPreview, ChunkSearchBox, MarkdownRenderBlock, KbProcessingState) **restano in repo** ma:
- Aggiungere commento header in ognuno: `// DEFERRED: see docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §5.2 — questo componente fa parte di un progetto "/kb/[id]" deferred post-Alpha. Non importare. Restano stub return null.`
- v2-migration-matrix linka a questa spec

### 5.3 Issue #921

Update issue #921 con commento di riformulazione:
- Original scope: nuova pagina /kb/[documentId] con TOC + chunks navigabili
- New scope: estensione CitationModal con PDF viewer gated by ownership
- Motivazione legale: copyright per-documentId, anti-leak, upload-only

---

## 6. Testing strategy

### 6.1 Unit (Vitest)

| Component | Scenari |
|---|---|
| `CitationModal` | tab switching · default snippet · pdf tab lazy mount · gameId prop passing · `onOpenInKb` rimosso (prop check) |
| `CitationPdfTab` | isPublic=true → PDF viewer immediately · ownership=true → PDF viewer · ownership=false → upsell · loading state · error state |
| `CitationOwnershipUpsell` | render · CTA href con/senza gameId · disclaimer text presente |
| `useCanViewPdf` | enabled=false → no fetch · gameId=undefined → canView=false · documentId in list → canView=true · documentId NOT in list → canView=false · isError propagation |

### 6.2 Integration (Vitest)

| Scenario | Setup | Asserzione |
|---|---|---|
| Default snippet visible | citation passed, tab=snippet | snippet text rendered, PDF tab content NOT mounted |
| Switch to PDF, owner | mock getGameDocuments returns [{id: documentId}] | PDF viewer mounted con initialPage |
| Switch to PDF, public | citation.isPublic=true | PDF viewer mounted senza fetch ownership |
| Switch to PDF, non-owner | mock getGameDocuments returns [] | upsell card visible, CTA href contiene gameId |
| Switch back to snippet | tab change | snippet visible, PDF tab unmounted? NO — preserved (pdfTabEverOpened) |
| No anti-leak buttons | render full modal | no `<button>Download</button>`, no `<a href*="/download">` |

### 6.3 G1+G5 reciprocal sblocco

In `GameChatTabV2.test.tsx`:
- Test "citation chip click opens CitationModal" → updated: ora il modal ha 2 tab visibili (Snippet + PDF originale)
- Test "no 'Apri nella KB' footer" → DELETE (prop rimossa, comportamento diverso)
- Aggiungere: test "click PDF tab triggers lazy mount" + integrazione minima

### 6.4 Acceptance

- [ ] G4.2: tab "PDF originale" sempre visibile (anche non-owner)
- [ ] G4.3: upsell render <100ms (no fetch)
- [ ] G4.4: PDF carica + seek alla pageNumber funzionante (manual smoke)
- [ ] G4.5: visual audit conferma niente download button
- [ ] PR #918 sbloccato: footer button rimosso, no regressioni in chat-tab

---

## 7. Open questions residue

- **OQ-final-1**: il `Tabs` primitive è già nel design system v2? Probabilmente sì (`apps/web/src/components/ui/v2/`). Se NO, fallback a `<Tabs>` shadcn. Verifica nel plan Step 0.
- **OQ-final-2**: `react-pdf` su mobile + `pdfjs-dist` worker → bundle weight check. Se Tier L violato, lazy import con `dynamic()`. Decisione delegata al plan.
- **OQ-final-3**: il `PdfViewerModal` esistente fetch via blob — dimensione modal: se PDF > 20MB, lo carichiamo lo stesso o mettiamo un cap? Decisione delegata al plan (probabilmente accettiamo, ma con loading skeleton chiaro).

---

## 8. Riferimenti

- Spec parent: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G4
- Sblocca: [`2026-05-09-game-chat-tab-v1-g5-design.md`](./2026-05-09-game-chat-tab-v1-g5-design.md) §4.3
- Spec G4 v1 (cancellato): originariamente "/kb/[documentId]" con TOC + chunks markdown. Cancellato post pivot legale.
- `PdfViewerModal` esistente: `apps/web/src/components/pdf/PdfViewerModal.tsx`
- `pdfClient.getPdfDownloadUrl`: `apps/web/src/lib/api/clients/pdfClient.ts:64`
- `library.declareOwnership`: `apps/web/src/lib/api/clients/libraryClient.ts:917`
- `Citation` type: `apps/web/src/types/domain.ts:134-145` (incluso `isPublic`)
- v2-migration-matrix: route `/kb/[id]` → status `deferred` post questa spec
- Issue: [#921](https://github.com/meepleAi-app/meepleai-monorepo/issues/921) (riformulato)

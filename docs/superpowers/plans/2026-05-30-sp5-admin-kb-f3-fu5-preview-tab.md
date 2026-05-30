# SP5 Admin KB — F3-FU-5: Preview tab in KbDocDetailPanel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un quarto tab `?tab=preview` a `KbDocDetailPanel` che renderizza inline il PDF originale del documento KB, estraendo il pattern `PdfRenderer` di `CitationPdfTab` in un componente shared `PdfInlineViewer` props-driven.

**Architecture:** Refactor + nuovo componente in due passi indipendenti. (1) `PdfInlineViewer.tsx` net-new in `components/pdf/` con feature flags (antiLeak/download/openInTab/jumpToPage/zoom). (2) `CitationPdfTab` refactorato per consumarlo con `{antiLeak:true}` (output invariato → regression test passano). (3) `KbDocPreviewPanel.tsx` net-new in `explorer/preview/` consuma `PdfInlineViewer` con admin variant (toolbar full). `KbDocDetailTabs` + `KbDocDetailPanel` estesi per il nuovo tab; il viewer è disponibile sia su `ready` sia su `locked` (special-case come Used-by).

**Tech Stack:** Next.js 16 App Router · React 19 · TypeScript · Tailwind 4 · react-pdf (già nel bundle) · pdfjs-dist · Vitest + React Testing Library · Playwright.

**Spec:** `docs/superpowers/specs/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab-design.md`.

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `audits/2026-05-30-pdf-download-locked-spike.md` | NEW | T0 spike report (download endpoint vs doc states + workerSrc multi-setup) |
| `apps/web/src/components/pdf/PdfInlineViewer.tsx` | NEW | Shared component: fetch blob + Document+Page + toolbar feature-flagged + error banner |
| `apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx` | NEW | Unit: feature-flag matrix + lifecycle + errors |
| `apps/web/src/components/features/game-chat/CitationPdfTab.tsx` | MODIFY | Consumer di PdfInlineViewer con `{antiLeak:true}`. Strip PdfRenderer (righe ~94-234). |
| `apps/web/src/components/features/game-chat/__tests__/CitationPdfTab.test.tsx` | MUST pass invariato | Regression gate: output e behavior esterni identici |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx` | MODIFY | `KbDocTabKey += 'preview'`, entry in `TABS` |
| `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx` | MODIFY | Estendi matrice test con `activeTab='preview'` |
| `apps/web/src/components/admin/knowledge-base/explorer/preview/KbDocPreviewPanel.tsx` | NEW | Wrapper admin variant: consuma PdfInlineViewer con toolbar full |
| `apps/web/src/components/admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx` | NEW | Unit: docId valido → viewer con admin flags · docId null → no-crash |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` | MODIFY | `activeTab` discriminator + branch `preview` in ready & locked |
| `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx` | MODIFY | Estendi matrice test: `?tab=preview` su ready + locked |
| `apps/web/e2e/admin/kb-doc-preview-tab.spec.ts` | NEW | 2 smoke: viewer visibile + tab routing |

---

## Test mock strategy (riusa pattern CitationPdfTab.test.tsx)

Per i test di `PdfInlineViewer` che richiedono `numPages > 0` (prev/next/jump-to-page) il mock globale di `vitest.setup.tsx` non basta (non chiama `onLoadSuccess`). Override locale (pattern da `CitationPdfTab.test.tsx:11-22`):

```tsx
vi.mock('react-pdf', () => ({
  Document: ({ children, onLoadSuccess }: any) => {
    setTimeout(() => onLoadSuccess?.({ numPages: 24 }), 0);
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber, scale }: { pageNumber: number; scale?: number }) => (
    <div data-testid="pdf-page" data-page-number={pageNumber} data-scale={scale}>
      Page {pageNumber}
    </div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));
```

E mock `fetch` globale per il blob:

```tsx
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);
mockFetch.mockResolvedValue({
  ok: true,
  blob: () => Promise.resolve(new Blob([new Uint8Array([0x25, 0x50, 0x44, 0x46])], { type: 'application/pdf' })),
});
```

E mock `api.pdf.getPdfDownloadUrl`:

```tsx
vi.mock('@/lib/api', () => ({
  api: {
    pdf: { getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download` },
  },
}));
```

---

## Task 0: T0 — Spike doc-only

**Files:**
- Create: `audits/2026-05-30-pdf-download-locked-spike.md`

> **Tipo Task:** spike read-only. Doc-only. Outcome decisorio per T5 (locked branch) e T1 (workerSrc setup pattern).

- [ ] **Step 1: Verifica (a) — endpoint download vs stato doc**

  Strategia: read-only code review del handler backend dell'endpoint `/api/v1/pdfs/{id}/download`.

  ```bash
  cd D:/Repositories/meepleai-monorepo-dev
  rg -n -l 'GetPdfDownload\|pdfs/.*/download' apps/api/src/Api -t cs
  rg -n 'GetPdfDownload\|pdfs/.*download' apps/api/src/Api -t cs
  ```

  Expected: identifica il endpoint handler (probabilmente in `BoundedContexts/DocumentProcessing/...`). Apri il file e verifica se ci sono guard su `processingStatus`:
  - Se il handler ritorna 200 per qualsiasi PDF con file blob presente (a prescindere da indexing status) → outcome OK, Q2 decisione "tutti gli stati" valida, T5 implementa il branch locked.
  - Se il handler ritorna 4xx su stato non-`ready` → outcome BLOCCATO, T5 rimuove il branch locked (Preview viewer disponibile SOLO su ready, branch locked usa il banner standard come Overview/Ingestion); FR-2 rimosso dal test plan.

- [ ] **Step 2: Verifica (b) — workerSrc multi-setup side effects**

  Strategia: read della docs react-pdf + osservazione del codice esistente.

  In CitationPdfTab.tsx:35-39 il workerSrc è settato a module-level (immediato all'import del file). Se PdfInlineViewer fa lo stesso, due chiamate `pdfjs.GlobalWorkerOptions.workerSrc = ...` accadono al primo import di entrambi i moduli.

  Verifica con due approcci:
  1. Repository search: `rg -n 'GlobalWorkerOptions' apps/web/src` — vedi quante volte è settato oggi.
  2. Documentazione react-pdf: il setting è una proprietà mutabile su `pdfjs.GlobalWorkerOptions` — assegnare ripetutamente lo stesso valore è idempotent.

  Outcome:
  - Se idempotent (atteso) → `PdfInlineViewer.tsx` può fare il setup module-level, `CitationPdfTab` lo rimuove (T2). Ordine import non rilevante.
  - Se NON idempotent → estrai il setup in un singleton module `apps/web/src/components/pdf/pdfjs-worker.ts` (eseguito una sola volta), `PdfInlineViewer` e `CitationPdfTab` lo importano.

- [ ] **Step 3: Scrivi il report**

  ```markdown
  # T0 Spike — PDF download endpoint vs doc states + pdfjs workerSrc multi-setup

  **Date:** 2026-05-30
  **Status:** decision
  **Scope:** outcome decisorio per T5 (locked branch) e T1 (workerSrc setup pattern)

  ## (a) Endpoint /api/v1/pdfs/{id}/download vs processingStatus

  **Handler location:** [file:line]

  **Behavior on processingStatus:**
  - `ready` → 200 con file blob
  - `processing`/`queued` → [200 / 4xx]
  - `failed` → [200 / 4xx]

  **Decision:** [OK — T5 implementa locked branch | BLOCKED — T5 rimuove locked branch, FR-2 rimosso]

  ## (b) pdfjs.GlobalWorkerOptions.workerSrc multi-setup

  **Current usage:** [N posti in codebase]

  **Behavior:** [idempotent / non-idempotent]

  **Decision:** [module-level setup in PdfInlineViewer | singleton module pdfjs-worker.ts]
  ```

- [ ] **Step 4: Commit spike report**

  ```bash
  git add audits/2026-05-30-pdf-download-locked-spike.md
  git commit -m "spike(admin-kb): #1654 T0 — endpoint download vs doc states + workerSrc multi-setup"
  ```

  > Se outcome (a) = BLOCKED → aggiorna la spec design § 7 stati + § 8 FR-2 + § 11 T5 con la nuova baseline prima di proseguire. Commit separato sul design doc se necessario.

---

## Task 1: `PdfInlineViewer` shared component (TDD)

**Files:**
- Create: `apps/web/src/components/pdf/PdfInlineViewer.tsx`
- Create: `apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx`

> Se T0 (b) ha indicato singleton module, crea anche `apps/web/src/components/pdf/pdfjs-worker.ts` prima di Step 4.

- [ ] **Step 1: Crea il test file con setup mocks**

  ```tsx
  // apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx
  import { render, screen, waitFor, fireEvent } from '@testing-library/react';
  import { describe, it, expect, vi, beforeEach } from 'vitest';

  vi.mock('react-pdf', () => ({
    Document: ({ children, onLoadSuccess }: any) => {
      setTimeout(() => onLoadSuccess?.({ numPages: 5 }), 0);
      return <div data-testid="pdf-document">{children}</div>;
    },
    Page: ({ pageNumber, scale }: { pageNumber: number; scale?: number }) => (
      <div data-testid="pdf-page" data-page-number={pageNumber} data-scale={scale ?? 1}>
        Page {pageNumber}
      </div>
    ),
    pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
  }));

  vi.mock('@/lib/api', () => ({
    api: {
      pdf: { getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download` },
    },
  }));

  const mockFetch = vi.fn();
  vi.stubGlobal('fetch', mockFetch);

  import { PdfInlineViewer } from '../PdfInlineViewer';

  function mockBlobSuccess() {
    mockFetch.mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob([new Uint8Array([0x25, 0x50, 0x44, 0x46])], { type: 'application/pdf' })),
    });
  }

  describe('PdfInlineViewer', () => {
    beforeEach(() => {
      mockFetch.mockReset();
      mockBlobSuccess();
    });

    it('shows loading skeleton while fetching blob', () => {
      mockFetch.mockReturnValue(new Promise(() => {})); // never resolves
      render(<PdfInlineViewer documentId="doc-1" />);
      expect(screen.getByRole('status', { name: /caricamento/i })).toBeInTheDocument();
    });

    it('renders Page at initialPage after fetch success', async () => {
      render(<PdfInlineViewer documentId="doc-1" initialPage={3} />);
      await waitFor(() => {
        expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '3');
      });
    });

    it('shows error banner on fetch HTTP 500', async () => {
      mockFetch.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
      render(<PdfInlineViewer documentId="doc-1" />);
      await waitFor(() => {
        expect(screen.getByText(/HTTP 500/i)).toBeInTheDocument();
      });
    });

    it('ignores AbortError silently (no banner)', async () => {
      mockFetch.mockImplementation((_, opts) => {
        return new Promise((_, reject) => {
          opts.signal?.addEventListener('abort', () => {
            const err = new Error('aborted');
            err.name = 'AbortError';
            reject(err);
          });
        });
      });
      const { unmount } = render(<PdfInlineViewer documentId="doc-1" />);
      unmount();
      // no error banner expected; the test passes if no exception thrown
    });

    it('prev/next clamps to [1, numPages]', async () => {
      render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '1'));
      // Prev at page 1 is disabled
      expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled();
      // Next advances
      fireEvent.click(screen.getByRole('button', { name: /next/i }));
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '2'));
    });

    it('antiLeak=true prevents contextmenu + applies select-none', async () => {
      render(<PdfInlineViewer documentId="doc-1" features={{ antiLeak: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      const canvas = screen.getByTestId('pdf-canvas-container');
      expect(canvas.className).toContain('select-none');
      const ev = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
      const prevented = !canvas.dispatchEvent(ev);
      expect(prevented).toBe(true);
    });

    it('antiLeak=false (default) allows contextmenu, no select-none', async () => {
      render(<PdfInlineViewer documentId="doc-1" />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      const canvas = screen.getByTestId('pdf-canvas-container');
      expect(canvas.className).not.toContain('select-none');
    });

    it('features.download=true renders <a download> with download URL', async () => {
      render(<PdfInlineViewer documentId="doc-1" features={{ download: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      const a = screen.getByRole('link', { name: /download/i });
      expect(a).toHaveAttribute('href', 'http://test/api/v1/pdfs/doc-1/download');
      expect(a).toHaveAttribute('download');
    });

    it('features.openInTab=true renders <a target="_blank" rel="noopener noreferrer">', async () => {
      render(<PdfInlineViewer documentId="doc-1" features={{ openInTab: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      const a = screen.getByRole('link', { name: /apri in tab/i });
      expect(a).toHaveAttribute('target', '_blank');
      expect(a).toHaveAttribute('rel', 'noopener noreferrer');
    });

    it('features.jumpToPage=true clamps input to [1, numPages]', async () => {
      render(<PdfInlineViewer documentId="doc-1" features={{ jumpToPage: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      const input = screen.getByRole('spinbutton', { name: /vai a pagina/i });
      fireEvent.change(input, { target: { value: '99' } });
      fireEvent.submit(input.closest('form')!);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '5'));
    });

    it('features.jumpToPage=true ignores NaN input silently', async () => {
      render(<PdfInlineViewer documentId="doc-1" features={{ jumpToPage: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '1'));
      const input = screen.getByRole('spinbutton', { name: /vai a pagina/i });
      fireEvent.change(input, { target: { value: 'abc' } });
      fireEvent.submit(input.closest('form')!);
      // page should stay at 1 (NaN ignored)
      await new Promise(r => setTimeout(r, 10));
      expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '1');
    });

    it('features.zoom=true switches preset → applies scale to Page', async () => {
      render(<PdfInlineViewer documentId="doc-1" defaultZoom={100} features={{ zoom: true }} />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-scale', '1'));
      const zoomSelect = screen.getByRole('combobox', { name: /zoom/i });
      fireEvent.change(zoomSelect, { target: { value: '150' } });
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-scale', '1.5'));
    });

    it('features.zoom=false (default) does not render zoom controls', async () => {
      render(<PdfInlineViewer documentId="doc-1" />);
      await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
      expect(screen.queryByRole('combobox', { name: /zoom/i })).not.toBeInTheDocument();
    });
  });
  ```

- [ ] **Step 2: Run test, confirm FAIL**

  ```bash
  cd apps/web
  pnpm test pdf/__tests__/PdfInlineViewer.test.tsx 2>&1 | tail -20
  ```

  Expected: FAIL "Cannot find module '../PdfInlineViewer'".

- [ ] **Step 3: Implementa `PdfInlineViewer`**

  ```tsx
  // apps/web/src/components/pdf/PdfInlineViewer.tsx
  'use client';

  import { useState, useEffect, useCallback, useRef, type ReactElement, type FormEvent } from 'react';
  import { Document, Page, pdfjs } from 'react-pdf';
  import 'react-pdf/dist/Page/AnnotationLayer.css';
  import 'react-pdf/dist/Page/TextLayer.css';
  import clsx from 'clsx';

  import { api } from '@/lib/api';

  // Worker setup (idempotent per T0 spike outcome).
  // If T0 outcome = singleton module, replace this with: `import '@/components/pdf/pdfjs-worker'`.
  pdfjs.GlobalWorkerOptions.workerSrc = new URL(
    'pdfjs-dist/build/pdf.worker.min.mjs',
    import.meta.url
  ).toString();

  export interface PdfInlineViewerFeatures {
    readonly antiLeak?: boolean;
    readonly download?: boolean;
    readonly openInTab?: boolean;
    readonly jumpToPage?: boolean;
    readonly zoom?: boolean;
  }

  export interface PdfInlineViewerProps {
    readonly documentId: string;
    readonly initialPage?: number;
    readonly defaultZoom?: 'fit-width' | number;
    readonly features?: PdfInlineViewerFeatures;
    readonly className?: string;
  }

  type ZoomState = 'fit-width' | number;

  export function PdfInlineViewer({
    documentId,
    initialPage = 1,
    defaultZoom = 'fit-width',
    features = {},
    className,
  }: PdfInlineViewerProps): ReactElement {
    const [numPages, setNumPages] = useState<number>(0);
    const [currentPage, setCurrentPage] = useState<number>(initialPage);
    const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [loading, setLoading] = useState<boolean>(true);
    const [zoom, setZoom] = useState<ZoomState>(defaultZoom);
    const [containerWidth, setContainerWidth] = useState<number>(800);
    const [jumpInput, setJumpInput] = useState<string>(String(initialPage));
    const containerRef = useRef<HTMLDivElement>(null);

    const downloadUrl = api.pdf.getPdfDownloadUrl(documentId);

    const onDocumentLoadSuccess = useCallback(({ numPages: n }: { numPages: number }) => {
      setNumPages(n);
    }, []);

    const onDocumentLoadError = useCallback((err: Error) => {
      setLoadError(`PDF non leggibile: ${err.message}`);
    }, []);

    // Fetch blob with credentials
    useEffect(() => {
      const controller = new AbortController();
      setLoading(true);
      setLoadError(null);
      setNumPages(0);
      setCurrentPage(initialPage);
      setPdfBlob(null);
      setJumpInput(String(initialPage));

      fetch(downloadUrl, { credentials: 'include', signal: controller.signal })
        .then(res => {
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
          return res.blob();
        })
        .then(blob => {
          setPdfBlob(blob);
          setLoading(false);
        })
        .catch((err: Error) => {
          if (err.name !== 'AbortError') {
            setLoadError(err.message);
            setLoading(false);
          }
        });

      return () => controller.abort();
    }, [downloadUrl, initialPage]);

    // ResizeObserver for fit-width
    useEffect(() => {
      if (!containerRef.current) return;
      const el = containerRef.current;
      const ro = new ResizeObserver(entries => {
        for (const entry of entries) {
          setContainerWidth(entry.contentRect.width);
        }
      });
      ro.observe(el);
      return () => ro.disconnect();
    }, []);

    const scale =
      zoom === 'fit-width' ? Math.max(0.5, containerWidth / 595) : zoom / 100;

    const goPrev = () => setCurrentPage(p => Math.max(1, p - 1));
    const goNext = () => setCurrentPage(p => Math.min(numPages, p + 1));

    const handleJumpSubmit = (e: FormEvent) => {
      e.preventDefault();
      const parsed = parseInt(jumpInput, 10);
      if (Number.isNaN(parsed)) return;
      const clamped = Math.max(1, Math.min(numPages || 1, parsed));
      setCurrentPage(clamped);
      setJumpInput(String(clamped));
    };

    const handleZoomChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
      const v = e.target.value;
      setZoom(v === 'fit-width' ? 'fit-width' : parseInt(v, 10));
    };

    return (
      <div
        ref={containerRef}
        data-slot="pdf-inline-viewer"
        className={clsx('flex flex-col gap-3', className)}
      >
        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-2 rounded-md bg-muted px-3 py-2 font-mono text-xs">
          <button
            type="button"
            onClick={goPrev}
            disabled={currentPage <= 1 || loading}
            className="rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            ← Prev
          </button>
          <span className="px-2">
            Pagina <strong>{currentPage}</strong> / {numPages || '?'}
          </span>
          <button
            type="button"
            onClick={goNext}
            disabled={currentPage >= numPages || loading}
            className="rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            Next →
          </button>

          {features.jumpToPage && (
            <form onSubmit={handleJumpSubmit} className="flex items-center gap-1">
              <label htmlFor="pdf-jump" className="text-muted-foreground">Vai a pagina</label>
              <input
                id="pdf-jump"
                type="number"
                role="spinbutton"
                aria-label="Vai a pagina"
                min={1}
                max={numPages || undefined}
                value={jumpInput}
                onChange={e => setJumpInput(e.target.value)}
                className="w-14 rounded-sm border border-border bg-card px-1 py-0.5 text-center"
              />
            </form>
          )}

          {features.zoom && (
            <label className="flex items-center gap-1">
              <span className="text-muted-foreground">Zoom</span>
              <select
                aria-label="Zoom"
                value={zoom === 'fit-width' ? 'fit-width' : String(zoom)}
                onChange={handleZoomChange}
                className="rounded-sm border border-border bg-card px-1 py-0.5"
              >
                <option value="50">50%</option>
                <option value="100">100%</option>
                <option value="150">150%</option>
                <option value="fit-width">fit-width</option>
              </select>
            </label>
          )}

          <div className="ml-auto flex items-center gap-2">
            {features.openInTab && (
              <a
                href={downloadUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
                ↗ Apri in tab
              </a>
            )}
            {features.download && (
              <a
                href={downloadUrl}
                download
                className="inline-flex items-center rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
                ⤓ Download
              </a>
            )}
            {features.antiLeak && (
              <span className="inline-flex items-center gap-1 text-[10px] text-[hsl(var(--c-success))]">
                🔒 Solo visualizzazione
              </span>
            )}
          </div>
        </div>

        {/* Canvas */}
        <div
          data-testid="pdf-canvas-container"
          onContextMenu={features.antiLeak ? e => e.preventDefault() : undefined}
          className={clsx(
            'relative overflow-hidden rounded-md border border-border bg-card',
            features.antiLeak && 'select-none',
            '[&_canvas]:max-w-full min-h-[400px]'
          )}
          style={{ aspectRatio: '210/297' }}
        >
          {loading && (
            <div
              role="status"
              aria-label="Caricamento PDF"
              className="absolute inset-0 flex items-center justify-center"
            >
              <div
                aria-hidden="true"
                className="h-10 w-10 rounded-full border-[3px] border-[hsl(var(--c-kb)/0.2)] border-t-[hsl(var(--c-kb))] motion-safe:animate-spin"
              />
            </div>
          )}
          {loadError && (
            <div className="flex h-full items-center justify-center p-6 text-sm text-destructive">
              Errore caricamento PDF: {loadError}
            </div>
          )}
          {pdfBlob && !loadError && (
            <Document
              file={pdfBlob}
              onLoadSuccess={onDocumentLoadSuccess}
              onLoadError={onDocumentLoadError}
              loading={null}
              error={null}
            >
              <Page
                pageNumber={currentPage}
                scale={scale}
                renderAnnotationLayer={false}
                renderTextLayer={false}
              />
            </Document>
          )}
        </div>
      </div>
    );
  }
  ```

- [ ] **Step 4: Run test, confirm PASS**

  ```bash
  cd apps/web
  pnpm test pdf/__tests__/PdfInlineViewer.test.tsx 2>&1 | tail -20
  ```

  Expected: 13 PASS.

- [ ] **Step 5: Run typecheck + lint**

  ```bash
  cd apps/web
  pnpm typecheck 2>&1 | tail -10
  pnpm lint pdf/PdfInlineViewer.tsx 2>&1 | tail -10
  pnpm lint:tokens 2>&1 | tail -10
  ```

  Expected: 0 errori.

- [ ] **Step 6: Commit**

  ```bash
  git add apps/web/src/components/pdf/PdfInlineViewer.tsx \
          apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx
  git commit -m "feat(pdf): #1654 PdfInlineViewer shared component with feature-flagged toolbar"
  ```

---

## Task 2: Refactor `CitationPdfTab` to consume `PdfInlineViewer`

**Files:**
- Modify: `apps/web/src/components/features/game-chat/CitationPdfTab.tsx`
- Verify pass: `apps/web/src/components/features/game-chat/__tests__/CitationPdfTab.test.tsx`

> Regression gate: il test esistente di CitationPdfTab DEVE passare invariato (zero modifiche al test file in questo task).

- [ ] **Step 1: Run il test esistente come baseline**

  ```bash
  cd apps/web
  pnpm test features/game-chat/__tests__/CitationPdfTab.test.tsx 2>&1 | tail -10
  ```

  Expected: tutti PASS (pre-refactor baseline).

- [ ] **Step 2: Refactor `CitationPdfTab.tsx`**

  Strip il `PdfRenderer` interno (righe ~94-234) e il setup `pdfjs.GlobalWorkerOptions.workerSrc` (righe 35-39). Il file diventa:

  ```tsx
  // apps/web/src/components/features/game-chat/CitationPdfTab.tsx
  /**
   * CitationPdfTab — body del tab "PDF originale" del CitationModal v3.
   *
   * Logica:
   *   - isPublic=true → render PDF viewer immediato
   *   - altrimenti useCanViewPdf:
   *       loading → spinner
   *       canView=true → PDF viewer (PdfInlineViewer con antiLeak=true)
   *       canView=false OR isError → CitationOwnershipUpsell
   *
   * Anti-leak gestito da PdfInlineViewer via features.antiLeak=true.
   *
   * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.2 §4.2
   * Shared viewer: docs/superpowers/specs/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab-design.md §4
   */
  'use client';

  import type { ReactElement } from 'react';
  import clsx from 'clsx';

  import { CitationOwnershipUpsell } from '@/components/features/game-chat/CitationOwnershipUpsell';
  import { PdfInlineViewer } from '@/components/pdf/PdfInlineViewer';
  import { useCanViewPdf } from '@/hooks/queries/useCanViewPdf';

  export interface CitationPdfTabProps {
    readonly documentId: string;
    readonly gameId?: string;
    readonly initialPage: number;
    readonly isPublic?: boolean;
    readonly className?: string;
  }

  export function CitationPdfTab({
    documentId,
    gameId,
    initialPage,
    isPublic = false,
    className,
  }: CitationPdfTabProps): ReactElement {
    const ownership = useCanViewPdf({
      documentId,
      gameId,
      enabled: !isPublic,
    });

    const canRenderPdf = isPublic || ownership.canView;
    const isCheckingOwnership = !isPublic && ownership.isLoading;
    const showUpsell = !canRenderPdf && !isCheckingOwnership;

    if (isCheckingOwnership) {
      return (
        <div
          data-slot="citation-pdf-loading-ownership"
          role="status"
          aria-label="Caricamento"
          className={clsx('flex flex-col items-center justify-center gap-3 p-9', className)}
        >
          <div
            aria-hidden="true"
            className={clsx(
              'h-10 w-10 rounded-full border-[3px] border-[hsl(var(--c-kb)/0.2)]',
              'border-t-[hsl(var(--c-kb))] motion-safe:animate-spin'
            )}
          />
          <div className="font-mono text-xs text-muted-foreground">Verifica accesso al PDF…</div>
        </div>
      );
    }

    if (showUpsell) {
      return <CitationOwnershipUpsell gameId={gameId} className={className} />;
    }

    return (
      <PdfInlineViewer
        documentId={documentId}
        initialPage={initialPage}
        features={{ antiLeak: true }}
        className={className}
      />
    );
  }
  ```

- [ ] **Step 3: Run il test esistente di CitationPdfTab — DEVE passare invariato**

  ```bash
  cd apps/web
  pnpm test features/game-chat/__tests__/CitationPdfTab.test.tsx 2>&1 | tail -10
  ```

  Expected: identico baseline pre-refactor (tutti PASS).

  > Se un test fallisce: STOP. Analizza la differenza (selector, behavior atteso, role/label). NON modificare il test. Aggiusta `PdfInlineViewer` se necessario (es. attributi a11y mancanti, data-testid divergente). Lo scope di questo task è "zero impact esterno".

- [ ] **Step 4: Run il test di PdfInlineViewer (no regression)**

  ```bash
  pnpm test pdf/__tests__/PdfInlineViewer.test.tsx 2>&1 | tail -10
  ```

  Expected: 13/13 PASS.

- [ ] **Step 5: Run typecheck**

  ```bash
  pnpm typecheck 2>&1 | tail -5
  ```

- [ ] **Step 6: Commit**

  ```bash
  git add apps/web/src/components/features/game-chat/CitationPdfTab.tsx
  git commit -m "refactor(citation): #1654 consume PdfInlineViewer (antiLeak variant)"
  ```

---

## Task 3: Aggiungi `'preview'` a `KbDocTabKey` + `TABS`

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`

- [ ] **Step 1: Estendi il test esistente**

  Apri `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx`. Aggiungi (al describe esistente):

  ```tsx
  it('renders Preview tab and marks aria-current when active', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="preview" />);
    const previewLink = screen.getByRole('link', { name: /preview/i });
    expect(previewLink).toHaveAttribute('aria-current', 'page');
    expect(previewLink).toHaveAttribute('href', '/admin/knowledge-base?doc=doc-1&tab=preview');
  });

  it('Overview link does NOT have tab param when activeTab=preview', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="preview" />);
    const overviewLink = screen.getByRole('link', { name: /overview/i });
    expect(overviewLink).toHaveAttribute('href', '/admin/knowledge-base?doc=doc-1');
  });
  ```

- [ ] **Step 2: Run test, confirm FAIL**

  ```bash
  cd apps/web
  pnpm test admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx 2>&1 | tail -10
  ```

  Expected: 2 FAIL "no element with name /preview/i".

- [ ] **Step 3: Estendi `KbDocTabKey` + `TABS`**

  Apri `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`. Modifica:

  ```tsx
  // riga 5
  export type KbDocTabKey = 'overview' | 'ingestion' | 'used-by' | 'preview';

  // riga 12-16 → aggiungi entry 'preview'
  const TABS: ReadonlyArray<{ readonly key: KbDocTabKey; readonly label: string }> = [
    { key: 'overview', label: 'Overview' },
    { key: 'ingestion', label: 'Ingestion log' },
    { key: 'used-by', label: 'Used by' },
    { key: 'preview', label: 'Preview' },
  ];
  ```

  Aggiorna anche il jsdoc:

  ```tsx
  /**
   * Inner tab nav for `KbDocDetailPanel`. URL-driven via `?tab=overview` (default)
   * | `?tab=ingestion` | `?tab=used-by` | `?tab=preview`. Preserves `docId` in each link.
   * Issues #1650 (Ingestion log) + #1651 (Used by) + #1654 (Preview).
   */
  ```

- [ ] **Step 4: Run test, confirm PASS**

  ```bash
  pnpm test admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx 2>&1 | tail -10
  ```

  Expected: tutti PASS (2 nuovi + esistenti).

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx \
          apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailTabs.test.tsx
  git commit -m "feat(admin-kb): #1654 add 'preview' to KbDocTabKey + TABS"
  ```

---

## Task 4: `KbDocPreviewPanel` admin variant

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/preview/KbDocPreviewPanel.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx`

- [ ] **Step 1: Crea il test file**

  ```tsx
  // apps/web/src/components/admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx
  import { render, screen } from '@testing-library/react';
  import { describe, it, expect, vi } from 'vitest';

  const mockPdfInlineViewer = vi.fn();
  vi.mock('@/components/pdf/PdfInlineViewer', () => ({
    PdfInlineViewer: (props: Record<string, unknown>) => {
      mockPdfInlineViewer(props);
      return <div data-testid="pdf-inline-viewer-mock" />;
    },
  }));

  import { KbDocPreviewPanel } from '../KbDocPreviewPanel';

  describe('KbDocPreviewPanel', () => {
    beforeEach(() => {
      mockPdfInlineViewer.mockReset();
    });

    it('renders PdfInlineViewer with documentId from docId prop', () => {
      render(<KbDocPreviewPanel docId="doc-abc" />);
      expect(screen.getByTestId('pdf-inline-viewer-mock')).toBeInTheDocument();
      expect(mockPdfInlineViewer).toHaveBeenCalledWith(
        expect.objectContaining({ documentId: 'doc-abc' })
      );
    });

    it('passes admin features { download, openInTab, jumpToPage, zoom } = true, antiLeak omitted', () => {
      render(<KbDocPreviewPanel docId="doc-abc" />);
      const props = mockPdfInlineViewer.mock.calls[0][0] as { features: Record<string, boolean> };
      expect(props.features.download).toBe(true);
      expect(props.features.openInTab).toBe(true);
      expect(props.features.jumpToPage).toBe(true);
      expect(props.features.zoom).toBe(true);
      expect(props.features.antiLeak ?? false).toBe(false);
    });

    it('renders nothing when docId is empty string', () => {
      const { container } = render(<KbDocPreviewPanel docId="" />);
      expect(container.firstChild).toBeNull();
    });
  });
  ```

  Importa `beforeEach` se non già nel scope:

  ```tsx
  import { describe, it, expect, vi, beforeEach } from 'vitest';
  ```

- [ ] **Step 2: Run test, confirm FAIL**

  ```bash
  cd apps/web
  pnpm test admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx 2>&1 | tail -10
  ```

  Expected: FAIL "Cannot find module '../KbDocPreviewPanel'".

- [ ] **Step 3: Implementa `KbDocPreviewPanel`**

  ```tsx
  // apps/web/src/components/admin/knowledge-base/explorer/preview/KbDocPreviewPanel.tsx
  'use client';

  import { PdfInlineViewer } from '@/components/pdf/PdfInlineViewer';

  export interface KbDocPreviewPanelProps {
    /** Document UUID. Empty string → render null (panel-level guard). */
    readonly docId: string;
  }

  /**
   * KbDocPreviewPanel — admin variant of PdfInlineViewer.
   * Renders the original PDF blob with the full admin toolbar
   * (jump-to-page, zoom, open-in-tab, download). No anti-leak (admin context).
   *
   * Mounted by KbDocDetailPanel when activeTab === 'preview'.
   *
   * Issue #1654 F3-FU-5.
   */
  export function KbDocPreviewPanel({ docId }: KbDocPreviewPanelProps) {
    if (!docId) return null;
    return (
      <div className="p-4">
        <PdfInlineViewer
          documentId={docId}
          features={{
            download: true,
            openInTab: true,
            jumpToPage: true,
            zoom: true,
          }}
        />
      </div>
    );
  }
  ```

- [ ] **Step 4: Run test, confirm PASS**

  ```bash
  pnpm test admin/knowledge-base/explorer/preview/__tests__/KbDocPreviewPanel.test.tsx 2>&1 | tail -10
  ```

  Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/components/admin/knowledge-base/explorer/preview/
  git commit -m "feat(admin-kb): #1654 KbDocPreviewPanel admin variant of PdfInlineViewer"
  ```

---

## Task 5: Branching in `KbDocDetailPanel` (ready + locked)

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`

> Se T0 (a) outcome = BLOCKED, skip Step 2 (locked branch test + impl) e relativo commit; FR-2 rimosso dal contratto.

- [ ] **Step 1: Estendi il test esistente — `?tab=preview` su `ready`**

  Apri `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx`. Aggiungi mock del nuovo panel (subito dopo i mock esistenti di `KbDocActions`/`KbChunkSearch`):

  ```tsx
  // ── KbDocPreviewPanel mock (panel test doesn't exercise viewer internals) ─────
  vi.mock('../preview/KbDocPreviewPanel', () => ({
    KbDocPreviewPanel: ({ docId }: { docId: string }) => (
      <div data-testid="kb-doc-preview-panel-mock" data-doc-id={docId} />
    ),
  }));
  ```

  Aggiungi i test (nel describe esistente):

  ```tsx
  it('renders KbDocPreviewPanel on ?tab=preview when doc is ready', () => {
    mockSearchParams = new URLSearchParams('doc=doc-1&tab=preview');
    mockUseKbDocDetail.mockReturnValue({ data: readyEnvelope, isLoading: false });
    mockUseKbChunksList.mockReturnValue({ data: { pages: [] }, hasNextPage: false });

    render(<KbDocDetailPanel docId="doc-1" />, { wrapper: makeWrapper() });

    expect(screen.getByTestId('kb-doc-preview-panel-mock')).toHaveAttribute('data-doc-id', 'doc-1');
    // chunks list NOT rendered when preview tab active
    expect(screen.queryByText(/^Chunks$/)).not.toBeInTheDocument();
  });
  ```

- [ ] **Step 2: Estendi il test — `?tab=preview` su `locked` (SOLO se T0 (a) = OK)**

  ```tsx
  it('renders KbDocPreviewPanel on ?tab=preview when doc is locked (special-case)', () => {
    mockSearchParams = new URLSearchParams('doc=doc-1&tab=preview');
    mockUseKbDocDetail.mockReturnValue({ data: lockedEnvelope, isLoading: false });

    render(<KbDocDetailPanel docId="doc-1" selectedDocMeta={{ id: 'doc-1', title: 'X.pdf', gameId: null }} />, { wrapper: makeWrapper() });

    expect(screen.getByTestId('kb-doc-preview-panel-mock')).toHaveAttribute('data-doc-id', 'doc-1');
    // locked banner ("Documento in elaborazione") NOT rendered when preview tab active
    expect(screen.queryByText(/Documento in elaborazione/i)).not.toBeInTheDocument();
  });
  ```

- [ ] **Step 3: Run test, confirm FAIL**

  ```bash
  cd apps/web
  pnpm test admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx 2>&1 | tail -10
  ```

  Expected: 1 o 2 FAIL (a seconda di T0).

- [ ] **Step 4: Modifica `KbDocDetailPanel.tsx`**

  Aggiorna `activeTab` discriminator (righe ~58-63):

  ```tsx
  const activeTab: KbDocTabKey = (() => {
    const tab = searchParams?.get('tab');
    if (tab === 'ingestion') return 'ingestion';
    if (tab === 'used-by') return 'used-by';
    if (tab === 'preview') return 'preview';
    return 'overview';
  })();
  ```

  Aggiungi import:

  ```tsx
  import { KbDocPreviewPanel } from './preview/KbDocPreviewPanel';
  ```

  **Locked branch — solo se T0 (a) = OK**. Aggiungi special-case PRIMA del fallback (analogo a `used-by` righe ~110-117):

  ```tsx
  if (activeTab === 'preview') {
    return (
      <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden">
        <KbDocDetailTabs docId={docId} activeTab={activeTab} />
        <KbDocPreviewPanel docId={docId} />
      </div>
    );
  }
  ```

  **Ready branch** — aggiungi nel render (dopo `{activeTab === 'used-by' && <UsedByPanel docId={doc.id} />}`):

  ```tsx
  {activeTab === 'preview' && <KbDocPreviewPanel docId={doc.id} />}
  ```

- [ ] **Step 5: Run test, confirm PASS**

  ```bash
  pnpm test admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx 2>&1 | tail -10
  ```

  Expected: tutti PASS.

- [ ] **Step 6: Run suite admin completa per regression**

  ```bash
  pnpm test:admin-dashboard 2>&1 | tail -10
  ```

  Expected: 558+/558+ PASS (numero esatto può crescere coi nuovi test, ma 0 fail).

- [ ] **Step 7: Run typecheck + lint**

  ```bash
  pnpm typecheck 2>&1 | tail -5
  pnpm lint admin/knowledge-base/explorer/ 2>&1 | tail -5
  pnpm lint:tokens 2>&1 | tail -5
  ```

- [ ] **Step 8: Commit**

  ```bash
  git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx \
          apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbDocDetailPanel.test.tsx
  git commit -m "feat(admin-kb): #1654 wire Preview tab in KbDocDetailPanel (ready + locked)"
  ```

---

## Task 6: E2E smoke + verification gate

**Files:**
- Create: `apps/web/e2e/admin/kb-doc-preview-tab.spec.ts`

- [ ] **Step 1: Scrivi gli scenari smoke**

  ```ts
  // apps/web/e2e/admin/kb-doc-preview-tab.spec.ts
  import { test, expect } from '@playwright/test';
  import { loginAsAdmin } from '../helpers/auth';
  import { seedKbDocFixture } from '../helpers/kb-fixtures';

  test.describe('KB doc Preview tab (#1654)', () => {
    test('admin opens ?tab=preview and sees the PDF viewer toolbar', async ({ page }) => {
      const { docId } = await seedKbDocFixture({ pageCount: 3 });
      await loginAsAdmin(page);
      await page.goto(`/admin/knowledge-base?doc=${docId}&tab=preview`);
      // Toolbar visible
      await expect(page.getByRole('button', { name: /prev/i })).toBeVisible();
      await expect(page.getByRole('button', { name: /next/i })).toBeVisible();
      // Page counter "1 / 3"
      await expect(page.getByText(/Pagina\s+1\s*\/\s*3/i)).toBeVisible({ timeout: 15_000 });
    });

    test('tab routing: Preview ↔ Overview updates URL param', async ({ page }) => {
      const { docId } = await seedKbDocFixture({ pageCount: 3 });
      await loginAsAdmin(page);
      await page.goto(`/admin/knowledge-base?doc=${docId}`); // overview default
      await page.getByRole('link', { name: /preview/i }).click();
      await expect(page).toHaveURL(new RegExp(`doc=${docId}&tab=preview`));
      await page.getByRole('link', { name: /overview/i }).click();
      await expect(page).toHaveURL(new RegExp(`doc=${docId}(?!&tab=)`));
    });
  });
  ```

  > Verifica preliminare: `apps/web/e2e/helpers/auth.ts` e `apps/web/e2e/helpers/kb-fixtures.ts` esistono? Run:
  > ```bash
  > find apps/web/e2e/helpers -name "*.ts" 2>&1
  > ```
  > Se mancano, riusa il pattern degli E2E esistenti per F3-FU-1/2/4: `apps/web/e2e/admin/kb-doc-actions.spec.ts` (output del commit #1653 `23b17f6a5`).

- [ ] **Step 2: Run E2E smoke (opzionale local)**

  ```bash
  cd apps/web
  pnpm test:e2e --grep "Preview tab"
  ```

  Expected: 2 PASS. (CI runner esegue automaticamente in PR.)

- [ ] **Step 3: Gate finale — run ALL verification commands**

  ```bash
  cd apps/web
  pnpm test pdf/__tests__/PdfInlineViewer.test.tsx
  pnpm test admin/knowledge-base/explorer/__tests__/
  pnpm test admin/knowledge-base/explorer/preview/__tests__/
  pnpm test features/game-chat/__tests__/CitationPdfTab.test.tsx
  pnpm test:admin-dashboard
  pnpm typecheck
  pnpm lint
  pnpm lint:tokens
  ```

  Expected: tutti PASS, 0 typecheck error, 0 lint error, 0 token violation.

- [ ] **Step 4: Commit E2E**

  ```bash
  git add apps/web/e2e/admin/kb-doc-preview-tab.spec.ts
  git commit -m "test(admin-kb): #1654 E2E smoke for Preview tab"
  ```

---

## Self-Review

**1. Spec coverage:** spec § 5 (data flow) → Task 1 step 3 (fetch lifecycle + AbortController). Spec § 6 (error handling) → Task 1 step 1 test cases per HTTP 500, AbortError silente. Spec § 7 stati doc → Task 5 step 1+2 (`ready` + `locked`). Spec § 8 FR-1..FR-12 → Task 5 (FR-1, FR-2), Task 3 (FR-3), Task 1 (FR-4..FR-9), Task 2 (FR-10), Task 6 (FR-11, FR-12). Spec § 9 test plan → Task 1+4 (unit), Task 6 (E2E + gate). Spec § 11 phasing T0-T6 → Tasks 0-6 1:1.

**2. Placeholder scan:** Nessun "TBD/TODO". Tutti gli step hanno codice concreto o comando esatto. T0 outcome decisorio esplicitato in T1 e T5 (no soggettività). I path file sono assoluti rispetto al repo root.

**3. Type consistency:** `KbDocTabKey = 'overview' | 'ingestion' | 'used-by' | 'preview'` consistente in Task 3 (definition) + Task 5 (consumer discriminator). `PdfInlineViewerProps.features` shape costante: T1 (definition) + T2 (`{antiLeak: true}`) + T4 (`{download, openInTab, jumpToPage, zoom}`). `KbDocPreviewPanelProps.docId: string` costante in T4 + T5.

**Note di rischio:**
- **T0 spike (a) outcome BLOCKED**: T5 Step 2 e relativi test rimossi; aggiorna spec § 8 FR-2 e § 11 T5 prima di T1 per evitare drift. Commit di aggiornamento spec separato.
- **T2 regression**: il test esistente di CitationPdfTab usa `setTimeout(() => onLoadSuccess?.({ numPages: 24 }), 0)` nel mock. PdfInlineViewer ha lo stesso pattern. Se un selettore esatto nel test ha cambiato (es. cerca `select-none` su un container che ora ha id diverso) → aggiusta `PdfInlineViewer` (non il test).
- **E2E helper paths**: Step 1 di T6 assume `loginAsAdmin` + `seedKbDocFixture` esistenti. Se mancano (improbabile dato che F3-FU-1/2/4 hanno E2E smoke), riusa il pattern del commit `23b17f6a5`.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab.md`. Two execution options:

**1. Subagent-Driven (recommended)** — dispatch fresh subagent per task, two-stage review (spec+quality) tra i task. Rate limit API permitting.

**2. Inline Execution** — execute tasks in this session using `executing-plans`, batch con checkpoints.

Which approach?

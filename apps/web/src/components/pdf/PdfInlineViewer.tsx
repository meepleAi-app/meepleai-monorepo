/**
 * PdfInlineViewer — shared inline PDF viewer with feature-flagged toolbar.
 *
 * Extracts the PdfRenderer interior of CitationPdfTab.tsx (apps/web/src/components/features/game-chat/)
 * into a reusable shared component. Consumers control toolbar surface via `features` prop:
 *   - antiLeak: contextmenu blocked + select-none + "🔒 Solo visualizzazione" badge
 *   - download: <a download> button linking to /api/v1/pdfs/{id}/download
 *   - openInTab: <a target="_blank" rel="noopener noreferrer"> button
 *   - jumpToPage: numeric input with form submit, clamped [1, numPages]
 *   - zoom: select with presets (50/100/150/fit-width) applied via Page.scale
 *
 * Worker setup: T0 spike (audits/2026-05-30-pdf-download-locked-spike.md) confirmed
 * idempotent — multi-module assignment of the same workerSrc URL is safe, no singleton needed.
 * It now runs inside the `dynamic()` loader below rather than at module scope (#4058).
 *
 * Spec: docs/superpowers/specs/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab-design.md
 * Plan: docs/superpowers/plans/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab.md (Task 1)
 * Pattern source: apps/web/src/components/features/game-chat/CitationPdfTab.tsx
 */
'use client';

import {
  useState,
  useEffect,
  useCallback,
  useMemo,
  useRef,
  type ChangeEvent,
  type FormEvent,
  type ReactElement,
} from 'react';

import clsx from 'clsx';
import dynamic from 'next/dynamic';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import { api } from '@/lib/api';
import type { ImageRegion } from '@/lib/api/schemas';
import type { CitationRegion } from '@/types';

import { makeQuoteTextRenderer } from './pdf-quote-highlight';
import { PdfBBoxOverlay } from './PdfBBoxOverlay';
import { PdfImageRegionOverlay } from './PdfImageRegionOverlay';

// ─── #4058: react-pdf's JS must never be evaluated on the server ──────────────
//
// pdfjs-dist@6.2.108 declares `engines.node: ">=22.13.0 || >=24"` and patches
// `Iterator.prototype.join` with no `typeof Iterator` guard, so merely LOADING the
// module throws `ReferenceError: Iterator is not defined` on the image's Node
// 20.18.3 (iterator helpers are a Node 22 global). `'use client'` does not avoid
// that: in the App Router a client component is rendered on the server too, which
// is why /games/[id]/card answered 500. Only `ssr: false` keeps a module out of
// the server pass.
//
// The boundary lives HERE rather than in the callers because a static import-graph
// walk over all 246 app-router entries found FOUR reaching this one module —
// /games/[id]/card, /library/[gameId], /admin/knowledge-base and
// /admin/knowledge-base/mechanic-extractor/analyses — through five distinct chains,
// two of them through barrels (`features/mechanic-card/index.ts`,
// `features/game-chat/index.ts`) that re-export a consumer wholesale. A per-caller
// `dynamic()` has to be re-established by every new caller and lapses silently when
// one forgets: #4058 reached a published route while this viewer's own consumer
// (MechanicCitationPanel) documented pdfjs as "lazily pulled".
//
// Only the JS is deferred. The two CSS imports above stay static on purpose: a
// stylesheet is resolved by the bundler and never `require`d in Node, so it cannot
// throw — measured, by the three app-router entries that reach
// `react-pdf/dist/Page/TextLayer.css` statically and answer 200.
//
// Pattern source: src/components/chat-unified/PdfPageModal.tsx, which already
// splits react-pdf this way — and is precisely why the routes that reach react-pdf
// only through it never 500'd.
const Document = dynamic(
  () =>
    import('react-pdf').then(mod => {
      // The worker assignment travels WITH the module instead of running at module
      // scope, otherwise it would re-introduce a server-side `pdfjs` reference. The
      // T0 spike (audits/2026-05-30-pdf-download-locked-spike.md) established the
      // assignment is idempotent, so running it per chunk-load is safe.
      mod.pdfjs.GlobalWorkerOptions.workerSrc = new URL(
        'pdfjs-dist/build/pdf.worker.min.mjs',
        import.meta.url
      ).toString();
      return mod.Document;
    }),
  // react-pdf is no longer in the entry chunk, so it only starts downloading when
  // <Document> first renders — i.e. after the blob fetch spinner below has already
  // cleared. Without a fallback the viewer would flash empty in that gap.
  {
    ssr: false,
    loading: () => <PdfSpinner className="flex min-h-40 items-center justify-center" />,
  }
);

const Page = dynamic(() => import('react-pdf').then(mod => mod.Page), { ssr: false });

/**
 * The blob-fetch spinner, shared with the `dynamic()` fallback above so both waits
 * look identical. `className` stays caller-supplied because the two sites position
 * it differently (overlay vs. in-flow).
 */
function PdfSpinner({ className }: { readonly className: string }): ReactElement {
  return (
    <div role="status" aria-label="Caricamento PDF" className={className}>
      <div
        aria-hidden="true"
        className="h-10 w-10 rounded-full border-[3px] border-[hsl(var(--c-kb)/0.2)] border-t-[hsl(var(--c-kb))] motion-safe:animate-spin"
      />
    </div>
  );
}

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
  readonly renderTextLayer?: boolean;
  readonly highlightQuote?: string;
  readonly onQuoteMatch?: (found: boolean) => void;
  /**
   * SP-D (#3408): normalized [0,1] top-left region boxes to draw as a precise overlay
   * (Pattern B). When present (any page), the quote text-layer highlight (Pattern A) is
   * suppressed — bbox grounding takes precedence. Rects are filtered to the visible page.
   */
  readonly highlightRects?: readonly CitationRegion[];
}

type ZoomState = 'fit-width' | number;

// A4 portrait width at 72dpi ≈ 595 pt. Used as denominator for fit-width scale.
const A4_WIDTH_PT = 595;
const MIN_FIT_WIDTH_SCALE = 0.5;

export function PdfInlineViewer({
  documentId,
  initialPage = 1,
  defaultZoom = 'fit-width',
  features = {},
  className,
  renderTextLayer = false,
  highlightQuote,
  onQuoteMatch,
  highlightRects,
}: PdfInlineViewerProps): ReactElement {
  const [numPages, setNumPages] = useState<number>(0);
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [zoom, setZoom] = useState<ZoomState>(defaultZoom);
  const [containerWidth, setContainerWidth] = useState<number>(800);
  const [jumpInput, setJumpInput] = useState<string>(String(initialPage));
  const [imageRegions, setImageRegions] = useState<readonly ImageRegion[]>([]); // #3447 slice
  const containerRef = useRef<HTMLDivElement>(null);

  const downloadUrl = api.pdf.getPdfDownloadUrl(documentId);

  // SP-D (#3408): Pattern B (bbox overlay) takes precedence over Pattern A (quote text-layer).
  // Once any region exists for this citation we are in bbox mode → skip the quote renderer.
  const hasRects = (highlightRects?.length ?? 0) > 0;
  const pageRects = useMemo(
    () => (highlightRects ?? []).filter(r => r.page === currentPage),
    [highlightRects, currentPage]
  );

  // #3447 slice: table-image regions for the current page (fetched on open, below).
  const pageImageRegions = useMemo(
    () => imageRegions.filter(r => r.page === currentPage),
    [imageRegions, currentPage]
  );

  const quoteRenderer = useMemo(
    () => (highlightQuote && !hasRects ? makeQuoteTextRenderer(highlightQuote) : null),
    [highlightQuote, hasRects]
  );

  const onDocumentLoadSuccess = useCallback(({ numPages: n }: { numPages: number }) => {
    setNumPages(n);
  }, []);

  const onDocumentLoadError = useCallback((err: Error) => {
    setLoadError(`PDF non leggibile: ${err.message}`);
  }, []);

  // Fetch blob with credentials. Mirror of CitationPdfTab fetch-blob lifecycle.
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

  // #3447 slice: fetch persisted image-table regions on open (independent of the blob fetch).
  useEffect(() => {
    let cancelled = false;
    api.pdf
      .getImageRegions(documentId)
      .then(regions => {
        if (!cancelled) setImageRegions(regions ?? []);
      })
      .catch(() => {
        if (!cancelled) setImageRegions([]);
      });
    return () => {
      cancelled = true;
    };
  }, [documentId]);

  // ResizeObserver for fit-width scale recompute.
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
    zoom === 'fit-width' ? Math.max(MIN_FIT_WIDTH_SCALE, containerWidth / A4_WIDTH_PT) : zoom / 100;

  // Spec FR-9: fetch failure → toolbar disabled (matches loading-state gating)
  const controlsDisabled = loading || Boolean(loadError);

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

  const handleZoomChange = (e: ChangeEvent<HTMLSelectElement>) => {
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
          disabled={currentPage <= 1 || controlsDisabled}
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
          disabled={currentPage >= numPages || controlsDisabled}
          className="rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          Next →
        </button>

        {features.jumpToPage && (
          <form onSubmit={handleJumpSubmit} className="flex items-center gap-1">
            <label htmlFor="pdf-jump" className="text-muted-foreground">
              Vai a pagina
            </label>
            <input
              id="pdf-jump"
              type="number"
              role="spinbutton"
              aria-label="Vai a pagina"
              min={1}
              max={numPages || undefined}
              value={jumpInput}
              disabled={controlsDisabled}
              onChange={e => setJumpInput(e.target.value)}
              className="w-14 rounded-sm border border-border bg-card px-1 py-0.5 text-center disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            />
          </form>
        )}

        {features.zoom && (
          <label className="flex items-center gap-1">
            <span className="text-muted-foreground">Zoom</span>
            <select
              aria-label="Zoom"
              value={zoom === 'fit-width' ? 'fit-width' : String(zoom)}
              disabled={controlsDisabled}
              onChange={handleZoomChange}
              className="rounded-sm border border-border bg-card px-1 py-0.5 disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              <option value="50">50%</option>
              <option value="100">100%</option>
              <option value="150">150%</option>
              <option value="fit-width">fit-width</option>
            </select>
          </label>
        )}

        <div className="ml-auto flex items-center gap-2">
          {features.openInTab && !loadError && (
            <a
              href={downloadUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label="Apri in tab"
              className="inline-flex items-center rounded-sm border border-border bg-card px-2 py-1 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              ↗ Apri in tab
            </a>
          )}
          {features.download && !loadError && (
            <a
              href={downloadUrl}
              download
              aria-label="Download"
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
        {loading && <PdfSpinner className="absolute inset-0 flex items-center justify-center" />}
        {loadError && (
          <div className="flex h-full items-center justify-center p-6 text-sm text-destructive">
            Errore caricamento PDF: {loadError}
          </div>
        )}
        {pdfBlob && !loadError && (
          // SP-D (#3408): center + shrink-wrap the page so the react-pdf `.react-pdf__Page`
          // ancestor hugs the canvas (not the full container width). Otherwise the block Page div
          // fills the container and the %-based bbox overlay (its child) misaligns for pages
          // narrower than A4. Mirrors the proven PdfPageModal `flex justify-center` layout.
          <div className="flex justify-center">
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
                renderTextLayer={renderTextLayer || (!!highlightQuote && !hasRects)}
                customTextRenderer={
                  quoteRenderer ? ({ str }) => quoteRenderer.render({ str }) : undefined
                }
                onRenderTextLayerSuccess={
                  quoteRenderer && onQuoteMatch
                    ? () => onQuoteMatch(quoteRenderer.matched())
                    : undefined
                }
              >
                {pageRects.length > 0 ? <PdfBBoxOverlay rects={pageRects} /> : null}
                {pageImageRegions.length > 0 ? (
                  <PdfImageRegionOverlay rects={pageImageRegions} />
                ) : null}
              </Page>
            </Document>
          </div>
        )}
      </div>
    </div>
  );
}

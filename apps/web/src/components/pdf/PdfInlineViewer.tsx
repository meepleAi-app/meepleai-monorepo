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
  useRef,
  type ChangeEvent,
  type FormEvent,
  type ReactElement,
} from 'react';

import clsx from 'clsx';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import { api } from '@/lib/api';

// Worker setup — T0 spike confirmed idempotent (multi-module assignment safe).
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

// A4 portrait width at 72dpi ≈ 595 pt. Used as denominator for fit-width scale.
const A4_WIDTH_PT = 595;
const MIN_FIT_WIDTH_SCALE = 0.5;

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

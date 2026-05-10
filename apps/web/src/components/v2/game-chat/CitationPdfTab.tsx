/**
 * CitationPdfTab — body del tab "PDF originale" del CitationModal v3.
 *
 * Logica:
 *   - isPublic=true → render PDF viewer immediato
 *   - altrimenti useCanViewPdf:
 *       loading → spinner
 *       canView=true → PDF viewer (Document+Page react-pdf)
 *       canView=false OR isError → CitationOwnershipUpsell
 *
 * Anti-leak:
 *   - oncontextmenu disabled sul container PDF
 *   - NO download button
 *   - NO link al /api/v1/pdfs/{id}/download esposto al click
 *
 * Riusa il pattern fetch-blob di PdfViewerModal.tsx (apps/web/src/components/pdf/).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.2 §4.2
 */
'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import type { ReactElement } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import clsx from 'clsx';

import { useCanViewPdf } from '@/hooks/queries/useCanViewPdf';
import { api } from '@/lib/api';

import { CitationOwnershipUpsell } from './CitationOwnershipUpsell';

// pdfjs worker setup (mirror of PdfViewerModal.tsx:17-20)
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

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
        <div className="font-mono text-xs text-muted-foreground">
          Verifica accesso al PDF…
        </div>
      </div>
    );
  }

  if (showUpsell) {
    return <CitationOwnershipUpsell gameId={gameId} className={className} />;
  }

  return (
    <PdfRenderer
      documentId={documentId}
      initialPage={initialPage}
      className={className}
    />
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// PdfRenderer — mirror of PdfViewerModal.tsx body, embedded inline (no Dialog),
// no download button, oncontextmenu blocked
// ─────────────────────────────────────────────────────────────────────────────

interface PdfRendererProps {
  readonly documentId: string;
  readonly initialPage: number;
  readonly className?: string;
}

function PdfRenderer({ documentId, initialPage, className }: PdfRendererProps): ReactElement {
  const [numPages, setNumPages] = useState<number>(0);
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const containerRef = useRef<HTMLDivElement>(null);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  // Fetch PDF as blob with credentials (mirror PdfViewerModal.tsx pattern)
  useEffect(() => {
    const pdfUrl = api.pdf.getPdfDownloadUrl(documentId);
    const controller = new AbortController();

    setLoading(true);
    setLoadError(null);
    setNumPages(0);
    setCurrentPage(initialPage);
    setPdfBlob(null);

    fetch(pdfUrl, { credentials: 'include', signal: controller.signal })
      .then(res => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.blob();
      })
      .then(blob => {
        setPdfBlob(blob);
        setLoading(false);
      })
      .catch(err => {
        if (err.name !== 'AbortError') {
          setLoadError(err.message);
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [documentId, initialPage]);

  const goPrev = () => setCurrentPage(p => Math.max(1, p - 1));
  const goNext = () => setCurrentPage(p => Math.min(numPages, p + 1));

  return (
    <div
      ref={containerRef}
      data-slot="citation-pdf-renderer"
      className={clsx('flex flex-col gap-3', className)}
    >
      {/* Toolbar: prev/next + page info + anti-leak indicator */}
      <div
        className={clsx(
          'flex items-center gap-2 rounded-md bg-muted px-3 py-2',
          'font-mono text-xs flex-wrap'
        )}
      >
        <button
          type="button"
          onClick={goPrev}
          disabled={currentPage <= 1 || loading}
          className={clsx(
            'rounded-sm border border-border bg-card px-2 py-1',
            'hover:bg-muted hover:border-[hsl(var(--c-kb))]',
            'disabled:opacity-40 disabled:cursor-not-allowed'
          )}
        >
          ← Prev
        </button>
        <span className="flex-1 text-center">
          Pagina <strong>{currentPage}</strong> / {numPages || '?'}
        </span>
        <button
          type="button"
          onClick={goNext}
          disabled={currentPage >= numPages || loading}
          className={clsx(
            'rounded-sm border border-border bg-card px-2 py-1',
            'hover:bg-muted hover:border-[hsl(var(--c-kb))]',
            'disabled:opacity-40 disabled:cursor-not-allowed'
          )}
        >
          Next →
        </button>
        <span className="inline-flex items-center gap-1 text-[10px] text-[hsl(var(--c-success))]">
          🔒 Solo visualizzazione
        </span>
      </div>

      {/* PDF canvas — anti-leak: no right-click, no user-select */}
      <div
        onContextMenu={e => e.preventDefault()}
        className={clsx(
          'relative overflow-hidden rounded-md border border-border bg-white',
          'select-none [&_canvas]:max-w-full',
          'min-h-[400px]'
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
        {pdfBlob && (
          <Document
            file={pdfBlob}
            onLoadSuccess={onDocumentLoadSuccess}
            loading={null}
            error={null}
          >
            <Page
              pageNumber={currentPage}
              renderAnnotationLayer={false}
              renderTextLayer={false}
            />
          </Document>
        )}
      </div>
    </div>
  );
}

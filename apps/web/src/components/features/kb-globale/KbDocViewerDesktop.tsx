/**
 * KbDocViewerDesktop — 3-col PDF viewer with thumbnails + center page + citations panel.
 *
 * 12% / 55% / 33% grid (matches admin-mockups/design_files/sp4-kb-globale.jsx:1035).
 *
 * D-E: citation deep-link is page-level — `activePage` highlights citations matching
 * that page in the right panel. No bbox/inline highlight.
 *
 * D-G: react-pdf@^10.4.1 + pdfjs-dist CDN worker. Lazy-imported from orchestrator.
 *
 * fileUrl derivation: Task 5 verification showed KbDocDetail has NO fileUrl field.
 * The orchestrator (Task 9) derives it as `/api/v1/pdfs/{doc.id}/download` and
 * passes here as a required prop. This component stays pure presentational.
 *
 * pageCount: orchestrator normalizes nullable doc.pageCount to a number (default 1).
 * react-pdf's onLoadSuccess({numPages}) overrides as the source of truth at render time.
 *
 * Pure presentational — all data props-injected (Phase 1 pattern).
 */

'use client';

import { type JSX, useState } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';

import { cn } from '@/lib/utils';

// Worker config (one-time on module load; safe with `'use client'` directive)
if (typeof window !== 'undefined' && !pdfjs.GlobalWorkerOptions.workerSrc) {
  pdfjs.GlobalWorkerOptions.workerSrc = `https://unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;
}

const ZOOM_LEVELS = [0.75, 1, 1.25, 1.5, 2] as const;

export interface KbDocViewerCitation {
  n: number;
  docId: string;
  page: number;
  refText: string;
  snippet: string;
}

export interface KbDocViewerLabels {
  pageLabel: (n: number) => string;
  zoomIn: string;
  zoomOut: string;
  zoomReset: string;
  thumbnailsLabel: string;
  closeLabel: string;
  pageOfTotal: (cur: number, total: number) => string;
}

export interface KbDocViewerDesktopProps {
  /** Caller derives fileUrl = `/api/v1/pdfs/${id}/download` from KbDocDetail.
      pageCount is the orchestrator-normalized number; nullable handled upstream. */
  doc: { id: string; title: string; fileUrl: string; pageCount: number };
  activePage: number;
  citations: readonly KbDocViewerCitation[];
  labels: KbDocViewerLabels;
  onPageChange: (page: number) => void;
  onClose: () => void;
}

export function KbDocViewerDesktop({
  doc,
  activePage,
  citations,
  labels,
  onPageChange,
  onClose,
}: KbDocViewerDesktopProps): JSX.Element {
  const [zoomIdx, setZoomIdx] = useState(1); // 100%
  const [numPages, setNumPages] = useState(doc.pageCount);
  const scale = ZOOM_LEVELS[zoomIdx];

  return (
    <div
      data-slot="kb-doc-viewer-desktop"
      className="rounded-xl border border-entity-kb/20 bg-card overflow-hidden shadow-md"
    >
      {/* Top toolbar */}
      <div className="flex items-center gap-3 px-5 py-3 border-b border-entity-kb/15 bg-gradient-to-br from-entity-kb/5 to-transparent">
        <div className="min-w-0 flex-1">
          <div className="font-display font-bold text-sm truncate">📄 {doc.title}</div>
          <div className="font-mono text-[10px] text-muted-foreground">
            {labels.pageOfTotal(activePage, numPages)}
          </div>
        </div>
        <div className="flex items-center border border-border rounded-md bg-muted overflow-hidden">
          <button
            type="button"
            aria-label={labels.zoomOut}
            onClick={() => setZoomIdx(i => Math.max(0, i - 1))}
            className="px-3 py-1.5 text-xs font-mono font-bold text-foreground hover:bg-card border-r border-border"
          >
            −
          </button>
          <span className="px-3 py-1.5 text-xs font-mono font-bold text-foreground bg-card border-r border-border">
            {Math.round(scale * 100)}%
          </span>
          <button
            type="button"
            aria-label={labels.zoomIn}
            onClick={() => setZoomIdx(i => Math.min(ZOOM_LEVELS.length - 1, i + 1))}
            className="px-3 py-1.5 text-xs font-mono font-bold text-foreground hover:bg-card"
          >
            +
          </button>
        </div>
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-8 h-8 rounded-full bg-muted hover:bg-border text-foreground"
        >
          ✕
        </button>
      </div>

      {/* 3-col body */}
      <div className="grid grid-cols-[12%_55%_33%] min-h-[680px]">
        {/* Page thumbnails sidebar */}
        <aside
          aria-label={labels.thumbnailsLabel}
          className="bg-muted border-r border-border overflow-y-auto p-3"
        >
          <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground mb-3 text-center">
            {labels.thumbnailsLabel}
          </div>
          {Array.from({ length: numPages }, (_, i) => i + 1).map(pageN => {
            const isActive = pageN === activePage;
            return (
              <button
                key={pageN}
                type="button"
                aria-label={labels.pageLabel(pageN)}
                aria-current={isActive ? 'page' : undefined}
                onClick={() => onPageChange(pageN)}
                className={cn(
                  'block w-full mb-2.5 cursor-pointer',
                  isActive ? 'ring-2 ring-entity-kb' : 'ring-1 ring-border',
                  'rounded-sm bg-card aspect-[0.71] hover:ring-entity-kb/50'
                )}
              >
                <span className="font-mono text-[9px] text-center block mt-1 text-foreground">
                  {pageN}
                </span>
              </button>
            );
          })}
        </aside>

        {/* Center — PDF render */}
        <div className="bg-muted p-6 overflow-y-auto">
          <Document
            file={doc.fileUrl}
            onLoadSuccess={({ numPages: n }: { numPages: number }) => setNumPages(n)}
          >
            <Page pageNumber={activePage} scale={scale} />
          </Document>
        </div>

        {/* Right — citations panel */}
        <aside className="bg-card border-l border-entity-kb/15 p-4">
          <div className="font-display font-bold text-sm mb-3">Citations</div>
          {citations.map(c => {
            const isActive = c.page === activePage;
            return (
              <div
                key={c.n}
                data-active={isActive ? 'true' : undefined}
                className={cn(
                  'rounded-md p-3 mb-2 border',
                  isActive ? 'bg-entity-kb/10 border-entity-kb/40' : 'bg-muted border-border'
                )}
              >
                <div className="font-mono text-[10px] text-muted-foreground">
                  {c.n} · {c.refText}
                </div>
                <div className="text-xs text-foreground mt-1">{c.snippet}</div>
              </div>
            );
          })}
        </aside>
      </div>
    </div>
  );
}

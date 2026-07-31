'use client';

import React, { useEffect, useRef, useState } from 'react';

import { PdfInlineViewer, type PdfInlineViewerFeatures } from './PdfInlineViewer';

export interface PdfQuoteViewerProps {
  readonly documentId: string;
  readonly page: number;
  readonly quote: string;
  /**
   * Resets the internal "matched" state whenever it changes (in addition to
   * documentId/page/quote). Pass the container's open flag so re-opening the
   * same citation re-arms the fallback banner.
   */
  readonly resetKey?: unknown;
  /**
   * Toolbar/behaviour surface forwarded to {@link PdfInlineViewer}. Defaults to
   * `{ jumpToPage: true, zoom: true }` (mechanic-card / admin usage). The chat
   * citation path (CitationPdfTab) passes `{ antiLeak: true }` instead.
   */
  readonly features?: PdfInlineViewerFeatures;
  readonly className?: string;
}

/**
 * #530 AD-1 — presentational PDF-quote viewer shared by BOTH the admin Dialog
 * ({@link PdfQuoteHighlighter}) and the public citation panel
 * ({@link import('@/components/features/mechanic-card/MechanicCitationPanel')}).
 *
 * Renders the source PDF at `page` (via {@link PdfInlineViewer}), highlights the
 * `quote` through the text-layer renderer, and surfaces a page-level fallback
 * banner (`data-testid="pdf-quote-fallback"`) when the quote can't be located
 * automatically. pdfjs stays lazy — it is only pulled in through PdfInlineViewer.
 *
 * This is intentionally a thin, chrome-free body: the surrounding container
 * (Dialog / Sheet / Drawer) owns the header, scroll region, and footer.
 */
export function PdfQuoteViewer({
  documentId,
  page,
  quote,
  resetKey,
  features = { jumpToPage: true, zoom: true },
  className,
}: PdfQuoteViewerProps): React.JSX.Element {
  const [matched, setMatched] = useState<boolean | null>(null);

  // Reset per (re)open / per target so a previous "not found" banner doesn't
  // leak. Skip the FIRST run: on mount the child text-layer renderer may report
  // its match synchronously during render, and an initial reset here would
  // clobber that result before the banner can show.
  const resetSignature = `${String(resetKey)}::${documentId}::${page}::${quote}`;
  const lastSignature = useRef(resetSignature);
  useEffect(() => {
    if (lastSignature.current !== resetSignature) {
      lastSignature.current = resetSignature;
      setMatched(null);
    }
  }, [resetSignature]);

  return (
    <div className={className} data-slot="pdf-quote-viewer">
      {matched === false && (
        <div
          className="mb-2 rounded-md border border-[hsl(var(--c-warning)/0.4)] bg-[hsl(var(--c-warning)/0.12)] p-2 text-xs text-[hsl(var(--c-warning-ink))]"
          role="status"
          data-testid="pdf-quote-fallback"
        >
          Quote non individuabile automaticamente a p.{page}; verifica manualmente.
        </div>
      )}
      <PdfInlineViewer
        documentId={documentId}
        initialPage={page}
        highlightQuote={quote}
        onQuoteMatch={setMatched}
        features={features}
      />
    </div>
  );
}

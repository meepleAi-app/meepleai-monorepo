'use client';

import React, { useEffect, useState } from 'react';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';

import { PdfInlineViewer } from './PdfInlineViewer';

export interface PdfQuoteHighlighterProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly documentId: string;
  readonly page: number;
  readonly quote: string;
}

/**
 * #526 AC-2 / #530 AD-1 — shared citation quote viewer. Opens the source PDF at `page`, highlights
 * `quote` via PdfInlineViewer's text-layer search (Pattern A), and shows a page-level fallback
 * banner when the quote can't be located automatically. Consumed by admin review (#526) and,
 * later, #528 public card + #530 chat citations.
 */
export function PdfQuoteHighlighter({
  open,
  onOpenChange,
  documentId,
  page,
  quote,
}: PdfQuoteHighlighterProps): React.JSX.Element {
  const [matched, setMatched] = useState<boolean | null>(null);

  useEffect(() => {
    if (open) setMatched(null); // reset per open
  }, [open, documentId, page, quote]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Citazione — p.{page}</DialogTitle>
        </DialogHeader>
        {matched === false && (
          <div
            className="rounded-md border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900"
            role="status"
            data-testid="pdf-quote-fallback"
          >
            Quote non individuabile automaticamente a p.{page}; verifica manualmente.
          </div>
        )}
        <div className="max-h-[70vh] overflow-auto">
          <PdfInlineViewer
            documentId={documentId}
            initialPage={page}
            highlightQuote={quote}
            onQuoteMatch={setMatched}
            features={{ jumpToPage: true, zoom: true }}
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}

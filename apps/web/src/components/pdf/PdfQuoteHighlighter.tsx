'use client';

import React from 'react';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';

import { PdfQuoteViewer } from './PdfQuoteViewer';

export interface PdfQuoteHighlighterProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly documentId: string;
  readonly page: number;
  readonly quote: string;
}

/**
 * #526 AC-2 / #530 AD-1 — shared citation quote viewer (admin Dialog surface).
 * Opens the source PDF at `page`, highlights `quote` via PdfInlineViewer's
 * text-layer search (Pattern A), and shows a page-level fallback banner when the
 * quote can't be located automatically. The PDF body + fallback banner live in
 * the shared {@link PdfQuoteViewer} so the public #530 citation panel reuses the
 * exact same rendering; this component only owns the Dialog chrome.
 *
 * Consumed by admin review (#526) and the #530 chat/card citation entry points.
 */
export function PdfQuoteHighlighter({
  open,
  onOpenChange,
  documentId,
  page,
  quote,
}: PdfQuoteHighlighterProps): React.JSX.Element {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Citazione — p.{page}</DialogTitle>
        </DialogHeader>
        <PdfQuoteViewer
          documentId={documentId}
          page={page}
          quote={quote}
          resetKey={open}
          className="max-h-[70vh] overflow-auto"
        />
      </DialogContent>
    </Dialog>
  );
}

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
import { PdfQuoteViewer } from '@/components/pdf/PdfQuoteViewer';
import { useCanViewPdf } from '@/hooks/queries/useCanViewPdf';

export interface CitationPdfTabProps {
  readonly documentId: string;
  readonly gameId?: string;
  readonly initialPage: number;
  readonly isPublic?: boolean;
  /**
   * SP0 (#3404): quote verbatim da evidenziare nel PDF (highlight della regione
   * citata + banner fallback via {@link PdfQuoteViewer}). Deve essere passato SOLO
   * per citazioni tier "full" — il chiamante (CitationModal) fa il gating copyright.
   * Se assente/vuoto si ripiega sul viewer semplice (solo pagina).
   */
  readonly quote?: string;
  readonly className?: string;
}

export function CitationPdfTab({
  documentId,
  gameId,
  initialPage,
  isPublic = false,
  quote,
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

  const trimmedQuote = quote?.trim();
  if (trimmedQuote) {
    return (
      <PdfQuoteViewer
        documentId={documentId}
        page={initialPage}
        quote={trimmedQuote}
        features={{ antiLeak: true }}
        className={className}
      />
    );
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

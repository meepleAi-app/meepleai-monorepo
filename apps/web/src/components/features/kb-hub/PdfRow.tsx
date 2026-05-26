/**
 * PdfRow — single PDF row in KB hub list.
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` PdfRow.
 *
 * Status badge + chunks count hidden gracefully when undefined (P83 deferred fields).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type PdfStatus = 'ready' | 'indexing' | 'stale' | 'failed';

export interface KbPdf {
  readonly id: string;
  readonly name: string;
  readonly sizeFormatted: string;
  readonly uploadedAtRelative: string;
  readonly status?: PdfStatus;
  readonly chunks?: number;
}

export interface PdfRowLabels {
  readonly openCta: string;
  readonly openAria: string; // supports {pdfName}
  readonly chunksLabel: string; // supports {count}
  readonly status: Record<PdfStatus, string>;
}

export interface PdfRowProps {
  readonly pdf: KbPdf;
  readonly labels: PdfRowLabels;
  readonly onActionClick: (pdfId: string) => void;
  readonly className?: string;
}

const STATUS_BG: Record<PdfStatus, string> = {
  ready: 'bg-emerald-600 text-white',
  indexing: 'bg-amber-500 text-white',
  stale: 'bg-slate-500 text-white',
  failed: 'bg-destructive text-white',
};

export function PdfRow(props: PdfRowProps): ReactElement {
  const { pdf, labels, onActionClick, className } = props;
  const ariaLabel = labels.openAria.replace('{pdfName}', pdf.name);

  return (
    <div
      data-slot="kb-hub-pdf-row"
      data-pdf-id={pdf.id}
      className={clsx(
        'grid grid-cols-[32px_1fr_auto_auto_auto] items-center gap-3 border-b border-border px-4 py-3 transition-colors hover:bg-muted/50',
        className
      )}
    >
      <div
        aria-hidden="true"
        className="flex h-8 w-8 items-center justify-center rounded-md bg-entity-kb/12 text-base"
      >
        📄
      </div>

      <div className="min-w-0">
        <div className="truncate font-display text-sm font-semibold text-foreground">
          {pdf.name}
        </div>
        <div className="mt-1 text-xs text-muted-foreground">
          <span>{pdf.sizeFormatted}</span>
          {pdf.chunks !== undefined && pdf.chunks > 0 && (
            <span className="ml-2 font-mono text-entity-kb/80">
              {labels.chunksLabel.replace('{count}', String(pdf.chunks))}
            </span>
          )}
        </div>
      </div>

      {pdf.status ? (
        <span
          data-slot="kb-hub-pdf-status"
          className={clsx(
            'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 font-display text-[10px] font-bold uppercase tracking-wider',
            STATUS_BG[pdf.status]
          )}
        >
          <span
            aria-hidden="true"
            className={clsx(
              // Inherits white text color from the parent badge (STATUS_BG includes text-white)
              // → bg-current/60 paints the dot in the same color at 60% alpha.
              'h-1.5 w-1.5 rounded-full bg-current/60',
              pdf.status === 'indexing' && 'animate-pulse'
            )}
          />
          {labels.status[pdf.status]}
        </span>
      ) : (
        <span data-slot="kb-hub-pdf-status-empty" />
      )}

      <div className="whitespace-nowrap font-mono text-xs text-muted-foreground">
        {pdf.uploadedAtRelative}
      </div>

      <button
        type="button"
        aria-label={ariaLabel}
        onClick={() => onActionClick(pdf.id)}
        data-slot="kb-hub-pdf-action"
        className="inline-flex items-center gap-1 whitespace-nowrap rounded-md border border-entity-kb/20 bg-entity-kb/8 px-3 py-1.5 font-display text-xs font-bold text-entity-kb transition-colors hover:bg-entity-kb/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
      >
        {labels.openCta} →
      </button>
    </div>
  );
}

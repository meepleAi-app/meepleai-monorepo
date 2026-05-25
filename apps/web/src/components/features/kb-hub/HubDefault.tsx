/**
 * HubDefault — KB hub default view (header + stats strip + PDF list).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` HubDefault.
 *
 * Composition of PdfRow children. Stats strip adaptive: hides metrics whose data
 * is undefined (P83 graceful).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { PdfRow, type KbPdf, type PdfRowLabels } from './PdfRow';

import type { CoverageLevel } from './KbStatsCard';

export interface HubDefaultGameInfo {
  readonly title: string;
  readonly emoji?: string;
  readonly coverGradient?: string; // CSS gradient string for cover thumb (optional)
}

export interface HubDefaultStatsStripLabels {
  readonly docs: string; // "{count} documenti"
  readonly chunks: string;
  readonly embeddings: string;
  readonly lastReindex: string; // "ultima reindex {relative}"
  readonly coverage: string; // "Copertura: {level}"
}

export interface HubDefaultColumnHeaders {
  readonly document: string;
  readonly status: string;
  readonly uploaded: string;
}

export interface HubDefaultCoverageLabels {
  readonly None: string;
  readonly Basic: string;
  readonly Standard: string;
  readonly Complete: string;
}

export interface HubDefaultLabels {
  readonly headerSubtitle: string; // "Knowledge Base"
  readonly uploadCta: string;
  readonly reindexAllCta: string;
  readonly statsStrip: HubDefaultStatsStripLabels;
  readonly coverage: HubDefaultCoverageLabels;
  readonly columnHeaders: HubDefaultColumnHeaders;
  readonly pdfRow: PdfRowLabels;
}

export interface HubDefaultProps {
  readonly game: HubDefaultGameInfo;
  readonly documentCount: number;
  readonly coverageLevel: CoverageLevel;
  readonly pdfs: ReadonlyArray<KbPdf>;
  readonly labels: HubDefaultLabels;
  readonly onUpload: () => void;
  readonly onReindexAll: () => void;
  readonly onPdfAction: (pdfId: string) => void;
  // Deferred (P83):
  readonly chunks?: number;
  readonly embeddings?: number;
  readonly lastReindexRelative?: string;
  readonly className?: string;
}

export function HubDefault(props: HubDefaultProps): ReactElement {
  const {
    game,
    documentCount,
    coverageLevel,
    pdfs,
    labels,
    onUpload,
    onReindexAll,
    onPdfAction,
    chunks,
    embeddings,
    lastReindexRelative,
    className,
  } = props;

  // Locale resolved at runtime (caller's IntlProvider); avoids hardcoded it-IT divergence.
  const formatNumber = (n: number): string => n.toLocaleString();

  // Build stats strip dynamically — only include available metrics + coverage
  const statsStripItems: ReadonlyArray<{ key: string; icon: string; text: string }> = [
    {
      key: 'docs',
      icon: '📄',
      text: labels.statsStrip.docs.replace('{count}', formatNumber(documentCount)),
    },
    ...(chunks !== undefined
      ? [
          {
            key: 'chunks',
            icon: '🧩',
            text: labels.statsStrip.chunks.replace('{count}', formatNumber(chunks)),
          },
        ]
      : []),
    ...(embeddings !== undefined
      ? [
          {
            key: 'embeddings',
            icon: '🔗',
            text: labels.statsStrip.embeddings.replace('{count}', formatNumber(embeddings)),
          },
        ]
      : []),
    ...(lastReindexRelative !== undefined
      ? [
          {
            key: 'lastReindex',
            icon: '🕐',
            text: labels.statsStrip.lastReindex.replace('{relative}', lastReindexRelative),
          },
        ]
      : []),
    {
      key: 'coverage',
      icon: '✅',
      text: labels.statsStrip.coverage.replace('{level}', labels.coverage[coverageLevel]),
    },
  ];

  return (
    <section
      data-slot="kb-hub-hub-default"
      className={clsx(
        'overflow-hidden rounded-2xl border border-entity-kb/20 bg-card shadow-sm',
        className
      )}
    >
      {/* Header */}
      <header className="border-b border-entity-kb/15 bg-gradient-to-br from-entity-kb/6 to-transparent px-6 pb-4 pt-5">
        <div className="mb-2.5 flex items-center gap-3.5">
          <div
            aria-hidden="true"
            className="flex h-12 w-12 shrink-0 items-center justify-center rounded-md border-2 border-foreground/15 text-2xl shadow-md"
            style={{
              background:
                game.coverGradient ?? 'linear-gradient(135deg, hsl(0,35%,28%), hsl(20,30%,20%))',
            }}
          >
            {game.emoji ?? '📚'}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h2 className="m-0 font-display text-lg font-extrabold text-foreground">
                {game.title} · KB
              </h2>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">{labels.headerSubtitle}</p>
          </div>
          <div className="flex shrink-0 gap-2">
            <button
              type="button"
              onClick={onUpload}
              data-slot="kb-hub-default-upload-cta"
              className="rounded-md border border-entity-kb/25 bg-entity-kb/10 px-3.5 py-2 font-display text-xs font-bold text-entity-kb transition-colors hover:bg-entity-kb/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
            >
              {labels.uploadCta}
            </button>
            <button
              type="button"
              onClick={onReindexAll}
              data-slot="kb-hub-default-reindex-cta"
              className="rounded-md bg-entity-kb px-3.5 py-2 font-display text-xs font-bold text-white shadow-md transition-colors hover:bg-entity-kb/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
            >
              {labels.reindexAllCta}
            </button>
          </div>
        </div>

        {/* Stats strip */}
        <div
          data-slot="kb-hub-default-stats-strip"
          className="flex flex-wrap items-center gap-1 rounded-md border border-entity-kb/10 bg-entity-kb/6 px-3.5 py-2.5"
        >
          {statsStripItems.map((s, i, arr) => (
            <span key={s.key} className="inline-flex items-center gap-1">
              <span aria-hidden="true" className="text-xs">
                {s.icon}
              </span>
              <span className="font-mono text-[11px] font-semibold text-foreground">{s.text}</span>
              {i < arr.length - 1 && (
                <span aria-hidden="true" className="mx-1.5 text-muted-foreground opacity-50">
                  ·
                </span>
              )}
            </span>
          ))}
        </div>
      </header>

      {/* Column headers */}
      <div className="grid grid-cols-[32px_1fr_auto_auto_auto] gap-3 border-b border-border px-4 pb-1.5 pt-2">
        <div aria-hidden="true" />
        <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          {labels.columnHeaders.document}
        </div>
        <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          {labels.columnHeaders.status}
        </div>
        <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          {labels.columnHeaders.uploaded}
        </div>
        <div aria-hidden="true" />
      </div>

      {/* PDF rows */}
      <div data-slot="kb-hub-default-pdf-list">
        {pdfs.map(pdf => (
          <PdfRow key={pdf.id} pdf={pdf} labels={labels.pdfRow} onActionClick={onPdfAction} />
        ))}
      </div>
    </section>
  );
}

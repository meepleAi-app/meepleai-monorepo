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
  // #1816 P3-7 — indexing-pending badge shown when caller flags the state
  // (`pdfs.length > 0 && !status.isIndexed`). Optional — when undefined the
  // hub does not render the badge slot.
  readonly indexingBadge?: string;
  readonly indexingDescription?: string;
  // F10 #1974 — bottom drop-zone CTA label (mockup: "Trascina un PDF o
  // clicca per caricarlo"). Optional — when undefined the hub omits the
  // drop zone (legacy consumers get no regression).
  readonly dropZoneCta?: string;
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
  // #1816 P3-7 Phase 2 — surfaces a banner above the stats strip when a PDF
  // is uploaded but BE indexing has not yet completed.
  readonly indexingPending?: boolean;
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
    indexingPending = false,
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
              className="rounded-md border border-entity-kb/25 bg-entity-kb/10 px-3.5 py-2 font-display text-xs font-bold text-entity-kb-text transition-colors hover:bg-entity-kb/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
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

        {/* #1816 P3-7 Phase 2 — indexing-pending banner above the stats strip.
            Rendered only when the caller flags the state AND provides at least
            an `indexingBadge` label. Warning tint = transient state, not error. */}
        {indexingPending && labels.indexingBadge && (
          <div
            data-slot="kb-hub-default-indexing-banner"
            role="status"
            aria-live="polite"
            className="mb-2 rounded-md border border-[hsl(var(--c-warning)/0.3)] bg-[hsl(var(--c-warning)/0.1)] px-3 py-2 text-xs"
          >
            <div className="font-display font-bold text-[hsl(var(--c-warning-ink))]">
              {labels.indexingBadge}
            </div>
            {labels.indexingDescription && (
              <div className="mt-1 text-muted-foreground">{labels.indexingDescription}</div>
            )}
          </div>
        )}

        {/*
          F5 #1974 (audit 2026-06-07): re-styled the stats strip from a
          single dot-separated mono line to a row of compact tag-style
          pills. The mockup ships "4 DOC · 1247 CHUNK · 4891 EMBED · ULTIMA
          IDX 3 GG FA · COPERTURA: STANDARD" as scannable bordered chips —
          much easier to track at a glance than a continuous text run.
          Each chip carries the icon + uppercase mono value; the legacy
          interstitial `·` separator is dropped (the chip border carries
          the visual rhythm now).
        */}
        <div data-slot="kb-hub-default-stats-strip" className="flex flex-wrap items-center gap-1.5">
          {statsStripItems.map(s => (
            <span
              key={s.key}
              data-slot="kb-hub-default-stats-chip"
              className="inline-flex items-center gap-1 rounded-md border border-entity-kb/22 bg-entity-kb/6 px-2 py-0.5"
            >
              <span aria-hidden="true" className="text-[11px]">
                {s.icon}
              </span>
              <span className="font-mono text-[10.5px] font-bold uppercase tracking-wide text-foreground">
                {s.text}
              </span>
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

      {/*
        F10 #1974 (audit 2026-06-07): bottom drop-zone CTA. The mockup
        ships a dashed-bordered tappable region under the PDF list that
        invites the user to drop a PDF (or click). Pre-fix the only upload
        affordance was the small "+ Carica PDF" button in the header —
        easy to miss on a wide screen and discovery-unfriendly for new
        users. We render the same `onUpload` handler so the click path
        stays unchanged; drag-and-drop wiring is deferred to the upload
        flow itself (Issue #1816 P3 / future PR). The CTA only renders
        when the caller provides a label, so consumers that don't want
        the affordance pay nothing.
      */}
      {labels.dropZoneCta && (
        <button
          type="button"
          onClick={onUpload}
          data-slot="kb-hub-default-drop-zone"
          className="m-4 flex w-[calc(100%-2rem)] items-center justify-center gap-2 rounded-md border-2 border-dashed border-entity-kb/30 bg-entity-kb/4 px-4 py-5 text-sm font-bold text-entity-kb-text transition-colors hover:border-entity-kb/55 hover:bg-entity-kb/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
        >
          <span aria-hidden="true" className="text-base">
            ⬆
          </span>
          <span>{labels.dropZoneCta}</span>
        </button>
      )}
    </section>
  );
}

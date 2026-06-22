/**
 * KbStatsCard — KB coverage stats card (reusable primitive).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` KbStatsCard.
 *
 * Deferred fields (P83): chunks, embeddings, lastReindex, raptorLastRebuild, lifetimeCost,
 * costHistory — hidden gracefully when undefined. BE schema enrichment in follow-up issue.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type CoverageLevel = 'None' | 'Basic' | 'Standard' | 'Complete';

export interface KbStatsCardLabels {
  readonly cardTitle: string;
  readonly cardSubtitle: string;
  readonly docsLabel: string; // "Documenti"
  readonly chunksLabel: string; // "Chunks"
  readonly embeddingsLabel: string; // "Embeddings"
  readonly lastReindexLabel: string; // "Ultima idx."
  readonly raptorLabel: string; // "RAPTOR last"
  readonly coverageLabel: string; // "Copertura"
  readonly coverage: Record<CoverageLevel, string>;
  readonly lifetimeCostLabel: string; // "Costo lifetime token"
  readonly sparklineLabel: string; // "Consumo token · ultimi 7 gg"
  readonly sparklineStart: string; // "-7gg"
  readonly sparklineEnd: string; // "oggi"
  // #1816 P3-7 — indexing-pending state surfaces a badge + short description
  // when a PDF is uploaded but the BE has not yet completed indexing. Optional
  // (component renders nothing for the badge slot when label is undefined).
  readonly indexingBadge?: string; // "⏳ Indicizzazione in corso"
  readonly indexingDescription?: string; // "Il documento è caricato ma non ancora ricercabile dalla chat."
}

export interface KbStatsCardProps {
  readonly documentCount: number;
  readonly coverageLevel: CoverageLevel;
  readonly coverageScore: number; // 0-100
  readonly labels: KbStatsCardLabels;
  readonly compact?: boolean;
  readonly className?: string;
  // Deferred props (P83 — hide UI when undefined):
  readonly chunks?: number;
  readonly embeddings?: number;
  readonly lastReindexRelative?: string;
  readonly raptorLastRebuildRelative?: string;
  readonly lifetimeCost?: string;
  readonly costHistory?: ReadonlyArray<number>;
  // #1816 P3-7 Phase 2 — surfaces a badge when a PDF is uploaded but the BE
  // has not yet completed indexing (PDF rows present + `status.isIndexed=false`).
  // Disambiguates the audit-found "0 Documenti / Copertura: Nessuna" UX while
  // the chunk count is still 0 because indexing has not run yet.
  readonly indexingPending?: boolean;
}

export function KbStatsCard(props: KbStatsCardProps): ReactElement {
  const {
    documentCount,
    coverageLevel,
    coverageScore,
    labels,
    compact = false,
    className,
    chunks,
    embeddings,
    lastReindexRelative,
    raptorLastRebuildRelative,
    lifetimeCost,
    costHistory,
    indexingPending = false,
  } = props;

  // Locale resolved at runtime (caller decides via IntlProvider); avoids hardcoding it-IT
  // which renders wrong output for English-locale users.
  const formatNumber = (n: number): string => n.toLocaleString();

  // Metric grid items — only include those with data (P83 graceful hide)
  const metrics: ReadonlyArray<{
    key: string;
    icon: string;
    label: string;
    value: string;
    color: string;
  }> = [
    {
      key: 'docs',
      icon: '📄',
      label: labels.docsLabel,
      value: formatNumber(documentCount),
      color: 'text-entity-kb',
    },
    ...(chunks !== undefined
      ? [
          {
            key: 'chunks',
            icon: '🧩',
            label: labels.chunksLabel,
            value: formatNumber(chunks),
            color: 'text-entity-kb',
          },
        ]
      : []),
    ...(embeddings !== undefined
      ? [
          {
            key: 'embeddings',
            icon: '🔗',
            label: labels.embeddingsLabel,
            value: formatNumber(embeddings),
            color: 'text-entity-agent',
          },
        ]
      : []),
    ...(lastReindexRelative !== undefined
      ? [
          {
            key: 'lastReindex',
            icon: '🕐',
            label: labels.lastReindexLabel,
            value: lastReindexRelative,
            color: 'text-entity-session',
          },
        ]
      : []),
    ...(raptorLastRebuildRelative !== undefined
      ? [
          {
            key: 'raptor',
            icon: '🦅',
            label: labels.raptorLabel,
            value: raptorLastRebuildRelative,
            color: 'text-warning',
          },
        ]
      : []),
  ];

  // F9 #1974 (audit 2026-06-07): hide the sparkline when costHistory contains
  // only zeros (BE seeds an empty 7-day window for KBs that have never been
  // used). Pre-fix the empty bars rendered as a low strip of grey rectangles
  // — pure UI noise. We require at least one non-zero datapoint so the chart
  // only appears once there is real signal.
  const hasSparklineSignal = !!costHistory && costHistory.some(v => v > 0);
  const maxSparklineVal = hasSparklineSignal ? Math.max(...costHistory!) : 1;
  const showSparkline = !compact && hasSparklineSignal;
  const showLifetimeCost = !compact && lifetimeCost !== undefined;
  const gridCols =
    metrics.length >= 4 ? 'grid-cols-4' : metrics.length === 3 ? 'grid-cols-3' : 'grid-cols-2';

  return (
    <section
      data-slot="kb-hub-stats-card"
      className={clsx(
        'rounded-2xl border border-entity-kb/22 bg-card shadow-sm',
        compact ? 'p-4' : 'p-5',
        className
      )}
    >
      {!compact && (
        <header className="mb-4 flex items-center gap-2">
          <span
            aria-hidden="true"
            className="flex h-8 w-8 items-center justify-center rounded-md bg-entity-kb/12 text-base"
          >
            📊
          </span>
          <div>
            <h3 className="font-display text-[13px] font-bold text-foreground">
              {labels.cardTitle}
            </h3>
            <p className="text-[11px] text-muted-foreground">{labels.cardSubtitle}</p>
          </div>
        </header>
      )}

      {/* #1816 P3-7 Phase 2 — indexing-pending badge. Rendered when caller
          flags `indexingPending` AND provides at least an `indexingBadge`
          label. Uses warning-tinted background so it reads as a transient
          state, not an error. */}
      {indexingPending && labels.indexingBadge && (
        <div
          data-slot="kb-hub-stats-indexing-badge"
          role="status"
          aria-live="polite"
          className={clsx(
            'mb-3 rounded-md border border-warning/30 bg-warning/10 px-3 py-2',
            compact ? 'text-[11px]' : 'text-xs'
          )}
        >
          <div className="font-display font-bold text-warning">{labels.indexingBadge}</div>
          {labels.indexingDescription && (
            <div className="mt-1 text-muted-foreground">{labels.indexingDescription}</div>
          )}
        </div>
      )}

      {/* Metric grid (adaptive cols) */}
      <div
        data-slot="kb-hub-stats-metrics"
        className={clsx('grid gap-3', gridCols, !compact && 'mb-4')}
      >
        {metrics.map(m => (
          <div
            key={m.key}
            data-slot={`kb-hub-stats-metric-${m.key}`}
            className="rounded-md bg-muted px-3 py-2.5 text-center"
          >
            <div aria-hidden="true" className="mb-1 text-lg">
              {m.icon}
            </div>
            <div className={clsx('font-mono text-sm font-bold leading-tight', m.color)}>
              {m.value}
            </div>
            <div className="mt-1 text-[10px] text-muted-foreground">{m.label}</div>
          </div>
        ))}
      </div>

      {!compact && (
        <div
          data-slot="kb-hub-stats-coverage"
          className="mb-3 flex items-center justify-between rounded-md border border-entity-kb/12 bg-entity-kb/6 px-3 py-2.5"
        >
          <span className="text-xs text-muted-foreground">{labels.coverageLabel}</span>
          <span className="font-mono text-sm font-bold text-entity-kb">
            {labels.coverage[coverageLevel]} · {coverageScore.toFixed(0)}%
          </span>
        </div>
      )}

      {showLifetimeCost && (
        <div
          data-slot="kb-hub-stats-lifetime-cost"
          className="mb-3 flex items-center justify-between rounded-md border border-entity-kb/12 bg-entity-kb/6 px-3 py-2.5"
        >
          <span className="text-xs text-muted-foreground">{labels.lifetimeCostLabel}</span>
          <span className="font-mono text-sm font-bold text-entity-kb">{lifetimeCost}</span>
        </div>
      )}

      {showSparkline && (
        <div data-slot="kb-hub-stats-sparkline">
          <div className="mb-1.5 text-[10px] text-muted-foreground">{labels.sparklineLabel}</div>
          <div className="flex items-end gap-1" style={{ height: 32 }}>
            {costHistory!.map((v, i) => (
              <div
                key={i}
                aria-hidden="true"
                className={clsx(
                  'flex-1 rounded-t-sm transition-all',
                  i === costHistory!.length - 1 ? 'bg-entity-kb' : 'bg-entity-kb/35'
                )}
                style={{ height: `${Math.max(4, (v / maxSparklineVal) * 100)}%` }}
              />
            ))}
          </div>
          <div className="mt-1 flex justify-between font-mono text-[9px] text-muted-foreground">
            <span>{labels.sparklineStart}</span>
            <span>{labels.sparklineEnd}</span>
          </div>
        </div>
      )}
    </section>
  );
}

/**
 * RaptorPanel — RAPTOR rebuild panel (free locked / pro active variants).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` RaptorPanel.
 *
 * MVP defaults tier='free' (no user tier data from BE yet). Mockup gold replaced
 * with semantic `text-warning` / `bg-warning` for AA-safe DS-15 conformity.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type RaptorTier = 'free' | 'pro';

export interface RaptorPanelLabels {
  readonly title: string;
  readonly description: string;
  readonly lockedBadge: string;
  readonly activeBadge: string;
  readonly lockedNote: string;
  readonly upgradeCta: string;
  readonly upgradeLink: string;
  readonly rebuildCta: string;
  readonly metrics: {
    readonly lastRebuild: string;
    readonly summaries: string;
  };
  readonly estimateLabel: string; // "Stima operazione"
  readonly estimateDescription: string; // supports {chunks}
}

export interface RaptorPanelProps {
  readonly tier: RaptorTier;
  readonly labels: RaptorPanelLabels;
  readonly onRebuild?: () => void;
  readonly onUpgrade?: () => void;
  // Deferred (P83):
  readonly lastRebuildRelative?: string;
  readonly summariesCount?: number;
  readonly estimateChunks?: number;
  readonly estimatedCost?: string;
  readonly estimatedDuration?: string;
  readonly className?: string;
}

export function RaptorPanel(props: RaptorPanelProps): ReactElement {
  const {
    tier,
    labels,
    onRebuild,
    onUpgrade,
    lastRebuildRelative,
    summariesCount,
    estimateChunks,
    estimatedCost,
    estimatedDuration,
    className,
  } = props;

  const locked = tier === 'free';

  const metrics: ReadonlyArray<{ key: string; label: string; value: string }> = [
    ...(lastRebuildRelative !== undefined
      ? [{ key: 'lastRebuild', label: labels.metrics.lastRebuild, value: lastRebuildRelative }]
      : []),
    ...(summariesCount !== undefined
      ? [{ key: 'summaries', label: labels.metrics.summaries, value: String(summariesCount) }]
      : []),
  ];

  const showEstimate =
    !locked &&
    estimateChunks !== undefined &&
    (estimatedCost !== undefined || estimatedDuration !== undefined);

  return (
    <section
      data-slot="kb-hub-raptor-panel"
      data-tier={tier}
      className={clsx(
        'overflow-hidden rounded-2xl border bg-card shadow-sm',
        locked ? 'border-border opacity-90' : 'border-warning/35',
        className
      )}
    >
      <header
        className={clsx(
          'flex items-center gap-2.5 border-b px-5 pb-3 pt-4',
          locked ? 'border-border bg-muted' : 'border-warning/18 bg-warning/6'
        )}
      >
        <span aria-hidden="true" className="text-2xl">
          🦅
        </span>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <span className="font-display text-sm font-extrabold text-foreground">
              {labels.title}
            </span>
            <span
              data-slot="kb-hub-raptor-badge"
              className={clsx(
                'rounded-full border px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider',
                locked
                  ? 'border-warning/25 bg-warning/15 text-warning'
                  : 'border-warning/0 bg-warning text-white'
              )}
            >
              {locked ? labels.lockedBadge : labels.activeBadge}
            </span>
          </div>
          <p className="mt-0.5 text-[11px] text-muted-foreground">{labels.description}</p>
        </div>
      </header>

      <div className="px-5 py-4">
        {metrics.length > 0 && (
          <div data-slot="kb-hub-raptor-metrics" className="mb-3.5 grid grid-cols-2 gap-2.5">
            {metrics.map(m => (
              <div key={m.key} className="rounded-md bg-muted px-3 py-2">
                <div
                  className={clsx(
                    'font-mono text-sm font-bold',
                    locked ? 'text-muted-foreground' : 'text-warning'
                  )}
                >
                  {m.value}
                </div>
                <div className="mt-0.5 text-[10px] text-muted-foreground">{m.label}</div>
              </div>
            ))}
          </div>
        )}

        {locked ? (
          <>
            <div
              data-slot="kb-hub-raptor-locked-note"
              className="mb-3.5 flex items-start gap-2.5 rounded-md border border-dashed border-border-strong bg-muted px-3.5 py-2.5"
            >
              <span aria-hidden="true" className="shrink-0 text-base">
                🔒
              </span>
              <p className="text-xs leading-relaxed text-muted-foreground">{labels.lockedNote}</p>
            </div>
            <button
              type="button"
              disabled
              data-slot="kb-hub-raptor-upgrade-cta"
              className="w-full cursor-not-allowed rounded-md border border-border bg-muted px-4 py-2.5 font-display text-sm font-bold text-muted-foreground opacity-50"
            >
              {labels.upgradeCta}
            </button>
            {onUpgrade && (
              <div className="mt-2.5 text-center">
                <button
                  type="button"
                  onClick={onUpgrade}
                  data-slot="kb-hub-raptor-upgrade-link"
                  className="text-xs font-semibold text-warning hover:underline"
                >
                  {labels.upgradeLink} →
                </button>
              </div>
            )}
          </>
        ) : (
          <>
            {showEstimate && (
              <div
                data-slot="kb-hub-raptor-estimate"
                className="mb-3.5 flex items-center justify-between rounded-md border border-warning/20 bg-warning/8 px-3.5 py-2.5"
              >
                <div>
                  <div className="text-xs font-bold text-foreground">{labels.estimateLabel}</div>
                  <div className="text-[11px] text-muted-foreground">
                    {labels.estimateDescription.replace(
                      '{chunks}',
                      estimateChunks!.toLocaleString()
                    )}
                  </div>
                </div>
                <div className="text-right">
                  {estimatedCost && (
                    <div className="font-mono text-base font-extrabold text-warning">
                      {estimatedCost}
                    </div>
                  )}
                  {estimatedDuration && (
                    <div className="text-[10px] text-muted-foreground">{estimatedDuration}</div>
                  )}
                </div>
              </div>
            )}
            <button
              type="button"
              onClick={onRebuild}
              data-slot="kb-hub-raptor-rebuild-cta"
              // eslint-disable-next-line local/no-hardcoded-color-utility -- text-white on bg-warning matches the mockup .e-bg pattern; bg-warning is a semantic token but not in the rule's exempt list (primary/secondary/accent only). Same pattern in line 108 above.
              className="w-full rounded-md bg-warning px-4 py-2.5 font-display text-sm font-bold text-white shadow-md transition-colors hover:bg-warning/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-warning focus-visible:ring-offset-2"
            >
              {labels.rebuildCta}
            </button>
          </>
        )}
      </div>
    </section>
  );
}

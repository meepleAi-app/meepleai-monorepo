/**
 * SessionKpiGrid — Wave D.3 v2 component (Issue #756).
 *
 * 4-column desktop / 2-column mobile grid of session-level KPIs (duration,
 * player count, turns, average round time). Pure component — orchestrator
 * resolves all values + plural strings via i18n with ICU plural (Gate A).
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary-parts.jsx (KpiGrid)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.4.
 *
 * MeepleCard divergence (Gate C): KPI tiles are short-form metric tiles with
 * left-accent border and emoji + value layout. MeepleCard's avatar/title/
 * subtitle/rating composition is incompatible with this single-metric tile
 * pattern. DIVERGE.
 *
 * A11y:
 *   - `<dl>` semantic markup with `<dt>` (label) + `<dd>` (value) pairs.
 *   - Grid container `aria-label` injected via `gridAriaLabel`.
 *
 * Responsive: 2 columns < sm, 4 columns ≥ sm. Tile height auto.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

/**
 * Entity color hint for the left-accent border + value text. Maps to
 * `tokens.ts` entityColors palette. The component itself is decoupled from
 * the token module to keep the dependency graph flat.
 */
export type KpiEntityHint = 'session' | 'toolkit' | 'game' | 'agent' | 'kb' | 'chat';

const ENTITY_HSL: Record<KpiEntityHint, { full: string; alpha: string }> = {
  session: { full: 'hsl(240, 60%, 45%)', alpha: 'hsla(240, 60%, 55%, 0.22)' },
  toolkit: { full: 'hsl(142, 70%, 31%)', alpha: 'hsla(142, 70%, 31%, 0.22)' },
  game: { full: 'hsl(25, 95%, 39%)', alpha: 'hsla(25, 95%, 45%, 0.22)' },
  agent: { full: 'hsl(38, 92%, 33%)', alpha: 'hsla(38, 92%, 33%, 0.22)' },
  kb: { full: 'hsl(210, 40%, 48%)', alpha: 'hsla(210, 40%, 48%, 0.22)' },
  chat: { full: 'hsl(220, 80%, 55%)', alpha: 'hsla(220, 80%, 55%, 0.22)' },
};

export interface KpiEntry {
  /** Stable key for React reconciliation. */
  readonly key: string;
  /** Resolved label string (no template — orchestrator already plural-formatted). */
  readonly label: string;
  /** Pre-formatted value (e.g. "1h 24min", "4", "Marco"). */
  readonly value: string;
  /** Decorative emoji rendered next to the label. */
  readonly emoji: string;
  /** Color hint for left accent + value text. */
  readonly entity: KpiEntityHint;
}

export interface SessionKpiGridProps {
  readonly kpis: readonly KpiEntry[];
  /** A11y label for the grid container. */
  readonly gridAriaLabel: string;
  readonly className?: string;
}

export function SessionKpiGrid({
  kpis,
  gridAriaLabel,
  className,
}: SessionKpiGridProps): ReactElement {
  return (
    <dl
      data-slot="session-kpi-grid"
      aria-label={gridAriaLabel}
      className={clsx('grid grid-cols-2 gap-2 sm:grid-cols-4 sm:gap-3', className)}
    >
      {kpis.map(k => {
        const colors = ENTITY_HSL[k.entity];
        return (
          <div
            key={k.key}
            data-slot="session-kpi-tile"
            data-kpi-key={k.key}
            className="flex flex-col gap-1 rounded-md border bg-card p-3 sm:p-4"
            style={{
              borderColor: colors.alpha,
              borderLeft: `3px solid ${colors.full}`,
            }}
          >
            <dt className="flex items-center gap-1.5 font-mono text-[10px] font-extrabold uppercase tracking-wide text-muted-foreground">
              <span aria-hidden="true">{k.emoji}</span>
              {k.label}
            </dt>
            <dd
              className="font-display text-xl font-extrabold leading-tight tracking-tight tabular-nums sm:text-2xl"
              style={{ color: colors.full }}
            >
              {k.value}
            </dd>
          </div>
        );
      })}
    </dl>
  );
}

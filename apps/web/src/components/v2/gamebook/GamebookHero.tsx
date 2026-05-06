/**
 * GamebookHero — SP6 Phase B Task 2 v2 component (Issue #788).
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-index.jsx` (Hero).
 *
 * Pure component: all i18n strings injected via `labels` (orchestrator
 * resolves ICU plurals via next-intl `t(key, { count })` upstream — Wave D.3
 * pure-component pattern).
 *
 * Layout (mockup-derived):
 *   - Mobile (< sm): Vertical stack — kicker + title + subtitle, KPI 1-col,
 *     CTA full-width.
 *   - Desktop (sm+): Horizontal flex — title block left, CTA right-aligned;
 *     KPI grid below in 3 columns.
 *
 * Visual identity: game entity (orange) — matches gamebook=>game color
 * mapping. WCAG AA SC 1.4.3: gamebook entity color l=39% gives ~4.6:1 vs
 * white for the CTA button text.
 *
 * data-slot="gamebook-hero" for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface GamebookHeroLabels {
  /** Title heading (e.g. "I tuoi manuali" / "Your manuals"). */
  readonly title: string;
  /** Subtitle line (e.g. "Manuali fotografati pronti per il tuo tavolo"). */
  readonly subtitle: string;
  /** Singular noun under totalGamebooks KPI value (e.g. "manuali"). */
  readonly kpiTotalGamebooks: string;
  /** Singular noun under totalSessions KPI value (e.g. "questo mese"). */
  readonly kpiTotalSessions: string;
  /** Label under activeAgents KPI value (e.g. "traduzioni"). */
  readonly kpiActiveAgents: string;
  /** Primary CTA label (e.g. "+ Aggiungi manuale" / "+ Add manual"). */
  readonly ctaAddManual: string;
}

export interface GamebookHeroProps {
  readonly totalGamebooks: number;
  readonly totalSessions: number;
  readonly activeAgents: number;
  /** Click handler — orchestrator navigates to /gamebook/upload (or picker). */
  readonly onAddManualClick: () => void;
  readonly labels: GamebookHeroLabels;
  readonly className?: string;
}

// game entity from `tokens.ts` — l=39% (4.6:1 on white WCAG AA).
const GAME_HSL_SOLID = 'hsl(25, 95%, 39%)';
const GAME_HSL_HOVER = 'hsl(25, 95%, 32%)';

export function GamebookHero({
  totalGamebooks,
  totalSessions,
  activeAgents,
  onAddManualClick,
  labels,
  className,
}: GamebookHeroProps): ReactElement {
  return (
    <section
      data-slot="gamebook-hero"
      className={clsx('mx-auto max-w-[1280px] px-4 py-6 sm:px-8 sm:py-8', className)}
    >
      {/* Title row — mobile stack / desktop side-by-side */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-6">
        <div className="flex flex-col gap-1">
          <h1
            className="text-2xl font-bold tracking-tight sm:text-3xl"
            style={{ color: GAME_HSL_SOLID }}
            data-slot="gamebook-hero-title"
          >
            {labels.title}
          </h1>
          <p className="text-sm text-slate-700">{labels.subtitle}</p>
        </div>

        {/* CTA — full-width mobile, inline desktop */}
        <button
          type="button"
          onClick={onAddManualClick}
          aria-label={labels.ctaAddManual}
          data-slot="gamebook-hero-cta"
          className={clsx(
            'inline-flex h-10 w-full shrink-0 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-semibold text-white shadow-sm sm:w-auto',
            'transition-colors motion-reduce:transition-none',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
          style={{ backgroundColor: GAME_HSL_SOLID }}
          onMouseEnter={e => (e.currentTarget.style.backgroundColor = GAME_HSL_HOVER)}
          onMouseLeave={e => (e.currentTarget.style.backgroundColor = GAME_HSL_SOLID)}
        >
          <span aria-hidden="true">+</span>
          <span>{labels.ctaAddManual}</span>
        </button>
      </div>

      {/* KPI grid — 1-col mobile, 3-col desktop */}
      <dl
        data-slot="gamebook-hero-kpis"
        className="mt-5 grid grid-cols-1 gap-3 rounded-xl border border-border bg-card p-4 sm:grid-cols-3"
      >
        <KpiTile
          slot="gamebook-hero-kpi-totalGamebooks"
          value={totalGamebooks}
          label={labels.kpiTotalGamebooks}
        />
        <KpiTile
          slot="gamebook-hero-kpi-totalSessions"
          value={totalSessions}
          label={labels.kpiTotalSessions}
        />
        <KpiTile
          slot="gamebook-hero-kpi-activeAgents"
          value={activeAgents}
          label={labels.kpiActiveAgents}
        />
      </dl>
    </section>
  );
}

interface KpiTileProps {
  readonly slot: string;
  readonly value: number;
  readonly label: string;
}

function KpiTile({ slot, value, label }: KpiTileProps): ReactElement {
  return (
    <div data-slot={slot} className="flex flex-col gap-1">
      <dt className="order-2 font-mono text-[10px] font-bold uppercase tracking-wide text-slate-700">
        {label}
      </dt>
      <dd
        className="order-1 text-2xl font-extrabold leading-none tabular-nums"
        style={{ color: GAME_HSL_SOLID }}
      >
        {value}
      </dd>
    </div>
  );
}

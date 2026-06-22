/**
 * DashboardHero — Stage 3 hero block for /dashboard.
 *
 * Time-of-day greeting + 4-KPI grid (games / sessions / hoursPlayed / winRate).
 * Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx
 * (route badge, H1 display 34/24px, 4-color KPI tints, gradient bg).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface DashboardHeroLabels {
  readonly greetingMorning: string;
  readonly greetingAfternoon: string;
  readonly greetingEvening: string;
  readonly subtitle: string;
  readonly kpiGames: string;
  readonly kpiSessions: string;
  readonly kpiHours: string;
  readonly kpiWinRate: string;
}

export interface DashboardHeroKpi {
  readonly games: number;
  /** When undefined, displayed as "—". */
  readonly sessions: number | undefined;
  /** When undefined, displayed as "—". */
  readonly hoursPlayed: number | undefined;
  /** When undefined, displayed as "—". */
  readonly winRate: number | undefined;
}

export interface DashboardHeroProps {
  readonly userName: string;
  readonly kpi: DashboardHeroKpi;
  readonly labels: DashboardHeroLabels;
  readonly className?: string;
}

function selectGreeting(labels: DashboardHeroLabels): string {
  if (typeof window === 'undefined') return labels.greetingMorning; // SSR-safe default
  const hour = new Date().getHours();
  if (hour < 12) return labels.greetingMorning;
  if (hour < 18) return labels.greetingAfternoon;
  return labels.greetingEvening;
}

function formatKpi(value: number | undefined, unit = ''): string {
  if (value === undefined || !Number.isFinite(value)) return '—';
  return `${value}${unit}`;
}

interface KpiCardProps {
  readonly icon: string;
  readonly label: string;
  readonly value: string;
  readonly unit?: string;
  readonly tint: 'game' | 'session' | 'toolkit' | 'agent';
}

function KpiCard({ icon, label, value, unit, tint }: KpiCardProps): JSX.Element {
  const tintBg = {
    game: 'bg-[hsl(var(--c-game)/0.12)] text-[hsl(var(--c-game))]',
    session: 'bg-[hsl(var(--c-session)/0.12)] text-[hsl(var(--c-session))]',
    toolkit: 'bg-[hsl(var(--c-toolkit)/0.12)] text-[hsl(var(--c-toolkit))]',
    agent: 'bg-[hsl(var(--c-agent)/0.12)] text-[hsl(var(--c-agent))]',
  }[tint];

  return (
    <div
      data-slot="dashboard-kpi-card"
      className="flex items-center gap-2.5 rounded-[10px] border border-border bg-card px-3 py-2.5 sm:gap-3 sm:px-4 sm:py-3.5"
    >
      <div
        aria-hidden="true"
        className={clsx(
          'flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-base sm:h-10 sm:w-10 sm:text-xl',
          tintBg
        )}
      >
        {icon}
      </div>
      <div className="min-w-0">
        <div className="mb-0.5 font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-muted-foreground">
          {label}
        </div>
        <div className="font-quicksand text-lg font-extrabold leading-none text-foreground tabular-nums sm:text-[22px]">
          {value}
          {unit && (
            <span className="ml-0.5 text-[11px] font-semibold text-muted-foreground">{unit}</span>
          )}
        </div>
      </div>
    </div>
  );
}

export function DashboardHero({
  userName,
  kpi,
  labels,
  className,
}: DashboardHeroProps): JSX.Element {
  const greeting = selectGreeting(labels);

  const winRateValue =
    kpi.winRate !== undefined && Number.isFinite(kpi.winRate)
      ? Math.round(kpi.winRate * 100).toString()
      : '—';

  return (
    <header
      data-slot="dashboard-hero"
      className={clsx(
        'relative overflow-hidden px-4 pb-4 pt-5 sm:px-8 sm:pb-[22px] sm:pt-8',
        className
      )}
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--c-game) / 0.06) 0%, hsl(var(--c-event) / 0.05) 50%, hsl(var(--c-agent) / 0.06) 100%)',
        borderBottom: '1px solid var(--border-light)',
      }}
    >
      <div className="mb-2.5 flex items-center gap-1.5">
        <span className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-[3px] font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-[var(--text-sec)]">
          <span aria-hidden="true">⌂</span>
          Dashboard · /dashboard
        </span>
      </div>

      <h1
        className="mb-1 font-quicksand text-[24px] font-extrabold tracking-[-0.02em] text-foreground sm:text-[34px]"
        style={{ lineHeight: 1.05 }}
      >
        {greeting}, {userName} <span aria-hidden="true">👋</span>
      </h1>

      <p className="mb-4 max-w-[620px] text-[13px] leading-[1.55] text-[var(--text-sec)] sm:text-sm">
        {labels.subtitle}
      </p>

      <div
        data-slot="dashboard-hero-kpi-grid"
        className="grid grid-cols-2 gap-2 sm:max-w-[720px] sm:grid-cols-4 sm:gap-3.5"
        aria-label="KPI"
      >
        <KpiCard icon="🎲" label={labels.kpiGames} value={formatKpi(kpi.games)} tint="game" />
        <KpiCard
          icon="🎯"
          label={labels.kpiSessions}
          value={formatKpi(kpi.sessions)}
          tint="session"
        />
        <KpiCard
          icon="⏱️"
          label={labels.kpiHours}
          value={formatKpi(kpi.hoursPlayed)}
          unit={kpi.hoursPlayed !== undefined ? 'h' : undefined}
          tint="toolkit"
        />
        <KpiCard
          icon="🏆"
          label={labels.kpiWinRate}
          value={winRateValue}
          unit={kpi.winRate !== undefined ? '%' : undefined}
          tint="agent"
        />
      </div>
    </header>
  );
}

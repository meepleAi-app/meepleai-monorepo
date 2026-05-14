/**
 * DashboardHero — Stage 3 hero block for /dashboard.
 *
 * Time-of-day greeting + 4-KPI grid (games / sessions / hoursPlayed / winRate).
 * Responsive: KPI grid is 2×2 on mobile, 4×1 on desktop. Hours-played and
 * win-rate are not currently exposed by useLibraryStats — passed via `kpi`
 * prop so the orchestrator can substitute fallback "—" until backend ships.
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
  readonly tint: 'game' | 'session' | 'toolkit' | 'agent';
}

function KpiCard({ icon, label, value, tint }: KpiCardProps): JSX.Element {
  const tintClass = {
    game: 'bg-[hsl(var(--c-game)/0.12)] text-[hsl(var(--c-game))]',
    session: 'bg-[hsl(var(--c-session)/0.12)] text-[hsl(var(--c-session))]',
    toolkit: 'bg-[hsl(var(--c-toolkit)/0.12)] text-[hsl(var(--c-toolkit))]',
    agent: 'bg-[hsl(var(--c-agent)/0.12)] text-[hsl(var(--c-agent))]',
  }[tint];

  return (
    <div
      data-slot="dashboard-kpi-card"
      className="flex items-center gap-3 rounded-lg border border-border bg-card p-3 sm:p-4"
    >
      <div
        aria-hidden="true"
        className={clsx(
          'flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-xl',
          tintClass
        )}
      >
        {icon}
      </div>
      <div className="min-w-0">
        <div className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          {label}
        </div>
        <div className="font-bold font-[Quicksand] text-lg sm:text-xl text-foreground tabular-nums">
          {value}
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

  return (
    <header
      data-slot="dashboard-hero"
      className={clsx('relative overflow-hidden rounded-2xl px-4 py-5 sm:px-6 sm:py-6', className)}
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--c-game) / 0.06) 0%, hsl(var(--c-event) / 0.05) 50%, hsl(var(--c-agent) / 0.06) 100%)',
        border: '1px solid var(--border-light, rgba(180, 130, 80, 0.1))',
      }}
    >
      <div className="mb-4 sm:mb-5">
        <h1
          className="font-bold font-[Quicksand] text-xl sm:text-2xl tracking-tight text-foreground"
          style={{ lineHeight: 1.1 }}
        >
          {greeting}, {userName} <span aria-hidden="true">👋</span>
        </h1>
        <p className="mt-1 max-w-xl text-sm text-muted-foreground">{labels.subtitle}</p>
      </div>

      <div
        data-slot="dashboard-hero-kpi-grid"
        className="grid grid-cols-2 gap-3 sm:grid-cols-4 sm:max-w-3xl"
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
          value={formatKpi(kpi.hoursPlayed, 'h')}
          tint="toolkit"
        />
        <KpiCard
          icon="🏆"
          label={labels.kpiWinRate}
          value={kpi.winRate !== undefined ? `${Math.round(kpi.winRate * 100)}%` : '—'}
          tint="agent"
        />
      </div>
    </header>
  );
}

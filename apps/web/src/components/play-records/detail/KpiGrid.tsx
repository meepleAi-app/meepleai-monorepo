/**
 * KpiGrid — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * 4-column grid of KPI cards for the play record detail view.
 * KPIs: duration · top score · average · spread (max - min).
 *
 * AC-2.4: durata, top score, media, distacco max-min
 * AC-2.10 EC-6: duration=null → "—" display (InProgress/Planned records)
 *
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx` KpiGrid
 */
import type { ReactElement } from 'react';

import { entityHsl } from '@/components/ui/data-display/meeple-card';

export interface KpiGridProps {
  readonly duration: string | null;
  readonly topScore: number | null;
  readonly avgScore: number | null;
  readonly spread: number | null;
  readonly className?: string;
}

const EM_DASH = '—';

interface KpiCardData {
  slot: string;
  icon: string;
  label: string;
  value: string | number;
  entity: 'event' | 'session' | 'player' | 'game';
}

export function KpiGrid({
  duration,
  topScore,
  avgScore,
  spread,
  className,
}: KpiGridProps): ReactElement {
  const kpis: KpiCardData[] = [
    {
      slot: 'kpi-duration',
      icon: '⏱',
      label: 'Durata',
      value: duration ?? EM_DASH,
      entity: 'event',
    },
    {
      slot: 'kpi-top-score',
      icon: '🏆',
      label: 'Punteggio top',
      value: topScore !== null ? topScore : EM_DASH,
      entity: 'session',
    },
    {
      slot: 'kpi-avg',
      icon: '📊',
      label: 'Media',
      value: avgScore !== null ? avgScore : EM_DASH,
      entity: 'player',
    },
    {
      slot: 'kpi-spread',
      icon: '↔',
      label: 'Distacco',
      value: spread !== null ? spread : EM_DASH,
      entity: 'game',
    },
  ];

  return (
    <section
      data-slot="kpi-grid"
      className={`grid grid-cols-2 gap-2 sm:grid-cols-4 sm:gap-3 ${className ?? ''}`}
      role="region"
      aria-label="Statistiche partita"
    >
      {kpis.map(k => (
        <div
          key={k.slot}
          data-slot="kpi-card"
          className="flex items-center gap-2.5 rounded-lg border border-border bg-card p-3 sm:p-4"
        >
          <div
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-base sm:h-10 sm:w-10 sm:text-lg"
            style={{
              background: entityHsl(k.entity, 0.12),
              color: entityHsl(k.entity),
            }}
            aria-hidden="true"
          >
            {k.icon}
          </div>
          <div className="min-w-0">
            <div className="font-mono text-[9px] font-extrabold uppercase tracking-widest text-muted-foreground">
              {k.label}
            </div>
            <div
              data-slot={k.slot}
              className="mt-0.5 font-display text-lg font-extrabold tabular-nums leading-tight text-foreground sm:text-xl"
            >
              {k.value}
            </div>
          </div>
        </div>
      ))}
    </section>
  );
}

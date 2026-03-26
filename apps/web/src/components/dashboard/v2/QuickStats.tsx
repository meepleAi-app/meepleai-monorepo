'use client';

/**
 * QuickStats Component — Dashboard v2 "Il Tavolo"
 *
 * Glassmorphism KPI cards row showing 4 key user stats.
 */

import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface QuickStatsData {
  totalGames: number;
  monthlyPlays: number;
  weeklyPlaytime: string;
  favorites: number;
}

export interface QuickStatsProps {
  stats: QuickStatsData | null;
  loading?: boolean;
  error?: boolean;
  className?: string;
}

// ─── Config ───────────────────────────────────────────────────────────────────

const STAT_CONFIG = [
  { key: 'totalGames', label: 'Giochi' },
  { key: 'monthlyPlays', label: 'Partite / Mese' },
  { key: 'weeklyPlaytime', label: 'Tempo / Sett.' },
  { key: 'favorites', label: 'Preferiti' },
] as const;

// ─── Card Styles ──────────────────────────────────────────────────────────────

const CARD_CLASS =
  'rounded-xl p-4 text-center ' +
  'bg-[rgba(255,255,255,0.75)] backdrop-blur-[12px] ' +
  'border border-[rgba(200,180,160,0.20)] ' +
  'shadow-[0_2px_12px_rgba(180,120,60,0.06)] ' +
  'transition-all duration-200 ' +
  'hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]';

const SKELETON_CLASS = 'rounded-xl bg-[rgba(200,180,160,0.20)] animate-pulse h-[80px]';

// ─── Component ────────────────────────────────────────────────────────────────

export function QuickStats({ stats, loading, error, className }: QuickStatsProps) {
  return (
    <div
      data-testid="quick-stats"
      className={cn('grid grid-cols-2 lg:grid-cols-4 gap-3', className)}
    >
      {loading
        ? STAT_CONFIG.map(cfg => (
            <div key={cfg.key} data-testid="stat-skeleton" className={SKELETON_CLASS} />
          ))
        : STAT_CONFIG.map(cfg => {
            const value = error || !stats ? '—' : stats[cfg.key];
            return (
              <div key={cfg.key} className={CARD_CLASS}>
                <p className="font-quicksand font-bold text-[28px] text-primary leading-none">
                  {String(value)}
                </p>
                <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground mt-1.5">
                  {cfg.label}
                </p>
              </div>
            );
          })}
    </div>
  );
}

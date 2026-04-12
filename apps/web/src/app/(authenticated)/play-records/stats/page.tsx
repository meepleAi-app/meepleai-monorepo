'use client';

/**
 * Statistiche Partite — mobile-first redesign (US-32)
 *
 * - MobileHeader
 * - KPI strip orizzontale (Partite / Vittorie / Win% / Giochi unici)
 * - Top 5 giochi più giocati con barra relativa
 * - Win rate per gioco (se disponibile)
 */

import { useRouter } from 'next/navigation';

import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { usePlayerStatistics } from '@/lib/domain-hooks/usePlayRecords';
import { cn } from '@/lib/utils';

// ── KPI card ──────────────────────────────────────────────────────────────────

function KpiCard({ value, label }: { value: string | number; label: string }) {
  return (
    <div className="flex-shrink-0 flex flex-col items-center justify-center rounded-xl bg-white/5 border border-white/8 px-4 py-3 min-w-[80px]">
      <span className="text-2xl font-bold text-white font-quicksand">{value}</span>
      <span className="mt-0.5 text-[10px] font-semibold uppercase tracking-wider text-white/40 text-center leading-tight">
        {label}
      </span>
    </div>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function StatisticsPage() {
  const router = useRouter();
  const { data: stats, isLoading, error } = usePlayerStatistics();

  const winRate =
    stats && stats.totalSessions > 0
      ? Math.round((stats.totalWins / stats.totalSessions) * 100)
      : 0;

  const uniqueGames = stats ? Object.keys(stats.gamePlayCounts).length : 0;

  const topGames = stats
    ? Object.entries(stats.gamePlayCounts)
        .sort(([, a], [, b]) => b - a)
        .slice(0, 5)
    : [];

  const maxCount = topGames[0]?.[1] ?? 1;

  const avgScoreGames = stats
    ? Object.entries(stats.averageScoresByGame)
        .map(([game, avg]) => ({ game, avg: Math.round(avg) }))
        .sort((a, b) => b.avg - a.avg)
        .slice(0, 5)
    : [];

  return (
    <div className="flex flex-col min-h-full bg-[var(--gaming-bg-base)]" data-testid="stats-page">
      <MobileHeader title="Le mie statistiche" onBack={() => router.back()} />

      <div className="flex-1 px-4 pt-3 pb-12 flex flex-col gap-5">
        {/* Loading skeleton */}
        {isLoading && (
          <div className="flex flex-col gap-3" data-testid="stats-loading">
            <div className="flex gap-3 overflow-x-auto scrollbar-none pb-1">
              {[...Array(4)].map((_, i) => (
                <div
                  key={i}
                  className="flex-shrink-0 h-20 w-20 animate-pulse rounded-xl bg-white/5"
                />
              ))}
            </div>
            <div className="h-40 animate-pulse rounded-xl bg-white/5" />
            <div className="h-40 animate-pulse rounded-xl bg-white/5" />
          </div>
        )}

        {/* Error */}
        {!isLoading && error && (
          <div
            className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
            data-testid="stats-error"
          >
            {error instanceof Error ? error.message : 'Errore nel caricamento delle statistiche.'}
          </div>
        )}

        {/* Content */}
        {!isLoading && !error && stats && (
          <>
            {/* KPI Strip */}
            <div className="flex gap-3 overflow-x-auto scrollbar-none pb-1" data-testid="kpi-strip">
              <KpiCard value={stats.totalSessions} label="Partite" />
              <KpiCard value={stats.totalWins} label="Vittorie" />
              <KpiCard value={`${winRate}%`} label="Win %" />
              <KpiCard value={uniqueGames} label="Giochi" />
            </div>

            {/* Empty state */}
            {stats.totalSessions === 0 && (
              <div
                className="flex flex-col items-center gap-4 py-16 text-center"
                data-testid="stats-empty"
              >
                <span className="text-5xl">📊</span>
                <div>
                  <p className="text-base font-bold text-white">Nessuna statistica</p>
                  <p className="mt-1 text-sm text-white/40">
                    Registra partite per vedere le tue statistiche!
                  </p>
                </div>
              </div>
            )}

            {/* Top giochi più giocati */}
            {topGames.length > 0 && (
              <div className="flex flex-col gap-2" data-testid="top-games">
                <p className="text-[10px] font-semibold uppercase tracking-wider text-white/30 px-1">
                  Giochi più giocati
                </p>
                <div className="rounded-xl bg-white/5 border border-white/8 px-4 py-3 flex flex-col gap-3">
                  {topGames.map(([game, count], i) => (
                    <div key={game} className="flex flex-col gap-1">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <span className="text-xs font-bold text-white/40 w-4">{i + 1}.</span>
                          <span className="text-sm font-semibold text-white truncate max-w-[180px]">
                            {game}
                          </span>
                        </div>
                        <span className="text-xs font-bold text-white/50 ml-2 flex-shrink-0">
                          {count}x
                        </span>
                      </div>
                      <div className="ml-6 h-1.5 rounded-full bg-white/5 overflow-hidden">
                        <div
                          className="h-full rounded-full bg-amber-500/60"
                          style={{ width: `${(count / maxCount) * 100}%` }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Punteggi medi per gioco */}
            {avgScoreGames.length > 0 && (
              <div className="flex flex-col gap-2" data-testid="avg-scores">
                <p className="text-[10px] font-semibold uppercase tracking-wider text-white/30 px-1">
                  Punteggio medio
                </p>
                <div className="rounded-xl bg-white/5 border border-white/8 px-4 py-3 flex flex-col gap-2">
                  {avgScoreGames.map(({ game, avg }) => (
                    <div
                      key={game}
                      className="flex items-center justify-between py-1 border-b border-white/5 last:border-0"
                    >
                      <span className="text-sm font-semibold text-white truncate max-w-[200px]">
                        {game}
                      </span>
                      <span className="text-sm font-bold text-amber-400 ml-2 flex-shrink-0">
                        {avg} pts
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Win rate note (se abbiamo vittorie) */}
            {stats.totalWins > 0 && (
              <div
                className={cn(
                  'flex items-center gap-3 rounded-xl px-4 py-3',
                  'bg-amber-500/10 border border-amber-500/20'
                )}
                data-testid="win-rate-highlight"
              >
                <span className="text-2xl">🏆</span>
                <div>
                  <p className="text-sm font-bold text-amber-300">{winRate}% di vittorie</p>
                  <p className="text-xs text-white/40">
                    {stats.totalWins} vittorie su {stats.totalSessions} partite
                  </p>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

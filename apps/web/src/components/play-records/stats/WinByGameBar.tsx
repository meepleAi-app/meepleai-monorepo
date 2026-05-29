'use client';

import Link from 'next/link';

import { useTranslation } from '@/hooks/useTranslation';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';
import { useSharedGames } from '@/lib/play-records/useSharedGames';

interface WinByGameBarProps {
  stats: PlayerStatistics;
}

export function WinByGameBar({ stats }: WinByGameBarProps) {
  const { t } = useTranslation();

  const games = stats.winByGame || [];
  const isEmpty = games.length === 0;

  // Get game covers
  const gameIds = games.map(g => g.gameId).filter(Boolean) as string[];
  const { data: sharedGamesMap } = useSharedGames(gameIds);

  if (isEmpty) {
    return (
      <section
        className="rounded-lg border border-border bg-card p-4 md:p-5"
        data-testid="win-by-game-empty"
        aria-label={t('playRecords.stats.winByGame.title')}
      >
        <header className="mb-4 flex items-center gap-3">
          <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-entity-toolkit">
            🏆
          </div>
          <h2 className="font-display text-base font-black text-foreground md:text-lg">
            {t('playRecords.stats.winByGame.title')}
          </h2>
        </header>

        {/* Empty State */}
        <div className="flex flex-col items-center gap-3 rounded-md border border-dashed border-entity-toolkit/30 bg-muted/30 px-4 py-6 text-center">
          <div className="text-2xl" aria-hidden="true">
            🏆
          </div>
          <p className="text-xs text-muted-foreground">{t('playRecords.stats.empty.noWins')}</p>
          <Link
            href="/play-records/new"
            className="rounded-md bg-entity-toolkit px-3 py-1.5 text-[11px] font-bold text-white transition-colors hover:bg-entity-toolkit/90"
            aria-label={t('playRecords.stats.cta.newRecord')}
          >
            + {t('playRecords.stats.cta.newRecord')}
          </Link>
        </div>
      </section>
    );
  }

  // Sort by win-rate descending
  const sortedGames = [...games].sort((a, b) => {
    const rateA = a.won / a.played;
    const rateB = b.won / b.played;
    return rateB - rateA;
  });

  return (
    <section
      className="rounded-lg border border-border bg-card p-4 md:p-5"
      data-testid="win-by-game-section"
      aria-label={t('playRecords.stats.winByGame.title')}
    >
      {/* Section Header */}
      <header className="mb-4 flex items-center gap-3">
        <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-entity-toolkit">
          🏆
        </div>
        <h2 className="font-display text-base font-black text-foreground md:text-lg">
          {t('playRecords.stats.winByGame.title')}
        </h2>
        <div className="flex-1" />
        <span className="font-mono text-[10px] font-bold text-muted-foreground">
          {t('playRecords.stats.winByGame.meta')}
        </span>
      </header>

      {/* Bar Items */}
      <div className="space-y-3 md:space-y-4">
        {sortedGames.map((game, index) => {
          const sharedGame = game.gameId ? sharedGamesMap?.get(game.gameId) : null;
          const winRate = Math.round((game.won / game.played) * 100);

          return (
            <div key={game.gameId || index} className="flex items-center gap-3 md:gap-4">
              {/* Cover Emoji */}
              <div className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-xs">
                {(sharedGame as { coverEmoji?: string } | null)?.coverEmoji || '🎲'}
              </div>

              {/* Game Title + Bar */}
              <div className="min-w-0 flex-1">
                <div
                  className="flex items-baseline justify-between gap-2 mb-1"
                  data-testid="game-label"
                  aria-label={`${sharedGame?.title || game.gameName} - ${winRate}% win rate (${game.won} of ${game.played} games)`}
                >
                  <span className="truncate font-display text-sm font-bold text-foreground">
                    {sharedGame?.title || game.gameName}
                  </span>
                  <span
                    className="flex-shrink-0 font-mono text-xs font-bold text-entity-toolkit"
                    data-testid="win-rate"
                    aria-hidden="true"
                  >
                    {winRate}%{' '}
                    <span className="ml-1 text-[10px] font-semibold text-muted-foreground">
                      {game.won}/{game.played}
                    </span>
                  </span>
                </div>

                {/* Progress Bar */}
                <div
                  className="h-2 rounded-full bg-muted overflow-hidden"
                  role="progressbar"
                  aria-valuenow={winRate}
                  aria-valuemax={100}
                  aria-label={`Win rate: ${winRate}%`}
                >
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-entity-toolkit/65 to-entity-toolkit transition-all"
                    style={{ width: `${winRate}%` }}
                    data-testid="win-bar"
                    data-percent={winRate}
                  />
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

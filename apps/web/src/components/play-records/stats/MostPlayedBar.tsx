'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';

import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';
import { useSharedGames } from '@/lib/play-records/useSharedGames';

interface MostPlayedBarProps {
  stats: PlayerStatistics;
}

export function MostPlayedBar({ stats }: MostPlayedBarProps) {
  const t = useTranslations('playRecords.stats');

  const games = stats.mostPlayedGames || [];
  const isEmpty = games.length === 0;

  // Get game covers
  const gameIds = games.map(g => g.gameId).filter(Boolean) as string[];
  const { data: sharedGamesMap } = useSharedGames(gameIds);

  if (isEmpty) {
    return (
      <section
        className="rounded-lg border border-border bg-card p-4 md:p-5"
        data-testid="most-played-empty"
        aria-label={t('mostPlayed.title')}
      >
        <header className="mb-4 flex items-center gap-3">
          <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
            🎲
          </div>
          <h2 className="font-display text-base font-black text-foreground md:text-lg">
            {t('mostPlayed.title')}
          </h2>
        </header>

        {/* Empty State */}
        <div className="flex flex-col items-center gap-3 rounded-md border border-dashed border-entity-game/30 bg-muted/30 px-4 py-6 text-center">
          <div className="text-2xl" aria-hidden="true">
            🎲
          </div>
          <p className="text-xs text-muted-foreground">{t('empty.noGames')}</p>
          <Link
            href="/play-records/new"
            className="rounded-md bg-entity-game px-3 py-1.5 text-[11px] font-bold text-white transition-colors hover:bg-entity-game/90"
            aria-label={t('cta.newRecord')}
          >
            + {t('cta.newRecord')}
          </Link>
        </div>
      </section>
    );
  }

  // Find max play count for proportional bars
  const maxPlays = Math.max(...games.map(g => g.plays), 1);

  return (
    <section
      className="rounded-lg border border-border bg-card p-4 md:p-5"
      data-testid="most-played-section"
      aria-label={t('mostPlayed.title')}
    >
      {/* Section Header */}
      <header className="mb-4 flex items-center gap-3">
        <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
          🎲
        </div>
        <h2 className="font-display text-base font-black text-foreground md:text-lg">
          {t('mostPlayed.title')}
        </h2>
        <div className="flex-1" />
        <span className="font-mono text-[10px] font-bold text-muted-foreground">
          {t('mostPlayed.meta', { count: games.length })}
        </span>
      </header>

      {/* Bar Items */}
      <div className="space-y-3 md:space-y-4">
        {games.map((game, index) => {
          const sharedGame = game.gameId ? sharedGamesMap?.get(game.gameId) : null;
          const percentage = Math.round((game.plays / maxPlays) * 100);

          return (
            <div key={game.gameId || index} className="flex items-center gap-3 md:gap-4">
              {/* Rank */}
              <span className="font-mono text-[11px] font-bold text-muted-foreground w-6 flex-shrink-0 text-center">
                {index + 1}
              </span>

              {/* Cover Emoji */}
              <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-sm">
                {(sharedGame as { coverEmoji?: string } | null)?.coverEmoji || '🎲'}
              </div>

              {/* Game Title + Bar */}
              <div className="min-w-0 flex-1">
                <div className="flex items-baseline justify-between gap-2 mb-1">
                  <span
                    className="truncate font-display text-sm font-bold text-foreground"
                    data-testid="game-label"
                    aria-label={`${index + 1}. ${sharedGame?.title || game.gameName} - ${game.plays} partite`}
                  >
                    {sharedGame?.title || game.gameName}
                  </span>
                  <span
                    className="flex-shrink-0 font-mono text-xs font-bold text-entity-game"
                    data-testid="play-count"
                    aria-hidden="true"
                  >
                    {game.plays}
                    <span className="ml-1 text-[10px] font-semibold text-muted-foreground">
                      {t('mostPlayed.plays')}
                    </span>
                  </span>
                </div>

                {/* Progress Bar */}
                <div
                  className="h-2 rounded-full bg-muted overflow-hidden"
                  role="progressbar"
                  aria-valuenow={game.plays}
                  aria-valuemax={maxPlays}
                  aria-label={`${game.plays} out of ${maxPlays} plays`}
                >
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-entity-game/70 to-entity-game transition-all"
                    style={{ width: `${percentage}%` }}
                    data-testid="play-bar"
                    data-percent={percentage}
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

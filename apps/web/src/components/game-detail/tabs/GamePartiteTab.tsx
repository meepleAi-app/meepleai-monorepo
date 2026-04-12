'use client';

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Partite tab — shows play record history for the current game.
 *
 * Initial version uses the summary data already exposed by
 * `useLibraryGameDetail` (`timesPlayed`, `lastPlayed`, `recentSessions`) so S4
 * ships without a new React Query hook. A follow-up can replace the summary
 * view with a full-page list backed by the existing
 * `GetUserPlayHistoryQuery(gameId)` backend query.
 */
export function GamePartiteTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId, !isNotInLibrary);

  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-partite" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per vedere lo storico delle partite.
        </p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-partite" className={containerClass}>
        <p className="text-sm text-muted-foreground">Caricamento in corso…</p>
      </div>
    );
  }

  if (isError || !game) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-partite" className={containerClass}>
        <p className="text-sm text-destructive">Impossibile caricare lo storico partite.</p>
      </div>
    );
  }

  const recentSessions = game.recentSessions ?? [];

  return (
    <div role="tabpanel" aria-labelledby="game-tab-partite" className={containerClass}>
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        Partite
      </h3>

      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 text-sm">
        <dt className="text-muted-foreground">Partite giocate</dt>
        <dd className="font-medium text-foreground">{game.timesPlayed}</dd>
        {game.lastPlayed && (
          <>
            <dt className="text-muted-foreground">Ultima partita</dt>
            <dd className="font-medium text-foreground">
              {new Date(game.lastPlayed).toLocaleDateString('it-IT')}
            </dd>
          </>
        )}
        {game.avgDuration && (
          <>
            <dt className="text-muted-foreground">Durata media</dt>
            <dd className="font-medium text-foreground">{game.avgDuration}</dd>
          </>
        )}
        {game.winRate && (
          <>
            <dt className="text-muted-foreground">% Vittorie</dt>
            <dd className="font-medium text-foreground">{game.winRate}</dd>
          </>
        )}
      </dl>

      {recentSessions.length > 0 ? (
        <div className="flex flex-col gap-2">
          <h4 className="text-xs font-bold uppercase tracking-wide text-muted-foreground">
            Partite recenti
          </h4>
          <ul className="flex flex-col gap-2">
            {recentSessions.slice(0, 5).map(session => (
              <li
                key={session.id}
                className="flex items-center justify-between rounded-lg border border-border bg-card/50 p-3 text-sm"
              >
                <div className="flex flex-col">
                  <span className="font-medium text-foreground">
                    {new Date(session.playedAt).toLocaleDateString('it-IT')}
                  </span>
                  {session.players && (
                    <span className="text-xs text-muted-foreground">{session.players}</span>
                  )}
                </div>
                <div className="flex items-center gap-3 text-xs text-muted-foreground">
                  <span>{session.durationFormatted}</span>
                  {session.didWin != null && (
                    <span
                      className={cn(
                        'rounded-full px-2 py-0.5 font-semibold',
                        session.didWin
                          ? 'bg-secondary/20 text-secondary-foreground'
                          : 'bg-muted text-muted-foreground'
                      )}
                    >
                      {session.didWin ? 'Vinta' : 'Persa'}
                    </span>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </div>
      ) : (
        <p className="text-xs italic text-muted-foreground">
          Ancora nessuna partita registrata. Registra una sessione per iniziare.
        </p>
      )}
    </div>
  );
}

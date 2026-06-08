'use client';

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Info tab — shows game metadata, description, and library-specific info.
 * Falls back to a "not in library" empty state when gated.
 */
export function GameInfoTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId, !isNotInLibrary);

  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi questo gioco alla tua libreria per vedere tutti i dettagli.
        </p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-muted-foreground">Caricamento in corso…</p>
      </div>
    );
  }

  if (isError || !game) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-destructive">Impossibile caricare i dettagli del gioco.</p>
      </div>
    );
  }

  const playersLabel =
    game.minPlayers && game.maxPlayers
      ? game.minPlayers === game.maxPlayers
        ? `${game.minPlayers}`
        : `${game.minPlayers}–${game.maxPlayers}`
      : null;

  return (
    <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        Informazioni
      </h3>

      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 text-sm">
        {/*
          F3 #1974 (audit 2026-06-07, partial): expose Designer in the
          spec list. The mockup ships designer as a first-class metadata
          row, and `LibraryGameDetail` already carries it via the catalog
          fallback in `useLibraryGameDetail`. Hidden when the BE doesn't
          surface it (private games / non-catalog entries).
        */}
        {game.designers && game.designers.length > 0 && (
          <>
            <dt className="text-muted-foreground">Designer</dt>
            <dd className="font-medium text-foreground">
              {game.designers.map(d => d.name).join(', ')}
            </dd>
          </>
        )}
        {game.gamePublisher && (
          <>
            <dt className="text-muted-foreground">Editore</dt>
            <dd className="font-medium text-foreground">{game.gamePublisher}</dd>
          </>
        )}
        {game.gameYearPublished && (
          <>
            <dt className="text-muted-foreground">Anno</dt>
            <dd className="font-medium text-foreground">{game.gameYearPublished}</dd>
          </>
        )}
        {playersLabel && (
          <>
            <dt className="text-muted-foreground">Giocatori</dt>
            <dd className="font-medium text-foreground">{playersLabel}</dd>
          </>
        )}
        {game.playingTimeMinutes && (
          <>
            <dt className="text-muted-foreground">Durata</dt>
            <dd className="font-medium text-foreground">{game.playingTimeMinutes} min</dd>
          </>
        )}
        {game.complexityRating != null && (
          <>
            <dt className="text-muted-foreground">Complessità</dt>
            <dd className="font-medium text-foreground">{game.complexityRating.toFixed(2)} / 5</dd>
          </>
        )}
        {game.categories && game.categories.length > 0 && (
          <>
            <dt className="text-muted-foreground">Categorie</dt>
            <dd className="font-medium text-foreground">
              {game.categories.map(c => c.name).join(', ')}
            </dd>
          </>
        )}
        {game.mechanics && game.mechanics.length > 0 && (
          <>
            <dt className="text-muted-foreground">Meccaniche</dt>
            <dd className="font-medium text-foreground">
              {game.mechanics.map(m => m.name).join(', ')}
            </dd>
          </>
        )}
        {game.addedAt && (
          <>
            <dt className="text-muted-foreground">In libreria dal</dt>
            <dd className="font-medium text-foreground">
              {new Date(game.addedAt).toLocaleDateString('it-IT')}
            </dd>
          </>
        )}
      </dl>

      {/*
        F3 #1974 partial: the description block used to render naked
        below the dl. Introduce it with a small heading so it reads as
        a dedicated "Descrizione" section per the mockup; the heading
        is omitted (along with the block) when the BE doesn't surface a
        description, so games without one don't show an empty section.
      */}
      {game.description && (
        <section
          data-testid="game-info-description"
          className={cn('flex flex-col gap-2', variant === 'desktop' ? 'mt-2' : 'mt-1')}
        >
          <h4
            className={cn(
              'font-heading font-semibold text-foreground',
              variant === 'desktop' ? 'text-base' : 'text-sm'
            )}
          >
            Descrizione
          </h4>
          <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
            {game.description}
          </p>
        </section>
      )}
    </div>
  );
}

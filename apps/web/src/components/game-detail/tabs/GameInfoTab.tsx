'use client';

import { useRouter, useSearchParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Info tab — shows game metadata, description, and library-specific info.
 * Falls back to a "not in library" empty state when gated.
 *
 * #2096 M6 (sub-issue #2188): refactor from `dl/dt/dd` minimal layout to
 * 3-Card structure (Descrizione + Informazioni + House Rules CTA) using
 * shadcn/ui `Card` primitive. House Rules CTA navigates to `?tab=houseRules`
 * via App Router `router.replace` — `GameTabsPanel` `useEffect` syncs
 * `initialTab` → `activeTab`. Existing 9 unit tests preserved (DEC-8: dl
 * structure unchanged inside Card 2's `<CardContent>`).
 */
export function GameInfoTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
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

  // DEC: Card 1+2 are static content (no interactive surface) → override
  // shadcn Card default `hover:-translate-y-0.5 hover:shadow-md` so they
  // don't lift on hover. Card 3 (House Rules CTA) keeps default lift since
  // it's clickable.
  const staticCardClass = 'hover:translate-y-0 hover:shadow-sm';

  const titleSizeClass = variant === 'desktop' ? 'text-base' : 'text-sm';

  const handleOpenHouseRules = () => {
    // App Router URL nav — `GameTabsPanel` `useEffect` on `initialTab`
    // change (line 86-91, #2105 M7 review follow-up) syncs `activeTab`.
    // `scroll: false` prevents spurious scroll-to-top on tab switch.
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('tab', 'houseRules');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  return (
    <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
      {/* Card 1: Descrizione (conditional on game.description) */}
      {game.description && (
        <Card className={staticCardClass} data-testid="game-info-description">
          <CardHeader>
            <CardTitle className={titleSizeClass}>Descrizione</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
              {game.description}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Card 2: Informazioni (specs grid dl preservato per test stability) */}
      <Card className={staticCardClass}>
        <CardHeader>
          <CardTitle className={titleSizeClass}>Informazioni</CardTitle>
        </CardHeader>
        <CardContent>
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
                <dd className="font-medium text-foreground">
                  {game.complexityRating.toFixed(2)} / 5
                </dd>
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
        </CardContent>
      </Card>

      {/* Card 3: House Rules CTA (default lift, always visible) */}
      <Card>
        <CardContent className="flex items-center justify-between gap-4 p-6">
          <div className="flex-1">
            <h4 className="font-heading font-semibold text-foreground">
              House Rules personalizzate
            </h4>
            <p className="text-sm text-muted-foreground">
              Aggiungi varianti e regole della casa per questo gioco.
            </p>
          </div>
          <Button variant="outline" onClick={handleOpenHouseRules}>
            Apri House Rules →
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

'use client';

import { FileText, Play } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card/types';
import { Button } from '@/components/ui/primitives/button';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

interface FocusedGameCardProps {
  game: LibraryGameDetail;
  onOpenDrawer: () => void;
  onStartSession: () => void;
}

/**
 * Mobile hero card + "Dettagli" CTA button.
 *
 * Renders the game as a MeepleCard hero variant and exposes a prominent
 * button that opens the `GameDetailsDrawer`. Designed to fill the viewport
 * except for the MobileActionBar at the bottom.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.5
 */
export function FocusedGameCard({ game, onOpenDrawer, onStartSession }: FocusedGameCardProps) {
  const metadata: MeepleCardMetadata[] = [];
  if (game.gameYearPublished) {
    metadata.push({ label: String(game.gameYearPublished) });
  }
  if (game.minPlayers && game.maxPlayers) {
    metadata.push({
      label:
        game.minPlayers === game.maxPlayers
          ? `${game.minPlayers} giocatori`
          : `${game.minPlayers}-${game.maxPlayers} giocatori`,
    });
  }
  if (game.playingTimeMinutes) {
    metadata.push({ label: `${game.playingTimeMinutes} min` });
  }

  return (
    <div className="flex h-full flex-col gap-4 p-4" data-testid="focused-game-card">
      <div className="flex-1">
        <MeepleCard
          entity="game"
          variant="hero"
          title={game.gameTitle}
          subtitle={game.gamePublisher ?? undefined}
          imageUrl={game.gameImageUrl ?? undefined}
          rating={game.averageRating ?? undefined}
          metadata={metadata.length > 0 ? metadata : undefined}
          data-testid="focused-meeple-card"
        />
      </div>

      <div className="flex gap-2">
        <Button
          type="button"
          onClick={onStartSession}
          size="lg"
          className="flex-1 gap-2 bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)]"
          data-testid="focused-game-start-button"
        >
          <Play className="h-4 w-4" />
          Avvia Partita
        </Button>
        <Button
          type="button"
          onClick={onOpenDrawer}
          size="lg"
          variant="outline"
          className="gap-2"
          data-testid="focused-game-details-button"
        >
          <FileText className="h-4 w-4" />
          Dettagli
        </Button>
      </div>
    </div>
  );
}

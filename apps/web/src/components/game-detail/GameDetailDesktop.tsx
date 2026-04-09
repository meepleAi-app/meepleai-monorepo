'use client';

import { SplitViewLayout } from '@/components/layout/SplitViewLayout/SplitViewLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card/types';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

import { GameTabsPanel } from './GameTabsPanel';

import type { GameTabId } from './tabs';

interface GameDetailDesktopProps {
  gameId: string;
  initialTab?: GameTabId;
  onTabChange?: (tab: GameTabId) => void;
  isPrivateGame?: boolean;
}

/**
 * Desktop variant of the game detail page.
 *
 * Uses the existing SplitViewLayout with:
 *  - list  (left):  MeepleCard hero for the selected game
 *  - detail (right): GameTabsPanel with 5 tabs (Info / AI Chat / Toolbox / House Rules / Partite)
 *
 * Note: `SplitViewLayout` uses preset `listRatio` ('narrow' | 'balanced' | 'wide').
 * `listRatio="wide"` renders a ~50/50 split without adding drag-to-resize logic.
 * If resizable layout is required in the future, extend SplitViewLayout directly.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4
 */
export function GameDetailDesktop({
  gameId,
  initialTab,
  onTabChange,
  isPrivateGame,
}: GameDetailDesktopProps) {
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId);

  if (isLoading) {
    return (
      <div
        className="flex items-center justify-center p-12 text-sm text-muted-foreground"
        data-testid="game-detail-desktop-loading"
      >
        Caricamento in corso…
      </div>
    );
  }

  const isNotInLibrary = !game;

  if (isError) {
    return (
      <div className="p-6 text-sm text-destructive" data-testid="game-detail-desktop-error">
        Impossibile caricare il gioco.
      </div>
    );
  }

  const heroMetadata: MeepleCardMetadata[] = [];
  if (game?.gameYearPublished) {
    heroMetadata.push({ label: String(game.gameYearPublished) });
  }
  if (game?.minPlayers && game?.maxPlayers) {
    const players =
      game.minPlayers === game.maxPlayers
        ? `${game.minPlayers} giocatori`
        : `${game.minPlayers}-${game.maxPlayers} giocatori`;
    heroMetadata.push({ label: players });
  }
  if (game?.playingTimeMinutes) {
    heroMetadata.push({ label: `${game.playingTimeMinutes} min` });
  }

  const heroCard = (
    <MeepleCard
      entity="game"
      variant="hero"
      title={game?.gameTitle ?? 'Gioco non in libreria'}
      subtitle={game?.gamePublisher ?? undefined}
      imageUrl={game?.gameImageUrl ?? undefined}
      rating={game?.averageRating ?? undefined}
      metadata={heroMetadata.length > 0 ? heroMetadata : undefined}
      data-testid="game-detail-hero-card"
    />
  );

  const tabsPanel = (
    <GameTabsPanel
      gameId={gameId}
      initialTab={initialTab}
      onTabChange={onTabChange}
      isPrivateGame={isPrivateGame}
      isNotInLibrary={isNotInLibrary}
    />
  );

  return (
    <div data-testid="game-detail-desktop" className="h-full">
      <SplitViewLayout
        list={heroCard}
        detail={tabsPanel}
        listRatio="wide"
        listLabel="Carta del gioco"
        detailLabel="Strumenti e informazioni"
      />
    </div>
  );
}

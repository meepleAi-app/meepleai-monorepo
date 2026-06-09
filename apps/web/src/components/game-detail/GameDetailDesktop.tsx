'use client';

import { useState } from 'react';

import { CustomCoverDialog } from '@/components/features/library/custom-cover/CustomCoverDialog';
import { EditCoverOverlay } from '@/components/features/library/custom-cover/EditCoverOverlay';
import { SplitViewLayout } from '@/components/layout/SplitViewLayout/SplitViewLayout';
import { ConnectionBar, buildGameConnections } from '@/components/ui/data-display/connection-bar';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card/types';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useConnectionBarNav } from '@/hooks/useConnectionBarNav';

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
  const { handlePipClick } = useConnectionBarNav(gameId);
  const [coverDialogOpen, setCoverDialogOpen] = useState(false);

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

  if (isError) {
    return (
      <div className="p-6 text-sm text-destructive" data-testid="game-detail-desktop-error">
        Impossibile caricare il gioco.
      </div>
    );
  }

  const isNotInLibrary = !game;

  // F3 #1974 (audit 2026-06-07, partial): extend the hero meta strip with
  // designer + complexity entries so the live page surfaces the same
  // identifier strip the mockup ships (sp4-game-detail.jsx — "designer ·
  // anno · durata · players · complessità · rating ★"). Each entry is
  // additive and skipped when the BE doesn't surface the field (catalog
  // fallback may omit designers; private games never expose complexity).
  // Ordering follows the mockup so the strip reads left → right as a
  // scannable identity row.
  const heroMetadata: MeepleCardMetadata[] = [];
  if (game?.designers && game.designers.length > 0) {
    const primary = game.designers[0]?.name;
    if (primary) heroMetadata.push({ label: primary });
  }
  if (game?.gameYearPublished) {
    heroMetadata.push({ label: String(game.gameYearPublished) });
  }
  // `playingTimeMinutes` matches the TS `LibraryGameDetail` interface
  // (apps/web/src/hooks/queries/useLibrary.ts:803). The raw API JSON uses
  // `playTimeMinutes`, but the useLibraryGameDetail hook renames the field
  // during DTO mapping — so the TS surface is `playingTimeMinutes`. A previous
  // attempted "fix" to `playTimeMinutes` here matched the wire shape instead
  // of the typed surface and silently dropped the entry.
  if (game?.playingTimeMinutes) {
    heroMetadata.push({ label: `${game.playingTimeMinutes} min` });
  }
  if (game?.minPlayers && game?.maxPlayers) {
    const players =
      game.minPlayers === game.maxPlayers
        ? `${game.minPlayers} giocatori`
        : `${game.minPlayers}-${game.maxPlayers} giocatori`;
    heroMetadata.push({ label: players });
  }
  if (game?.complexityRating != null) {
    // BGG weight comes in [1, 5] — one decimal is enough for the strip.
    heroMetadata.push({ label: `Complessità ${game.complexityRating.toFixed(1)}` });
  }

  const gameConnections = game
    ? buildGameConnections({
        agentCount: 0,
        kbCount: game.hasCustomPdf || game.hasRagAccess ? 1 : 0,
        chatCount: 0,
        sessionCount: game.timesPlayed ?? 0,
      })
    : [];

  const hasCustomCover = Boolean(game?.customCoverR2Key);

  const listContent = (
    <div className="flex flex-col gap-3">
      <div className="group relative">
        <MeepleCard
          entity="game"
          variant="hero"
          title={game?.gameTitle ?? 'Gioco non in libreria'}
          // Mockup parity: SP3 GameHero shows a "🎲 Gioco" pill above the
          // title so the reader scans entity type first.
          showEntityLabel
          entityLabel="Gioco"
          // Required so HeroCard.shouldRenderRichPlaceholder evaluates true
          // for catalog games whose BGG image is rejected by shouldUsePlaceholder
          // (#1822 — runtime BGG URL allow-list). Without an id the fallback
          // collapses to a generic entity-icon and the live hero looks empty.
          id={game?.gameId ?? gameId}
          subtitle={
            game?.gamePublisher && game.gamePublisher.length > 0
              ? game.gamePublisher
              : undefined
          }
          imageUrl={game?.gameImageUrl ?? undefined}
          rating={game?.averageRating ?? undefined}
          // BGG averageRating is on a 0–10 scale (e.g. Catan 7.09).
          // Without `ratingMax`, MeepleCard defaults to /5 and renders all 5
          // stars filled for any rating ≥5 — visually wrong.
          ratingMax={10}
          metadata={heroMetadata.length > 0 ? heroMetadata : undefined}
          data-testid="game-detail-hero-card"
        />
        {game && (
          <EditCoverOverlay
            onEditClick={() => setCoverDialogOpen(true)}
            hasCustomCover={hasCustomCover}
          />
        )}
      </div>
      <ConnectionBar connections={gameConnections} onPipClick={handlePipClick} />
    </div>
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
        list={listContent}
        detail={tabsPanel}
        listRatio="wide"
        listLabel="Carta del gioco"
        detailLabel="Strumenti e informazioni"
      />
      {game && (
        <CustomCoverDialog
          gameId={game.gameId}
          open={coverDialogOpen}
          onClose={() => setCoverDialogOpen(false)}
          hasCustomCover={hasCustomCover}
        />
      )}
    </div>
  );
}

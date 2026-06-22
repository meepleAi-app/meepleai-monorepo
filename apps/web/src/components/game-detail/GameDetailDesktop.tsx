'use client';

import { useState } from 'react';

import { CustomCoverDialog } from '@/components/features/library/custom-cover/CustomCoverDialog';
import { EditCoverOverlay } from '@/components/features/library/custom-cover/EditCoverOverlay';
import { SessionContributorsStrip } from '@/components/features/library/SessionContributorsStrip';
import { ConnectionBar, buildGameConnections } from '@/components/ui/data-display/connection-bar';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card/types';
import { useGameSessionContributors } from '@/hooks/queries/useGameSessionContributors';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useConnectionBarNav } from '@/hooks/useConnectionBarNav';

import { GameHero, type GameHeroMetaItem } from './GameHero';
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
 * #2105 M7 (Epic #2096) — Layout restructure. Switched from SplitView 50/50
 * (hero left-column 50% + vertical-rail tabs right-column 50%) to a vertical
 * full-width stack matching mockup `sp3-shared-game-detail`:
 *
 *   1. Hero          — full-width 100% (240px tall)
 *   2. Strip         — connection-bar + session contributors (full-width)
 *   3. Tabs panel    — horizontal underline tabs + scrollable content
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4
 * Mockup:    admin-mockups/design_files/sp3-shared-game-detail.jsx
 */
export function GameDetailDesktop({
  gameId,
  initialTab,
  onTabChange,
  isPrivateGame,
}: GameDetailDesktopProps) {
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId);
  const { handlePipClick } = useConnectionBarNav(gameId);
  // #2036: Top session contributors strip. Public endpoint — runs even when
  // the user hasn't actually added the game to their library yet (the share
  // surfaces social proof before deciding to play). `enabled` gates on gameId
  // being present so the hook doesn't fire with the empty-string sentinel.
  const { data: contributors = [] } = useGameSessionContributors(gameId, 8, !!gameId);
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

  // #2034: agentCount + chatCount now flow from /library/{gameId} instead of
  // being hardcoded zeros. `agentCount` is cross-user (AgentDefinitions linked
  // to this SharedGame); `chatThreadCount` is the requesting user's own
  // threads for the game. Both default to 0 when the BE field is undefined
  // (legacy response shapes).
  const gameConnections = game
    ? buildGameConnections({
        agentCount: game.agentCount ?? 0,
        kbCount: game.hasCustomPdf || game.hasRagAccess ? 1 : 0,
        chatCount: game.chatThreadCount ?? 0,
        sessionCount: game.timesPlayed ?? 0,
      })
    : [];

  const hasCustomCover = Boolean(game?.customCoverR2Key);

  // #2100 M1: GameHero v2 — mockup parity con cover gradient overlay + title
  // bottom-anchor. Sostituisce MeepleCard hero precedente (#2079 F1 closed).
  // Append rating come ultimo entry meta (parity con mockup "★ 7.7" footer).
  const heroMetaItems: GameHeroMetaItem[] = heroMetadata.map(m => ({ label: m.label }));
  if (game?.averageRating != null) {
    heroMetaItems.push({ label: `★ ${game.averageRating.toFixed(1)}` });
  }

  return (
    <div data-testid="game-detail-desktop" className="flex h-full flex-col gap-4 px-4 py-4">
      {/* 1. Hero — full-width 240px */}
      <div className="group relative">
        <GameHero
          title={game?.gameTitle ?? 'Gioco non in libreria'}
          variant={isNotInLibrary ? 'community' : 'own'}
          imageUrl={game?.gameImageUrl ?? undefined}
          metadata={heroMetaItems.length > 0 ? heroMetaItems : undefined}
          className="rounded-2xl"
        />
        {game && (
          <EditCoverOverlay
            onEditClick={() => setCoverDialogOpen(true)}
            hasCustomCover={hasCustomCover}
          />
        )}
      </div>

      {/* 2. Strip — connection-bar + session contributors */}
      <div className="flex flex-col gap-3">
        <ConnectionBar connections={gameConnections} onPipClick={handlePipClick} />
        {/* #2036: SP3 mockup parity — avatar strip of past players. Strip
            renders nothing when the contributor list is empty (no recorded
            finalized sessions for this game), so it never claims layout
            space on freshly-added games. */}
        <SessionContributorsStrip contributors={contributors} />
      </div>

      {/* 3. Tabs panel — horizontal tabs + content */}
      <div className="min-h-0 flex-1">
        <GameTabsPanel
          gameId={gameId}
          initialTab={initialTab}
          onTabChange={onTabChange}
          isPrivateGame={isPrivateGame}
          isNotInLibrary={isNotInLibrary}
        />
      </div>

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

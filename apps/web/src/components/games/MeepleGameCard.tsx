/**
 * MeepleGameCard - Game Catalog adapter using MeepleCard
 * Issue #4041 - Cleanup legacy GameCard wrapper
 *
 * Adapter for Game type (from /api/v1/games catalog) using MeepleCard.
 * Different from MeepleGameCatalogCard which uses SharedGame type.
 *
 * @example
 * ```tsx
 * <MeepleGameCard
 *   game={catalogGame}
 *   variant="grid"
 *   onClick={(id) => router.push(`/games/${id}`)}
 * />
 * ```
 */

'use client';

import { useState, useCallback } from 'react';

import { Users, Clock } from 'lucide-react';

import { AgentCreationSheet } from '@/components/agent/config';
import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { MeepleCard, type MeepleCardVariant, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { useEntityActions } from '@/hooks/use-entity-actions';
import { getNavigationLinks } from '@/config/entity-navigation';
import type { Game } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface MeepleGameCardProps {
  /** Game data from catalog API */
  game: Game;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Click handler */
  onClick?: (gameId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Format player count range
 */
function formatPlayerCount(min: number | null, max: number | null): string {
  if (min === null && max === null) return 'N/A';
  if (min === max) return `${min}`;
  return `${min || '?'}–${max || '?'}`;
}

/**
 * Format play time range
 */
function formatPlayTime(min: number | null, max: number | null): string {
  if (min === null && max === null) return 'N/A';
  const minTime = min || 0;
  const maxTime = max || 0;
  if (minTime === maxTime) return `${minTime}m`;
  return `${minTime}–${maxTime}m`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleGameCard({
  game,
  variant = 'grid',
  onClick,
  className,
}: MeepleGameCardProps) {
  // Issue #4777: Agent creation sheet state
  const [agentSheetOpen, setAgentSheetOpen] = useState(false);
  const handleCreateAgent = useCallback(() => setAgentSheetOpen(true), []);

  // Issue #4822: Open wizard instead of direct add
  const { openWizard } = useAddGameWizard();
  const handleAddToCollection = useCallback(() => {
    openWizard(
      { type: 'fromGameCard', sharedGameId: game.id },
      {
        gameId: game.id,
        title: game.title,
        imageUrl: game.imageUrl || undefined,
        minPlayers: game.minPlayers ?? undefined,
        maxPlayers: game.maxPlayers ?? undefined,
        playingTimeMinutes: game.minPlayTimeMinutes ?? undefined,
        yearPublished: game.yearPublished ?? undefined,
        source: 'catalog',
      },
    );
  }, [openWizard, game]);

  // Issue #4041: Entity-specific quick actions
  const entityActions = useEntityActions({
    entity: 'game',
    id: game.id,
    onCreateAgent: handleCreateAgent,
    onAddToCollection: handleAddToCollection,
  });

  // Build metadata
  const metadata: MeepleCardMetadata[] = [];

  const playerCount = formatPlayerCount(game.minPlayers, game.maxPlayers);
  if (playerCount !== 'N/A') {
    metadata.push({ icon: Users, value: playerCount });
  }

  const playTime = formatPlayTime(game.minPlayTimeMinutes, game.maxPlayTimeMinutes);
  if (playTime !== 'N/A') {
    metadata.push({ icon: Clock, value: playTime });
  }

  // Build subtitle with publisher and year
  const subtitleParts: string[] = [];
  if (game.publisher) subtitleParts.push(game.publisher);
  if (game.yearPublished) subtitleParts.push(String(game.yearPublished));
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : undefined;

  return (
    <>
      <MeepleCard
        id={game.id}
        entity="game"
        variant={variant}
        title={game.title}
        subtitle={subtitle}
        imageUrl={game.imageUrl || undefined}
        rating={game.averageRating || undefined}
        ratingMax={10}
        metadata={metadata}
        onClick={onClick ? () => onClick(game.id) : undefined}
        className={className}
        // Issue #4041: Quick actions + Info button
        entityQuickActions={entityActions.quickActions}
        showInfoButton
        infoHref={`/games/${game.id}`}
        infoTooltip="Vai al dettaglio"
        // Epic #4688: Navigation footer
        navigateTo={getNavigationLinks('game', { id: game.id })}
        // Issue #4777, #4999: Agent action footer
        // Catalog context: show "Aggiungi" CTA via !hasKb + onAddToCollection
        hasAgent={false}
        hasKb={false}
        onAddToCollection={handleAddToCollection}
        onCreateAgent={handleCreateAgent}
        data-testid={`game-card-${game.id}`}
      />

      {/* Issue #4777: Agent creation wizard */}
      <AgentCreationSheet
        isOpen={agentSheetOpen}
        onClose={() => setAgentSheetOpen(false)}
        initialGameId={game.id}
        initialGameTitle={game.title}
      />
    </>
  );
}

export default MeepleGameCard;

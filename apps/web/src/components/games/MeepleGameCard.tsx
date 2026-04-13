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

import { AgentCreationSheet } from '@/components/agent/config';
import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { useEntityActions } from '@/hooks/useEntityActions';
import type { Game } from '@/lib/api';
import { buildGameCardProps } from '@/lib/card-mappers';

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
      }
    );
  }, [openWizard, game]);

  // Issue #4041: Entity-specific quick actions
  const entityActions = useEntityActions({
    entity: 'game',
    id: game.id,
    entityName: game.title,
    onCreateAgent: handleCreateAgent,
    onAddToCollection: handleAddToCollection,
  });

  // Build card props from mapper
  const mapperProps = buildGameCardProps(game);

  // Map QuickAction (icon: LucideIcon) → MeepleCardAction (icon: ReactNode)
  const cardActions = entityActions.quickActions
    .filter(a => !a.hidden)
    .map(a => ({
      icon: <a.icon className="h-4 w-4" />,
      label: a.label,
      onClick: a.onClick,
      disabled: a.disabled,
      variant: a.destructive ? ('danger' as const) : ('default' as const),
    }));

  return (
    <>
      <MeepleCard
        id={game.id}
        entity="game"
        variant={variant}
        title={game.title}
        subtitle={mapperProps.subtitle}
        imageUrl={mapperProps.imageUrl}
        rating={mapperProps.rating}
        ratingMax={mapperProps.ratingMax}
        onClick={onClick ? () => onClick(game.id) : undefined}
        className={className}
        actions={cardActions}
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

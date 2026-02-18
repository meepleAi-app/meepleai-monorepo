/**
 * MeepleLibraryGameCard - Library adapter using MeepleCard
 * Issue #4045 - Integrate MeepleCard in Library pages
 *
 * Adapter component that wraps MeepleCard for UserLibrary usage.
 * Replaces legacy UserGameCard (~400 lines) with MeepleCard-based implementation.
 *
 * Provides:
 * - MeepleCard entity="game" with library-specific features
 * - Quick actions: Chat, Configure Agent, Upload PDF, Edit Notes, Remove
 * - Favorite toggle (heart icon)
 * - Bulk selection checkbox
 * - Status badges (Owned, Wishlist, In Prestito, Nuovo)
 * - Agent configuration and PDF status indicators
 * - Grid/List view modes
 *
 * @example
 * ```tsx
 * <MeepleLibraryGameCard
 *   game={libraryEntry}
 *   variant="grid"
 *   onConfigureAgent={handleConfigureAgent}
 *   onUploadPdf={handleUploadPdf}
 *   onEditNotes={handleEditNotes}
 *   onRemove={handleRemove}
 *   onAskAgent={handleAskAgent}
 *   selectable={selectionMode}
 *   selected={isSelected}
 *   onSelect={handleSelect}
 * />
 * ```
 */

'use client';

import { useState } from 'react';

import {
  MessageCircle,
  Settings,
  Upload,
  Edit2,
  Trash2,
  Bot,
  Heart,
  Gamepad2,
  Trophy,
} from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { MeepleCard, type MeepleCardVariant, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardFlipData } from '@/components/ui/data-display/meeple-card-features/FlipCard';
import { useAgentConfig, useToggleLibraryFavorite } from '@/hooks/queries';
import { getNavigationLinks } from '@/config/entity-navigation';
import type { UserLibraryEntry, GameStateType } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface MeepleLibraryGameCardProps {
  /** User library entry data */
  game: UserLibraryEntry;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Configure agent callback */
  onConfigureAgent: (gameId: string, gameTitle: string) => void;
  /** Upload PDF callback */
  onUploadPdf: (gameId: string, gameTitle: string) => void;
  /** Edit notes callback */
  onEditNotes: (gameId: string, gameTitle: string, currentNotes?: string | null) => void;
  /** Remove game callback */
  onRemove: (gameId: string, gameTitle: string) => void;
  /** Ask AI Agent callback */
  onAskAgent: (gameId: string) => void;
  /** Change game state callback */
  onChangeState?: (gameId: string, gameTitle: string, newState: GameStateType) => void;
  /** Share game callback */
  onShare?: (gameId: string, gameTitle: string) => void;
  /** Selection mode enabled */
  selectionMode?: boolean;
  /** Card is selected */
  isSelected?: boolean;
  /** Selection callback */
  onSelect?: (gameId: string, shiftKey: boolean) => void;
  /** Enable flip card with back content */
  flippable?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Map game state to MeepleCard status format
 */
function mapGameStateToStatus(state: GameStateType | null | undefined): 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade' | undefined {
  if (!state) return undefined;

  const stateMap: Record<GameStateType, 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade'> = {
    Owned: 'owned',
    Wishlist: 'wishlisted',
    Nuovo: 'owned', // New items are owned
    InPrestito: 'borrowed',
  };

  // eslint-disable-next-line security/detect-object-injection -- state is validated enum type
  return stateMap[state];
}

/**
 * Format play count
 */
function formatPlayCount(count: number): string {
  if (count === 0) return 'Mai giocato';
  if (count === 1) return '1 partita';
  return `${count} partite`;
}

/**
 * Format win rate
 */
function formatWinRate(rate: number | null | undefined): string {
  if (rate === null || rate === undefined) return 'N/A';
  return `${Math.round(rate * 100)}% vittorie`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleLibraryGameCard({
  game,
  variant = 'grid',
  onConfigureAgent,
  onUploadPdf,
  onEditNotes,
  onRemove,
  onAskAgent,
  onChangeState: _onChangeState,
  onShare: _onShare,
  selectionMode = false,
  isSelected = false,
  onSelect,
  flippable,
  className,
}: MeepleLibraryGameCardProps) {
  const [isTogglingFavorite, setIsTogglingFavorite] = useState(false);

  // Fetch agent configuration status
  const { data: agentConfig } = useAgentConfig(game.gameId, true);
  const agentConfigured = agentConfig !== null;
  const agentModel = agentConfig?.modelType || 'default';

  // Favorite toggle mutation
  const toggleFavoriteMutation = useToggleLibraryFavorite();

  // Map model type to display name
  const modelDisplayName: Record<string, string> = {
    'llama-3.3-70b-free': 'Llama Free',
    'google-gemini-pro': 'Gemini Pro',
    'deepseek-chat': 'DeepSeek',
    'llama-3.3-70b': 'Llama Pro',
    default: 'Default',
  };

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleToggleFavorite = async () => {
    if (isTogglingFavorite) return;

    setIsTogglingFavorite(true);
    try {
      await toggleFavoriteMutation.mutateAsync({
        gameId: game.gameId,
        isFavorite: !game.isFavorite,
      });
      toast.success(
        `${game.gameTitle} ${!game.isFavorite ? 'aggiunto ai' : 'rimosso dai'} preferiti`
      );
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Errore durante l\'aggiornamento dei preferiti'
      );
    } finally {
      setIsTogglingFavorite(false);
    }
  };

  const handleSelect = (id: string, _selected: boolean) => {
    if (onSelect) {
      onSelect(id, false); // shiftKey handled by parent
    }
  };

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    {
      icon: MessageCircle,
      label: 'Chat con Agent',
      onClick: () => {
        window.location.href = `/chat/new?game=${game.gameId}`;
      },
      hidden: !game.hasPdfDocuments,
    },
    {
      icon: Settings,
      label: 'Configura Agent',
      onClick: () => onConfigureAgent(game.gameId, game.gameTitle),
    },
    {
      icon: Upload,
      label: 'Carica PDF',
      onClick: () => onUploadPdf(game.gameId, game.gameTitle),
    },
    {
      icon: Edit2,
      label: 'Modifica Note',
      onClick: () => onEditNotes(game.gameId, game.gameTitle, game.notes),
    },
    {
      icon: Heart,
      label: game.isFavorite ? 'Rimuovi dai Preferiti' : 'Aggiungi ai Preferiti',
      onClick: handleToggleFavorite,
      disabled: isTogglingFavorite,
    },
    {
      icon: Bot,
      label: 'Chiedi all\'Agent',
      onClick: () => onAskAgent(game.gameId),
      disabled: !game.hasPdfDocuments,
      hidden: !game.hasPdfDocuments,
    },
    {
      icon: Trash2,
      label: 'Rimuovi dalla Libreria',
      onClick: () => onRemove(game.gameId, game.gameTitle),
    },
  ];

  // ============================================================================
  // Metadata Configuration
  // ============================================================================

  const metadata: MeepleCardMetadata[] = [
    { icon: Gamepad2, value: formatPlayCount(0) }, // TODO: Add play count from game data
    { icon: Trophy, value: formatWinRate(null) }, // TODO: Add win rate from game data
  ];

  // Add agent status if configured
  if (agentConfigured) {
    metadata.push({
      icon: Bot,
      // eslint-disable-next-line security/detect-object-injection -- agentModel is validated enum
      value: `Agent: ${modelDisplayName[agentModel]}`,
    });
  }

  // Add PDF status if documents available
  if (game.hasPdfDocuments) {
    metadata.push({
      label: '📄 PDF',
    });
  }

  // ============================================================================
  // Build Props
  // ============================================================================

  const subtitle = game.gamePublisher || `Aggiunto il ${new Date(game.addedAt).toLocaleDateString('it-IT')}`;

  const mappedStatus = mapGameStateToStatus(game.currentState);

  // Badge: Show favorite if applicable
  const badge = game.isFavorite ? '❤️ Preferito' : undefined;

  // Flip data
  const flipData: MeepleCardFlipData | undefined = flippable && game.notes
    ? {
        description: game.notes,
      }
    : undefined;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title={game.gameTitle}
      subtitle={subtitle}
      imageUrl={game.gameImageUrl || undefined}
      rating={undefined} // UserLibraryEntry doesn't have gameRating field
      ratingMax={10}
      metadata={metadata}
      badge={badge}
      status={mappedStatus}
      onClick={selectionMode && onSelect ? undefined : () => window.location.href = `/library/games/${game.gameId}`}
      flippable={flippable && !!game.notes}
      flipData={flipData}
      flipTrigger="button"
      className={className}
      // Epic #4688: Navigation footer
      navigateTo={getNavigationLinks('game', { id: game.gameId })}
      data-testid={`library-game-card-${game.gameId}`}
      // Issue #4045: Quick actions + Info button
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={`/library/games/${game.gameId}`}
      infoTooltip="Vai al dettaglio"
      // Bulk selection
      selectable={selectionMode}
      selected={isSelected}
      onSelect={handleSelect}
    />
  );
}

/**
 * MeepleLibraryGameCard Skeleton for loading state
 */
export function MeepleLibraryGameCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title=""
      loading
      data-testid="library-game-card-skeleton"
    />
  );
}

export default MeepleLibraryGameCard;

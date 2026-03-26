/**
 * MeepleUserLibraryCard - User Library Card using MeepleCard
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Adapter component that wraps MeepleCard for UserLibrary usage.
 * Mirrors MeepleGameCatalogCard visually and functionally, with actions
 * contextually relevant to the user's private library.
 *
 * Features:
 * - imageUrl full-size (not thumbnailUrl)
 * - Flip card with lazy-fetched SharedGameDetail (description, categories, mechanics)
 * - Quick actions: Avvia Sessione, Rimuovi dalla Libreria, Condividi
 * - Footer navigateTo: KB / Agents / Chats / Sessions
 * - Info button → /library/games/{id}
 *
 * @example
 * ```tsx
 * <MeepleUserLibraryCard
 *   game={userGameDto}
 *   variant="grid"
 *   onClick={(id) => router.push(`/library/games/${id}`)}
 * />
 * ```
 */

'use client';

import { useState, useCallback } from 'react';

import { Clock, Play, RotateCcw, Share2, Trash2, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  MeepleCard,
  type MeepleCardVariant,
  type MeepleEntityType,
} from '@/components/ui/data-display/meeple-card';
import type { MeepleCardFlipData } from '@/components/ui/data-display/meeple-card-features/FlipCard';
import { useSharedGame } from '@/hooks/queries';
import { useRemoveGameFromLibrary } from '@/hooks/queries';
import type { UserGameDto } from '@/lib/api/dashboard-client';

import { AgentDrawerSheet } from './AgentDrawerSheet';
import { ChatDrawerSheet } from './ChatDrawerSheet';
import { KbDrawerSheet } from './KbDrawerSheet';
import { SessionDrawerSheet } from './SessionDrawerSheet';

// ============================================================================
// Types
// ============================================================================

export interface MeepleUserLibraryCardProps {
  /** User library game data */
  game: UserGameDto;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Click handler for card navigation */
  onClick?: (gameId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function formatPlaytime(minutes: number | null | undefined): string {
  if (!minutes) return 'N/A';
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleUserLibraryCard({
  game,
  variant = 'grid',
  onClick,
  className,
}: MeepleUserLibraryCardProps) {
  const router = useRouter();

  // Lazy-load full game detail for flip card.
  // fetchDetail becomes true only when the user first clicks the flip button.
  const [fetchDetail, setFetchDetail] = useState(false);
  const { data: gameDetail } = useSharedGame(game.id, fetchDetail);

  const removeMutation = useRemoveGameFromLibrary();

  // Drawer states
  const [kbDrawerOpen, setKbDrawerOpen] = useState(false);
  const [agentDrawerOpen, setAgentDrawerOpen] = useState(false);
  const [chatDrawerOpen, setChatDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);

  const handleFlip = useCallback(() => {
    setFetchDetail(true);
  }, []);

  // Build flip data from lazy-loaded detail (undefined until user flips)
  const flipData: MeepleCardFlipData | undefined = gameDetail
    ? {
        description: gameDetail.description || undefined,
        categories: gameDetail.categories,
        mechanics: gameDetail.mechanics,
        designers: gameDetail.designers,
        publishers: gameDetail.publishers,
        complexityRating: gameDetail.complexityRating,
        minAge: gameDetail.minAge || undefined,
      }
    : undefined;

  // Build metadata chips
  const metadata = [
    game.minPlayers && game.maxPlayers
      ? { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` }
      : null,
    game.playingTimeMinutes
      ? { icon: Clock, value: formatPlaytime(game.playingTimeMinutes) }
      : null,
    game.playCount > 0 ? { icon: RotateCcw, value: `${game.playCount}x` } : null,
  ].filter((m): m is NonNullable<typeof m> => m !== null);

  const quickActions = [
    {
      icon: Play,
      label: 'Avvia Sessione',
      onClick: () => router.push(`/sessions/new?gameId=${game.id}`),
    },
    {
      icon: Trash2,
      label: 'Rimuovi dalla Libreria',
      onClick: () => removeMutation.mutate(game.id),
      disabled: removeMutation.isPending,
    },
    {
      icon: Share2,
      label: 'Condividi',
      onClick: () => {
        navigator.clipboard?.writeText(`${window.location.origin}/library/games/${game.id}`);
      },
    },
  ];

  const badge = game.isOwned ? 'Owned' : game.inWishlist ? 'Wishlist' : undefined;

  const drawerNavLinks = [
    { entity: 'kb' as MeepleEntityType, label: 'KB', onClick: () => setKbDrawerOpen(true) },
    {
      entity: 'agent' as MeepleEntityType,
      label: 'Agents',
      onClick: () => setAgentDrawerOpen(true),
    },
    {
      entity: 'chatSession' as MeepleEntityType,
      label: 'Chats',
      onClick: () => setChatDrawerOpen(true),
    },
    {
      entity: 'session' as MeepleEntityType,
      label: 'Sessions',
      onClick: () => setSessionDrawerOpen(true),
    },
  ];

  return (
    <>
      <MeepleCard
        id={game.id}
        entity="game"
        variant={variant}
        title={game.title}
        subtitle={game.publisher ?? undefined}
        imageUrl={game.imageUrl ?? undefined}
        rating={game.averageRating ?? undefined}
        ratingMax={10}
        metadata={metadata}
        badge={badge}
        onClick={onClick ? () => onClick(game.id) : undefined}
        flippable
        flipData={flipData}
        flipTrigger="button"
        onFlip={handleFlip}
        navigateTo={drawerNavLinks}
        entityQuickActions={quickActions}
        showInfoButton
        infoHref={`/library/games/${game.id}`}
        infoTooltip="Vai al dettaglio"
        className={className}
        data-testid={`library-game-card-${game.id}`}
      />

      <KbDrawerSheet
        open={kbDrawerOpen}
        onOpenChange={setKbDrawerOpen}
        gameId={game.id}
        gameTitle={game.title}
      />
      <AgentDrawerSheet
        open={agentDrawerOpen}
        onOpenChange={setAgentDrawerOpen}
        gameId={game.id}
        gameTitle={game.title}
      />
      <ChatDrawerSheet
        open={chatDrawerOpen}
        onOpenChange={setChatDrawerOpen}
        gameId={game.id}
        gameTitle={game.title}
      />
      <SessionDrawerSheet
        open={sessionDrawerOpen}
        onOpenChange={setSessionDrawerOpen}
        gameId={game.id}
        gameTitle={game.title}
      />
    </>
  );
}

/**
 * MeepleUserLibraryCard Skeleton for loading state
 */
export function MeepleUserLibraryCardSkeleton({
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

export default MeepleUserLibraryCard;

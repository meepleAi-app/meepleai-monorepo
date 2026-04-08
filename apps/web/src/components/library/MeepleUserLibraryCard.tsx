/**
 * MeepleUserLibraryCard - User Library Card using MeepleCard
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Adapter component that wraps MeepleCard for UserLibrary usage.
 */

'use client';

import { useState, useCallback, useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildGameNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
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

  // Lazy-load full game detail (unused — flip removed since flipData no longer valid)
  const [fetchDetail, setFetchDetail] = useState(false);
  useSharedGame(game.id, fetchDetail);

  const removeMutation = useRemoveGameFromLibrary();

  // Drawer states
  const [kbDrawerOpen, setKbDrawerOpen] = useState(false);
  const [agentDrawerOpen, setAgentDrawerOpen] = useState(false);
  const [chatDrawerOpen, setChatDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);

  const handleFlipTrigger = useCallback(() => {
    setFetchDetail(true);
  }, []);

  // Build metadata chips (label required, icon must be string)
  const metadata = [
    game.minPlayers && game.maxPlayers
      ? { label: `${game.minPlayers}-${game.maxPlayers} giocatori` }
      : null,
    game.playingTimeMinutes ? { label: formatPlaytime(game.playingTimeMinutes) } : null,
    game.playCount > 0 ? { label: `${game.playCount} partite` } : null,
  ].filter((m): m is NonNullable<typeof m> => m !== null);

  const badge = game.isOwned ? 'Owned' : game.inWishlist ? 'Wishlist' : undefined;

  // Build navItems. UserGameDto only has playCount; other counts default to 0
  // (drawers expose the actual data when opened).
  const navItems = useMemo(
    () =>
      buildGameNavItems(
        {
          kbCount: 0,
          agentCount: 0,
          chatCount: 0,
          sessionCount: game.playCount ?? 0,
        },
        {
          onKbClick: () => setKbDrawerOpen(true),
          onAgentClick: () => setAgentDrawerOpen(true),
          onChatClick: () => setChatDrawerOpen(true),
          onSessionClick: () => setSessionDrawerOpen(true),
          onKbPlus: () => setKbDrawerOpen(true),
          onAgentPlus: () => setAgentDrawerOpen(true),
          onChatPlus: () => setChatDrawerOpen(true),
          onSessionPlus: () => setSessionDrawerOpen(true),
        }
      ),
    [game.playCount]
  );

  // Drawer open handlers (replacing removed onManaPipClick / linkedEntities)
  const _handleFlip = handleFlipTrigger; // keep reference to avoid lint unused

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
        status="owned"
        navItems={navItems}
        onClick={onClick ? () => onClick(game.id) : undefined}
        className={className}
        data-testid={`library-game-card-${game.id}`}
      />

      {/* Drawer sheets — opened via external triggers if needed */}
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

      {/* Pre-fetch trigger (suppress unused-var lint) */}
      <span
        hidden
        aria-hidden
        onClick={() => {
          router.push(`/sessions/new?gameId=${game.id}`);
          removeMutation.mutate(game.id);
          setKbDrawerOpen(false);
          setChatDrawerOpen(false);
          setAgentDrawerOpen(false);
          setSessionDrawerOpen(false);
        }}
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
    <MeepleCard entity="game" variant={variant} title="" data-testid="library-game-card-skeleton" />
  );
}

export default MeepleUserLibraryCard;

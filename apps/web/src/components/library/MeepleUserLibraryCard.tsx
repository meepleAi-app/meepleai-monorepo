/**
 * MeepleUserLibraryCard - User Library Card using MeepleCard
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Adapter component that wraps MeepleCard for UserLibrary usage.
 */

'use client';

import { useMemo, useState } from 'react';

import dynamic from 'next/dynamic';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildGameConnections } from '@/components/ui/data-display/meeple-card/nav-items';
import type { UserGameDto } from '@/lib/api/dashboard-client';

// Dynamic imports to avoid pulling pdfjs-dist (via KbDrawerSheet → PdfViewerModal)
// into static dependency graphs of consumers like AdminShell. SSR disabled because
// drawer sheets are interactive client-only surfaces.
const KbDrawerSheet = dynamic(() => import('./KbDrawerSheet').then(m => m.KbDrawerSheet), {
  ssr: false,
});
const AgentDrawerSheet = dynamic(() => import('./AgentDrawerSheet').then(m => m.AgentDrawerSheet), {
  ssr: false,
});
const ChatDrawerSheet = dynamic(() => import('./ChatDrawerSheet').then(m => m.ChatDrawerSheet), {
  ssr: false,
});
const SessionDrawerSheet = dynamic(
  () => import('./SessionDrawerSheet').then(m => m.SessionDrawerSheet),
  { ssr: false }
);

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
  // Drawer states — opened via connections click handlers
  const [kbDrawerOpen, setKbDrawerOpen] = useState(false);
  const [agentDrawerOpen, setAgentDrawerOpen] = useState(false);
  const [chatDrawerOpen, setChatDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);

  // Build metadata chips
  const metadata = [
    game.minPlayers && game.maxPlayers
      ? { label: `${game.minPlayers}-${game.maxPlayers} giocatori` }
      : null,
    game.playingTimeMinutes ? { label: formatPlaytime(game.playingTimeMinutes) } : null,
    game.playCount > 0 ? { label: `${game.playCount} partite` } : null,
  ].filter((m): m is NonNullable<typeof m> => m !== null);

  const badge = game.isOwned ? 'Owned' : game.inWishlist ? 'Wishlist' : undefined;

  // Build connections. UserGameDto only has playCount; other counts default to 0
  // (drawers expose the actual data when opened).
  // TODO: deferred Task 5/6 in plan — wire real KB/agent/chat counts via batch endpoint.
  const connections = useMemo(
    () =>
      buildGameConnections(
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
        connections={connections}
        onClick={onClick ? () => onClick(game.id) : undefined}
        className={className}
        data-testid={`library-game-card-${game.id}`}
      />

      {/* Drawer sheets — wired to connections click handlers */}
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
    <MeepleCard entity="game" variant={variant} title="" data-testid="library-game-card-skeleton" />
  );
}

export default MeepleUserLibraryCard;

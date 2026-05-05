/**
 * MeepleGameCatalogCard - Shared Catalog Card using MeepleCard
 * Issue #3334 - MeepleCard Integration with SharedGameCatalog
 *
 * Adapter component that wraps MeepleCard for SharedGameCatalog usage.
 * Provides:
 * - MeepleCard entity="game" with catalog-specific styling
 * - "Add to Library" action button
 * - "Already in Library" badge
 * - Complexity stars in metadata
 * - Players and playtime metadata
 *
 * @example
 * ```tsx
 * <MeepleGameCatalogCard
 *   game={sharedGame}
 *   variant="grid"
 *   onAdd={handleAddToLibrary}
 * />
 * ```
 */

'use client';

import { useCallback, useMemo, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import dynamic from 'next/dynamic';

import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import {
  MeepleCard,
  MeepleCardSkeleton,
  type MeepleCardAction,
  type MeepleCardMetadata,
  type MeepleCardVariant,
} from '@/components/ui/data-display/meeple-card';
import { buildGameConnections } from '@/components/ui/data-display/meeple-card/nav-items';
import { useGameInLibraryStatus } from '@/hooks/queries';
import type { GameStatusSimple } from '@/hooks/queries/useBatchGameStatus';
import { api } from '@/lib/api';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

// Dynamic imports to avoid DOMMatrix SSR error on statically generated pages
const KbDrawerSheet = dynamic(
  () => import('@/components/library/KbDrawerSheet').then(m => m.KbDrawerSheet),
  { ssr: false }
);
const AgentDrawerSheet = dynamic(
  () => import('@/components/library/AgentDrawerSheet').then(m => m.AgentDrawerSheet),
  { ssr: false }
);
const ChatDrawerSheet = dynamic(
  () => import('@/components/library/ChatDrawerSheet').then(m => m.ChatDrawerSheet),
  { ssr: false }
);
const SessionDrawerSheet = dynamic(
  () => import('@/components/library/SessionDrawerSheet').then(m => m.SessionDrawerSheet),
  { ssr: false }
);

// ============================================================================
// Types
// ============================================================================

export interface MeepleGameCatalogCardProps {
  /** Shared game data */
  game: SharedGame | SharedGameDetail;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Click handler for card navigation */
  onClick?: (gameId: string) => void;
  /** Additional CSS classes */
  className?: string;
  /**
   * Optional library status from batch API.
   * If provided, skips individual useGameInLibraryStatus hook call.
   * Enables N+1 prevention when rendering multiple cards.
   */
  libraryStatus?: GameStatusSimple;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Format complexity as stars (e.g., "●●●○○")
 */
function formatComplexity(complexity: number | null | undefined): string {
  if (complexity == null) return 'N/A';
  const filled = Math.round(complexity);
  const empty = 5 - filled;
  return '●'.repeat(filled) + '○'.repeat(empty);
}

/**
 * Format players range
 */
function formatPlayers(min: number | null | undefined, max: number | null | undefined): string {
  if (!min && !max) return 'N/A';
  if (min === max) return `${min}`;
  return `${min || '?'}-${max || '?'}`;
}

/**
 * Format playtime
 */
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

export function MeepleGameCatalogCard({
  game,
  variant = 'grid',
  onClick,
  className,
  libraryStatus,
}: MeepleGameCatalogCardProps) {
  // Issue #4822: Open wizard instead of direct add
  const { openWizard } = useAddGameWizard();
  const handleAddToCollection = useCallback(() => {
    openWizard(
      { type: 'fromGameCard', sharedGameId: game.id },
      {
        gameId: game.id,
        title: game.title,
        imageUrl: game.imageUrl || undefined,
        thumbnailUrl: game.thumbnailUrl || undefined,
        minPlayers: game.minPlayers ?? undefined,
        maxPlayers: game.maxPlayers ?? undefined,
        playingTimeMinutes: game.playingTimeMinutes ?? undefined,
        complexityRating: game.complexityRating ?? undefined,
        averageRating: game.averageRating ?? undefined,
        yearPublished: game.yearPublished ?? undefined,
        description: game.description || undefined,
        source: 'catalog',
      }
    );
  }, [openWizard, game]);

  // Check if game is already in user's library
  const { data: individualStatus, isLoading: statusLoading } = useGameInLibraryStatus(
    game.id,
    !libraryStatus
  );

  const status = libraryStatus || individualStatus;
  const inLibrary = status?.inLibrary || false;

  // Drawer states
  const [kbDrawerOpen, setKbDrawerOpen] = useState(false);
  const [agentDrawerOpen, setAgentDrawerOpen] = useState(false);
  const [chatDrawerOpen, setChatDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);

  // Fetch KB documents for status badge
  const { data: kbDocuments } = useQuery({
    queryKey: ['kb-docs', game.id],
    queryFn: () => api.documents.getDocumentsByGame(game.id),
    enabled: inLibrary,
    staleTime: 2 * 60 * 1000,
  });

  void kbDocuments; // used indirectly via drawer

  // Build metadata array
  const metadata = useMemo<MeepleCardMetadata[]>(() => {
    const items: MeepleCardMetadata[] = [
      { label: formatPlayers(game.minPlayers, game.maxPlayers) },
      { label: formatPlaytime(game.playingTimeMinutes) },
    ];
    if (game.complexityRating) {
      items.push({ label: formatComplexity(game.complexityRating) });
    }
    return items;
  }, [game]);

  // Build subtitle with publisher/year if available
  const subtitleParts: string[] = [];
  if (game.yearPublished) {
    subtitleParts.push(String(game.yearPublished));
  }
  if (game.bggId) {
    subtitleParts.push(`ID: ${game.bggId}`);
  }
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : 'N/A';

  // Loading state: only show loading if fetching individually (no batch status provided)
  const isLoadingStatus = !libraryStatus && statusLoading;

  // Build actions for featured/hero variant
  const showActions = variant === 'featured' || variant === 'hero';
  const actions = useMemo<MeepleCardAction[] | undefined>(() => {
    if (!showActions || isLoadingStatus) return undefined;
    return [
      {
        icon: inLibrary ? '✓' : '+',
        label: inLibrary ? 'Nella Libreria' : 'Aggiungi',
        onClick: inLibrary ? () => {} : handleAddToCollection,
        disabled: inLibrary,
        variant: inLibrary ? 'default' : 'primary',
      },
    ];
  }, [showActions, isLoadingStatus, inLibrary, handleAddToCollection]);

  // Build badge
  const badge = inLibrary && !isLoadingStatus ? 'In Libreria' : undefined;

  // Build connections — only when game is in user's library (drawers only render then)
  const connections = useMemo(() => {
    if (!inLibrary) return undefined;
    return buildGameConnections(
      {
        kbCount: kbDocuments?.length ?? 0,
        agentCount: 0,
        chatCount: 0,
        sessionCount: 0,
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
    );
  }, [inLibrary, kbDocuments]);

  if (isLoadingStatus) {
    return <MeepleCardSkeleton variant={variant} />;
  }

  return (
    <>
      <MeepleCard
        entity="game"
        variant={variant}
        title={game.title}
        subtitle={subtitle}
        imageUrl={game.imageUrl || undefined}
        rating={game.averageRating ?? 0}
        ratingMax={10}
        metadata={metadata}
        badge={badge}
        status={inLibrary ? 'owned' : undefined}
        actions={actions}
        connections={connections}
        onClick={onClick ? () => onClick(game.id) : undefined}
        className={className}
        data-testid={`catalog-game-card-${game.id}`}
      />

      {/* Drawers — only rendered when user has game in library */}
      {inLibrary && (
        <>
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
      )}
    </>
  );
}

/**
 * MeepleGameCatalogCard Skeleton for loading state
 */
export function MeepleGameCatalogCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return <MeepleCardSkeleton variant={variant} data-testid="catalog-game-card-skeleton" />;
}

export default MeepleGameCatalogCard;

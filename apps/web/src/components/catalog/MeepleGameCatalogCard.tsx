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
import { Check, Plus, Users, Clock, BarChart2 } from 'lucide-react';
import dynamic from 'next/dynamic';

import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { mapToIndexingStatus } from '@/components/library/kb-utils';

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
import {
  MeepleCard,
  type MeepleCardVariant,
  type MeepleEntityType,
} from '@/components/ui/data-display/meeple-card';
import type { MeepleCardFlipData } from '@/components/ui/data-display/meeple-card-features/FlipCard';
import type { QuickAction } from '@/components/ui/data-display/meeple-card-quick-actions';
import type { ResolvedNavigationLink } from '@/config/entity-navigation';
import { useGameInLibraryStatus } from '@/hooks/queries';
import type { GameStatusSimple } from '@/hooks/queries/useBatchGameStatus';
import { api } from '@/lib/api';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

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
  /** Enable flip card with back content */
  flippable?: boolean;
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

function isDetailGame(game: SharedGame | SharedGameDetail): game is SharedGameDetail {
  return 'categories' in game;
}

export function MeepleGameCatalogCard({
  game,
  variant = 'grid',
  onClick,
  flippable,
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
  // Use provided batch status if available, otherwise fetch individually
  const { data: individualStatus, isLoading: statusLoading } = useGameInLibraryStatus(
    game.id,
    !libraryStatus // Only fetch if libraryStatus not provided
  );

  const status = libraryStatus || individualStatus;
  const inLibrary = status?.inLibrary || false;

  // Catalog-specific quick actions: only "Add to library"
  const catalogQuickActions: QuickAction[] = useMemo(
    () => [
      inLibrary
        ? {
            icon: Check,
            label: 'Gioco gia nella tua libreria',
            onClick: () => {},
            disabled: true,
            disabledTooltip: 'Gioco gia nella tua libreria',
          }
        : {
            icon: Plus,
            label: 'Aggiungi alla libreria',
            onClick: handleAddToCollection,
          },
    ],
    [inLibrary, handleAddToCollection]
  );

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

  // Navigation footer: drawers when in library, link to detail page otherwise
  const catalogNavLinks: ResolvedNavigationLink[] = useMemo(
    () =>
      inLibrary
        ? [
            {
              entity: 'kb' as MeepleEntityType,
              label: 'KB',
              onClick: () => setKbDrawerOpen(true),
            },
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
          ]
        : [
            {
              entity: 'kb' as MeepleEntityType,
              label: 'Regolamento',
              href: `/library/games/${game.id}`,
            },
          ],
    [game.id, inLibrary]
  );

  // Build metadata array
  const metadata = [
    { icon: Users, value: formatPlayers(game.minPlayers, game.maxPlayers) },
    { icon: Clock, value: formatPlaytime(game.playingTimeMinutes) },
  ];

  // Add complexity if available
  if (game.complexityRating) {
    metadata.push({
      icon: BarChart2,
      value: formatComplexity(game.complexityRating),
    });
  }

  // Build subtitle with publisher/year if available
  const subtitleParts: string[] = [];
  if (game.yearPublished) {
    subtitleParts.push(String(game.yearPublished));
  }
  if (game.bggId) {
    subtitleParts.push(`ID: ${game.bggId}`);
  }
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : 'N/A';

  // Build actions for featured variant
  const showActions = variant === 'featured' || variant === 'hero';
  // Loading state: only show loading if fetching individually (no batch status provided)
  const isLoadingStatus = !libraryStatus && statusLoading;
  const actions =
    showActions && !isLoadingStatus
      ? [
          {
            label: inLibrary ? 'Nella Libreria' : 'Aggiungi',
            primary: !inLibrary,
            disabled: inLibrary,
            onClick: inLibrary ? undefined : handleAddToCollection,
          },
        ]
      : undefined;

  // Build badge
  const badge = inLibrary && !isLoadingStatus ? 'In Libreria' : undefined;

  // Build flip data from game fields
  const flipData: MeepleCardFlipData | undefined = flippable
    ? {
        description: game.description || undefined,
        complexityRating: game.complexityRating,
        minAge: game.minAge || undefined,
        ...(isDetailGame(game)
          ? {
              categories: game.categories,
              mechanics: game.mechanics,
              designers: game.designers,
              publishers: game.publishers,
            }
          : {}),
      }
    : undefined;

  return (
    <>
      <MeepleCard
        id={game.id}
        entity="game"
        variant={variant}
        title={game.title}
        subtitle={subtitle}
        imageUrl={game.imageUrl || undefined}
        rating={game.averageRating ?? 0}
        ratingMax={10}
        metadata={metadata}
        badge={badge}
        actions={actions}
        loading={isLoadingStatus}
        onClick={onClick ? () => onClick(game.id) : undefined}
        flippable={flippable && !!game.description}
        flipData={flipData}
        flipTrigger="button"
        className={className}
        kbCards={kbDocuments?.map(d => ({ status: mapToIndexingStatus(d) }))}
        navigateTo={catalogNavLinks}
        data-testid={`catalog-game-card-${game.id}`}
        entityQuickActions={catalogQuickActions}
        showInfoButton
        infoHref={`/library/games/${game.id}`}
        infoTooltip="Vai al dettaglio"
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
  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title=""
      loading
      data-testid="catalog-game-card-skeleton"
    />
  );
}

export default MeepleGameCatalogCard;

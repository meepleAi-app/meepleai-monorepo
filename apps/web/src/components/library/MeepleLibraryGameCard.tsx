/**
 * MeepleLibraryGameCard - Library adapter using MeepleCard
 * Issue #4045 - Integrate MeepleCard in Library pages
 *
 * Adapter component that wraps MeepleCard for UserLibrary usage.
 */

'use client';

import { useState, useCallback, useMemo } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';

import { AgentCreationSheet } from '@/components/agent/config';
import { toast } from '@/components/layout/Toast';
import {
  MeepleCard,
  type MeepleCardVariant,
  type MeepleCardMetadata,
  type CardStatus,
} from '@/components/ui/data-display/meeple-card';
import { buildGameNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { AddToWishlistDialog } from '@/components/wishlist/AddToWishlistDialog';
import { useAgentConfig, useToggleLibraryFavorite } from '@/hooks/queries';
import { libraryKeys } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { UserLibraryEntry, GameStateType } from '@/lib/api';
import { useViewTransition } from '@/lib/domain-hooks/useViewTransition';

import { AgentDrawerSheet } from './AgentDrawerSheet';
import { ChatDrawerSheet } from './ChatDrawerSheet';
import { DeclareOwnershipButton } from './DeclareOwnershipButton';
import { KbDrawerSheet } from './KbDrawerSheet';
import { SessionDrawerSheet } from './SessionDrawerSheet';

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

function mapGameStateToStatus(state: GameStateType | null | undefined): CardStatus | undefined {
  // "Posseduto" overlay only when the user has declared ownership.
  // Drives copyright-safe citation rendering: owners see verbatim PDF quotes,
  // non-owners get paraphrased snippets. 'Nuovo' = added but not yet declared.
  if (state === 'Owned') return 'owned';
  if (state === 'Wishlist') return 'wishlist';
  return undefined;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleLibraryGameCard({
  game,
  variant = 'grid',
  onConfigureAgent: _onConfigureAgent,
  onUploadPdf: _onUploadPdf,
  onEditNotes: _onEditNotes,
  onRemove: _onRemove,
  onChangeState: _onChangeState,
  onShare: _onShare,
  selectionMode = false,
  isSelected: _isSelected = false,
  onSelect: _onSelect,
  flippable,
  className,
}: MeepleLibraryGameCardProps) {
  const { navigateWithTransition } = useViewTransition();
  const queryClient = useQueryClient();
  const [isTogglingFavorite, setIsTogglingFavorite] = useState(false);
  const [agentSheetOpen, setAgentSheetOpen] = useState(false);
  const _handleCreateAgent = useCallback(() => setAgentSheetOpen(true), []);

  const [wishlistDialogOpen, setWishlistDialogOpen] = useState(false);
  const [kbDrawerOpen, setKbDrawerOpen] = useState(false);
  const [agentDrawerOpen, setAgentDrawerOpen] = useState(false);
  const [chatDrawerOpen, setChatDrawerOpen] = useState(false);
  const [sessionDrawerOpen, setSessionDrawerOpen] = useState(false);

  // Fetch agent configuration status
  const { data: agentConfig } = useAgentConfig(game.gameId, true);
  const agentConfigured = agentConfig !== null;
  const agentModel = agentConfig?.modelType || 'default';

  const toggleFavoriteMutation = useToggleLibraryFavorite();

  // Fetch KB documents
  const { data: _kbDocuments } = useQuery({
    queryKey: ['kb-docs', game.gameId],
    queryFn: () => api.documents.getDocumentsByGame(game.gameId),
    enabled: !!game.hasKb || game.kbProcessingCount > 0,
    staleTime: 2 * 60 * 1000,
  });

  const modelDisplayName = useMemo<Record<string, string>>(
    () => ({
      'llama-3.3-70b-free': 'Llama Free',
      'google-gemini-pro': 'Gemini Pro',
      'deepseek-chat': 'DeepSeek',
      'llama-3.3-70b': 'Llama Pro',
      default: 'Default',
    }),
    []
  );

  const _handleToggleFavorite = useCallback(async () => {
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
        error instanceof Error ? error.message : "Errore durante l'aggiornamento dei preferiti"
      );
    } finally {
      setIsTogglingFavorite(false);
    }
  }, [isTogglingFavorite, toggleFavoriteMutation, game.gameId, game.isFavorite, game.gameTitle]);

  const handleOwnershipDeclared = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
    queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(game.gameId) });
  }, [queryClient, game.gameId]);

  // ============================================================================
  // Metadata Configuration (string icons only in new API)
  // ============================================================================

  const metadata: MeepleCardMetadata[] = useMemo(() => {
    // Note: real play count not yet available on UserLibraryEntry — chip omitted
    // until backend exposes it (deferred Task 5/6 in plan).
    const items: MeepleCardMetadata[] = [];

    if (agentConfigured) {
      items.push({ label: `Agent: ${modelDisplayName[agentModel]}` });
    }

    if (game.hasKb) {
      items.push({
        label: game.kbCardCount > 1 ? `📄 ${game.kbIndexedCount}/${game.kbCardCount} KB` : '📄 KB',
      });
    } else if (game.kbProcessingCount > 0) {
      items.push({ label: '⏳ KB in elaborazione' });
    }

    return items;
  }, [
    agentConfigured,
    agentModel,
    game.hasKb,
    game.kbCardCount,
    game.kbIndexedCount,
    game.kbProcessingCount,
    modelDisplayName,
  ]);

  // ============================================================================
  // Build Props
  // ============================================================================

  const subtitle =
    game.gamePublisher || `Aggiunto il ${new Date(game.addedAt).toLocaleDateString('it-IT')}`;

  const mappedStatus = mapGameStateToStatus(game.currentState);
  const badge = game.isFavorite ? '❤️ Preferito' : undefined;

  const navItems = useMemo(
    () =>
      buildGameNavItems(
        {
          kbCount: game.kbCardCount ?? 0,
          agentCount: agentConfigured ? 1 : 0,
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
      ),
    [game.kbCardCount, agentConfigured]
  );

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <>
      <MeepleCard
        id={game.gameId}
        entity="game"
        variant={variant}
        title={game.gameTitle}
        subtitle={subtitle}
        imageUrl={game.gameImageUrl || undefined}
        rating={game.averageRating ?? undefined}
        ratingMax={10}
        metadata={metadata}
        badge={badge}
        status={mappedStatus}
        navItems={navItems}
        onClick={
          selectionMode
            ? undefined
            : flippable
              ? undefined
              : () => navigateWithTransition(`/library/games/${game.gameId}`)
        }
        flippable={flippable}
        className={className}
        data-testid={`library-game-card-${game.gameId}`}
      />

      {/* Ownership declaration CTA (RAG access is now shown as status overlay on the card) */}
      {game.currentState === 'Nuovo' && (
        <div className="flex items-center gap-2 mt-1 px-1">
          <DeclareOwnershipButton
            gameId={game.gameId}
            gameName={game.gameTitle}
            gameState={game.currentState}
            onOwnershipDeclared={handleOwnershipDeclared}
          />
        </div>
      )}

      {/* KB Drawer */}
      <KbDrawerSheet
        open={kbDrawerOpen}
        onOpenChange={setKbDrawerOpen}
        gameId={game.gameId}
        gameTitle={game.gameTitle}
      />

      {/* Agent Drawer */}
      <AgentDrawerSheet
        open={agentDrawerOpen}
        onOpenChange={setAgentDrawerOpen}
        gameId={game.gameId}
        gameTitle={game.gameTitle}
      />

      {/* Chat Drawer */}
      <ChatDrawerSheet
        open={chatDrawerOpen}
        onOpenChange={setChatDrawerOpen}
        gameId={game.gameId}
        gameTitle={game.gameTitle}
      />

      {/* Session Drawer */}
      <SessionDrawerSheet
        open={sessionDrawerOpen}
        onOpenChange={setSessionDrawerOpen}
        gameId={game.gameId}
        gameTitle={game.gameTitle}
      />

      {/* Agent creation wizard */}
      <AgentCreationSheet
        isOpen={agentSheetOpen}
        onClose={() => setAgentSheetOpen(false)}
        initialGameId={game.gameId}
        initialGameTitle={game.gameTitle}
      />

      {/* Add to Wishlist dialog */}
      <AddToWishlistDialog
        gameId={game.gameId}
        gameName={game.gameTitle}
        open={wishlistDialogOpen}
        onOpenChange={setWishlistDialogOpen}
      />
    </>
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
    <MeepleCard entity="game" variant={variant} title="" data-testid="library-game-card-skeleton" />
  );
}

export default MeepleLibraryGameCard;

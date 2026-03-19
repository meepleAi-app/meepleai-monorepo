/**
 * GameTableZoneTools — Tools & management zone for the Game Table
 *
 * Renders toolkit link, notes preview, related entities, ownership controls,
 * favorite toggle, game state selector, labels, and remove-from-library action.
 * Each item is a dark card row.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import React, { useMemo, useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import {
  Calendar,
  Check,
  ChevronDown,
  Loader2,
  Package,
  Pencil,
  Star,
  Trash2,
  Wrench,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { DeclareOwnershipButton } from '@/components/library/DeclareOwnershipButton';
import { EditNotesModal } from '@/components/library/EditNotesModal';
import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { LabelBadge, LabelSelector } from '@/components/library/labels';
import { RagAccessBadge } from '@/components/library/RagAccessBadge';
import { RemoveGameDialog } from '@/components/library/RemoveGameDialog';
import { RelatedEntitiesSection } from '@/components/ui/data-display/entity-link/related-entities-section';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useGameLabels, useRemoveLabelFromGame } from '@/hooks/queries/useLabels';
import { libraryKeys, useUpdateGameState } from '@/hooks/queries/useLibrary';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface GameTableZoneToolsProps {
  gameDetail: LibraryGameDetail;
  gameId: string;
}

// ============================================================================
// State configuration
// ============================================================================

const stateConfig: Record<
  GameStateType,
  { label: string; color: string; bgColor: string; icon: typeof Check }
> = {
  Owned: {
    label: 'Posseduto',
    color: 'text-emerald-400',
    bgColor: 'bg-emerald-600 hover:bg-emerald-700',
    icon: Check,
  },
  Wishlist: {
    label: 'Wishlist',
    color: 'text-amber-400',
    bgColor: 'bg-amber-600 hover:bg-amber-700',
    icon: Star,
  },
  Nuovo: {
    label: 'Nuovo',
    color: 'text-blue-400',
    bgColor: 'bg-blue-600 hover:bg-blue-700',
    icon: Package,
  },
  InPrestito: {
    label: 'In Prestito',
    color: 'text-rose-400',
    bgColor: 'bg-rose-600 hover:bg-rose-700',
    icon: Calendar,
  },
};

// ============================================================================
// Styling constants
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ============================================================================
// Component
// ============================================================================

export function GameTableZoneTools({
  gameDetail,
  gameId,
}: GameTableZoneToolsProps): React.ReactNode {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [isNotesOpen, setIsNotesOpen] = useState(false);
  const [isRemoveOpen, setIsRemoveOpen] = useState(false);

  const updateGameState = useUpdateGameState();
  const { data: gameLabels = [], isLoading: isLoadingLabels } = useGameLabels(gameId);
  const removeLabelMutation = useRemoveLabelFromGame();

  const truncatedNotes =
    gameDetail.notes && gameDetail.notes.length > 120
      ? `${gameDetail.notes.slice(0, 120)}...`
      : gameDetail.notes;

  // Type-safe state validation
  const currentState: GameStateType = useMemo(() => {
    const state = gameDetail.currentState;
    return state && state in stateConfig ? (state as GameStateType) : 'Owned';
  }, [gameDetail.currentState]);

  const stateInfo = stateConfig[currentState];
  const StateIcon = stateInfo.icon;

  const handleStateChange = async (newState: GameStateType) => {
    try {
      await updateGameState.mutateAsync({
        gameId,
        request: { newState },
      });
      toast.success('Stato aggiornato', {
        description: `Gioco impostato come "${stateConfig[newState].label}"`,
      });
    } catch (error) {
      toast.error('Errore', {
        description: 'Impossibile aggiornare lo stato del gioco. Riprova.',
      });
      logger.error('Failed to update game state:', error);
    }
  };

  const handleOwnershipDeclared = () => {
    queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
    queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
  };

  const handleRemoveLabel = async (labelId: string) => {
    try {
      await removeLabelMutation.mutateAsync({ gameId, labelId });
      toast.success('Etichetta rimossa');
    } catch {
      toast.error('Errore', {
        description: "Impossibile rimuovere l'etichetta",
      });
    }
  };

  return (
    <div className="space-y-3">
      {/* Toolkit link */}
      <Link
        href={`/library/games/${gameId}/toolkit`}
        className={`${CARD_ROW} flex items-center gap-3 text-[#e6edf3] hover:border-amber-500/50 transition-colors`}
        data-testid="toolkit-link"
      >
        <Wrench className="h-5 w-5 text-amber-400 shrink-0" />
        <span className="font-quicksand font-semibold">Toolkit</span>
      </Link>

      {/* State dropdown + Favorite toggle */}
      <div
        className={`${CARD_ROW} flex items-center gap-3 flex-wrap`}
        data-testid="state-favorite-section"
      >
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              className={cn('font-quicksand font-semibold text-white', stateInfo.bgColor)}
              size="sm"
              disabled={updateGameState.isPending}
            >
              {updateGameState.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <StateIcon className="mr-2 h-4 w-4" />
              )}
              {stateInfo.label}
              <ChevronDown className="ml-2 h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-48">
            {(Object.keys(stateConfig) as GameStateType[]).map(state => {
              const config = stateConfig[state];
              const Icon = config.icon;
              return (
                <DropdownMenuItem
                  key={state}
                  onClick={() => handleStateChange(state)}
                  className={cn('cursor-pointer', state === currentState && 'bg-muted/40')}
                >
                  <Icon className={cn('mr-2 h-4 w-4', config.color)} />
                  <span className={cn('font-nunito', config.color)}>{config.label}</span>
                  {state === currentState && (
                    <Check className="ml-auto h-4 w-4 text-[hsl(25,95%,38%)]" />
                  )}
                </DropdownMenuItem>
              );
            })}
          </DropdownMenuContent>
        </DropdownMenu>

        <FavoriteToggle
          gameId={gameId}
          isFavorite={gameDetail.isFavorite}
          gameTitle={gameDetail.gameTitle}
          className="text-[#8b949e] hover:text-[#e6edf3]"
          data-testid="favorite-toggle"
        />
      </div>

      {/* Labels */}
      <div className={`${CARD_ROW}`} data-testid="labels-section">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs font-nunito text-[#8b949e] mr-1">Etichette:</span>

          {isLoadingLabels ? (
            <>
              <Skeleton className="h-5 w-14 rounded-full bg-[#30363d]" />
              <Skeleton className="h-5 w-18 rounded-full bg-[#30363d]" />
            </>
          ) : (
            <>
              {gameLabels.map(label => (
                <LabelBadge
                  key={label.id}
                  label={label}
                  onRemove={() => handleRemoveLabel(label.id)}
                />
              ))}
              <LabelSelector gameId={gameId} currentLabels={gameLabels} />
            </>
          )}
        </div>
      </div>

      {/* Notes preview */}
      <div className={`${CARD_ROW}`} data-testid="notes-section">
        <div className="flex items-center justify-between mb-1">
          <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Note</span>
          <button
            onClick={() => setIsNotesOpen(true)}
            className="p-1 rounded hover:bg-[#30363d] text-[#8b949e] hover:text-[#e6edf3] transition-colors"
            aria-label="Modifica note"
            data-testid="edit-notes-btn"
          >
            <Pencil className="h-4 w-4" />
          </button>
        </div>
        <p className="text-sm text-[#8b949e] font-nunito" data-testid="notes-preview">
          {truncatedNotes || 'Nessuna nota'}
        </p>
      </div>

      {/* Related entities */}
      <div className={CARD_ROW} data-testid="related-entities-section">
        <RelatedEntitiesSection entityType="Game" entityId={gameId} />
      </div>

      {/* Ownership + RAG badge */}
      <div
        className={`${CARD_ROW} flex items-center gap-3 flex-wrap`}
        data-testid="ownership-section"
      >
        <DeclareOwnershipButton
          gameId={gameId}
          gameName={gameDetail.gameTitle}
          gameState={gameDetail.currentState}
          onOwnershipDeclared={handleOwnershipDeclared}
        />
        <RagAccessBadge hasRagAccess={gameDetail.hasRagAccess} isRagPublic={false} />
      </div>

      {/* Remove from library */}
      <Button
        variant="destructive"
        className="w-full bg-red-900/30 border border-red-800/50 hover:bg-red-900/50 text-red-400"
        onClick={() => setIsRemoveOpen(true)}
        data-testid="remove-game-btn"
      >
        <Trash2 className="h-4 w-4 mr-2" />
        Rimuovi dalla libreria
      </Button>

      {/* Modals */}
      <EditNotesModal
        isOpen={isNotesOpen}
        onClose={() => setIsNotesOpen(false)}
        gameId={gameId}
        gameTitle={gameDetail.gameTitle}
        currentNotes={gameDetail.notes}
      />
      <RemoveGameDialog
        isOpen={isRemoveOpen}
        onClose={() => setIsRemoveOpen(false)}
        gameId={gameId}
        gameTitle={gameDetail.gameTitle}
        onRemoved={() => router.push('/library')}
      />
    </div>
  );
}

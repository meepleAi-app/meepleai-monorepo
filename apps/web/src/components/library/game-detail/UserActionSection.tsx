/**
 * UserActionSection Component (Issue #3513, #3514, #3515)
 *
 * Bottom section with user-specific actions and information:
 * - Collection status (Owned/Wishlist/etc.) with dropdown
 * - Favorite toggle
 * - Labels/tags with API integration
 * - Play statistics (games played, last played, win rate)
 * - Notes section
 * - Remove from collection action
 *
 * Follows MeepleAI design system.
 */

'use client';

import { useMemo } from 'react';

import {
  Calendar,
  Check,
  ChevronDown,
  Edit2,
  FileText,
  Loader2,
  Package,
  Star,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';

import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { LabelBadge, LabelSelector } from '@/components/library/labels';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useGameLabels, useRemoveLabelFromGame } from '@/hooks/queries/useLabels';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useUpdateGameState } from '@/hooks/queries/useLibrary';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

export interface UserActionSectionProps {
  gameDetail: LibraryGameDetail;
}

// State configuration
const stateConfig: Record<
  GameStateType,
  { label: string; color: string; bgColor: string; icon: typeof Check }
> = {
  Owned: {
    label: 'Posseduto',
    color: 'text-emerald-600',
    bgColor: 'bg-emerald-500 hover:bg-emerald-600',
    icon: Check,
  },
  Wishlist: {
    label: 'Wishlist',
    color: 'text-amber-600',
    bgColor: 'bg-amber-500 hover:bg-amber-600',
    icon: Star,
  },
  Nuovo: {
    label: 'Nuovo',
    color: 'text-blue-600',
    bgColor: 'bg-blue-500 hover:bg-blue-600',
    icon: Package,
  },
  InPrestito: {
    label: 'In Prestito',
    color: 'text-rose-600',
    bgColor: 'bg-rose-500 hover:bg-rose-600',
    icon: Calendar,
  },
};

export function UserActionSection({ gameDetail }: UserActionSectionProps) {
  const updateGameState = useUpdateGameState();

  // Fetch labels for this game (Issue #3514)
  const { data: gameLabels = [], isLoading: isLoadingLabels } = useGameLabels(gameDetail.gameId);
  const removeLabelMutation = useRemoveLabelFromGame();

  // Type-safe state validation
  const currentState: GameStateType = useMemo(() => {
    const state = gameDetail.currentState;
    return state && state in stateConfig ? (state as GameStateType) : 'Owned';
  }, [gameDetail.currentState]);

  const stateInfo = stateConfig[currentState];
  const StateIcon = stateInfo.icon;

  // Play statistics from backend GameDetailDto
  const stats = {
    gamesPlayed: gameDetail.timesPlayed,
    lastPlayed: gameDetail.lastPlayed,
    winRate: gameDetail.winRate,
    avgDuration: gameDetail.avgDuration,
  };

  const handleStateChange = async (newState: GameStateType) => {
    try {
      await updateGameState.mutateAsync({
        gameId: gameDetail.gameId,
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

  const handleRemoveLabel = async (labelId: string) => {
    try {
      await removeLabelMutation.mutateAsync({
        gameId: gameDetail.gameId,
        labelId,
      });
      toast.success('Etichetta rimossa');
    } catch (_error) {
      toast.error('Errore', {
        description: "Impossibile rimuovere l'etichetta",
      });
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('it-IT', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  };

  return (
    <>
      <section className="rounded-3xl border border-border/40 bg-card p-6 shadow-sm">
        {/* Action Bar */}
        <div className="mb-6 flex flex-wrap items-center gap-3">
          {/* State Dropdown */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                className={cn('font-quicksand font-semibold text-white', stateInfo.bgColor)}
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

          {/* Rules PDF Button */}
          {gameDetail.hasCustomPdf && (
            <Button
              variant="outline"
              className="border-border/40 font-quicksand font-semibold text-muted-foreground hover:bg-muted/40 hover:text-foreground"
            >
              <FileText className="mr-2 h-4 w-4" />
              Regole PDF
            </Button>
          )}

          {/* Favorite Toggle */}
          <FavoriteToggle
            gameId={gameDetail.gameId}
            isFavorite={gameDetail.isFavorite}
            className="border-border/40 font-quicksand font-semibold"
          />

          {/* Spacer */}
          <div className="flex-grow" />

          {/* Remove Button */}
          <Button
            variant="ghost"
            className="font-quicksand font-semibold text-rose-500 hover:bg-rose-500/10 hover:text-rose-600"
            onClick={() => document.dispatchEvent(new CustomEvent('game-detail:remove-game'))}
          >
            <Trash2 className="mr-2 h-4 w-4" />
            Rimuovi
          </Button>
        </div>

        {/* Labels Row (Issue #3514) */}
        <div className="mb-6 flex flex-wrap items-center gap-2 border-t border-border/40 pt-6">
          <span className="mr-2 font-nunito text-sm text-muted-foreground">Etichette:</span>

          {isLoadingLabels ? (
            <>
              <Skeleton className="h-6 w-16 rounded-full" />
              <Skeleton className="h-6 w-20 rounded-full" />
              <Skeleton className="h-6 w-14 rounded-full" />
            </>
          ) : (
            <>
              {gameLabels.map(label => (
                <LabelBadge
                  key={label.id}
                  label={label}
                  onRemove={handleRemoveLabel}
                  disabled={removeLabelMutation.isPending}
                />
              ))}
            </>
          )}

          {/* Add Label Selector */}
          <LabelSelector
            gameId={gameDetail.gameId}
            currentLabels={gameLabels}
            disabled={isLoadingLabels}
          />
        </div>

        {/* Stats Grid */}
        <div className="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4">
          {/* Added Date */}
          <div className="rounded-xl bg-muted/40 p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-muted-foreground">Aggiunto</p>
            <p className="font-quicksand text-base font-semibold text-foreground">
              {formatDate(gameDetail.addedAt)}
            </p>
          </div>

          {/* Games Played */}
          <div className="rounded-xl bg-muted/40 p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-muted-foreground">Partite giocate</p>
            <p className="font-quicksand text-base font-semibold text-foreground">
              {stats.gamesPlayed}
            </p>
          </div>

          {/* Last Played */}
          <div className="rounded-xl bg-muted/40 p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-muted-foreground">Ultima partita</p>
            <p className="font-quicksand text-base font-semibold text-foreground">
              {stats.lastPlayed ? formatDate(stats.lastPlayed) : 'Mai'}
            </p>
          </div>

          {/* Win Rate */}
          <div className="rounded-xl bg-muted/40 p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-muted-foreground">Win rate</p>
            <p className="font-quicksand text-base font-semibold text-emerald-600">
              {stats.winRate ?? 'N/A'}
            </p>
          </div>
        </div>

        {/* Notes Section */}
        <div className="rounded-xl border border-border/40 bg-muted/20 p-4">
          <div className="mb-2 flex items-center justify-between">
            <h4 className="font-quicksand text-sm font-semibold text-foreground">Note personali</h4>
            <Button
              variant="ghost"
              size="sm"
              className="h-8 px-2 text-muted-foreground hover:text-[hsl(25,95%,38%)]"
              onClick={() => document.dispatchEvent(new CustomEvent('game-detail:edit-notes'))}
            >
              <Edit2 className="h-3.5 w-3.5" />
            </Button>
          </div>
          {gameDetail.notes ? (
            <p className="font-nunito text-sm leading-relaxed text-muted-foreground">
              {gameDetail.notes}
            </p>
          ) : (
            <p className="font-nunito text-sm italic text-muted-foreground">
              Nessuna nota. Clicca per aggiungerne una.
            </p>
          )}
        </div>
      </section>
    </>
  );
}

/**
 * UserActionSection Component (Issue #3513)
 *
 * Bottom section with user-specific actions and information:
 * - Collection status (Owned/Wishlist/etc.) with dropdown
 * - Favorite toggle
 * - Labels/tags
 * - Play statistics (games played, last played, win rate)
 * - Notes section
 * - Remove from collection action
 *
 * Follows MeepleAI design system.
 */

'use client';

import { useState, useMemo } from 'react';

import {
  Calendar,
  Check,
  ChevronDown,
  Edit2,
  FileText,
  Loader2,
  Package,
  Plus,
  Star,
  Trash2,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { EditNotesModal } from '@/components/library/EditNotesModal';
import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { RemoveGameDialog } from '@/components/library/RemoveGameDialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useUpdateGameState } from '@/hooks/queries/useLibrary';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

export interface UserActionSectionProps {
  gameDetail: LibraryGameDetail;
}

// State configuration
const stateConfig: Record<GameStateType, { label: string; color: string; bgColor: string; icon: typeof Check }> = {
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

// Label configuration
const predefinedLabels = [
  { id: 'family', name: 'Family', color: 'bg-green-500/20 text-green-600 border-green-500/30' },
  { id: 'strategy', name: 'Strategy', color: 'bg-purple-500/20 text-purple-600 border-purple-500/30' },
  { id: 'solo', name: 'Solo', color: 'bg-cyan-500/20 text-cyan-600 border-cyan-500/30' },
  { id: 'party', name: 'Party', color: 'bg-amber-500/20 text-amber-600 border-amber-500/30' },
];

export function UserActionSection({ gameDetail }: UserActionSectionProps) {
  const router = useRouter();
  const [isNotesModalOpen, setIsNotesModalOpen] = useState(false);
  const [isRemoveDialogOpen, setIsRemoveDialogOpen] = useState(false);
  const [activeLabels, setActiveLabels] = useState<string[]>([]);

  const updateGameState = useUpdateGameState();

  // Type-safe state validation
  const currentState: GameStateType = useMemo(() => {
    const state = gameDetail.currentState;
    return state && state in stateConfig ? (state as GameStateType) : 'Owned';
  }, [gameDetail.currentState]);

  const stateInfo = stateConfig[currentState];
  const StateIcon = stateInfo.icon;

  // Play statistics from backend GameDetailDto
  const hasPlayStats = gameDetail.timesPlayed > 0;
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
      console.error('Failed to update game state:', error);
    }
  };

  const handleLabelToggle = (labelId: string) => {
    setActiveLabels((prev) =>
      prev.includes(labelId) ? prev.filter((id) => id !== labelId) : [...prev, labelId]
    );
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
      <section
        className="rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] p-6"
        style={{ boxShadow: '0 4px 20px rgba(45, 42, 38, 0.1)' }}
      >
        {/* Action Bar */}
        <div className="mb-6 flex flex-wrap items-center gap-3">
          {/* State Dropdown */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                className={cn(
                  'font-quicksand font-semibold text-white',
                  stateInfo.bgColor
                )}
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
              {(Object.keys(stateConfig) as GameStateType[]).map((state) => {
                const config = stateConfig[state];
                const Icon = config.icon;
                return (
                  <DropdownMenuItem
                    key={state}
                    onClick={() => handleStateChange(state)}
                    className={cn(
                      'cursor-pointer',
                      state === currentState && 'bg-[rgba(45,42,38,0.04)]'
                    )}
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
          {gameDetail.hasPdfDocuments && (
            <Button
              variant="outline"
              className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]"
            >
              <FileText className="mr-2 h-4 w-4" />
              Regole PDF
            </Button>
          )}

          {/* Favorite Toggle */}
          <FavoriteToggle
            gameId={gameDetail.gameId}
            isFavorite={gameDetail.isFavorite}
            className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold"
          />

          {/* Spacer */}
          <div className="flex-grow" />

          {/* Remove Button */}
          <Button
            variant="ghost"
            className="font-quicksand font-semibold text-rose-500 hover:bg-rose-500/10 hover:text-rose-600"
            onClick={() => setIsRemoveDialogOpen(true)}
          >
            <Trash2 className="mr-2 h-4 w-4" />
            Rimuovi
          </Button>
        </div>

        {/* Labels Row */}
        <div className="mb-6 flex flex-wrap items-center gap-2 border-t border-[rgba(45,42,38,0.08)] pt-6">
          <span className="mr-2 font-nunito text-sm text-[#9C958A]">Etichette:</span>

          {predefinedLabels.map((label) => (
            <button
              key={label.id}
              onClick={() => handleLabelToggle(label.id)}
              className={cn(
                'inline-flex items-center rounded-full border px-3 py-1 font-nunito text-sm font-medium transition-all',
                activeLabels.includes(label.id)
                  ? label.color
                  : 'border-[rgba(45,42,38,0.12)] bg-transparent text-[#9C958A] hover:border-[rgba(45,42,38,0.2)] hover:text-[#6B665C]'
              )}
            >
              {label.name}
            </button>
          ))}

          {/* Add Label Button */}
          <button className="inline-flex items-center gap-1 rounded-full bg-[rgba(45,42,38,0.04)] px-3 py-1 font-nunito text-sm font-medium text-[#6B665C] transition-colors hover:bg-[rgba(45,42,38,0.08)]">
            <Plus className="h-3.5 w-3.5" />
            Aggiungi
          </button>
        </div>

        {/* Stats Grid */}
        <div className="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4">
          {/* Added Date */}
          <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-[#9C958A]">Aggiunto</p>
            <p className="font-quicksand text-base font-semibold text-[#2D2A26]">
              {formatDate(gameDetail.addedAt)}
            </p>
          </div>

          {/* Games Played */}
          <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-[#9C958A]">Partite giocate</p>
            <p className="font-quicksand text-base font-semibold text-[#2D2A26]">
              {stats.gamesPlayed}
            </p>
          </div>

          {/* Last Played */}
          <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-[#9C958A]">Ultima partita</p>
            <p className="font-quicksand text-base font-semibold text-[#2D2A26]">
              {stats.lastPlayed ? formatDate(stats.lastPlayed) : 'Mai'}
            </p>
          </div>

          {/* Win Rate */}
          <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
            <p className="mb-1 font-nunito text-xs text-[#9C958A]">Win rate</p>
            <p className="font-quicksand text-base font-semibold text-emerald-600">
              {stats.winRate ?? 'N/A'}
            </p>
          </div>
        </div>

        {/* Notes Section */}
        <div className="rounded-xl border border-[rgba(45,42,38,0.08)] bg-[rgba(45,42,38,0.02)] p-4">
          <div className="mb-2 flex items-center justify-between">
            <h4 className="font-quicksand text-sm font-semibold text-[#2D2A26]">Note personali</h4>
            <Button
              variant="ghost"
              size="sm"
              className="h-8 px-2 text-[#6B665C] hover:text-[hsl(25,95%,38%)]"
              onClick={() => setIsNotesModalOpen(true)}
            >
              <Edit2 className="h-3.5 w-3.5" />
            </Button>
          </div>
          {gameDetail.notes ? (
            <p className="font-nunito text-sm leading-relaxed text-[#6B665C]">
              {gameDetail.notes}
            </p>
          ) : (
            <p className="font-nunito text-sm italic text-[#9C958A]">
              Nessuna nota. Clicca per aggiungerne una.
            </p>
          )}
        </div>
      </section>

      {/* Edit Notes Modal */}
      <EditNotesModal
        isOpen={isNotesModalOpen}
        onClose={() => setIsNotesModalOpen(false)}
        gameId={gameDetail.gameId}
        gameTitle={gameDetail.gameTitle}
        currentNotes={gameDetail.notes}
      />

      {/* Remove Game Dialog */}
      <RemoveGameDialog
        isOpen={isRemoveDialogOpen}
        onClose={() => setIsRemoveDialogOpen(false)}
        gameId={gameDetail.gameId}
        gameTitle={gameDetail.gameTitle}
        onRemoved={() => {
          router.push('/library');
        }}
      />
    </>
  );
}

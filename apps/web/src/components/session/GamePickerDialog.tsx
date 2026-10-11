'use client';

/**
 * GamePickerDialog — Modal dialog for choosing a game from the user's library
 * and starting a new session with KB readiness validation.
 *
 * Features:
 * - Shows user's library games in a scrollable list
 * - Each game shows real-time KB readiness status via GamePickerItem
 * - Search games by title on the server (#4187), so a game past the first page is found
 * - Games without ready KB are disabled with tooltip
 * - On selection: creates session via contextual hand store, navigates to it
 * - Optional gameNightEventId for adding games to existing nights
 *
 * Plan 2 Task 6 — Session Flow v2.1
 */

import { useMemo, useState } from 'react';

import { Gamepad2, Loader2, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { TooltipProvider } from '@/components/ui/overlays/tooltip';
import { Input } from '@/components/ui/primitives/input';
import { useLibrarySearch } from '@/hooks/queries/useLibrarySearch';
import { useContextualHandStore } from '@/stores/contextual-hand';

import { GamePickerItem, type GamePickerGame } from './GamePickerItem';

// ─── Types ──────────────────────────────────────────────────────────────────

export interface GamePickerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** When adding a game to an existing game night */
  gameNightEventId?: string;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function GamePickerDialog({ open, onOpenChange, gameNightEventId }: GamePickerDialogProps) {
  const router = useRouter();
  const [search, setSearch] = useState('');

  // #4187: it asked for 200 games "to have them all", the server returns at most 100 and
  // said nothing, and the filter ran on the client. Now the server searches the library.
  const {
    items: libraryItems,
    totalCount,
    isLoading: isLibraryLoading,
    term,
  } = useLibrarySearch(search, {
    enabled: open, // only fetch when dialog is open
  });

  const startSession = useContextualHandStore(s => s.startSession);
  const isStarting = useContextualHandStore(s => s.isLoading);

  // Map library entries to the picker shape
  const games: GamePickerGame[] = useMemo(() => {
    return libraryItems.map(entry => ({
      id: entry.id,
      gameId: entry.gameId,
      gameTitle: entry.gameTitle,
      gameImageUrl: entry.gameImageUrl,
      gamePublisher: entry.gamePublisher,
      averageRating: entry.averageRating,
      minPlayers: entry.minPlayers,
      maxPlayers: entry.maxPlayers,
    }));
  }, [libraryItems]);

  // Handle game selection: start session and navigate
  const handleSelect = async (gameId: string) => {
    await startSession(gameId, [], gameNightEventId);

    // Read the newly created session from the store
    const { currentSession } = useContextualHandStore.getState();
    if (currentSession?.sessionId) {
      onOpenChange(false);
      router.push(`/sessions/${currentSession.sessionId}`);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-lg max-h-[80vh] flex flex-col gap-0"
        data-testid="game-picker-dialog"
      >
        <DialogHeader className="pb-3">
          <DialogTitle className="flex items-center gap-2">
            <Gamepad2 className="h-5 w-5 text-primary" />
            Scegli un gioco
          </DialogTitle>
          <DialogDescription>
            Seleziona un gioco dalla tua libreria per avviare una partita.
          </DialogDescription>
        </DialogHeader>

        {/* Search */}
        <div className="relative pb-3">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
          <Input
            autoFocus
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Cerca un gioco..."
            className="pl-9"
            data-testid="game-picker-search"
          />
        </div>

        {/* Game List */}
        <TooltipProvider delayDuration={300}>
          <div
            className="overflow-y-auto flex-1 min-h-0 space-y-1 pr-1 -mr-1"
            data-testid="game-picker-list"
          >
            {isLibraryLoading ? (
              <div className="flex items-center justify-center py-12 gap-2 text-muted-foreground">
                <Loader2 className="h-5 w-5 animate-spin" />
                <span className="text-sm">Caricamento libreria...</span>
              </div>
            ) : games.length === 0 ? (
              <div className="py-12 text-center">
                <Gamepad2 className="h-8 w-8 text-muted-foreground/50 mx-auto mb-2" />
                <p className="text-sm text-muted-foreground">
                  {term
                    ? 'Nessun gioco trovato per la ricerca.'
                    : 'Nessun gioco nella tua libreria.'}
                </p>
              </div>
            ) : (
              games.map(game => (
                <GamePickerItem
                  key={game.id}
                  game={game}
                  onSelect={handleSelect}
                  isSelecting={isStarting}
                />
              ))
            )}
          </div>
        </TooltipProvider>

        {/* Footer: count */}
        {!isLibraryLoading && games.length > 0 && (
          <div className="pt-3 border-t text-xs text-muted-foreground text-center">
            {games.length} di {totalCount} giochi
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

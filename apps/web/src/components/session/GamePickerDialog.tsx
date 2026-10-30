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
 * - On selection: starts the game within the night (`POST /game-nights/{id}/sessions`, the same call
 *   as the "▶ Avvia" action of the night live page) and goes to that page; a refusal is said in the dialog
 *
 * #4752: it used to create the session through the contextual hand store, on `POST /games/{id}/sessions`,
 * a route the backend does not register (405), with no word to the user. The only place this dialog is
 * mounted is a night, so the night is no longer optional.
 *
 * Plan 2 Task 6 — Session Flow v2.1
 */

import { useEffect, useMemo, useRef, useState } from 'react';

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
import { startGameErrorMessage, useStartNextGame } from '@/lib/game-nights/hooks/useStartNextGame';

import { GamePickerItem, type GamePickerGame } from './GamePickerItem';

// ─── Types ──────────────────────────────────────────────────────────────────

export interface GamePickerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The game night the chosen game is added to. */
  gameNightEventId: string;
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
    resultsMatchQuery,
    term,
  } = useLibrarySearch(search, {
    enabled: open, // only fetch when dialog is open
  });

  // The page that mounted the dialog may be gone by the time the request ends (route change): the game
  // started, but going to its night page then would bring back someone who has left.
  const isMounted = useRef(true);
  useEffect(() => {
    isMounted.current = true;
    return () => {
      isMounted.current = false;
    };
  }, []);

  // The way out sits on the hook, not on `mutate()`: a per-call callback is dropped when the mutation is reset
  // or the component unmounts, and the game would start with nothing happening here.
  const startGame = useStartNextGame(gameNightEventId, {
    onSuccess: () => {
      if (!isMounted.current) return;
      onOpenChange(false);
      router.push(`/game-nights/${gameNightEventId}/live`);
    },
  });
  const { reset: resetStartGame } = startGame;
  const isStarting = startGame.isPending;

  // A refusal belongs to the attempt that was refused: closing the dialog must not leave it for the next opening.
  // Never while the request is pending: `reset()` clears `isPending`, so a dialog closed and reopened mid-request
  // would enable the game again and send a second POST (the backend refuses it with a 409 that the reopened
  // dialog would then show, while the first request is about to take the page away). It resets once it settles.
  useEffect(() => {
    if (!open && !startGame.isPending) resetStartGame();
  }, [open, startGame.isPending, resetStartGame]);

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

  // An empty list that belongs to an older text says nothing about the current one: keep
  // the loading state until the answer arrives, instead of "no games".
  const isSearching = isLibraryLoading || (!resultsMatchQuery && games.length === 0);

  // Handle game selection: start the game in the night and go to the night page, where it is played.
  // The sessions of a night are SessionTracking sessions with no page under /sessions/{id} (#4156 D7).
  const handleSelect = (gameId: string) => {
    const game = games.find(g => g.gameId === gameId);
    if (!game) return;
    startGame.mutate({ gameId, gameTitle: game.gameTitle });
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
            {isSearching ? (
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

        {startGame.isError && (
          <p
            role="alert"
            data-testid="game-picker-start-error"
            className="mt-3 break-words rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {startGameErrorMessage(startGame.error)}
          </p>
        )}

        {/* Footer: count */}
        {!isSearching && games.length > 0 && (
          <div className="pt-3 border-t text-xs text-muted-foreground text-center">
            {games.length} di {totalCount} giochi
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

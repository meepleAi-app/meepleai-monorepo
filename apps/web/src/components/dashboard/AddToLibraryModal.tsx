'use client';

/**
 * AddToLibraryModal — Confirmation modal for adding a shared library game
 * to the user's personal library and creating a chat thread in one action.
 *
 * Orchestration flow:
 * 1. Add game to library via useAddGameToLibrary mutation
 * 2. Create chat thread via api.chat.createThread
 * 3. Call onSuccess with IDs so the chat drawer can open
 *
 * Error handling:
 * - If step 1 fails: show error, no side effects
 * - If step 2 fails: game stays in library, show "Riprova" to retry thread creation only
 */

import { useState, useCallback } from 'react';

import { Gamepad2 } from 'lucide-react';
import Image from 'next/image';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface AddToLibraryModalGame {
  id: string;
  name: string;
  imageUrl: string | null;
  playerCount: string;
}

export interface AddToLibraryModalProps {
  game: AddToLibraryModalGame | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (result: {
    gameId: string;
    threadId: string;
    agentId: string;
    gameName: string;
  }) => void;
}

type ModalState =
  | { phase: 'idle' }
  | { phase: 'loading' }
  | { phase: 'addFailed'; message: string }
  | { phase: 'threadFailed'; message: string };

// ============================================================================
// Component
// ============================================================================

export function AddToLibraryModal({ game, isOpen, onClose, onSuccess }: AddToLibraryModalProps) {
  const [state, setState] = useState<ModalState>({ phase: 'idle' });
  const addGameMutation = useAddGameToLibrary();

  const resetState = useCallback(() => {
    setState({ phase: 'idle' });
  }, []);

  const handleOpenChange = useCallback(
    (open: boolean) => {
      if (!open) {
        onClose();
        resetState();
      }
    },
    [onClose, resetState]
  );

  const createThread = useCallback(async (gameId: string, gameName: string) => {
    const thread = await api.chat.createThread({
      gameId,
      title: gameName,
    });
    return thread;
  }, []);

  const handleConfirm = useCallback(async () => {
    if (!game) return;

    setState({ phase: 'loading' });

    // Step 1: Add game to library
    try {
      await addGameMutation.mutateAsync({ gameId: game.id });
    } catch {
      setState({
        phase: 'addFailed',
        message: 'Non è stato possibile aggiungere il gioco alla libreria.',
      });
      return;
    }

    // Step 2: Create chat thread
    try {
      const thread = await createThread(game.id, game.name);
      setState({ phase: 'idle' });
      onSuccess({
        gameId: game.id,
        threadId: thread.id,
        agentId: thread.agentId ?? '',
        gameName: game.name,
      });
    } catch {
      setState({
        phase: 'threadFailed',
        message: 'Gioco aggiunto alla libreria, ma la creazione della chat è fallita. Riprova.',
      });
    }
  }, [game, addGameMutation, createThread, onSuccess]);

  const handleRetryThread = useCallback(async () => {
    if (!game) return;

    setState({ phase: 'loading' });

    try {
      const thread = await createThread(game.id, game.name);
      setState({ phase: 'idle' });
      onSuccess({
        gameId: game.id,
        threadId: thread.id,
        agentId: thread.agentId ?? '',
        gameName: game.name,
      });
    } catch {
      setState({
        phase: 'threadFailed',
        message: 'Gioco aggiunto alla libreria, ma la creazione della chat è fallita. Riprova.',
      });
    }
  }, [game, createThread, onSuccess]);

  const isLoading = state.phase === 'loading';
  const showContent = isOpen && game !== null;

  return (
    <Dialog open={showContent} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Aggiungi alla libreria</DialogTitle>
          <DialogDescription>
            Per chattare con l&apos;AI su questo gioco, aggiungilo alla tua libreria.
          </DialogDescription>
        </DialogHeader>

        {game && (
          <>
            {/* Game preview */}
            <div className="flex items-center gap-4 rounded-lg border border-border/50 p-3">
              <div className="h-16 w-16 flex-shrink-0 overflow-hidden rounded-md bg-muted">
                {game.imageUrl ? (
                  <Image
                    src={game.imageUrl}
                    alt={game.name}
                    width={64}
                    height={64}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <div
                    className="flex h-full w-full items-center justify-center"
                    data-testid="game-placeholder-icon"
                  >
                    <Gamepad2 className="h-8 w-8 text-muted-foreground" />
                  </div>
                )}
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold">{game.name}</p>
                <p className="text-sm text-muted-foreground">{game.playerCount} giocatori</p>
              </div>
            </div>

            {/* Error messages */}
            {state.phase === 'addFailed' && (
              <p className="text-sm text-destructive" role="alert">
                {state.message}
              </p>
            )}

            {state.phase === 'threadFailed' && (
              <p className="text-sm text-destructive" role="alert">
                {state.message}
              </p>
            )}

            {/* Actions */}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => handleOpenChange(false)}
                disabled={isLoading}
              >
                Annulla
              </Button>

              {state.phase === 'threadFailed' ? (
                <Button
                  type="button"
                  onClick={handleRetryThread}
                  disabled={isLoading}
                  className="bg-gradient-to-r from-purple-600 to-purple-500 text-white hover:from-purple-700 hover:to-purple-600"
                >
                  {isLoading ? 'Creando chat...' : 'Riprova'}
                </Button>
              ) : (
                <Button
                  type="button"
                  onClick={handleConfirm}
                  disabled={isLoading}
                  className="bg-gradient-to-r from-purple-600 to-purple-500 text-white hover:from-purple-700 hover:to-purple-600"
                >
                  {isLoading ? 'Aggiungendo...' : 'Aggiungi e Chiedi 🤖'}
                </Button>
              )}
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

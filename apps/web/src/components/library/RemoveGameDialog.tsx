/**
 * RemoveGameDialog Component (Issue #2464)
 *
 * Confirmation dialog for removing a game from user's library.
 * Features:
 * - AlertDialog for destructive action
 * - Game title display
 * - Confirmation required
 * - Optimistic UI update
 */

'use client';

import React from 'react';

import { Trash2, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { useRemoveGameFromLibrary } from '@/hooks/queries';

export interface RemoveGameDialogProps {
  /** Dialog open state */
  isOpen: boolean;
  /** Callback to close dialog */
  onClose: () => void;
  /** Game ID to remove */
  gameId: string;
  /** Game title for display */
  gameTitle: string;
  /** Callback when game is removed */
  onRemoved?: () => void;
}

export function RemoveGameDialog({
  isOpen,
  onClose,
  gameId,
  gameTitle,
  onRemoved,
}: RemoveGameDialogProps) {
  const removeMutation = useRemoveGameFromLibrary();

  const isLoading = removeMutation.isPending;

  const handleRemove = async () => {
    if (isLoading) return;

    try {
      await removeMutation.mutateAsync(gameId);
      toast.success(`${gameTitle} rimosso dalla tua libreria.`);
      onRemoved?.();
      onClose();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Impossibile rimuovere il gioco dalla libreria.'
      );
    }
  };

  return (
    <AlertDialog open={isOpen} onOpenChange={onClose}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Rimuovi dalla Libreria?</AlertDialogTitle>
          <AlertDialogDescription>
            Sei sicuro di voler rimuovere <strong>{gameTitle}</strong> dalla tua libreria?
            <br />
            Questa azione non può essere annullata.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isLoading}>Annulla</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleRemove}
            disabled={isLoading}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Rimozione...
              </>
            ) : (
              <>
                <Trash2 className="mr-2 h-4 w-4" />
                Rimuovi
              </>
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

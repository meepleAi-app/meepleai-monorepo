/**
 * BulkRemoveDialog Component (Issue #2613)
 *
 * Confirmation dialog for bulk removing games from library.
 * Shows count and list of games to be removed.
 */

'use client';

import { useState } from 'react';

import { Loader2, Trash2 } from 'lucide-react';

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
import { useRemoveGameFromLibrary } from '@/hooks/queries/useLibrary';

export interface BulkRemoveDialogProps {
  isOpen: boolean;
  onClose: () => void;
  gameIds: string[];
  gameTitles: string[];
  onSuccess?: () => void;
}

export function BulkRemoveDialog({
  isOpen,
  onClose,
  gameIds,
  gameTitles,
  onSuccess,
}: BulkRemoveDialogProps) {
  const [isRemoving, setIsRemoving] = useState(false);
  const removeGame = useRemoveGameFromLibrary();

  const handleConfirm = async () => {
    if (isRemoving || gameIds.length === 0) return;

    setIsRemoving(true);
    try {
      // Execute all removals in parallel
      const results = await Promise.allSettled(
        gameIds.map(id => removeGame.mutateAsync(id))
      );

      const successCount = results.filter(r => r.status === 'fulfilled').length;
      const failCount = results.filter(r => r.status === 'rejected').length;

      if (failCount === 0) {
        toast.success(`${successCount} giochi rimossi dalla libreria`);
      } else if (successCount > 0) {
        toast.warning(`${successCount} giochi rimossi, ${failCount} errori`);
      } else {
        toast.error('Impossibile rimuovere i giochi');
      }

      onSuccess?.();
      onClose();
    } catch (error) {
      toast.error('Errore durante la rimozione');
    } finally {
      setIsRemoving(false);
    }
  };

  const displayTitles = gameTitles.slice(0, 5);
  const remaining = gameTitles.length - 5;

  return (
    <AlertDialog open={isOpen} onOpenChange={open => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <Trash2 className="h-5 w-5 text-destructive" />
            Rimuovi {gameIds.length} giochi
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <p>
                Stai per rimuovere {gameIds.length} giochi dalla tua libreria.
                Questa azione non può essere annullata.
              </p>
              <div className="bg-muted rounded-md p-3 text-sm">
                <p className="font-medium mb-2">Giochi selezionati:</p>
                <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                  {displayTitles.map((title, i) => (
                    <li key={i} className="truncate">{title}</li>
                  ))}
                </ul>
                {remaining > 0 && (
                  <p className="text-xs mt-2 text-muted-foreground">
                    ...e altri {remaining} giochi
                  </p>
                )}
              </div>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isRemoving}>Annulla</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={isRemoving}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isRemoving ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Rimozione...
              </>
            ) : (
              <>
                <Trash2 className="mr-2 h-4 w-4" />
                Rimuovi {gameIds.length} giochi
              </>
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

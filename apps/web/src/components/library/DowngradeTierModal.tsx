/**
 * DowngradeTierModal Component (Task 12)
 *
 * Informational modal shown when a user is about to downgrade their tier.
 * Displays which games will be kept within the new quota and which ones
 * exceed the limit. The user must acknowledge before proceeding.
 *
 * Features:
 * - Loads downgrade preview from API
 * - Lists games to keep and games to remove
 * - Confirm button disabled when no games need removal
 * - Calls onComplete when user confirms
 */

'use client';

import React from 'react';

import { Loader2, Trash2, CheckCircle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryDowngradePreview } from '@/hooks/queries/useLibraryDowngrade';
import type { LibraryDowngradeGame } from '@/lib/api/schemas/library.schemas';

export interface DowngradeTierModalProps {
  /** New quota the user is downgrading to */
  newQuota: number;
  /** Dialog open state */
  open: boolean;
  /** Callback to control open state */
  onOpenChange: (open: boolean) => void;
  /** Callback when user confirms the downgrade */
  onComplete: () => void;
}

export function DowngradeTierModal({
  newQuota,
  open,
  onOpenChange,
  onComplete,
}: DowngradeTierModalProps) {
  const { data, isLoading } = useLibraryDowngradePreview(newQuota, open);

  const gamesToRemove: LibraryDowngradeGame[] = data?.gamesToRemove ?? [];
  const gamesToKeep: LibraryDowngradeGame[] = data?.gamesToKeep ?? [];

  const hasGamesToRemove = gamesToRemove.length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Anteprima cambio piano</DialogTitle>
          <DialogDescription>
            Con il nuovo piano potrai tenere al massimo {newQuota}{' '}
            {newQuota === 1 ? 'gioco' : 'giochi'} in libreria.
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center justify-center py-8 gap-2 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span>Caricamento anteprima...</span>
          </div>
        ) : (
          <div className="space-y-4">
            {hasGamesToRemove ? (
              <section aria-label="Giochi da rimuovere">
                <h3 className="text-sm font-semibold text-destructive flex items-center gap-1 mb-2">
                  <Trash2 className="h-4 w-4" />
                  Giochi che verranno rimossi ({gamesToRemove.length})
                </h3>
                <ul className="space-y-1 max-h-48 overflow-y-auto">
                  {gamesToRemove.map(game => (
                    <li
                      key={game.entryId}
                      className="text-sm px-2 py-1 rounded bg-destructive/10 text-destructive-foreground"
                    >
                      {game.gameTitle}
                      {game.isFavorite && (
                        <span className="ml-1 text-xs text-muted-foreground">(preferito)</span>
                      )}
                    </li>
                  ))}
                </ul>
              </section>
            ) : (
              <p className="text-sm text-muted-foreground flex items-center gap-1">
                <CheckCircle className="h-4 w-4 text-green-500" />
                Nessun gioco verrà rimosso con questo piano.
              </p>
            )}

            {gamesToKeep.length > 0 && (
              <section aria-label="Giochi che rimangono">
                <h3 className="text-sm font-semibold text-foreground flex items-center gap-1 mb-2">
                  <CheckCircle className="h-4 w-4 text-green-500" />
                  Giochi che rimarranno ({gamesToKeep.length})
                </h3>
                <ul className="space-y-1 max-h-32 overflow-y-auto">
                  {gamesToKeep.map(game => (
                    <li key={game.entryId} className="text-sm px-2 py-1 rounded bg-muted">
                      {game.gameTitle}
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Annulla
          </Button>
          <Button
            variant="destructive"
            disabled={isLoading || !hasGamesToRemove}
            onClick={onComplete}
          >
            {hasGamesToRemove
              ? `Rimuovi ${gamesToRemove.length} ${gamesToRemove.length === 1 ? 'gioco' : 'giochi'}`
              : 'Rimuovi'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

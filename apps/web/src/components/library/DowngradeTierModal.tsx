'use client';

import { useEffect, useState } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import {
  useBulkRemoveFromLibrary,
  useLibraryDowngradePreview,
} from '@/hooks/queries/useLibraryDowngrade';
import { useToast } from '@/hooks/useToast';

interface DowngradeTierModalProps {
  /** The new library quota the user is downgrading to */
  newQuota: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Called after the removal is completed successfully */
  onComplete: () => void;
}

/**
 * Modal shown when a user is about to downgrade to a tier with a smaller library quota.
 * Displays which games would be kept (highest priority) and lets the user select which
 * games to remove from their library.
 */
export function DowngradeTierModal({
  newQuota,
  open,
  onOpenChange,
  onComplete,
}: DowngradeTierModalProps) {
  const { data, isLoading, error: previewError } = useLibraryDowngradePreview(newQuota, open);
  const { mutate: bulkRemove, isPending } = useBulkRemoveFromLibrary();
  const [selectedToRemove, setSelectedToRemove] = useState<Set<string>>(new Set());
  const { toast } = useToast();

  useEffect(() => {
    if (open) setSelectedToRemove(new Set());
  }, [open]);

  const suggestedRemove = data?.gamesToRemove ?? [];
  const toKeep = data?.gamesToKeep ?? [];

  function toggleRemove(gameId: string) {
    setSelectedToRemove(prev => {
      const next = new Set(prev);
      if (next.has(gameId)) {
        next.delete(gameId);
      } else {
        next.add(gameId);
      }
      return next;
    });
  }

  function handleConfirm() {
    const ids = Array.from(selectedToRemove);
    if (ids.length === 0) return;
    bulkRemove(ids, {
      onSuccess: onComplete,
      onError: () => {
        toast({
          title: 'Errore',
          description: 'Impossibile rimuovere i giochi selezionati. Riprova.',
          variant: 'destructive',
        });
      },
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Gestisci la libreria ({newQuota} giochi max)</DialogTitle>
        </DialogHeader>

        {isLoading ? (
          <p className="text-muted-foreground py-8 text-center">Caricamento...</p>
        ) : previewError ? (
          <p className="text-destructive py-8 text-center text-sm">
            Impossibile caricare l&apos;anteprima. Riprova più tardi.
          </p>
        ) : (
          <div className="space-y-6">
            {/* Games that will be kept */}
            <div>
              <h3 className="font-medium mb-2 text-green-700">
                Giochi che verranno mantenuti ({toKeep.length})
              </h3>
              {toKeep.length > 0 ? (
                <div className="grid grid-cols-3 gap-2 opacity-70">
                  {toKeep.map(g => (
                    <MeepleCard
                      key={g.entryId}
                      entity="game"
                      variant="compact"
                      title={g.gameTitle}
                      imageUrl={g.gameImageUrl ?? undefined}
                    />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">Nessun gioco da mantenere.</p>
              )}
            </div>

            {/* Games suggested for removal */}
            <div>
              <h3 className="font-medium mb-2 text-destructive">
                Seleziona giochi da rimuovere ({selectedToRemove.size} selezionati)
              </h3>
              {suggestedRemove.length > 0 ? (
                <div className="space-y-2">
                  {suggestedRemove.map(g => (
                    <div
                      key={g.entryId}
                      className="flex items-center gap-3 p-2 rounded border cursor-pointer hover:bg-muted/50"
                      onClick={() => toggleRemove(g.gameId)}
                    >
                      <Checkbox
                        checked={selectedToRemove.has(g.gameId)}
                        onCheckedChange={() => toggleRemove(g.gameId)}
                      />
                      <div className="flex-1 min-w-0">
                        <MeepleCard
                          entity="game"
                          variant="compact"
                          title={g.gameTitle}
                          imageUrl={g.gameImageUrl ?? undefined}
                        />
                      </div>
                      {g.isFavorite && (
                        <span className="text-xs text-amber-500 shrink-0">&#9733; Preferito</span>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  La tua libreria rientra già nel nuovo limite.
                </p>
              )}
            </div>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Annulla
          </Button>
          <Button
            variant="destructive"
            onClick={handleConfirm}
            disabled={selectedToRemove.size === 0 || isPending}
          >
            Rimuovi {selectedToRemove.size > 0 ? `${selectedToRemove.size} ` : ''}giochi selezionati
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

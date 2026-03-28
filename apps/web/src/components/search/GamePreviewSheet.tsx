'use client';

import React, { useCallback, useState } from 'react';

import { Check } from 'lucide-react';
import { toast } from 'sonner';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { useLibraryQuota } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { BggGameSummary } from '@/lib/api/clients/gameNightBggClient';

export interface GamePreviewSheetProps {
  open: boolean;
  game: BggGameSummary | null;
  onOpenChange: (open: boolean) => void;
  onAdded: (result: { privateGameId: string; libraryEntryId: string }) => void;
}

export function GamePreviewSheet({ open, game, onOpenChange, onAdded }: GamePreviewSheetProps) {
  const [isAdding, setIsAdding] = useState(false);
  const [isAdded, setIsAdded] = useState(false);
  const { data: quota } = useLibraryQuota();

  const quotaReached = quota ? quota.remainingSlots <= 0 : false;

  const handleAdd = useCallback(async () => {
    if (!game || isAdding || isAdded) return;

    setIsAdding(true);
    try {
      const result = await api.gameNightBgg.importGame(game.bggId);
      setIsAdded(true);
      toast.success(`"${game.title}" aggiunto alla libreria!`);
      onAdded({ privateGameId: result.privateGameId, libraryEntryId: result.libraryEntryId });
    } catch (err) {
      const message = err instanceof Error ? err.message : "Errore durante l'aggiunta";
      toast.error(message);
    } finally {
      setIsAdding(false);
    }
  }, [game, isAdding, isAdded, onAdded]);

  const handleAddToWishlist = useCallback(() => {
    toast.info('Funzione wishlist in arrivo!');
  }, []);

  // Reset state when sheet closes
  const handleOpenChange = useCallback(
    (value: boolean) => {
      if (!value) {
        // Reset after animation completes
        setTimeout(() => {
          setIsAdded(false);
          setIsAdding(false);
        }, 300);
      }
      onOpenChange(value);
    },
    [onOpenChange]
  );

  if (!game) return null;

  return (
    <BottomSheet open={open} onOpenChange={handleOpenChange}>
      <div className="flex flex-col items-center gap-4 pb-4">
        {/* Game cover image */}
        {game.thumbnailUrl ? (
          <img
            src={game.thumbnailUrl}
            alt={game.title}
            className="h-40 w-40 rounded-xl object-cover shadow-lg"
          />
        ) : (
          <div className="flex h-40 w-40 items-center justify-center rounded-xl bg-white/10">
            <span className="text-4xl">🎲</span>
          </div>
        )}

        {/* Title + year */}
        <div className="text-center">
          <h3 className="text-lg font-semibold text-[var(--gaming-text-primary)]">{game.title}</h3>
          {game.yearPublished && (
            <p className="text-sm text-[var(--gaming-text-secondary)]">{game.yearPublished}</p>
          )}
        </div>

        {/* Add to library button */}
        {isAdded ? (
          <div className="flex w-full items-center justify-center gap-2 rounded-lg bg-green-500/20 py-3 text-green-400">
            <Check className="h-5 w-5" />
            <span className="font-medium">Aggiunto</span>
          </div>
        ) : (
          <>
            <GradientButton
              fullWidth
              size="lg"
              loading={isAdding}
              disabled={quotaReached}
              onClick={handleAdd}
            >
              Aggiungi alla Libreria
            </GradientButton>
            {quotaReached && (
              <p className="text-center text-xs text-[var(--gaming-text-secondary)]">
                Hai raggiunto il limite di {quota?.maxAllowed} giochi per il tuo piano{' '}
                {quota?.userTier}.
              </p>
            )}
          </>
        )}

        {/* Wishlist secondary action */}
        {!isAdded && (
          <button
            onClick={handleAddToWishlist}
            className="text-sm text-[var(--gaming-text-secondary)] transition-colors hover:text-[var(--gaming-text-primary)]"
          >
            Aggiungi alla Wishlist
          </button>
        )}
      </div>
    </BottomSheet>
  );
}

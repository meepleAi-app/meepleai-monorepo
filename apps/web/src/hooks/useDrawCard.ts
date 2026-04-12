'use client';

import { useEffect } from 'react';

import type { HandCard } from '@/lib/stores/card-hand-store';
import { useCardHand } from '@/lib/stores/card-hand-store';

type DrawCardInput = Omit<HandCard, 'addedAt'>;

/**
 * Deals a card into the hand on mount.
 * Pass `null` to skip (useful when entity data is still loading).
 */
export function useDrawCard(card: DrawCardInput | null) {
  const drawCard = useCardHand(s => s.drawCard);

  useEffect(() => {
    if (card) {
      drawCard(card);
    }
    // drawCard is stable (Zustand store method)
    // card?.id as dep to re-draw if entityId changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [card?.id]);
}

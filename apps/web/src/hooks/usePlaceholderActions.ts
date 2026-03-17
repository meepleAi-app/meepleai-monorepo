'use client';

import { useState, useCallback } from 'react';

import type { HandCard } from '@/stores/use-card-hand';

export type PlaceholderActionType = 'search-agent' | 'search-game' | 'start-session' | 'toolkit';

/**
 * Hook that intercepts clicks on placeholder action cards and opens the
 * corresponding action sheet instead of navigating to a page.
 *
 * @returns handleCardClick — call on any card click; returns true if the card
 *   was a placeholder (and the sheet was opened), false otherwise.
 * @returns activeSheet — the currently open sheet type, or null if none.
 * @returns closeSheet — closes the active sheet.
 */
export function usePlaceholderActions() {
  const [activeSheet, setActiveSheet] = useState<PlaceholderActionType | null>(null);

  const handleCardClick = useCallback((card: HandCard): boolean => {
    if (card.isPlaceholder && card.placeholderAction) {
      setActiveSheet(card.placeholderAction as PlaceholderActionType);
      return true;
    }
    return false;
  }, []);

  const closeSheet = useCallback(() => {
    setActiveSheet(null);
  }, []);

  return { handleCardClick, activeSheet, closeSheet };
}

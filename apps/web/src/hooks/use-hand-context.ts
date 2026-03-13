/**
 * useHandContext - Zustand store for "hand of cards" navigation state
 *
 * Manages the collection of cards in the user's hand, focus state,
 * and swipe navigation for the mobile card-focus layout.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export type HandContextType =
  | 'library'
  | 'sessions'
  | 'agents'
  | 'chats'
  | 'kbs'
  | 'toolkits'
  | 'players';

const MAX_HAND_SIZE = 7;

// ============================================================================
// Store
// ============================================================================

interface HandContextState {
  cards: HandCard[];
  focusedIdx: number;
  handContext: HandContextType;
  addCard: (card: HandCard) => void;
  removeCard: (id: string) => void;
  focusCard: (index: number) => void;
  swipeNext: () => void;
  swipePrev: () => void;
  setHandContext: (context: HandContextType) => void;
  clear: () => void;
}

export const useHandContext = create<HandContextState>((set, get) => ({
  cards: [],
  focusedIdx: -1,
  handContext: 'library',

  addCard: (card) => {
    const { cards } = get();
    // Skip duplicates — focus the existing card instead
    if (cards.some((c) => c.id === card.id)) {
      const idx = cards.findIndex((c) => c.id === card.id);
      set({ focusedIdx: idx });
      return;
    }
    // Enforce max hand size — drop oldest (first) if full
    const next =
      cards.length >= MAX_HAND_SIZE
        ? [...cards.slice(1), card]
        : [...cards, card];
    set({ cards: next, focusedIdx: next.length - 1 });
  },

  removeCard: (id) => {
    const { cards, focusedIdx } = get();
    const idx = cards.findIndex((c) => c.id === id);
    if (idx === -1) return;
    const next = cards.filter((c) => c.id !== id);
    const newFocused =
      next.length === 0
        ? -1
        : focusedIdx >= next.length
          ? next.length - 1
          : focusedIdx > idx
            ? focusedIdx - 1
            : focusedIdx;
    set({ cards: next, focusedIdx: newFocused });
  },

  focusCard: (index) => {
    const { cards } = get();
    if (index >= 0 && index < cards.length) {
      set({ focusedIdx: index });
    }
  },

  swipeNext: () => {
    const { focusedIdx, cards } = get();
    if (focusedIdx < cards.length - 1) {
      set({ focusedIdx: focusedIdx + 1 });
    }
  },

  swipePrev: () => {
    const { focusedIdx } = get();
    if (focusedIdx > 0) {
      set({ focusedIdx: focusedIdx - 1 });
    }
  },

  setHandContext: (context) => set({ handContext: context }),

  clear: () => set({ cards: [], focusedIdx: -1 }),
}));

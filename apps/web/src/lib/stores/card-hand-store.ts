// apps/web/src/lib/stores/card-hand-store.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface HandCard {
  id: string; // `${entityType}:${entityId}`
  entityType: MeepleEntityType;
  entityId: string;
  label: string;
  sublabel?: string;
  href: string;
  pinned: boolean;
  addedAt: number;
}

interface CardHandStore {
  cards: HandCard[];
  drawCard: (card: Omit<HandCard, 'addedAt'>) => void;
  discardCard: (id: string) => void;
  pinCard: (id: string) => void;
  unpinCard: (id: string) => void;
  clearHand: () => void;
}

const MAX_CARDS = 10;

function evict(cards: HandCard[]): HandCard[] {
  let result = cards;
  while (result.length > MAX_CARDS) {
    const oldest = [...result].filter(c => !c.pinned).sort((a, b) => a.addedAt - b.addedAt)[0];
    if (!oldest) break; // all pinned, cannot evict further
    result = result.filter(c => c.id !== oldest.id);
  }
  return result;
}

export const useCardHand = create<CardHandStore>()(
  persist(
    set => ({
      cards: [],
      drawCard: card =>
        set(s => {
          const existing = s.cards.find(c => c.id === card.id);
          if (existing) {
            return {
              cards: evict(
                s.cards.map(c =>
                  c.id === card.id ? { ...c, ...card, pinned: c.pinned, addedAt: Date.now() } : c
                )
              ),
            };
          }
          return { cards: evict([...s.cards, { ...card, addedAt: Date.now() }]) };
        }),
      discardCard: id => set(s => ({ cards: s.cards.filter(c => c.id !== id) })),
      pinCard: id =>
        set(s => ({ cards: s.cards.map(c => (c.id === id ? { ...c, pinned: true } : c)) })),
      unpinCard: id =>
        set(s => ({ cards: s.cards.map(c => (c.id === id ? { ...c, pinned: false } : c)) })),
      clearHand: () => set({ cards: [] }),
    }),
    {
      name: 'meepleai:hand',
      partialize: state => ({ cards: state.cards.filter(c => c.pinned) }),
    }
  )
);

export const selectPinnedCards = (s: CardHandStore) => s.cards.filter(c => c.pinned);

export const selectRecentCards = (s: CardHandStore) =>
  s.cards.filter(c => !c.pinned).sort((a, b) => b.addedAt - a.addedAt);

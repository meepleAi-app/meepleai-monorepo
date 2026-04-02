import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type { CounterConfig } from '@/lib/types/standalone-toolkit';

// ── Card Deck State ─────────────────────────────────────────────────

export interface CardDeckState {
  deckId: string;
  name: string;
  totalCards: number;
  cardFaces: string[];
  drawPile: string[];
  discardPile: string[];
  reshuffleOnEmpty: boolean;
  lastDrawnCard: string | null;
  undoSnapshot: { drawPile: string[]; discardPile: string[] } | null;
  undoExpiry: number | null;
}

// ── Counter State ───────────────────────────────────────────────────

export interface CounterState {
  id: string;
  name: string;
  value: number;
  min?: number;
  max?: number;
  initialValue: number;
}

// ── Randomizer State ────────────────────────────────────────────────

export interface RandomizerState {
  originalItems: string[];
  remainingItems: string[];
  lastExtracted: string | null;
}

// ── Store ───────────────────────────────────────────────────────────

interface StandaloneToolkitStore {
  // Card Deck
  decks: Record<string, CardDeckState>;
  initDeck: (deckId: string, name: string, cards: string[], reshuffleOnEmpty: boolean) => void;
  drawCard: (deckId: string) => string | null;
  discardCard: (deckId: string, card: string) => void;
  shuffleDeck: (deckId: string) => void;
  resetDeck: (deckId: string) => void;
  undoDraw: (deckId: string) => boolean;

  // Counter
  counters: CounterState[];
  initCounters: (configs: CounterConfig[]) => void;
  incrementCounter: (id: string, delta?: number) => void;
  decrementCounter: (id: string, delta?: number) => void;
  resetCounter: (id: string) => void;
  setCounter: (id: string, value: number) => void;
  addCounter: (config: CounterConfig) => void;
  removeCounter: (id: string) => void;

  // Randomizer
  randomizer: RandomizerState;
  setRandomizerItems: (items: string[]) => void;
  extractRandom: () => string | null;
  resetRandomizer: () => void;
}

function shuffleArray<T>(arr: T[]): T[] {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

const UNDO_WINDOW_MS = 30_000;

export const useStandaloneToolkitStore = create<StandaloneToolkitStore>()(
  persist(
    (set, get) => ({
      // ── Card Deck ──────────────────────────────────────────────────
      decks: {},

      initDeck: (deckId, name, cards, reshuffleOnEmpty) => {
        const drawPile = shuffleArray(cards);
        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              deckId,
              name,
              totalCards: cards.length,
              cardFaces: cards,
              drawPile,
              discardPile: [],
              reshuffleOnEmpty,
              lastDrawnCard: null,
              undoSnapshot: null,
              undoExpiry: null,
            },
          },
        }));
      },

      drawCard: deckId => {
        const deck = get().decks[deckId];
        if (!deck) return null;

        let drawPile = [...deck.drawPile];
        let discardPile = [...deck.discardPile];

        if (drawPile.length === 0) {
          if (!deck.reshuffleOnEmpty || discardPile.length === 0) return null;
          drawPile = shuffleArray(discardPile);
          discardPile = [];
        }

        const snapshot = { drawPile, discardPile };
        const [card, ...rest] = drawPile;

        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              ...deck,
              drawPile: rest,
              lastDrawnCard: card,
              undoSnapshot: snapshot,
              undoExpiry: Date.now() + UNDO_WINDOW_MS,
            },
          },
        }));
        return card;
      },

      discardCard: (deckId, card) => {
        const deck = get().decks[deckId];
        if (!deck) return;
        const snapshot = { drawPile: [...deck.drawPile], discardPile: [...deck.discardPile] };
        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              ...deck,
              discardPile: [...deck.discardPile, card],
              undoSnapshot: snapshot,
              undoExpiry: Date.now() + UNDO_WINDOW_MS,
            },
          },
        }));
      },

      shuffleDeck: deckId => {
        const deck = get().decks[deckId];
        if (!deck) return;
        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              ...deck,
              drawPile: shuffleArray(deck.drawPile),
              undoSnapshot: null,
              undoExpiry: null,
            },
          },
        }));
      },

      resetDeck: deckId => {
        const deck = get().decks[deckId];
        if (!deck) return;
        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              ...deck,
              drawPile: shuffleArray(deck.cardFaces),
              discardPile: [],
              lastDrawnCard: null,
              undoSnapshot: null,
              undoExpiry: null,
            },
          },
        }));
      },

      undoDraw: deckId => {
        const deck = get().decks[deckId];
        if (!deck?.undoSnapshot || !deck.undoExpiry) return false;
        if (Date.now() > deck.undoExpiry) return false;

        set(s => ({
          decks: {
            ...s.decks,
            [deckId]: {
              ...deck,
              drawPile: deck.undoSnapshot!.drawPile,
              discardPile: deck.undoSnapshot!.discardPile,
              lastDrawnCard: null,
              undoSnapshot: null,
              undoExpiry: null,
            },
          },
        }));
        return true;
      },

      // ── Counter ───────────────────────────────────────────────────
      counters: [],

      initCounters: configs => {
        set({
          counters: configs.map(c => ({
            id: c.id,
            name: c.name,
            value: c.initialValue,
            min: c.min,
            max: c.max,
            initialValue: c.initialValue,
          })),
        });
      },

      incrementCounter: (id, delta = 1) => {
        set(s => ({
          counters: s.counters.map(c => {
            if (c.id !== id) return c;
            const next = c.value + delta;
            return { ...c, value: c.max !== undefined ? Math.min(next, c.max) : next };
          }),
        }));
      },

      decrementCounter: (id, delta = 1) => {
        set(s => ({
          counters: s.counters.map(c => {
            if (c.id !== id) return c;
            const next = c.value - delta;
            return { ...c, value: c.min !== undefined ? Math.max(next, c.min) : next };
          }),
        }));
      },

      resetCounter: id => {
        set(s => ({
          counters: s.counters.map(c => (c.id === id ? { ...c, value: c.initialValue } : c)),
        }));
      },

      setCounter: (id, value) => {
        set(s => ({
          counters: s.counters.map(c => {
            if (c.id !== id) return c;
            const clamped =
              c.min !== undefined && value < c.min
                ? c.min
                : c.max !== undefined && value > c.max
                  ? c.max
                  : value;
            return { ...c, value: clamped };
          }),
        }));
      },

      addCounter: config => {
        set(s => ({
          counters: [...s.counters, { ...config, value: config.initialValue }],
        }));
      },

      removeCounter: id => {
        set(s => ({ counters: s.counters.filter(c => c.id !== id) }));
      },

      // ── Randomizer ────────────────────────────────────────────────
      randomizer: { originalItems: [], remainingItems: [], lastExtracted: null },

      setRandomizerItems: items => {
        set({
          randomizer: { originalItems: items, remainingItems: [...items], lastExtracted: null },
        });
      },

      extractRandom: () => {
        const { randomizer } = get();
        if (randomizer.remainingItems.length === 0) return null;
        const idx = Math.floor(Math.random() * randomizer.remainingItems.length);
        const extracted = randomizer.remainingItems[idx];
        const remaining = randomizer.remainingItems.filter((_, i) => i !== idx);
        set({ randomizer: { ...randomizer, remainingItems: remaining, lastExtracted: extracted } });
        return extracted;
      },

      resetRandomizer: () => {
        set(s => ({
          randomizer: {
            ...s.randomizer,
            remainingItems: [...s.randomizer.originalItems],
            lastExtracted: null,
          },
        }));
      },
    }),
    {
      name: 'meepleai:toolkit:state',
      partialize: s => ({ counters: s.counters, randomizer: s.randomizer }),
    }
  )
);

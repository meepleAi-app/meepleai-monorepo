'use client';

import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
  isPlaceholder?: boolean;
  placeholderAction?: string;
}

const MAX_HAND_SIZE = 10;
const STORAGE_KEY = 'meeple-card-hand';
const PINS_KEY = 'meeple-card-pins';
const EXPANDED_KEY = 'meeple-card-stack-expanded';
const HAND_COLLAPSED_KEY = 'meeple-hand-collapsed';

// ---------------------------------------------------------------------------
// sessionStorage helpers
// ---------------------------------------------------------------------------

function readCards(): HandCard[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as HandCard[]) : [];
  } catch {
    return [];
  }
}

function writeCards(cards: HandCard[]): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(cards));
  } catch {
    /* full or unavailable */
  }
  window.dispatchEvent(new Event('card-hand-change'));
}

function readPins(): Set<string> {
  if (typeof window === 'undefined') return new Set();
  try {
    const raw = localStorage.getItem(PINS_KEY);
    return raw ? new Set(JSON.parse(raw) as string[]) : new Set();
  } catch {
    return new Set();
  }
}

function writePins(pins: Set<string>): void {
  if (typeof window === 'undefined') return;
  try {
    localStorage.setItem(PINS_KEY, JSON.stringify([...pins]));
  } catch {
    /* ignore */
  }
}

function readExpanded(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(EXPANDED_KEY) === 'true';
}

function writeExpanded(expanded: boolean): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(EXPANDED_KEY, String(expanded));
}

function readHandCollapsed(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(HAND_COLLAPSED_KEY) === 'true';
}

function writeHandCollapsed(collapsed: boolean): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(HAND_COLLAPSED_KEY, String(collapsed));
}

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

interface CardHandState {
  cards: HandCard[];
  focusedIdx: number;
  pinnedIds: Set<string>;
  protectedIds: Set<string>;
  maxHandSize: number;
  context: 'user' | 'admin';
  expandedStack: boolean;
  isHandCollapsed: boolean;
  highlightEntity: MeepleEntityType | null;

  drawCard: (card: HandCard) => void;
  discardCard: (id: string) => void;
  focusCard: (index: number) => void;
  focusByHref: (href: string) => void;
  pinCard: (id: string) => void;
  unpinCard: (id: string) => void;
  protectCard: (id: string) => void;
  unprotectCard: (id: string) => void;
  swipeNext: () => void;
  swipePrev: () => void;
  toggleContext: () => void;
  toggleExpandStack: () => void;
  collapseHand: () => void;
  expandHand: () => void;
  setHighlightEntity: (entity: MeepleEntityType | null) => void;
  clear: () => void;
}

export const useCardHand = create<CardHandState>((set, get) => ({
  cards: readCards(),
  focusedIdx: -1,
  pinnedIds: readPins(),
  protectedIds: new Set(),
  maxHandSize: MAX_HAND_SIZE,
  context: 'user',
  expandedStack: readExpanded(),
  isHandCollapsed: readHandCollapsed(),
  highlightEntity: null,

  drawCard: card => {
    const { cards, pinnedIds, protectedIds, maxHandSize } = get();

    // Duplicate check — focus existing card
    const existingIdx = cards.findIndex(c => c.id === card.id);
    if (existingIdx >= 0) {
      set({ focusedIdx: existingIdx });
      return;
    }

    let next = [...cards, card];

    // FIFO eviction if over limit — skip pinned, protected, AND placeholder cards
    if (next.length > maxHandSize) {
      const evictIdx = next.findIndex(
        c => !pinnedIds.has(c.id) && !protectedIds.has(c.id) && !c.isPlaceholder
      );
      if (evictIdx >= 0) {
        next = [...next.slice(0, evictIdx), ...next.slice(evictIdx + 1)];
      } else {
        // All cards pinned, protected, or placeholder — drop the oldest (edge case)
        next = next.slice(1);
      }
    }

    writeCards(next);
    set({ cards: next, focusedIdx: next.length - 1 });
  },

  discardCard: id => {
    const { cards, focusedIdx, pinnedIds } = get();
    const idx = cards.findIndex(c => c.id === id);
    if (idx === -1) return;

    const next = cards.filter(c => c.id !== id);
    const newPins = new Set(pinnedIds);
    newPins.delete(id);

    let newFocused: number;
    if (next.length === 0) {
      newFocused = -1;
    } else if (focusedIdx >= next.length) {
      newFocused = next.length - 1;
    } else if (focusedIdx > idx) {
      newFocused = focusedIdx - 1;
    } else if (focusedIdx === idx) {
      // Discarding focused card — move to next (or previous if last)
      newFocused = Math.min(focusedIdx, next.length - 1);
    } else {
      newFocused = focusedIdx;
    }

    writeCards(next);
    writePins(newPins);
    set({ cards: next, focusedIdx: newFocused, pinnedIds: newPins });
  },

  focusCard: index => {
    const { cards } = get();
    if (index >= 0 && index < cards.length) {
      set({ focusedIdx: index });
    }
  },

  focusByHref: href => {
    const { cards } = get();
    const idx = cards.findIndex(c => c.href === href);
    if (idx >= 0) {
      set({ focusedIdx: idx });
    }
  },

  pinCard: id => {
    const { pinnedIds } = get();
    const next = new Set(pinnedIds);
    next.add(id);
    writePins(next);
    set({ pinnedIds: next });
  },

  unpinCard: id => {
    const { pinnedIds } = get();
    const next = new Set(pinnedIds);
    next.delete(id);
    writePins(next);
    set({ pinnedIds: next });
  },

  protectCard: id => {
    set(state => ({ protectedIds: new Set([...state.protectedIds, id]) }));
  },

  unprotectCard: id => {
    set(state => {
      const next = new Set(state.protectedIds);
      next.delete(id);
      return { protectedIds: next };
    });
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

  toggleContext: () => {
    const { context } = get();
    set({ context: context === 'user' ? 'admin' : 'user' });
  },

  toggleExpandStack: () => {
    const { expandedStack } = get();
    writeExpanded(!expandedStack);
    set({ expandedStack: !expandedStack });
  },

  collapseHand: () => {
    writeHandCollapsed(true);
    set({ isHandCollapsed: true });
  },

  expandHand: () => {
    writeHandCollapsed(false);
    set({ isHandCollapsed: false });
  },

  setHighlightEntity: entity => set({ highlightEntity: entity }),

  clear: () => {
    const { cards, pinnedIds } = get();
    const pinned = cards.filter(c => pinnedIds.has(c.id));
    writeCards(pinned);
    set({
      cards: pinned,
      focusedIdx: pinned.length > 0 ? pinned.length - 1 : -1,
    });
  },
}));

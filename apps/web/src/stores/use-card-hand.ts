/**
 * @deprecated This store is a compatibility stub for components scheduled for
 * deletion in Task 5 (DesktopHandRail, HandRailItem, HandRailToolbar).
 * Do NOT add new consumers — use useRecentsStore from ./use-recents instead.
 */
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

// Noop stub — returns empty state, all mutations are no-ops.
// Only kept alive so that the Task-5 deletion targets (DesktopHandRail,
// HandRailItem, HandRailToolbar) continue to typecheck until they are removed.
export const useCardHand = create<CardHandState>(() => ({
  cards: [],
  focusedIdx: -1,
  pinnedIds: new Set(),
  protectedIds: new Set(),
  maxHandSize: 10,
  context: 'user',
  expandedStack: false,
  isHandCollapsed: false,
  highlightEntity: null,

  drawCard: () => {},
  discardCard: () => {},
  focusCard: () => {},
  focusByHref: () => {},
  pinCard: () => {},
  unpinCard: () => {},
  protectCard: () => {},
  unprotectCard: () => {},
  swipeNext: () => {},
  swipePrev: () => {},
  toggleContext: () => {},
  toggleExpandStack: () => {},
  collapseHand: () => {},
  expandHand: () => {},
  setHighlightEntity: () => {},
  clear: () => {},
}));

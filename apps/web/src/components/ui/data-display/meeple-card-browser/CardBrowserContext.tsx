'use client';

import React, { createContext, useContext, useCallback, useState, useEffect } from 'react';

import type { MeepleEntityType } from '../meeple-card-styles';

export interface CardRef {
  id: string;
  entity: MeepleEntityType;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  color: string; // HSL entity color
}

export interface TapOrigin {
  x: number;
  y: number;
}

interface CardBrowserState {
  isOpen: boolean;
  cards: CardRef[];
  currentIndex: number;
  history: CardRef[];
  origin?: TapOrigin;
}

interface CardBrowserActions {
  open: (cards: CardRef[], index: number, origin?: TapOrigin) => void;
  close: () => void;
  setIndex: (index: number) => void;
  navigateTo: (card: CardRef) => void;
  clearHistory: () => void;
}

type CardBrowserContextValue = CardBrowserState & CardBrowserActions;

const CardBrowserContext = createContext<CardBrowserContextValue | null>(null);

const MAX_HISTORY = 50;
const STORAGE_KEY = 'meeple-card-browser-history';

function loadHistory(): CardRef[] {
  if (typeof window === 'undefined') return [];
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY);
    return stored ? JSON.parse(stored) : [];
  } catch {
    return [];
  }
}

function saveHistory(history: CardRef[]) {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(history));
  } catch {
    /* quota exceeded -- silently ignore */
  }
}

export function CardBrowserProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<CardBrowserState>({
    isOpen: false,
    cards: [],
    currentIndex: 0,
    history: [],
  });

  useEffect(() => {
    setState(prev => ({ ...prev, history: loadHistory() }));
  }, []);

  useEffect(() => {
    saveHistory(state.history);
  }, [state.history]);

  const pushHistory = useCallback((card: CardRef) => {
    setState(prev => {
      const last = prev.history[prev.history.length - 1];
      if (last?.id === card.id) return prev;
      const newHistory = [...prev.history, card];
      if (newHistory.length > MAX_HISTORY) newHistory.shift();
      return { ...prev, history: newHistory };
    });
  }, []);

  const open = useCallback(
    (cards: CardRef[], index: number, origin?: TapOrigin) => {
      const card = cards[index];
      setState(prev => ({ ...prev, isOpen: true, cards, currentIndex: index, origin }));
      if (card) pushHistory(card);
    },
    [pushHistory]
  );

  const close = useCallback(() => {
    setState(prev => ({ ...prev, isOpen: false }));
  }, []);

  const setIndex = useCallback(
    (index: number) => {
      setState(prev => {
        const card = prev.cards[index];
        if (card) pushHistory(card);
        return { ...prev, currentIndex: index };
      });
    },
    [pushHistory]
  );

  const navigateTo = useCallback(
    (card: CardRef) => {
      pushHistory(card);
      setState(prev => {
        const existingIndex = prev.cards.findIndex(c => c.id === card.id);
        if (existingIndex >= 0) {
          return { ...prev, currentIndex: existingIndex };
        }
        const newCards = [...prev.cards, card];
        return { ...prev, cards: newCards, currentIndex: newCards.length - 1 };
      });
    },
    [pushHistory]
  );

  const clearHistory = useCallback(() => {
    setState(prev => ({ ...prev, history: [] }));
  }, []);

  return (
    <CardBrowserContext.Provider
      value={{ ...state, open, close, setIndex, navigateTo, clearHistory }}
    >
      {children}
    </CardBrowserContext.Provider>
  );
}

export function useCardBrowser(): CardBrowserContextValue {
  const context = useContext(CardBrowserContext);
  if (!context) throw new Error('useCardBrowser must be used within CardBrowserProvider');
  return context;
}

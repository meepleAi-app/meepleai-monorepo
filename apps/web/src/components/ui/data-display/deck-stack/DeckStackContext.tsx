'use client';
import { createContext, useContext, useState, useCallback, useMemo } from 'react';

import type { MeepleEntityType } from '../meeple-card-styles';
import type { DeckStackState, DeckStackContextValue, DeckStackItem } from './deck-stack-types';

const INITIAL_STATE: DeckStackState = {
  isOpen: false,
  sourceEntityType: null,
  items: [],
  anchorRect: null,
};

const DeckStackCtx = createContext<DeckStackContextValue | null>(null);

export function DeckStackProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<DeckStackState>(INITIAL_STATE);

  const openDeckStack = useCallback(
    (entityType: MeepleEntityType, items: DeckStackItem[], anchorRect: DOMRect) => {
      setState({ isOpen: true, sourceEntityType: entityType, items, anchorRect });
    },
    []
  );

  const closeDeckStack = useCallback(() => {
    setState(INITIAL_STATE);
  }, []);

  const value = useMemo(
    () => ({ state, openDeckStack, closeDeckStack }),
    [state, openDeckStack, closeDeckStack]
  );

  return <DeckStackCtx.Provider value={value}>{children}</DeckStackCtx.Provider>;
}

export function useDeckStack(): DeckStackContextValue {
  const ctx = useContext(DeckStackCtx);
  if (!ctx) throw new Error('useDeckStack must be used within DeckStackProvider');
  return ctx;
}

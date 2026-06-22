/**
 * StatePreviewProvider — heavy implementation behind dynamic gate.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4: this module is loaded via
 * dynamic({ssr:false}) from state-preview-loader.tsx so its bundle cost
 * tree-shakes out of production builds.
 *
 * CRITICAL: do NOT import this file directly from app code. ESLint rule
 * `no-restricted-imports` enforces consumption via the barrel `index.ts`
 * (which re-exports the dynamic loader, not this provider).
 */

'use client';

import { createContext, useCallback, useState } from 'react';

import type {
  StateKind,
  StatePreviewContextValue,
  StatePreviewProviderProps,
} from './state-preview-types';

const DEFAULT_CONTEXT: StatePreviewContextValue = {
  states: {},
  setStateFor: () => {},
};

export const StatePreviewContext = createContext<StatePreviewContextValue>(DEFAULT_CONTEXT);

export function StatePreviewProvider({ children }: StatePreviewProviderProps) {
  const [states, setStates] = useState<Record<string, StateKind>>({});

  // Belt-and-suspenders: even if the provider somehow lands in prod bundle,
  // setStateFor becomes a no-op (DEC-4 hardening).
  const setStateFor = useCallback((pageId: string, state: StateKind) => {
    if (process.env.NODE_ENV === 'production') return;
    setStates(prev => ({ ...prev, [pageId]: state }));
  }, []);

  return (
    <StatePreviewContext.Provider value={{ states, setStateFor }}>
      {children}
    </StatePreviewContext.Provider>
  );
}

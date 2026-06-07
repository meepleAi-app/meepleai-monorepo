/**
 * useStatePreview — hook for consumers to read/write preview state for a pageId.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4 hardening: in production builds the hook
 * short-circuits to a fixed `default` return without touching context, so even
 * if a consumer somehow renders the toggle in prod (e.g. forgot conditional),
 * no state mutations propagate.
 */

import { useContext } from 'react';

import { StatePreviewContext } from './state-preview-provider';

import type { StateKind } from './state-preview-types';

interface UseStatePreviewResult {
  state: StateKind;
  setStateFor: (pageId: string, state: StateKind) => void;
}

const PROD_NOOP: UseStatePreviewResult = {
  state: 'default',
  setStateFor: () => {},
};

export function useStatePreview(pageId: string): UseStatePreviewResult {
  const ctx = useContext(StatePreviewContext);

  if (process.env.NODE_ENV === 'production') {
    return PROD_NOOP;
  }

  return {
    state: ctx.states[pageId] ?? 'default',
    setStateFor: ctx.setStateFor,
  };
}

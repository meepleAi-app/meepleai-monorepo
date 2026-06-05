/**
 * StatePreview primitive types — dev-only state simulation for design preview.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4: provider behind dynamic({ssr:false}) so
 * the implementation tree-shakes out of production builds.
 */

import type { ReactNode } from 'react';

export type StateKind = 'default' | 'empty' | 'loading' | 'error' | 'offline';

export interface StatePreviewContextValue {
  states: Record<string, StateKind>;
  setStateFor: (pageId: string, state: StateKind) => void;
}

export interface StatePreviewProviderProps {
  children: ReactNode;
}

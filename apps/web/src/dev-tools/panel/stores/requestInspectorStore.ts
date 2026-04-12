/**
 * requestInspectorStore — Zustand vanilla store for the Request Inspector.
 *
 * Keeps a ring buffer of the last 50 HTTP requests (newest first).
 */

import { createStore } from 'zustand/vanilla';

import type { InspectorEntry } from '@/dev-tools/types';

let counter = 0;

export function generateId(): string {
  return `req-${++counter}`;
}

export interface RequestInspectorState {
  entries: InspectorEntry[];
  record: (entry: InspectorEntry) => void;
  clear: () => void;
}

const MAX_ENTRIES = 50;

export function createRequestInspectorStore() {
  return createStore<RequestInspectorState>(set => ({
    entries: [],
    record: (entry: InspectorEntry) =>
      set(state => ({
        entries: [entry, ...state.entries].slice(0, MAX_ENTRIES),
      })),
    clear: () => set({ entries: [] }),
  }));
}

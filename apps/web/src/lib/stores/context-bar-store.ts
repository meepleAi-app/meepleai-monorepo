import type { ReactNode } from 'react';

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

interface ContextBarOptions {
  alwaysVisible: boolean;
}

interface ContextBarState {
  content: ReactNode | null;
  options: ContextBarOptions;
  setContent: (content: ReactNode | null) => void;
  setOptions: (options: Partial<ContextBarOptions>) => void;
  clear: () => void;
}

const DEFAULT_OPTIONS: ContextBarOptions = { alwaysVisible: false };

export const useContextBarStore = create<ContextBarState>()(
  devtools(
    set => ({
      content: null,
      options: DEFAULT_OPTIONS,
      setContent: content => set({ content }),
      setOptions: options => set(state => ({ options: { ...state.options, ...options } })),
      clear: () => set({ content: null, options: DEFAULT_OPTIONS }),
    }),
    { name: 'context-bar-store' }
  )
);

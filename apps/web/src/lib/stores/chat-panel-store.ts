/**
 * Chat panel store.
 *
 * Holds the state of the global chat slide-over panel (open/closed + active
 * game context). Pages and components open the panel via the `useChatPanel`
 * hook (Task 2), which also handles URL state sync (?chat=open&gameId=X).
 *
 * The store is framework-agnostic — do NOT add a `'use client'` directive here.
 * Client boundaries live on the consumers (hook + panel components).
 *
 * See: docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md §5.3
 */

import { create } from 'zustand';

export interface ChatGameContext {
  id: string;
  name: string;
  year?: number;
  pdfCount: number;
  kbStatus: 'ready' | 'indexing' | 'failed';
  imageUrl?: string;
}

interface ChatPanelState {
  isOpen: boolean;
  gameContext: ChatGameContext | null;
  open: (gameContext?: ChatGameContext) => void;
  close: () => void;
  setGameContext: (gameContext: ChatGameContext) => void;
  clearGameContext: () => void;
}

export const useChatPanelStore = create<ChatPanelState>(set => ({
  isOpen: false,
  gameContext: null,
  open: gameContext =>
    set(state => ({
      isOpen: true,
      gameContext: gameContext ?? state.gameContext,
    })),
  close: () => set({ isOpen: false }),
  setGameContext: gameContext => set({ gameContext }),
  clearGameContext: () => set({ gameContext: null }),
}));

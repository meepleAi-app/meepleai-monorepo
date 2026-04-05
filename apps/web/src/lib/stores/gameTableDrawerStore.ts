import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

export type DrawerContent =
  | { type: 'chat'; agentId: string; threadId?: string }
  | { type: 'stats'; gameId: string }
  | { type: 'kb'; gameId: string }
  | { type: 'toolkit'; gameId: string }
  | { type: 'document'; documentId: string }
  | { type: 'session'; sessionId: string };

interface GameTableDrawerState {
  isOpen: boolean;
  content: DrawerContent | null;
  open: (content: DrawerContent) => void;
  close: () => void;
}

export const useGameTableDrawer = create<GameTableDrawerState>()(
  devtools(
    set => ({
      isOpen: false,
      content: null,
      open: content => set({ isOpen: true, content }, false, 'open'),
      close: () => set({ isOpen: false, content: null }, false, 'close'),
    }),
    { name: 'GameTableDrawer' }
  )
);

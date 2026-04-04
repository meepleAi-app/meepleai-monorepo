import { create } from 'zustand';

export interface SearchGameResult {
  id: string;
  name: string;
  imageUrl: string | null;
  playerCount: string;
}

export interface ChatDrawerState {
  threadId: string;
  agentId: string;
  gameId: string;
  gameName: string;
  kbCardCount?: number;
}

interface DashboardSearchState {
  isSearchOpen: boolean;
  selectedGame: SearchGameResult | null;
  drawerState: ChatDrawerState | null;
  openSearch: () => void;
  closeSearch: () => void;
  setSelectedGame: (game: SearchGameResult | null) => void;
  openChatDrawer: (state: ChatDrawerState) => void;
  closeChatDrawer: () => void;
  reset: () => void;
}

export const useDashboardSearchStore = create<DashboardSearchState>(set => ({
  isSearchOpen: false,
  selectedGame: null,
  drawerState: null,
  openSearch: () => set({ isSearchOpen: true }),
  closeSearch: () => set({ isSearchOpen: false, selectedGame: null }),
  setSelectedGame: game => set({ selectedGame: game }),
  openChatDrawer: state => set({ drawerState: state, isSearchOpen: false, selectedGame: null }),
  closeChatDrawer: () => set({ drawerState: null }),
  reset: () => set({ isSearchOpen: false, selectedGame: null, drawerState: null }),
}));

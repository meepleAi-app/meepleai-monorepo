import { createStore, type StoreApi } from 'zustand/vanilla';

import type { Scenario, MockGame, MockSession, MockChat, MockLibrary } from './types';

export interface ScenarioState {
  scenario: Scenario;
  games: MockGame[];
  sessions: MockSession[];
  chatHistory: MockChat[];
  library: MockLibrary;
  isSwitching: boolean;

  loadScenario: (next: Scenario) => void;
  resetToScenario: () => void;
  beginSwitch: () => void;
  endSwitch: () => void;

  addGame: (game: MockGame) => void;
  updateGame: (id: string, patch: Partial<MockGame>) => void;
  removeGame: (id: string) => void;

  addSession: (session: MockSession) => void;
  removeSession: (id: string) => void;

  toggleOwned: (gameId: string) => void;
  toggleWishlist: (gameId: string) => void;

  appendChatMessage: (chatId: string, message: MockChat['messages'][number]) => void;
}

function snapshotOf(
  scenario: Scenario
): Pick<ScenarioState, 'games' | 'sessions' | 'chatHistory' | 'library'> {
  return {
    games: [...scenario.games],
    sessions: [...scenario.sessions],
    chatHistory: scenario.chatHistory.map(c => ({
      chatId: c.chatId,
      messages: [...c.messages],
    })),
    library: {
      ownedGameIds: [...scenario.library.ownedGameIds],
      wishlistGameIds: [...scenario.library.wishlistGameIds],
    },
  };
}

export function createScenarioStore(initial: Scenario): StoreApi<ScenarioState> {
  return createStore<ScenarioState>((set, get) => ({
    scenario: initial,
    ...snapshotOf(initial),
    isSwitching: false,

    loadScenario: (next: Scenario) => set({ scenario: next, ...snapshotOf(next) }),
    resetToScenario: () => set(snapshotOf(get().scenario)),
    beginSwitch: () => set({ isSwitching: true }),
    endSwitch: () => set({ isSwitching: false }),

    addGame: game => set({ games: [...get().games, game] }),
    updateGame: (id, patch) =>
      set({ games: get().games.map(g => (g.id === id ? { ...g, ...patch } : g)) }),
    removeGame: id => set({ games: get().games.filter(g => g.id !== id) }),

    addSession: session => set({ sessions: [...get().sessions, session] }),
    removeSession: id => set({ sessions: get().sessions.filter(s => s.id !== id) }),

    toggleOwned: gameId => {
      const owned = get().library.ownedGameIds;
      const next = owned.includes(gameId) ? owned.filter(id => id !== gameId) : [...owned, gameId];
      set({ library: { ...get().library, ownedGameIds: next } });
    },
    toggleWishlist: gameId => {
      const wl = get().library.wishlistGameIds;
      const next = wl.includes(gameId) ? wl.filter(id => id !== gameId) : [...wl, gameId];
      set({ library: { ...get().library, wishlistGameIds: next } });
    },
    appendChatMessage: (chatId, message) => {
      const history = get().chatHistory;
      const idx = history.findIndex(c => c.chatId === chatId);
      if (idx === -1) {
        set({ chatHistory: [...history, { chatId, messages: [message] }] });
      } else {
        const updated = [...history];
        updated[idx] = { chatId, messages: [...history[idx].messages, message] };
        set({ chatHistory: updated });
      }
    },
  }));
}

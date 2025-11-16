/**
 * Chat Store Hooks (Issue #1083)
 *
 * Auto-generated selector hooks for optimal performance.
 * Components subscribe only to needed state slices.
 *
 * Usage:
 *   const games = useChatStore.use.games();
 *   const selectGame = useChatStore.use.selectGame();
 */

import { StoreApi, UseBoundStore } from 'zustand';
import { useChatStore } from './store';
import { ChatStore } from './types';

// ============================================================================
// Auto-Generated Selectors Utility
// ============================================================================

type WithSelectors<S> = S extends { getState: () => infer T }
  ? S & { use: { [K in keyof T]: () => T[K] } }
  : never;

const createSelectors = <S extends UseBoundStore<StoreApi<object>>>(
  _store: S,
) => {
  const store = _store as WithSelectors<typeof _store>;
  store.use = {} as any;

  for (const k of Object.keys(store.getState())) {
    (store.use as any)[k] = () => store((s) => s[k as keyof typeof s]);
  }

  return store;
};

// ============================================================================
// Exported Store with Auto-Generated Selectors
// ============================================================================

/**
 * Chat store with auto-generated selector hooks
 *
 * Performance-optimized access to store state:
 * - Each property becomes a hook: use.games(), use.messages(), etc.
 * - Components re-render only when subscribed slice changes
 * - No manual selector functions needed
 *
 * Example:
 *   const games = useChatStoreWithSelectors.use.games();
 *   const createChat = useChatStoreWithSelectors.use.createChat();
 */
export const useChatStoreWithSelectors = createSelectors(useChatStore);

// ============================================================================
// Convenience Hooks for Common Patterns
// ============================================================================

/**
 * Get current game's chats
 * Combines selectedGameId + chatsByGame for convenience
 */
export function useCurrentChats() {
  return useChatStore((state) => {
    const gameId = state.selectedGameId;
    if (!gameId) return [];
    return state.chatsByGame[gameId] ?? [];
  });
}

/**
 * Get active chat for current game
 * Combines selectedGameId + activeChatIds + chatsByGame
 */
export function useActiveChat() {
  return useChatStore((state) => {
    const gameId = state.selectedGameId;
    if (!gameId) return null;

    const activeChatId = state.activeChatIds[gameId];
    if (!activeChatId) return null;

    const chats = state.chatsByGame[gameId] ?? [];
    return chats.find((c) => c.id === activeChatId) ?? null;
  });
}

/**
 * Get messages for active chat
 * Combines selectedGameId + activeChatIds + messagesByChat
 */
export function useActiveMessages() {
  return useChatStore((state) => {
    const gameId = state.selectedGameId;
    if (!gameId) return [];

    const activeChatId = state.activeChatIds[gameId];
    if (!activeChatId) return [];

    return state.messagesByChat[activeChatId] ?? [];
  });
}

/**
 * Get selected game object
 */
export function useSelectedGame() {
  return useChatStore((state) => {
    const gameId = state.selectedGameId;
    if (!gameId) return undefined;
    return state.games.find((g) => g.id === gameId);
  });
}

/**
 * Get selected agent object
 */
export function useSelectedAgent() {
  return useChatStore((state) => {
    const agentId = state.selectedAgentId;
    if (!agentId) return undefined;
    return state.agents.find((a) => a.id === agentId);
  });
}

/**
 * Check if loading state is active for any operation
 */
export function useIsLoading() {
  return useChatStore((state) =>
    Object.values(state.loading).some((isLoading) => isLoading)
  );
}

/**
 * Check if creating a new chat
 */
export function useIsCreating() {
  return useChatStore((state) => state.loading.creating);
}

/**
 * Check if sending a message
 */
export function useIsSending() {
  return useChatStore((state) => state.loading.sending);
}

/**
 * Zustand Test Utilities (Issue #1083)
 *
 * Test helpers for components using Zustand chat store.
 * Replaces old provider-based test utilities.
 *
 * Usage:
 *   import { renderWithChatStore } from '@/__tests__/utils/zustand-test-utils';
 *
 *   const { getByText } = renderWithChatStore(<MyComponent />);
 */

import React, { PropsWithChildren } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { useChatStore } from '@/store/chat/store';
import { ChatStore } from '@/store/chat/types';
import { AuthProvider } from '@/components/auth/AuthProvider';

/**
 * Default initial state for tests
 */
export const createMockStoreState = (overrides?: Partial<ChatStore>): Partial<ChatStore> => {
  const defaultLoading = {
    chats: false,
    messages: false,
    sending: false,
    creating: false,
    updating: false,
    deleting: false,
    games: false,
    agents: false,
  };

  return {
    // Session
    selectedGameId: null,
    selectedAgentId: null,
    sidebarCollapsed: false,

    // Games
    games: [],
    agents: [],

    // Chats
    chatsByGame: {},
    activeChatIds: {},

    // Messages
    messagesByChat: {},

    // UI
    loading: { ...defaultLoading, ...overrides?.loading },
    error: null,
    inputValue: '',
    editingMessageId: null,
    editContent: '',
    searchMode: 'Hybrid',

    // Override with custom state (excluding loading which is already merged)
    ...overrides,
  };
};

/**
 * Test wrapper that initializes Zustand store with mock state
 */
export function ChatStoreTestProvider({
  children,
  initialState,
}: PropsWithChildren<{ initialState?: Partial<ChatStore> }>) {
  // Initialize store with mock state on mount ONCE
  const initializedRef = React.useRef(false);

  if (!initializedRef.current && initialState) {
    useChatStore.setState(initialState as any);
    initializedRef.current = true;
  }

  return <>{children}</>;
}

/**
 * Custom render function that wraps component with ChatStoreTestProvider
 *
 * @example
 * const { getByText } = renderWithChatStore(<MyComponent />, {
 *   initialState: { selectedGameId: 'game-1', games: [{ id: 'game-1', name: 'Chess' }] }
 * });
 */
export function renderWithChatStore(
  ui: React.ReactElement,
  options?: Omit<RenderOptions, 'wrapper'> & {
    initialState?: Partial<ChatStore>;
    includeAuth?: boolean;
  }
) {
  const { initialState, includeAuth = false, ...renderOptions } = options || {};

  // Only update state properties, NOT actions
  // This prevents overwriting action functions which causes infinite loops
  if (initialState) {
    useChatStore.setState(initialState as any);
  }

  function Wrapper({ children }: PropsWithChildren) {
    return includeAuth ? <AuthProvider>{children}</AuthProvider> : <>{children}</>;
  }

  return render(ui, { wrapper: Wrapper, ...renderOptions });
}

/**
 * Reset store to initial state between tests
 * Call this in afterEach or beforeEach
 *
 * IMPORTANT: Only resets STATE properties, NOT action functions
 * to avoid breaking store functionality
 */
export function resetChatStore() {
  const mockState = createMockStoreState();
  // Only set state properties, preserve action functions
  useChatStore.setState(mockState as any, false); // false = partial update
}

/**
 * Get current store state for assertions
 */
export function getChatStoreState(): ChatStore {
  return useChatStore.getState();
}

/**
 * Update store state during test
 */
export function updateChatStoreState(updates: Partial<ChatStore>) {
  useChatStore.setState(updates as any);
}

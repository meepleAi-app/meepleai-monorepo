/**
 * Test Providers Utility
 *
 * Provides wrapper components for tests that need the full provider hierarchy.
 * This ensures tests work correctly with the refactored provider structure.
 */

import React from 'react';
import { AuthProvider } from '@/components/auth/AuthProvider';
import { GameProvider } from '@/components/game/GameProvider';
import { ChatStoreProvider } from '@/store/chat/ChatStoreProvider';
import { UIProvider } from '@/components/ui/meeple/UIProvider';
import { api } from '@/lib/api';

// Mock API reference for setup
const mockApi = api as Mocked<typeof api>;

/**
 * Sets up default API mocks for provider initialization
 * Providers make API calls on mount, so tests need these mocks
 */
export function setupProviderMocks() {
  // Default empty responses for providers
  mockApi.get.mockImplementation((path: string) => {
    if (path === '/api/v1/auth/me') {
      return Promise.resolve(null); // No user by default
    }
    if (path === '/api/v1/games') {
      return Promise.resolve([]); // No games by default
    }
    if (path.startsWith('/api/v1/games/') && path.endsWith('/agents')) {
      return Promise.resolve([]); // No agents by default
    }
    if (path.startsWith('/api/v1/chats?gameId=')) {
      return Promise.resolve([]); // No chats by default
    }
    return Promise.resolve(null);
  });
}

/**
 * Full provider tree wrapper for integration tests
 * Issue #1676 Phase 3: Migrated from ChatProvider to ChatStoreProvider
 * Wraps children in: Auth → Game → ChatStore → UI
 */
export function AllProviders({ children }: { children: React.ReactNode }) {
  return (
    <AuthProvider>
      <GameProvider>
        <ChatStoreProvider>
          <UIProvider>{children}</UIProvider>
        </ChatStoreProvider>
      </GameProvider>
    </AuthProvider>
  );
}

/**
 * Creates a wrapper function for React Testing Library
 * Usage: render(<Component />, { wrapper: createWrapper() })
 *
 * Note: Call setupProviderMocks() in your beforeEach if you haven't already mocked the API
 */
export function createWrapper() {
  return ({ children }: { children: React.ReactNode }) => <AllProviders>{children}</AllProviders>;
}

/**
 * Mock providers for unit tests (Option B pattern)
 * These are empty wrappers that just render children
 */
export const mockProviders = () => {
  // Mock AuthProvider
  vi.mock('@/components/auth/AuthProvider', () => ({
    useAuth: vi.fn(() => ({
      user: null,
      loading: false,
      error: null,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
      clearError: vi.fn(),
    })),
    AuthProvider: ({ children }: { children: React.ReactNode }) => children,
  }));

  // Mock GameProvider
  vi.mock('@/components/game/GameProvider', () => ({
    useGame: vi.fn(() => ({
      selectedGameId: null,
      selectedAgentId: null,
      games: [],
      agents: [],
      isLoading: false,
      error: null,
      setSelectedGameId: vi.fn(),
      setSelectedAgentId: vi.fn(),
      refreshGames: vi.fn(),
      refreshAgents: vi.fn(),
    })),
    GameProvider: ({ children }: { children: React.ReactNode }) => children,
  }));

  // Issue #1676 Phase 3: ChatProvider mock removed (migrated to Zustand store)
  // Components now use useChatStore directly, no need for provider mock

  // Mock UIProvider
  vi.mock('@/components/ui/UIProvider', () => ({
    useUI: vi.fn(() => ({
      isMobileMenuOpen: false,
      toggleMobileMenu: vi.fn(),
      closeMobileMenu: vi.fn(),
    })),
    UIProvider: ({ children }: { children: React.ReactNode }) => children,
  }));
};

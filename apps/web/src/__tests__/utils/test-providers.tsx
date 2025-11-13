/**
 * Test Providers Utility
 *
 * Provides wrapper components for tests that need the full provider hierarchy.
 * This ensures tests work correctly with the refactored provider structure.
 */

import React from 'react';
import { AuthProvider } from '@/components/auth/AuthProvider';
import { GameProvider } from '@/components/game/GameProvider';
import { ChatProvider } from '@/components/chat/ChatProvider';
import { UIProvider } from '@/components/ui/UIProvider';
import { api } from '@/lib/api';

// Mock API reference for setup
const mockApi = api as jest.Mocked<typeof api>;

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
 * Wraps children in: Auth → Game → Chat → UI
 */
export function AllProviders({ children }: { children: React.ReactNode }) {
  return (
    <AuthProvider>
      <GameProvider>
        <ChatProvider>
          <UIProvider>{children}</UIProvider>
        </ChatProvider>
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
  return ({ children }: { children: React.ReactNode }) => (
    <AllProviders>{children}</AllProviders>
  );
}

/**
 * Mock providers for unit tests (Option B pattern)
 * These are empty wrappers that just render children
 */
export const mockProviders = () => {
  // Mock AuthProvider
  jest.mock('@/components/auth/AuthProvider', () => ({
    useAuth: jest.fn(() => ({
      user: null,
      loading: false,
      error: null,
      login: jest.fn(),
      register: jest.fn(),
      logout: jest.fn(),
      refreshUser: jest.fn(),
      clearError: jest.fn(),
    })),
    AuthProvider: ({ children }: { children: React.ReactNode }) => children,
  }));

  // Mock GameProvider
  jest.mock('@/components/game/GameProvider', () => ({
    useGame: jest.fn(() => ({
      selectedGameId: null,
      selectedAgentId: null,
      games: [],
      agents: [],
      isLoading: false,
      error: null,
      setSelectedGameId: jest.fn(),
      setSelectedAgentId: jest.fn(),
      refreshGames: jest.fn(),
      refreshAgents: jest.fn(),
    })),
    GameProvider: ({ children }: { children: React.ReactNode }) => children,
  }));

  // Mock ChatProvider
  jest.mock('@/components/chat/ChatProvider', () => ({
    useChat: jest.fn(() => ({
      activeChatId: null,
      chats: [],
      isLoading: false,
      error: null,
      setActiveChatId: jest.fn(),
      refreshChats: jest.fn(),
      createChat: jest.fn(),
      deleteChat: jest.fn(),
    })),
    ChatProvider: ({ children }: { children: React.ReactNode }) => children,
  }));

  // Mock UIProvider
  jest.mock('@/components/ui/UIProvider', () => ({
    useUI: jest.fn(() => ({
      isMobileMenuOpen: false,
      toggleMobileMenu: jest.fn(),
      closeMobileMenu: jest.fn(),
    })),
    UIProvider: ({ children }: { children: React.ReactNode }) => children,
  }));
};

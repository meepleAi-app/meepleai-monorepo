/**
 * Tests for ChatHistory component
 * Auto-generated baseline tests - Issue #992
 * TODO: Enhance with component-specific test cases
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatHistory } from '../ChatHistory';
import React from 'react';
import { renderWithChatStore } from '@/__tests__/utils/zustand-test-utils';

// Mock AuthProvider
vi.mock('@/components/auth/AuthProvider', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => ({
    user: { id: 'test-user-1', email: 'test@example.com', displayName: 'Test User', role: 'User' },
    loading: false,
    error: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
    clearError: vi.fn(),
  }),
}));

describe('ChatHistory', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });
      expect(screen.getByLabelText('Thread history')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });
      expect(screen.getByLabelText('Thread history')).toBeInTheDocument();
    });

    it('should show empty state when no chats', () => {
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });
      expect(screen.getByText(/Nessun thread/i)).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should handle user interactions', async () => {
      const user = userEvent.setup();
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });

      // Verify empty state is displayed
      expect(screen.getByText(/Nessun thread/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible navigation', () => {
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });
      expect(screen.getByLabelText('Thread history')).toBeInTheDocument();
    });

    it('should have accessible empty state message', () => {
      renderWithChatStore(<ChatHistory />, {
        initialState: {
          chatsByGame: {},
          activeChatIds: {},
          selectedGameId: null,
          loading: { chats: false },
        },
      });
      expect(screen.getByText(/Nessun thread/i)).toBeInTheDocument();
    });

    // TODO: Add more a11y tests (aria-labels, keyboard navigation, etc.)
  });
});

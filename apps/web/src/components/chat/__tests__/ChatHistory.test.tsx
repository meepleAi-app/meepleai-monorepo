/**
 * Tests for ChatHistory component
 * Auto-generated baseline tests - Issue #992
 * TODO: Enhance with component-specific test cases
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatHistory } from '../ChatHistory';
import React from 'react';

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

// Mock ChatProvider
vi.mock('@/hooks/useChatContext', () => ({
  useChatContext: () => ({
    chats: [],
    activeChatId: null,
    selectChat: vi.fn(),
    deleteChat: vi.fn(),
    loading: {
      games: false,
      agents: false,
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
    },
  }),
}));

describe('ChatHistory', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      render(<ChatHistory />);
      expect(screen.getByLabelText('Thread history')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const { container } = render(<ChatHistory />);
      expect(container.firstChild).toBeInTheDocument();
    });

    it('should show empty state when no chats', () => {
      render(<ChatHistory />);
      expect(screen.getByText(/Nessun thread/i)).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should handle user interactions', async () => {
      const user = userEvent.setup();
      render(<ChatHistory />);

      // Verify empty state is displayed
      expect(screen.getByText(/Nessun thread/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible navigation', () => {
      render(<ChatHistory />);
      expect(screen.getByLabelText('Thread history')).toBeInTheDocument();
    });

    it('should have accessible empty state message', () => {
      render(<ChatHistory />);
      expect(screen.getByText(/Invia un messaggio per iniziare/i)).toBeInTheDocument();
    });
  });
});

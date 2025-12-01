/**
 * Tests for MessageEditForm component
 * Auto-generated baseline tests - Issue #992
 * TODO: Enhance with component-specific test cases
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageEditForm } from '../MessageEditForm';
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
    editingMessageId: 'msg-1',
    editContent: 'Test message',
    setEditContent: vi.fn(),
    saveEdit: vi.fn(),
    cancelEdit: vi.fn(),
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

describe('MessageEditForm', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Edit message content')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const { container } = render(<MessageEditForm />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should handle user interactions', async () => {
      const user = userEvent.setup();
      render(<MessageEditForm />);

      // Find the save button
      const saveButton = screen.getByLabelText('Save edited message');
      expect(saveButton).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible textarea', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Edit message content')).toBeInTheDocument();
    });

    it('should have accessible buttons', () => {
      render(<MessageEditForm />);
      expect(screen.getByLabelText('Save edited message')).toBeInTheDocument();
      expect(screen.getByLabelText('Cancel editing')).toBeInTheDocument();
    });
  });
});

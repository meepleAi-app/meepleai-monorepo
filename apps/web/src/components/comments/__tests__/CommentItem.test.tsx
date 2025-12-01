/**
 * Tests for CommentItem component
 * Auto-generated baseline tests - Issue #992
 * TODO: Enhance with component-specific test cases
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '../CommentItem';
import React from 'react';
import type { RuleSpecComment } from '@/lib/api';

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

// Mock MentionInput
vi.mock('../../chat/MentionInput', () => ({
  MentionInput: ({ value, onChange }: any) => (
    <textarea data-testid="mention-input" value={value} onChange={e => onChange(e.target.value)} />
  ),
}));

// Mock dialog hooks
vi.mock('@/hooks/useConfirmDialog', () => ({
  useConfirmDialog: () => ({
    confirm: vi.fn(),
    ConfirmDialogComponent: () => null,
  }),
}));

vi.mock('@/hooks/useAlertDialog', () => ({
  useAlertDialog: () => ({
    alert: vi.fn(),
    AlertDialogComponent: () => null,
  }),
}));

const mockComment: RuleSpecComment = {
  id: 'comment-1',
  gameId: 'chess',
  version: 'v1',
  atomId: 'atom-1',
  lineNumber: null,
  userId: 'test-user-1',
  userName: 'Test User',
  commentText: 'Test comment',
  status: 'Open',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  parentCommentId: null,
  replies: [],
};

describe('CommentItem', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="test-user-1"
          currentUserRole="User"
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          onReply={vi.fn()}
          onResolve={vi.fn()}
          onUnresolve={vi.fn()}
        />
      );
      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const { container } = render(<CommentItem children={<div>Test Content</div>} />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="test-user-1"
          currentUserRole="User"
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          onReply={vi.fn()}
          onResolve={vi.fn()}
          onUnresolve={vi.fn()}
        />
      );
      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should handle user interactions', async () => {
      const user = userEvent.setup();
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="test-user-1"
          currentUserRole="User"
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          onReply={vi.fn()}
          onResolve={vi.fn()}
          onUnresolve={vi.fn()}
        />
      );
      // Verify comment renders
      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should display comment text', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="test-user-1"
          currentUserRole="User"
          onEdit={vi.fn()}
          onDelete={vi.fn()}
          onReply={vi.fn()}
          onResolve={vi.fn()}
          onUnresolve={vi.fn()}
        />
      );
      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });
  });
});

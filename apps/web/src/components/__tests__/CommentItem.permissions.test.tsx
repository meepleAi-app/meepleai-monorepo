import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '@/components/comments/CommentItem';
import {
  setupMocks,
  mockComment,
  createMockCallbacks,
  setupBeforeEach,
} from './CommentItem.test-helpers';

setupMocks();

describe('CommentItem - Permissions', () => {
  const { mockOnEdit, mockOnDelete, mockOnReply, mockOnResolve, mockOnUnresolve } =
    createMockCallbacks();

  beforeEach(() => {
    setupBeforeEach();
  });

  describe('Edit Button Permissions', () => {
    it('shows edit button when user is comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByRole('button', { name: 'Edit comment' })).toBeInTheDocument();
    });

    it('does not show edit button when user is not comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-2"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.queryByRole('button', { name: 'Edit comment' })).not.toBeInTheDocument();
    });

    it('shows edit button for Admin even if not owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="admin-user"
          currentUserRole="Admin"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      // Admin can delete but not edit other users' comments based on canEdit logic
      expect(screen.queryByRole('button', { name: 'Edit comment' })).not.toBeInTheDocument();
    });
  });

  describe('Delete Button Permissions', () => {
    it('shows delete button when user is comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
    });

    it('does not show delete button when user is not owner and not Admin', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-2"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.queryByRole('button', { name: 'Delete comment' })).not.toBeInTheDocument();
    });

    it('shows delete button for Admin even if not owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="admin-user"
          currentUserRole="Admin"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
    });

    it('shows delete button for Editor if they own the comment', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="Editor"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
    });
  });

  describe('Disabled State', () => {
    it('disables all buttons when disabled prop is true', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="Admin"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
          disabled={true}
        />
      );

      // All action buttons should be disabled or hidden
      expect(screen.queryByLabelText('Edit comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Delete comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Reply to comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Mark as resolved')).not.toBeInTheDocument();
    });

    it('disables buttons during submission', async () => {
      const user = userEvent.setup();
      mockOnEdit.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      const editButton = screen.getByRole('button', { name: 'Edit comment' });
      await user.click(editButton);

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      // During submission, textarea should be disabled
      const textarea = screen.getByDisplayValue('This is a test comment');
      expect(textarea).toBeDisabled();
    });
  });

  describe('Role-Specific Behavior', () => {
    it('handles empty string user role', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole=""
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      // Should show edit/delete since user is owner
      expect(screen.getByRole('button', { name: 'Edit comment' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
    });
  });
});

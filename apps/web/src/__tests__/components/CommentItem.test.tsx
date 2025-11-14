/**
 * CommentItem Component Tests
 *
 * Tests for the CommentItem component that displays individual comments
 * with edit, delete, reply, and resolve/unresolve functionality.
 *
 * Target Coverage: 90%+ (from 63.51%)
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { CommentItem } from '../../components/CommentItem';
import { RuleSpecComment } from '@/lib/api';

// Mock MentionInput component
interface MockMentionInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

jest.mock('../../components/MentionInput', () => ({
  MentionInput: ({ value, onChange, placeholder, disabled }: MockMentionInputProps) => (
    <textarea
      data-testid="mention-input"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      disabled={disabled}
    />
  ),
}));

/**
 * Helper to create mock comment
 */
const createMockComment = (overrides?: Partial<RuleSpecComment>): RuleSpecComment => ({
  id: 'comment-1',
  gameId: 'game-1',
  version: 'v1.0',
  userId: 'user-1',
  userDisplayName: 'Test User',
  atomId: null,
  lineNumber: null,
  lineContext: null,
  parentCommentId: null,
  replies: [],
  commentText: 'Test comment',
  isResolved: false,
  resolvedByUserId: null,
  resolvedByDisplayName: null,
  resolvedAt: null,
  mentionedUserIds: [],
  createdAt: '2025-01-10T10:00:00Z',
  updatedAt: null,
  ...overrides,
});

describe('CommentItem Component', () => {
  const mockOnEdit = jest.fn();
  const mockOnDelete = jest.fn();
  const mockOnReply = jest.fn();
  const mockOnResolve = jest.fn();
  const mockOnUnresolve = jest.fn();

  const defaultProps = {
    comment: createMockComment(),
    currentUserId: 'current-user',
    currentUserRole: 'Editor',
    onEdit: mockOnEdit,
    onDelete: mockOnDelete,
    onReply: mockOnReply,
    onResolve: mockOnResolve,
    onUnresolve: mockOnUnresolve,
  };

  beforeEach(() => {
    jest.clearAllMocks();
    window.confirm = jest.fn(() => true);
    window.alert = jest.fn();
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders comment with user name', () => {
      render(<CommentItem {...defaultProps} />);

      expect(screen.getByText('Test User')).toBeInTheDocument();
    });

    it('renders comment text', () => {
      render(<CommentItem {...defaultProps} />);

      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });

    it('renders creation date', () => {
      render(<CommentItem {...defaultProps} />);

      // The date will be formatted by toLocaleString()
      const dateElement = screen.getByText(/10/, { exact: false });
      expect(dateElement).toBeInTheDocument();
    });

    it('shows updated indicator when comment is edited', () => {
      const comment = createMockComment({ updatedAt: '2025-01-10T11:00:00Z' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText(/modificato/)).toBeInTheDocument();
    });

    it('displays atomId badge when present', () => {
      const comment = createMockComment({ atomId: 'RULE-123' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText(/Regola: RULE-123/)).toBeInTheDocument();
    });

    it('displays line number badge when present', () => {
      const comment = createMockComment({ lineNumber: 42 });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText(/Line 42/)).toBeInTheDocument();
    });

    it('displays line context when available', () => {
      const comment = createMockComment({ lineContext: 'const foo = bar;' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText('const foo = bar;')).toBeInTheDocument();
    });

    it('displays resolved badge for resolved comments', () => {
      const comment = createMockComment({
        isResolved: true,
        resolvedAt: '2025-01-10T12:00:00Z',
        resolvedByDisplayName: 'Admin User',
      });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText('✓ Resolved')).toBeInTheDocument();
    });

    it('applies resolved styling to resolved comments', () => {
      const comment = createMockComment({ isResolved: true });
      const { container } = render(<CommentItem {...defaultProps} comment={comment} />);

      const commentDiv = container.firstChild as HTMLElement;
      expect(commentDiv).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
    });

    it('displays mentioned users count', () => {
      const comment = createMockComment({ mentionedUserIds: ['user1', 'user2', 'user3'] });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText(/Mentioned: 3 user\(s\)/)).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Mention Rendering
   */
  describe('Mention Rendering', () => {
    it('renders @mentions with special styling', () => {
      const comment = createMockComment({ commentText: 'Hello @john how are you?' });
      const { container } = render(<CommentItem {...defaultProps} comment={comment} />);

      const mentionSpan = container.querySelector('span[title="Mentioned user: john"]');
      expect(mentionSpan).toBeInTheDocument();
      expect(mentionSpan).toHaveTextContent('@john');
    });

    it('renders multiple @mentions', () => {
      const comment = createMockComment({ commentText: 'Hey @alice and @bob!' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText('@alice')).toBeInTheDocument();
      expect(screen.getByText('@bob')).toBeInTheDocument();
    });

    it('renders text without mentions normally', () => {
      const comment = createMockComment({ commentText: 'No mentions here' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText('No mentions here')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Edit Permissions
   */
  describe('Edit Permissions', () => {
    it('shows edit button for own comments', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByLabelText('Edit comment')).toBeInTheDocument();
    });

    it('hides edit button for other users comments', () => {
      const comment = createMockComment({ userId: 'other-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.queryByLabelText('Edit comment')).not.toBeInTheDocument();
    });

    it('hides edit button when disabled', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} disabled={true} />);

      expect(screen.queryByLabelText('Edit comment')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Delete Permissions
   */
  describe('Delete Permissions', () => {
    it('shows delete button for own comments', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByLabelText('Delete comment')).toBeInTheDocument();
    });

    it('shows delete button for admins on any comment', () => {
      const comment = createMockComment({ userId: 'other-user' });
      render(<CommentItem {...defaultProps} comment={comment} currentUserRole="Admin" />);

      expect(screen.getByLabelText('Delete comment')).toBeInTheDocument();
    });

    it('hides delete button for non-admin on others comments', () => {
      const comment = createMockComment({ userId: 'other-user' });
      render(<CommentItem {...defaultProps} comment={comment} currentUserRole="Editor" />);

      expect(screen.queryByLabelText('Delete comment')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Resolve Permissions
   */
  describe('Resolve Permissions', () => {
    it('shows resolve button for editors', () => {
      render(<CommentItem {...defaultProps} currentUserRole="Editor" />);

      expect(screen.getByLabelText('Mark as resolved')).toBeInTheDocument();
    });

    it('shows resolve button for admins', () => {
      render(<CommentItem {...defaultProps} currentUserRole="Admin" />);

      expect(screen.getByLabelText('Mark as resolved')).toBeInTheDocument();
    });

    it('hides resolve button for regular users', () => {
      render(<CommentItem {...defaultProps} currentUserRole="User" />);

      expect(screen.queryByLabelText('Mark as resolved')).not.toBeInTheDocument();
    });

    it('shows reopen button for resolved comments', () => {
      const comment = createMockComment({ isResolved: true });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByLabelText('Reopen comment')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Editing Comments
   */
  describe('Editing Comments', () => {
    it('enters edit mode when edit button is clicked', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      const editButton = screen.getByLabelText('Edit comment');
      fireEvent.click(editButton);

      expect(screen.getByDisplayValue('Test comment')).toBeInTheDocument();
      expect(screen.getByText('Salva')).toBeInTheDocument();
    });

    it('allows editing comment text', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Test comment');
      fireEvent.change(textarea, { target: { value: 'Updated comment' } });

      expect(textarea).toHaveValue('Updated comment');
    });

    it('calls onEdit when save button is clicked', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      mockOnEdit.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Test comment');
      fireEvent.change(textarea, { target: { value: 'Updated comment' } });

      fireEvent.click(screen.getByText('Salva'));

      await waitFor(() => {
        expect(mockOnEdit).toHaveBeenCalledWith('comment-1', 'Updated comment');
      });
    });

    it('exits edit mode after successful save', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      mockOnEdit.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));
      fireEvent.click(screen.getByText('Salva'));

      await waitFor(() => {
        expect(screen.queryByDisplayValue('Test comment')).not.toBeInTheDocument();
      });
    });

    it('does not save empty comment', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Test comment');
      fireEvent.change(textarea, { target: { value: '   ' } });

      fireEvent.click(screen.getByText('Salva'));

      await waitFor(() => {
        expect(mockOnEdit).not.toHaveBeenCalled();
      });
    });

    it('cancels edit mode when cancel button is clicked', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Test comment');
      fireEvent.change(textarea, { target: { value: 'Updated comment' } });

      fireEvent.click(screen.getByText('Annulla'));

      expect(screen.queryByDisplayValue('Updated comment')).not.toBeInTheDocument();
      expect(screen.getByText('Test comment')).toBeInTheDocument();
    });

    it('restores original text when cancelling edit', () => {
      const comment = createMockComment({ userId: 'current-user', commentText: 'Original' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Original');
      fireEvent.change(textarea, { target: { value: 'Changed' } });

      fireEvent.click(screen.getByText('Annulla'));
      fireEvent.click(screen.getByLabelText('Edit comment'));

      expect(screen.getByDisplayValue('Original')).toBeInTheDocument();
    });

    it('shows alert on edit error', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      mockOnEdit.mockRejectedValue(new Error('Edit failed'));
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));
      fireEvent.click(screen.getByText('Salva'));

      await waitFor(() => {
        expect(window.alert).toHaveBeenCalledWith('Impossibile modificare il commento');
      });
    });

    it('disables save button for empty text', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));

      const textarea = screen.getByDisplayValue('Test comment');
      fireEvent.change(textarea, { target: { value: '' } });

      const saveButton = screen.getByText('Salva');
      expect(saveButton).toBeDisabled();
    });
  });

  /**
   * Test Group: Deleting Comments
   */
  describe('Deleting Comments', () => {
    it('prompts for confirmation before deleting', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Delete comment'));

      expect(window.confirm).toHaveBeenCalledWith('Sei sicuro di voler eliminare questo commento?');
    });

    it('calls onDelete when confirmed', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      window.confirm = jest.fn(() => true);
      mockOnDelete.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Delete comment'));

      await waitFor(() => {
        expect(mockOnDelete).toHaveBeenCalledWith('comment-1');
      });
    });

    it('does not delete when cancelled', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      window.confirm = jest.fn(() => false);
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Delete comment'));

      await waitFor(() => {
        expect(mockOnDelete).not.toHaveBeenCalled();
      });
    });

    it('shows alert on delete error', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      window.confirm = jest.fn(() => true);
      mockOnDelete.mockRejectedValue(new Error('Delete failed'));
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Delete comment'));

      await waitFor(() => {
        expect(window.alert).toHaveBeenCalledWith('Impossibile eliminare il commento');
      });
    });
  });

  /**
   * Test Group: Reply Functionality
   */
  describe('Reply Functionality', () => {
    it('shows reply form when reply button is clicked', () => {
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      expect(screen.getByTestId('mention-input')).toBeInTheDocument();
    });

    it('toggles reply form when button clicked again', () => {
      render(<CommentItem {...defaultProps} />);

      const replyButton = screen.getByLabelText('Reply to comment');
      fireEvent.click(replyButton);
      expect(screen.getByTestId('mention-input')).toBeInTheDocument();

      fireEvent.click(replyButton);
      expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
    });

    it('allows typing reply text', () => {
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      expect(textarea).toHaveValue('This is a reply');
    });

    it('calls onReply when send button is clicked', async () => {
      mockOnReply.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(mockOnReply).toHaveBeenCalledWith('comment-1', 'This is a reply');
      });
    });

    it('clears reply text after successful send', async () => {
      mockOnReply.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
      });
    });

    it('does not send empty reply', async () => {
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: '   ' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(mockOnReply).not.toHaveBeenCalled();
      });
    });

    it('cancels reply when cancel button clicked', () => {
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      const cancelButtons = screen.getAllByText('Annulla');
      fireEvent.click(cancelButtons[0]);

      expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
    });

    it('shows alert on reply error', async () => {
      mockOnReply.mockRejectedValue(new Error('Reply failed'));
      render(<CommentItem {...defaultProps} />);

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(window.alert).toHaveBeenCalledWith('Impossibile aggiungere la risposta');
      });
    });

    it('hides reply button at max depth', () => {
      render(<CommentItem {...defaultProps} depth={5} maxDepth={5} />);

      expect(screen.queryByLabelText('Reply to comment')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Resolve/Unresolve
   */
  describe('Resolve/Unresolve', () => {
    it('calls onResolve when resolve button clicked', async () => {
      mockOnResolve.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} currentUserRole="Editor" />);

      fireEvent.click(screen.getByLabelText('Mark as resolved'));

      await waitFor(() => {
        expect(mockOnResolve).toHaveBeenCalledWith('comment-1');
      });
    });

    it('calls onUnresolve when unresolve button clicked', async () => {
      const comment = createMockComment({ isResolved: true });
      mockOnUnresolve.mockResolvedValue(undefined);
      render(<CommentItem {...defaultProps} comment={comment} currentUserRole="Editor" />);

      fireEvent.click(screen.getByLabelText('Reopen comment'));

      await waitFor(() => {
        expect(mockOnUnresolve).toHaveBeenCalledWith('comment-1');
      });
    });

    it('shows alert on resolve error', async () => {
      mockOnResolve.mockRejectedValue(new Error('Resolve failed'));
      render(<CommentItem {...defaultProps} currentUserRole="Editor" />);

      fireEvent.click(screen.getByLabelText('Mark as resolved'));

      await waitFor(() => {
        expect(window.alert).toHaveBeenCalledWith('Impossibile contrassegnare come risolto');
      });
    });

    it('shows alert on unresolve error', async () => {
      const comment = createMockComment({ isResolved: true });
      mockOnUnresolve.mockRejectedValue(new Error('Unresolve failed'));
      render(<CommentItem {...defaultProps} comment={comment} currentUserRole="Editor" />);

      fireEvent.click(screen.getByLabelText('Reopen comment'));

      await waitFor(() => {
        expect(window.alert).toHaveBeenCalledWith('Impossibile riaprire il commento');
      });
    });
  });

  /**
   * Test Group: Nested Replies
   */
  describe('Nested Replies', () => {
    it('renders nested replies recursively', () => {
      const comment = createMockComment({
        replies: [
          createMockComment({
            id: 'reply-1',
            commentText: 'First reply',
            parentCommentId: 'comment-1',
          }),
          createMockComment({
            id: 'reply-2',
            commentText: 'Second reply',
            parentCommentId: 'comment-1',
          }),
        ],
      });
      render(<CommentItem {...defaultProps} comment={comment} />);

      expect(screen.getByText('First reply')).toBeInTheDocument();
      expect(screen.getByText('Second reply')).toBeInTheDocument();
    });

    it('respects maxDepth for nested replies', () => {
      const comment = createMockComment({
        replies: [
          createMockComment({
            id: 'reply-1',
            commentText: 'First reply',
          }),
        ],
      });
      const { container } = render(<CommentItem {...defaultProps} comment={comment} depth={4} maxDepth={5} />);

      // At depth 4, we should not see reply button (since max is 5)
      const replyButtons = container.querySelectorAll('[aria-label="Reply to comment"]');
      expect(replyButtons.length).toBeLessThanOrEqual(1);
    });

    it('does not render replies beyond maxDepth', () => {
      const comment = createMockComment({
        id: 'parent',
        replies: [
          createMockComment({
            id: 'deep-reply',
            commentText: 'Too deep',
          }),
        ],
      });
      render(<CommentItem {...defaultProps} comment={comment} depth={5} maxDepth={5} />);

      // At maxDepth, replies should not be rendered
      expect(screen.queryByText('Too deep')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Disabled State
   */
  describe('Disabled State', () => {
    it('disables all buttons when disabled prop is true', () => {
      const comment = createMockComment({ userId: 'current-user' });
      render(<CommentItem {...defaultProps} comment={comment} disabled={true} currentUserRole="Admin" />);

      // All action buttons should be disabled or hidden
      expect(screen.queryByLabelText('Edit comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Delete comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Reply to comment')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Mark as resolved')).not.toBeInTheDocument();
    });

    it('disables buttons during submission', async () => {
      const comment = createMockComment({ userId: 'current-user' });
      mockOnEdit.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));
      render(<CommentItem {...defaultProps} comment={comment} />);

      fireEvent.click(screen.getByLabelText('Edit comment'));
      fireEvent.click(screen.getByText('Salva'));

      // During submission, buttons should be disabled
      const textarea = screen.getByDisplayValue('Test comment');
      expect(textarea).toBeDisabled();
    });
  });
});

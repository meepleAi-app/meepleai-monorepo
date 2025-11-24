import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { CommentItem } from '@/components/comments/CommentItem';
import {
  setupMocks,
  mockComment,
  mockAlert,
  createMockCallbacks,
  setupBeforeEach,
  createCommentVariant,
} from './CommentItem.test-helpers';

setupMocks();

describe('CommentItem - Resolve and Unresolve', () => {
  const { mockOnEdit, mockOnDelete, mockOnReply, mockOnResolve, mockOnUnresolve } =
    createMockCallbacks();

  beforeEach(() => {
    setupBeforeEach();
  });

  describe('Resolve/Unresolve', () => {
    it('calls onResolve when resolve button clicked', async () => {
      mockOnResolve.mockResolvedValue(undefined);
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

      fireEvent.click(screen.getByLabelText('Mark as resolved'));

      await waitFor(() => {
        expect(mockOnResolve).toHaveBeenCalledWith('comment-1');
      });
    });

    it('calls onUnresolve when unresolve button clicked', async () => {
      const resolvedComment = createCommentVariant({
        isResolved: true,
      });
      mockOnUnresolve.mockResolvedValue(undefined);
      render(
        <CommentItem
          comment={resolvedComment}
          currentUserId="user-1"
          currentUserRole="Editor"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      fireEvent.click(screen.getByLabelText('Reopen comment'));

      await waitFor(() => {
        expect(mockOnUnresolve).toHaveBeenCalledWith('comment-1');
      });
    });

    it('shows alert on resolve error', async () => {
      mockOnResolve.mockRejectedValue(new Error('Resolve failed'));
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

      fireEvent.click(screen.getByLabelText('Mark as resolved'));

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith({
          title: 'Errore',
          message: 'Impossibile contrassegnare come risolto',
          variant: 'error',
        });
      });
    });

    it('shows alert on unresolve error', async () => {
      const resolvedComment = createCommentVariant({
        isResolved: true,
      });
      mockOnUnresolve.mockRejectedValue(new Error('Unresolve failed'));
      render(
        <CommentItem
          comment={resolvedComment}
          currentUserId="user-1"
          currentUserRole="Editor"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      fireEvent.click(screen.getByLabelText('Reopen comment'));

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith({
          title: 'Errore',
          message: 'Impossibile riaprire il commento',
          variant: 'error',
        });
      });
    });
  });
});

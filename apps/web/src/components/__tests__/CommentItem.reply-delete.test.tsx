import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '@/components/comments/CommentItem';
import {
  setupMocks,
  mockComment,
  mockAlert,
  mockConfirm,
  createMockCallbacks,
  setupBeforeEach,
} from './CommentItem.test-helpers';

setupMocks();

describe('CommentItem - Reply and Delete', () => {
  const { mockOnEdit, mockOnDelete, mockOnReply, mockOnResolve, mockOnUnresolve } =
    createMockCallbacks();

  beforeEach(() => {
    setupBeforeEach();
  });

  describe('Delete Functionality', () => {
    it('shows confirmation dialog when delete is clicked', async () => {
      const user = userEvent.setup();
      mockConfirm.mockResolvedValueOnce(false);

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockConfirm).toHaveBeenCalledWith({
          title: 'Elimina commento',
          message:
            'Sei sicuro di voler eliminare questo commento? Questa azione non può essere annullata.',
          variant: 'destructive',
          confirmText: 'Elimina',
          cancelText: 'Annulla',
        });
      });
    });

    it('does not delete when confirmation is cancelled', async () => {
      const user = userEvent.setup();
      mockConfirm.mockResolvedValueOnce(false);

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockOnDelete).not.toHaveBeenCalled();
      });
    });

    it('deletes comment when confirmation is accepted', async () => {
      const user = userEvent.setup();
      mockOnDelete.mockResolvedValue(undefined);

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockOnDelete).toHaveBeenCalledWith('comment-1');
      });
    });

    it('shows loading state while deleting', async () => {
      const user = userEvent.setup();
      mockOnDelete.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Delete comment' })).toBeDisabled();
      });
    });

    it('disables edit button while deleting', async () => {
      const user = userEvent.setup();
      mockOnDelete.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        const editButton = screen.getByRole('button', { name: 'Edit comment' });
        expect(editButton).toBeDisabled();
      });
    });

    it('shows error alert when delete fails', async () => {
      const user = userEvent.setup();
      mockOnDelete.mockRejectedValue(new Error('Network error'));

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith({
          title: 'Errore',
          message: 'Impossibile eliminare il commento',
          variant: 'error',
        });
      });
    });

    it('shows error when delete fails and component continues to work', async () => {
      const user = userEvent.setup();
      const error = new Error('Network error');
      mockOnDelete.mockRejectedValue(error);

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

      const deleteButton = screen.getByRole('button', { name: 'Delete comment' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalled();
      });

      // Component should still be functional
      expect(screen.getByRole('button', { name: 'Delete comment' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Edit comment' })).toBeInTheDocument();
    });
  });

  describe('Reply Functionality', () => {
    it('shows reply form when reply button is clicked', () => {
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      expect(screen.getByTestId('mention-input')).toBeInTheDocument();
    });

    it('toggles reply form when button clicked again', () => {
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

      const replyButton = screen.getByLabelText('Reply to comment');
      fireEvent.click(replyButton);
      expect(screen.getByTestId('mention-input')).toBeInTheDocument();

      fireEvent.click(replyButton);
      expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
    });

    it('allows typing reply text', () => {
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      expect(textarea).toHaveValue('This is a reply');
    });

    it('calls onReply when send button is clicked', async () => {
      mockOnReply.mockResolvedValue(undefined);
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
      });
    });

    it('does not send empty reply', async () => {
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: '   ' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(mockOnReply).not.toHaveBeenCalled();
      });
    });

    it('cancels reply when cancel button clicked', () => {
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      const cancelButtons = screen.getAllByText('Annulla');
      fireEvent.click(cancelButtons[0]);

      expect(screen.queryByTestId('mention-input')).not.toBeInTheDocument();
    });

    it('shows alert on reply error', async () => {
      mockOnReply.mockRejectedValue(new Error('Reply failed'));
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

      fireEvent.click(screen.getByLabelText('Reply to comment'));

      const textarea = screen.getByTestId('mention-input');
      fireEvent.change(textarea, { target: { value: 'This is a reply' } });

      fireEvent.click(screen.getByText('Invia risposta'));

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith({
          title: 'Errore',
          message: 'Impossibile aggiungere la risposta',
          variant: 'error',
        });
      });
    });

    it('hides reply button at max depth', () => {
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
          depth={5}
          maxDepth={5}
        />
      );

      expect(screen.queryByLabelText('Reply to comment')).not.toBeInTheDocument();
    });
  });
});

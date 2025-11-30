import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '@/components/comments/CommentItem';
import {
  setupMocks,
  mockComment,
  mockAlert,
  createMockCallbacks,
  setupBeforeEach,
} from './CommentItem.test-helpers';

setupMocks();

describe('CommentItem - Edit Functionality', () => {
  const { mockOnEdit, mockOnDelete, mockOnReply, mockOnResolve, mockOnUnresolve } =
    createMockCallbacks();

  beforeEach(() => {
    setupBeforeEach();
  });

  describe('Edit Mode', () => {
    it('enters edit mode when edit button is clicked', async () => {
      const user = userEvent.setup();

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

      expect(screen.getByDisplayValue('This is a test comment')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Salva' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
    });

    it('allows editing comment text in edit mode', async () => {
      const user = userEvent.setup();

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated comment text');

      expect(screen.getByDisplayValue('Updated comment text')).toBeInTheDocument();
    });

    it('saves edited comment when save button is clicked', async () => {
      const user = userEvent.setup();
      mockOnEdit.mockResolvedValue(undefined);

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated comment');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockOnEdit).toHaveBeenCalledWith('comment-1', 'Updated comment');
      });
    });

    it('exits edit mode after successful save', async () => {
      const user = userEvent.setup();
      mockOnEdit.mockResolvedValue(undefined);

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated comment');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.queryByRole('button', { name: 'Salva' })).not.toBeInTheDocument();
      });
    });

    it('cancels edit and restores original text when cancel is clicked', async () => {
      const user = userEvent.setup();

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Changed text');

      const cancelButton = screen.getByRole('button', { name: 'Annulla' });
      await user.click(cancelButton);

      expect(screen.queryByRole('button', { name: 'Salva' })).not.toBeInTheDocument();
      expect(screen.getByText('This is a test comment')).toBeInTheDocument();
    });
  });

  describe('Edit Validation', () => {
    it('disables save button when comment text is empty', async () => {
      const user = userEvent.setup();

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      expect(saveButton).toBeDisabled();
    });

    it('disables save button when comment text is only whitespace', async () => {
      const user = userEvent.setup();

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, '   ');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      expect(saveButton).toBeDisabled();
    });

    it('does not save when text is only whitespace', async () => {
      const user = userEvent.setup();

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, '   ');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      expect(mockOnEdit).not.toHaveBeenCalled();
    });
  });

  describe('Edit Loading States', () => {
    it('shows loading state while saving', async () => {
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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      expect(screen.getByRole('button', { name: 'Salvataggio...' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Salvataggio...' })).toBeDisabled();
    });

    it('disables textarea while saving', async () => {
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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      expect(textarea).toBeDisabled();
    });

    it('disables cancel button while saving', async () => {
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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      const cancelButton = screen.getByRole('button', { name: 'Annulla' });
      expect(cancelButton).toBeDisabled();
    });
  });

  describe('Edit Error Handling', () => {
    it('shows error alert when edit fails', async () => {
      const user = userEvent.setup();
      mockOnEdit.mockRejectedValue(new Error('Network error'));

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith({
          title: 'Errore',
          message: 'Impossibile modificare il commento',
          variant: 'error',
        });
      });
    });

    it('stays in edit mode when edit fails', async () => {
      const user = userEvent.setup();
      mockOnEdit.mockRejectedValue(new Error('Network error'));

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

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalled();
      });

      // Should still be in edit mode
      expect(screen.getByRole('button', { name: 'Salva' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
    });
  });
});

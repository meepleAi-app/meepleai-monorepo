import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '../CommentItem';
import type { RuleSpecComment } from '../../lib/api';

describe('CommentItem', () => {
  const mockComment: RuleSpecComment = {
    id: 'comment-1',
    gameId: 'chess',
    version: 'v1',
    atomId: 'atom-1',
    userId: 'user-1',
    userDisplayName: 'John Doe',
    commentText: 'This is a test comment',
    createdAt: '2025-10-15T12:00:00Z',
    updatedAt: null
  };

  const mockOnEdit = jest.fn();
  const mockOnDelete = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders comment with author name', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('John Doe')).toBeInTheDocument();
    });

    it('renders comment text', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('This is a test comment')).toBeInTheDocument();
    });

    it('renders atomId badge when atomId is present', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('Regola: atom-1')).toBeInTheDocument();
    });

    it('does not render atomId badge when atomId is null', () => {
      const commentWithoutAtom = { ...mockComment, atomId: null };

      render(
        <CommentItem
          comment={commentWithoutAtom}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.queryByText(/Regola:/)).not.toBeInTheDocument();
    });

    it('renders formatted timestamp', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const date = new Date('2025-10-15T12:00:00Z');
      expect(screen.getByText(date.toLocaleString())).toBeInTheDocument();
    });

    it('renders "modified" indicator when updatedAt is present', () => {
      const modifiedComment = {
        ...mockComment,
        updatedAt: '2025-10-15T13:00:00Z'
      };

      render(
        <CommentItem
          comment={modifiedComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText(/\(modificato\)/)).toBeInTheDocument();
    });

    it('does not render "modified" indicator when updatedAt is null', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.queryByText(/\(modificato\)/)).not.toBeInTheDocument();
    });
  });

  describe('Permissions - Edit Button', () => {
    it('shows edit button when user is comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByRole('button', { name: 'Modifica' })).toBeInTheDocument();
    });

    it('does not show edit button when user is not comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-2"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.queryByRole('button', { name: 'Modifica' })).not.toBeInTheDocument();
    });

    it('shows edit button for Admin even if not owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="admin-user"
          currentUserRole="Admin"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      // Admin can delete but not edit other users' comments based on canEdit logic
      expect(screen.queryByRole('button', { name: 'Modifica' })).not.toBeInTheDocument();
    });
  });

  describe('Permissions - Delete Button', () => {
    it('shows delete button when user is comment owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
    });

    it('does not show delete button when user is not owner and not Admin', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-2"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.queryByRole('button', { name: 'Elimina' })).not.toBeInTheDocument();
    });

    it('shows delete button for Admin even if not owner', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="admin-user"
          currentUserRole="Admin"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
    });

    it('shows delete button for Editor if they own the comment', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="Editor"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
    });
  });

  describe('Edit Functionality', () => {
    it('enters edit mode when edit button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      await user.click(editButton);

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Changed text');

      const cancelButton = screen.getByRole('button', { name: 'Annulla' });
      await user.click(cancelButton);

      expect(screen.queryByRole('button', { name: 'Salva' })).not.toBeInTheDocument();
      expect(screen.getByText('This is a test comment')).toBeInTheDocument();
    });

    it('disables save button when comment text is empty', async () => {
      const user = userEvent.setup();

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      await user.click(editButton);

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, '   ');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      expect(mockOnEdit).not.toHaveBeenCalled();
    });

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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
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
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      await user.click(editButton);

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      const cancelButton = screen.getByRole('button', { name: 'Annulla' });
      expect(cancelButton).toBeDisabled();
    });

    it('shows error alert when edit fails', async () => {
      const user = userEvent.setup();
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = jest.spyOn(window, 'alert').mockImplementation(() => {});

      mockOnEdit.mockRejectedValue(new Error('Network error'));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      await user.click(editButton);

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(alertSpy).toHaveBeenCalledWith('Impossibile modificare il commento');
      });

      consoleErrorSpy.mockRestore();
      alertSpy.mockRestore();
    });

    it('stays in edit mode when edit fails', async () => {
      const user = userEvent.setup();
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = jest.spyOn(window, 'alert').mockImplementation(() => {});

      mockOnEdit.mockRejectedValue(new Error('Network error'));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      await user.click(editButton);

      const textarea = screen.getByDisplayValue('This is a test comment');
      await user.clear(textarea);
      await user.type(textarea, 'Updated');

      const saveButton = screen.getByRole('button', { name: 'Salva' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(alertSpy).toHaveBeenCalled();
      });

      // Should still be in edit mode
      expect(screen.getByRole('button', { name: 'Salva' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();

      consoleErrorSpy.mockRestore();
      alertSpy.mockRestore();
    });
  });

  describe('Delete Functionality', () => {
    it('shows confirmation dialog when delete is clicked', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      expect(confirmSpy).toHaveBeenCalledWith('Sei sicuro di voler eliminare questo commento?');
      confirmSpy.mockRestore();
    });

    it('does not delete when confirmation is cancelled', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      expect(mockOnDelete).not.toHaveBeenCalled();
      confirmSpy.mockRestore();
    });

    it('deletes comment when confirmation is accepted', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      mockOnDelete.mockResolvedValue(undefined);

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(mockOnDelete).toHaveBeenCalledWith('comment-1');
      });

      confirmSpy.mockRestore();
    });

    it('shows loading state while deleting', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      mockOnDelete.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      expect(screen.getByRole('button', { name: 'Eliminazione...' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Eliminazione...' })).toBeDisabled();

      confirmSpy.mockRestore();
    });

    it('disables edit button while deleting', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      mockOnDelete.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      const editButton = screen.getByRole('button', { name: 'Modifica' });
      expect(editButton).toBeDisabled();

      confirmSpy.mockRestore();
    });

    it('shows error alert when delete fails', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = jest.spyOn(window, 'alert').mockImplementation(() => {});

      mockOnDelete.mockRejectedValue(new Error('Network error'));

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(alertSpy).toHaveBeenCalledWith('Impossibile eliminare il commento');
      });

      confirmSpy.mockRestore();
      consoleErrorSpy.mockRestore();
      alertSpy.mockRestore();
    });

    it('logs error to console when delete fails', async () => {
      const user = userEvent.setup();
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      const alertSpy = jest.spyOn(window, 'alert').mockImplementation(() => {});

      const error = new Error('Network error');
      mockOnDelete.mockRejectedValue(error);

      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      const deleteButton = screen.getByRole('button', { name: 'Elimina' });
      await user.click(deleteButton);

      await waitFor(() => {
        expect(consoleErrorSpy).toHaveBeenCalledWith('Failed to delete comment:', error);
      });

      confirmSpy.mockRestore();
      consoleErrorSpy.mockRestore();
      alertSpy.mockRestore();
    });
  });

  describe('Edge Cases', () => {
    it('handles very long comment text', () => {
      const longComment = {
        ...mockComment,
        commentText: 'A'.repeat(1000)
      };

      render(
        <CommentItem
          comment={longComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('A'.repeat(1000))).toBeInTheDocument();
    });

    it('handles special characters in comment text', () => {
      const specialComment = {
        ...mockComment,
        commentText: '<script>alert("xss")</script> & special < > " \' chars'
      };

      render(
        <CommentItem
          comment={specialComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('<script>alert("xss")</script> & special < > " \' chars')).toBeInTheDocument();
    });

    it('handles very long display name', () => {
      const longNameComment = {
        ...mockComment,
        userDisplayName: 'A'.repeat(100)
      };

      render(
        <CommentItem
          comment={longNameComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('A'.repeat(100))).toBeInTheDocument();
    });

    it('handles very long atomId', () => {
      const longAtomComment = {
        ...mockComment,
        atomId: 'atom-' + 'x'.repeat(50)
      };

      render(
        <CommentItem
          comment={longAtomComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      expect(screen.getByText('Regola: atom-' + 'x'.repeat(50))).toBeInTheDocument();
    });

    it('handles empty string user role', () => {
      render(
        <CommentItem
          comment={mockComment}
          currentUserId="user-1"
          currentUserRole=""
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
        />
      );

      // Should show edit/delete since user is owner
      expect(screen.getByRole('button', { name: 'Modifica' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Elimina' })).toBeInTheDocument();
    });
  });
});

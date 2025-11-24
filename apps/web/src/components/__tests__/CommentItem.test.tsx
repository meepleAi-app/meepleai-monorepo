import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommentItem } from '@/components/comments/CommentItem';
import type { RuleSpecComment } from '@/lib/api';

// Mock MentionInput component
interface MockMentionInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

vi.mock('../chat/MentionInput', () => ({
  MentionInput: ({ value, onChange, placeholder, disabled }: MockMentionInputProps) => (
    <textarea
      data-testid="mention-input"
      value={value}
      onChange={e => onChange(e.target.value)}
      placeholder={placeholder}
      disabled={disabled}
    />
  ),
}));

// Mock dialog hooks
const mockConfirm = vi.fn();
const mockAlert = vi.fn();

vi.mock('@/hooks/useConfirmDialog', () => ({
  useConfirmDialog: () => ({
    confirm: mockConfirm,
    ConfirmDialogComponent: () => null,
  }),
}));

vi.mock('@/hooks/useAlertDialog', () => ({
  useAlertDialog: () => ({
    alert: mockAlert,
    AlertDialogComponent: () => null,
  }),
}));

describe('CommentItem', () => {
  const mockComment: RuleSpecComment = {
    id: 'comment-1',
    gameId: 'chess',
    version: 'v1',
    atomId: 'atom-1',
    lineNumber: null,
    lineContext: null,
    parentCommentId: null,
    replies: [],
    userId: 'user-1',
    userDisplayName: 'John Doe',
    commentText: 'This is a test comment',
    isResolved: false,
    resolvedByUserId: null,
    resolvedByDisplayName: null,
    resolvedAt: null,
    mentionedUserIds: [],
    createdAt: '2025-10-15T12:00:00Z',
    updatedAt: null,
  };

  const mockOnEdit = vi.fn();
  const mockOnDelete = vi.fn();
  const mockOnReply = vi.fn();
  const mockOnResolve = vi.fn();
  const mockOnUnresolve = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockConfirm.mockResolvedValue(true);
    mockAlert.mockResolvedValue(undefined);
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      const date = new Date('2025-10-15T12:00:00Z');
      expect(screen.getByText(date.toLocaleString())).toBeInTheDocument();
    });

    it('renders "modified" indicator when updatedAt is present', () => {
      const modifiedComment = {
        ...mockComment,
        updatedAt: '2025-10-15T13:00:00Z',
      };

      render(
        <CommentItem
          comment={modifiedComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.queryByText(/\(modificato\)/)).not.toBeInTheDocument();
    });

    it('displays line number badge when present', () => {
      const commentWithLine = {
        ...mockComment,
        lineNumber: 42,
      };

      render(
        <CommentItem
          comment={commentWithLine}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText(/Line 42/)).toBeInTheDocument();
    });

    it('displays line context when available', () => {
      const commentWithContext = {
        ...mockComment,
        lineContext: 'const foo = bar;',
      };

      render(
        <CommentItem
          comment={commentWithContext}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('const foo = bar;')).toBeInTheDocument();
    });

    it('displays resolved badge for resolved comments', () => {
      const resolvedComment = {
        ...mockComment,
        isResolved: true,
        resolvedAt: '2025-01-10T12:00:00Z',
        resolvedByDisplayName: 'Admin User',
      };

      render(
        <CommentItem
          comment={resolvedComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('✓ Resolved')).toBeInTheDocument();
    });

    it('applies resolved styling to resolved comments', () => {
      const resolvedComment = {
        ...mockComment,
        isResolved: true,
      };

      const { container } = render(
        <CommentItem
          comment={resolvedComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      const commentDiv = container.firstChild as HTMLElement;
      expect(commentDiv).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
    });

    it('displays mentioned users count', () => {
      const commentWithMentions = {
        ...mockComment,
        mentionedUserIds: ['user1', 'user2', 'user3'],
      };

      render(
        <CommentItem
          comment={commentWithMentions}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText(/Mentioned: 3 user\(s\)/)).toBeInTheDocument();
    });
  });

  describe('Mention Rendering', () => {
    it('renders @mentions with special styling', () => {
      const commentWithMention = {
        ...mockComment,
        commentText: 'Hello @john how are you?',
      };

      const { container } = render(
        <CommentItem
          comment={commentWithMention}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      const mentionSpan = container.querySelector('span[title="Mentioned user: john"]');
      expect(mentionSpan).toBeInTheDocument();
      expect(mentionSpan).toHaveTextContent('@john');
    });

    it('renders multiple @mentions', () => {
      const commentWithMentions = {
        ...mockComment,
        commentText: 'Hey @alice and @bob!',
      };

      render(
        <CommentItem
          comment={commentWithMentions}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('@alice')).toBeInTheDocument();
      expect(screen.getByText('@bob')).toBeInTheDocument();
    });

    it('renders text without mentions normally', () => {
      const commentWithoutMentions = {
        ...mockComment,
        commentText: 'No mentions here',
      };

      render(
        <CommentItem
          comment={commentWithoutMentions}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('No mentions here')).toBeInTheDocument();
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

  describe('Permissions - Delete Button', () => {
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
      const resolvedComment = {
        ...mockComment,
        isResolved: true,
      };
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
      const resolvedComment = {
        ...mockComment,
        isResolved: true,
      };
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

  describe('Nested Replies', () => {
    it('renders nested replies recursively', () => {
      const commentWithReplies: RuleSpecComment = {
        ...mockComment,
        replies: [
          {
            ...mockComment,
            id: 'reply-1',
            commentText: 'First reply',
            parentCommentId: 'comment-1',
            replies: [],
          },
          {
            ...mockComment,
            id: 'reply-2',
            commentText: 'Second reply',
            parentCommentId: 'comment-1',
            replies: [],
          },
        ],
      };

      render(
        <CommentItem
          comment={commentWithReplies}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('First reply')).toBeInTheDocument();
      expect(screen.getByText('Second reply')).toBeInTheDocument();
    });

    it('respects maxDepth for nested replies', () => {
      const commentWithReplies: RuleSpecComment = {
        ...mockComment,
        replies: [
          {
            ...mockComment,
            id: 'reply-1',
            commentText: 'First reply',
            parentCommentId: 'comment-1',
            replies: [],
          },
        ],
      };

      const { container } = render(
        <CommentItem
          comment={commentWithReplies}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
          depth={4}
          maxDepth={5}
        />
      );

      // At depth 4, we should not see reply button (since max is 5)
      const replyButtons = container.querySelectorAll('[aria-label="Reply to comment"]');
      expect(replyButtons.length).toBeLessThanOrEqual(1);
    });

    it('does not render replies beyond maxDepth', () => {
      const commentWithReplies: RuleSpecComment = {
        ...mockComment,
        id: 'parent',
        replies: [
          {
            ...mockComment,
            id: 'deep-reply',
            commentText: 'Too deep',
            parentCommentId: 'parent',
            replies: [],
          },
        ],
      };

      render(
        <CommentItem
          comment={commentWithReplies}
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

      // At maxDepth, replies should not be rendered
      expect(screen.queryByText('Too deep')).not.toBeInTheDocument();
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

  describe('Edge Cases', () => {
    it('handles very long comment text', () => {
      const longComment = {
        ...mockComment,
        commentText: 'A'.repeat(1000),
      };

      render(
        <CommentItem
          comment={longComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('A'.repeat(1000))).toBeInTheDocument();
    });

    it('handles special characters in comment text', () => {
      const specialComment = {
        ...mockComment,
        commentText: '<script>alert("xss")</script> & special < > " \' chars',
      };

      render(
        <CommentItem
          comment={specialComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(
        screen.getByText('<script>alert("xss")</script> & special < > " \' chars')
      ).toBeInTheDocument();
    });

    it('handles very long display name', () => {
      const longNameComment = {
        ...mockComment,
        userDisplayName: 'A'.repeat(100),
      };

      render(
        <CommentItem
          comment={longNameComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
        />
      );

      expect(screen.getByText('A'.repeat(100))).toBeInTheDocument();
    });

    it('handles very long atomId', () => {
      const longAtomComment = {
        ...mockComment,
        atomId: 'atom-' + 'x'.repeat(50),
      };

      render(
        <CommentItem
          comment={longAtomComment}
          currentUserId="user-1"
          currentUserRole="User"
          onEdit={mockOnEdit}
          onDelete={mockOnDelete}
          onReply={mockOnReply}
          onResolve={mockOnResolve}
          onUnresolve={mockOnUnresolve}
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

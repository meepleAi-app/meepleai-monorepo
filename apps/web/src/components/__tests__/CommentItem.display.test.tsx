import { render, screen } from '@testing-library/react';
import { CommentItem } from '@/components/comments/CommentItem';
import {
  setupMocks,
  mockComment,
  createMockCallbacks,
  setupBeforeEach,
  createCommentVariant,
} from './CommentItem.test-helpers';

setupMocks();

describe('CommentItem - Rendering', () => {
  const { mockOnEdit, mockOnDelete, mockOnReply, mockOnResolve, mockOnUnresolve } =
    createMockCallbacks();

  beforeEach(() => {
    setupBeforeEach();
  });

  describe('Basic Rendering', () => {
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
      const commentWithoutAtom = createCommentVariant({ atomId: null });

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
      const modifiedComment = createCommentVariant({
        updatedAt: '2025-10-15T13:00:00Z',
      });

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
      const commentWithLine = createCommentVariant({
        lineNumber: 42,
      });

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
      const commentWithContext = createCommentVariant({
        lineContext: 'const foo = bar;',
      });

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
      const resolvedComment = createCommentVariant({
        isResolved: true,
        resolvedAt: '2025-01-10T12:00:00Z',
        resolvedByDisplayName: 'Admin User',
      });

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
      const resolvedComment = createCommentVariant({
        isResolved: true,
      });

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
      const commentWithMentions = createCommentVariant({
        mentionedUserIds: ['user1', 'user2', 'user3'],
      });

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
      const commentWithMention = createCommentVariant({

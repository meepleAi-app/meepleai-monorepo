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

        commentText: 'Hello @john how are you?',
      });

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
      const commentWithMentions = createCommentVariant({
        commentText: 'Hey @alice and @bob!',
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

      expect(screen.getByText('@alice')).toBeInTheDocument();
      expect(screen.getByText('@bob')).toBeInTheDocument();
    });

    it('renders text without mentions normally', () => {
      const commentWithoutMentions = createCommentVariant({
        commentText: 'No mentions here',
      });

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

  describe('Nested Replies', () => {
    it('renders nested replies recursively', () => {
      const commentWithReplies = createCommentVariant({
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
      });

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
      const commentWithReplies = createCommentVariant({
        replies: [
          {
            ...mockComment,
            id: 'reply-1',
            commentText: 'First reply',
            parentCommentId: 'comment-1',
            replies: [],
          },
        ],
      });

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
      const commentWithReplies = createCommentVariant({
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
      });

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

  describe('Edge Cases', () => {
    it('handles very long comment text', () => {
      const longComment = createCommentVariant({
        commentText: 'A'.repeat(1000),
      });

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
      const specialComment = createCommentVariant({
        commentText: '<script>alert("xss")</script> & special < > " \' chars',
      });

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
      const longNameComment = createCommentVariant({
        userDisplayName: 'A'.repeat(100),
      });

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
      const longAtomComment = createCommentVariant({
        atomId: 'atom-' + 'x'.repeat(50),
      });

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
  });
});

/**
 * CommentBox Component (Issue #2052)
 *
 * Comment input for shared chat threads with 'comment' role.
 * Rate-limited to 10 comments per hour per share link.
 *
 * Features:
 * - WCAG 2.1 AA accessible textarea
 * - Character counter (max 4000 chars)
 * - Rate limit feedback
 * - Loading and error states
 * - Success feedback with auto-clear
 * - Keyboard shortcuts (Ctrl+Enter to submit)
 *
 * @example
 * ```tsx
 * <CommentBox
 *   token={shareToken}
 *   onCommentAdded={() => {
 *     toast.success('Comment added!');
 *     reloadThread();
 *   }}
 * />
 * ```
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { api } from '@/lib/api';
import { Send, AlertCircle, Check } from 'lucide-react';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

export interface CommentBoxProps {
  /** Share link JWT token */
  token: string;

  /** Callback when comment is successfully added */
  onCommentAdded?: () => void;
}

const MAX_CONTENT_LENGTH = 4000;
const MAX_COMMENTS_PER_HOUR = 10;

/**
 * CommentBox component
 */
export function CommentBox({ token, onCommentAdded }: CommentBoxProps) {
  const [content, setContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async () => {
    // Validation
    if (!content.trim()) {
      setError('Please enter a message');
      return;
    }

    if (content.length > MAX_CONTENT_LENGTH) {
      setError(`Message is too long (max ${MAX_CONTENT_LENGTH} characters)`);
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setSuccess(false);

    try {
      await api.shareLinks.addCommentToSharedThread({
        token,
        content: content.trim(),
      });

      setContent('');
      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
      onCommentAdded?.();

      logger.info('Comment added to shared thread');
    } catch (err: unknown) {
      const context = createErrorContext('CommentBox', 'submitComment', { token: '***' });
      const error = err instanceof Error ? err : new Error(String(err));
      logger.error('Failed to add comment', error, context);

      // Distinguish error types
      if (err instanceof Error) {
        if (err.message.includes('403')) {
          setError('You do not have permission to comment on this thread');
        } else if (err.message.includes('429')) {
          setError(`Rate limit exceeded: Maximum ${MAX_COMMENTS_PER_HOUR} comments per hour`);
        } else if (err.message.includes('401') || err.message.includes('Unauthorized')) {
          setError('Your share link has expired or been revoked');
        } else {
          setError(err.message || 'Failed to add comment');
        }
      } else {
        setError('Failed to add comment');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // Ctrl+Enter to submit
    if (e.ctrlKey && e.key === 'Enter') {
      e.preventDefault();
      handleSubmit();
    }
  };

  const remainingChars = MAX_CONTENT_LENGTH - content.length;
  const isNearLimit = remainingChars < 100;

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        <label htmlFor="comment-textarea" className="text-sm font-medium">
          Add a Comment
        </label>
        <Textarea
          id="comment-textarea"
          placeholder="Type your message here... (Ctrl+Enter to send)"
          value={content}
          onChange={e => setContent(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isSubmitting}
          className="min-h-[100px] resize-none"
          maxLength={MAX_CONTENT_LENGTH}
          aria-describedby="char-counter comment-help"
        />

        {/* Character counter */}
        <div className="flex items-center justify-between text-xs">
          <p id="comment-help" className="text-muted-foreground">
            Press Ctrl+Enter to send
          </p>
          <p
            id="char-counter"
            className={`${isNearLimit ? 'text-destructive font-medium' : 'text-muted-foreground'}`}
          >
            {remainingChars} / {MAX_CONTENT_LENGTH}
          </p>
        </div>
      </div>

      {/* Error alert */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Success alert */}
      {success && (
        <Alert className="border-green-600 bg-green-50 dark:bg-green-950">
          <Check className="h-4 w-4 text-green-600" />
          <AlertDescription className="text-green-800 dark:text-green-200">
            Comment added successfully!
          </AlertDescription>
        </Alert>
      )}

      {/* Submit button */}
      <div className="flex justify-end">
        <LoadingButton
          type="button"
          onClick={handleSubmit}
          isLoading={isSubmitting}
          loadingText="Sending..."
          disabled={!content.trim() || content.length > MAX_CONTENT_LENGTH}
        >
          <Send className="mr-2 h-4 w-4" />
          Send Comment
        </LoadingButton>
      </div>

      {/* Rate limit info */}
      <p className="text-xs text-muted-foreground text-center">
        Rate limited to {MAX_COMMENTS_PER_HOUR} comments per hour
      </p>
    </div>
  );
}

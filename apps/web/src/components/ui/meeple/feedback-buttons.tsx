/**
 * FeedbackButtons Component - Thumbs Up/Down AI Response Feedback
 *
 * Allows users to rate AI responses with optional comment for negative feedback.
 * Integrates with backend ProvideAgentFeedbackCommand.
 *
 * @issue #3352 (AI Response Feedback System)
 */

import React, { useState, useCallback, useRef, useEffect } from 'react';

import { ThumbsUp, ThumbsDown, Loader2, Check } from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type FeedbackValue = 'helpful' | 'not-helpful' | null;

export interface FeedbackButtonsProps {
  /** Current feedback value */
  value: FeedbackValue;
  /** Callback when feedback changes */
  onFeedbackChange: (feedback: FeedbackValue, comment?: string) => Promise<void>;
  /** Whether feedback submission is in progress */
  isLoading?: boolean;
  /** Whether to show comment input for negative feedback */
  showCommentOnNegative?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Size variant */
  size?: 'sm' | 'md';
  /** Disabled state */
  disabled?: boolean;
}

// ============================================================================
// Main Component
// ============================================================================

/**
 * FeedbackButtons - Thumbs up/down rating with optional comment
 *
 * @example
 * ```tsx
 * <FeedbackButtons
 *   value={message.feedback}
 *   onFeedbackChange={async (feedback, comment) => {
 *     await api.agents.submitFeedback({ ... });
 *   }}
 *   showCommentOnNegative
 * />
 * ```
 */
export const FeedbackButtons = React.memo<FeedbackButtonsProps>(
  ({
    value,
    onFeedbackChange,
    isLoading = false,
    showCommentOnNegative = true,
    className,
    size = 'sm',
    disabled = false,
  }) => {
    const [showComment, setShowComment] = useState(false);
    const [comment, setComment] = useState('');
    const [submittedFeedback, setSubmittedFeedback] = useState<FeedbackValue>(null);
    const submittedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
      return () => {
        if (submittedTimerRef.current) clearTimeout(submittedTimerRef.current);
      };
    }, []);

    const handlePositive = useCallback(async () => {
      if (disabled || isLoading) return;

      const newValue = value === 'helpful' ? null : 'helpful';
      setShowComment(false);
      setComment('');

      await onFeedbackChange(newValue);
      if (newValue) {
        setSubmittedFeedback(newValue);
        // Reset submitted state after animation
        submittedTimerRef.current = setTimeout(() => setSubmittedFeedback(null), 1500);
      }
    }, [value, onFeedbackChange, disabled, isLoading]);

    const handleNegative = useCallback(async () => {
      if (disabled || isLoading) return;

      if (value === 'not-helpful') {
        // Toggle off
        setShowComment(false);
        setComment('');
        await onFeedbackChange(null);
        return;
      }

      if (showCommentOnNegative && !showComment) {
        // Show comment input first
        setShowComment(true);
        return;
      }

      // Submit negative feedback (with or without comment)
      await onFeedbackChange('not-helpful', comment.trim() || undefined);
      setShowComment(false);
      setComment('');
      setSubmittedFeedback('not-helpful');
      submittedTimerRef.current = setTimeout(() => setSubmittedFeedback(null), 1500);
    }, [value, onFeedbackChange, showCommentOnNegative, showComment, comment, disabled, isLoading]);

    const handleCommentSubmit = useCallback(async () => {
      if (disabled || isLoading) return;
      await onFeedbackChange('not-helpful', comment.trim() || undefined);
      setShowComment(false);
      setComment('');
      setSubmittedFeedback('not-helpful');
      submittedTimerRef.current = setTimeout(() => setSubmittedFeedback(null), 1500);
    }, [onFeedbackChange, comment, disabled, isLoading]);

    const handleCommentKeyDown = useCallback(
      (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' && !e.shiftKey) {
          e.preventDefault();
          void handleCommentSubmit();
        }
        if (e.key === 'Escape') {
          setShowComment(false);
          setComment('');
        }
      },
      [handleCommentSubmit]
    );

    const handleCancelComment = useCallback(() => {
      setShowComment(false);
      setComment('');
    }, []);

    const iconSize = size === 'sm' ? 'h-4 w-4' : 'h-5 w-5';
    const buttonSize = size === 'sm' ? 'p-1.5' : 'p-2';

    const isPositiveActive = value === 'helpful';
    const isNegativeActive = value === 'not-helpful';
    const justSubmitted = submittedFeedback !== null;

    return (
      <div className={cn('flex flex-col gap-2', className)}>
        <div className="flex items-center gap-1">
          {/* Thumbs Up Button */}
          <button
            type="button"
            onClick={handlePositive}
            disabled={disabled || isLoading}
            className={cn(
              'rounded-md transition-all duration-200',
              buttonSize,
              'focus:outline-none focus:ring-2 focus:ring-primary/50',
              'disabled:opacity-50 disabled:cursor-not-allowed',
              isPositiveActive
                ? 'bg-green-100 text-green-600 dark:bg-green-900/30 dark:text-green-400'
                : 'text-muted-foreground hover:text-green-600 hover:bg-green-50 dark:hover:bg-green-900/20'
            )}
            aria-label={isPositiveActive ? 'Remove helpful rating' : 'Rate as helpful'}
            aria-pressed={isPositiveActive}
          >
            {isLoading && value === 'helpful' ? (
              <Loader2 className={cn(iconSize, 'animate-spin')} />
            ) : submittedFeedback === 'helpful' ? (
              <Check className={cn(iconSize, 'text-green-600')} />
            ) : (
              <ThumbsUp className={cn(iconSize, isPositiveActive && 'fill-current')} />
            )}
          </button>

          {/* Thumbs Down Button */}
          <button
            type="button"
            onClick={handleNegative}
            disabled={disabled || isLoading}
            className={cn(
              'rounded-md transition-all duration-200',
              buttonSize,
              'focus:outline-none focus:ring-2 focus:ring-primary/50',
              'disabled:opacity-50 disabled:cursor-not-allowed',
              isNegativeActive
                ? 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400'
                : 'text-muted-foreground hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20'
            )}
            aria-label={isNegativeActive ? 'Remove not helpful rating' : 'Rate as not helpful'}
            aria-pressed={isNegativeActive}
          >
            {isLoading && value === 'not-helpful' ? (
              <Loader2 className={cn(iconSize, 'animate-spin')} />
            ) : submittedFeedback === 'not-helpful' ? (
              <Check className={cn(iconSize, 'text-red-600')} />
            ) : (
              <ThumbsDown className={cn(iconSize, isNegativeActive && 'fill-current')} />
            )}
          </button>

          {/* Success indicator */}
          {justSubmitted && (
            <span className="text-xs text-muted-foreground ml-1 animate-in fade-in duration-200">
              Thanks!
            </span>
          )}
        </div>

        {/* Comment Input (shown for negative feedback) */}
        {showComment && (
          <div className="flex flex-col gap-2 animate-in slide-in-from-top-2 duration-200">
            <textarea
              value={comment}
              onChange={e => setComment(e.target.value)}
              onKeyDown={handleCommentKeyDown}
              placeholder="What could be improved? (optional)"
              maxLength={500}
              rows={2}
              disabled={disabled || isLoading}
              className={cn(
                'w-full px-3 py-2 text-sm rounded-md border',
                'bg-background text-foreground',
                'border-input focus:border-primary focus:ring-1 focus:ring-primary',
                'placeholder:text-muted-foreground',
                'resize-none',
                'disabled:opacity-50 disabled:cursor-not-allowed'
              )}
              aria-label="Feedback comment"
            />
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleCommentSubmit}
                disabled={disabled || isLoading}
                className={cn(
                  'px-3 py-1.5 text-sm font-medium rounded-md',
                  'bg-primary text-primary-foreground',
                  'hover:bg-primary/90',
                  'focus:outline-none focus:ring-2 focus:ring-primary/50',
                  'disabled:opacity-50 disabled:cursor-not-allowed',
                  'transition-colors'
                )}
              >
                {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Submit'}
              </button>
              <button
                type="button"
                onClick={handleCancelComment}
                disabled={isLoading}
                className={cn(
                  'px-3 py-1.5 text-sm font-medium rounded-md',
                  'text-muted-foreground hover:text-foreground',
                  'hover:bg-muted',
                  'focus:outline-none focus:ring-2 focus:ring-primary/50',
                  'transition-colors'
                )}
              >
                Cancel
              </button>
              <span className="text-xs text-muted-foreground ml-auto">{comment.length}/500</span>
            </div>
          </div>
        )}
      </div>
    );
  }
);

FeedbackButtons.displayName = 'FeedbackButtons';

// ============================================================================
// Exports
// ============================================================================

export default FeedbackButtons;

/**
 * MessageActions - Message action buttons (edit, delete, feedback)
 *
 * Displays appropriate action buttons based on message role:
 * - User messages: Edit and delete buttons (shown on hover)
 * - Assistant messages: Helpful/Not-helpful feedback buttons
 *
 * Performance: React.memo optimized (Issue #2245)
 * - Used in message lists (VirtualizedMessageList)
 * - Pure presentational component
 * - Prevents re-renders when message list updates
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Hover state management
 * - Loading states during operations
 * - Confirmation dialogs
 */

import React from 'react';

import { Button } from '@/components/ui/primitives/button';
import { FEEDBACK_OUTCOMES, type FeedbackOutcome } from '@/lib/constants/feedback';
import { cn } from '@/lib/utils';
import { Message } from '@/types';

interface MessageActionsProps {
  message: Message;
  isUser: boolean;
  onEdit?: (messageId: string, content: string) => void;
  onDelete?: (messageId: string) => void;
  onFeedback?: (messageId: string, feedback: FeedbackOutcome) => void;
  isEditing?: boolean;
  isUpdating?: boolean;
}

export const MessageActions = React.memo(function MessageActions({
  message,
  isUser,
  onEdit,
  onDelete,
  onFeedback,
  isEditing = false,
  isUpdating = false,
}: MessageActionsProps) {
  // User message actions (edit/delete)
  if (isUser && !isEditing) {
    return (
      <div className={cn('message-actions flex gap-1 opacity-0 transition-opacity duration-200')}>
        <Button
          onClick={() => onEdit?.(message.id, message.content)}
          disabled={isUpdating}
          aria-label="Edit message"
          title="Modifica messaggio"
          variant="secondary"
          size="sm"
          className="h-6 px-2 text-[10px]"
        >
          ✏️
        </Button>
        <Button
          onClick={() => onDelete?.(message.id)}
          disabled={isUpdating}
          aria-label="Delete message"
          title="Elimina messaggio"
          variant="secondary"
          size="sm"
          className="h-6 px-2 text-[10px]"
        >
          🗑️
        </Button>
      </div>
    );
  }

  // Assistant message actions (feedback)
  if (!isUser) {
    return (
      <div role="group" aria-label="Message feedback" className="flex gap-2 mt-2">
        <Button
          onClick={() => onFeedback?.(message.id, FEEDBACK_OUTCOMES.HELPFUL)}
          aria-label="Mark as helpful"
          aria-pressed={message.feedback === FEEDBACK_OUTCOMES.HELPFUL}
          variant="ghost"
          size="sm"
          className={cn(
            'h-auto px-2 py-1 text-xs gap-1',
            message.feedback === FEEDBACK_OUTCOMES.HELPFUL
              ? 'bg-green-600 text-white hover:bg-green-700'
              : 'bg-muted text-muted-foreground hover:bg-muted/70 dark:hover:bg-muted/50'
          )}
        >
          <span>👍</span>
          <span>Utile</span>
        </Button>
        <Button
          onClick={() => onFeedback?.(message.id, FEEDBACK_OUTCOMES.NOT_HELPFUL)}
          aria-label="Mark as not helpful"
          aria-pressed={message.feedback === FEEDBACK_OUTCOMES.NOT_HELPFUL}
          variant="ghost"
          size="sm"
          className={cn(
            'h-auto px-2 py-1 text-xs gap-1',
            message.feedback === FEEDBACK_OUTCOMES.NOT_HELPFUL
              ? 'bg-red-600 text-white hover:bg-red-700'
              : 'bg-muted text-muted-foreground hover:bg-muted/70 dark:hover:bg-muted/50'
          )}
        >
          <span>👎</span>
          <span>Non utile</span>
        </Button>
      </div>
    );
  }

  return null;
});

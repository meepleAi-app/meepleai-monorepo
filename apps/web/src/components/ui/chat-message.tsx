/**
 * ChatMessage Component - User/AI Message Display
 *
 * Displays chat messages with role-based layout, confidence badges,
 * citations, and typing indicators following Playful Boardroom design.
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (Page 4 - Chat AI)
 * @issue #1831 (UI-004)
 */

import React from 'react';
import { cn } from '@/lib/utils';
import { MeepleAvatar, type MeepleAvatarState } from './meeple-avatar';
import { Avatar, AvatarImage, AvatarFallback } from './avatar';
import { Badge } from './badge';

// ============================================================================
// Types
// ============================================================================

export interface Citation {
  id: string;
  label: string;
  page?: number;
  source?: string;
}

export interface ChatMessageProps {
  /** Message role: 'user' or 'assistant' */
  role: 'user' | 'assistant';
  /** Message content text */
  content: string;
  /** AI confidence score (0-100) - only for assistant messages */
  confidence?: number;
  /** Citations for the message - only for assistant messages */
  citations?: Citation[];
  /** Message timestamp */
  timestamp?: string | Date;
  /** User avatar configuration (only for user messages) */
  avatar?: {
    src?: string;
    fallback: string;
  };
  /** Show typing indicator instead of content */
  isTyping?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Citation click handler */
  onCitationClick?: (citationId: string) => void;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Maps confidence score to MeepleAvatar state
 */
function getAvatarState(confidence?: number, isTyping?: boolean): MeepleAvatarState {
  if (isTyping) return 'thinking';
  if (confidence === undefined) return 'idle';
  if (confidence >= 85) return 'confident';
  if (confidence >= 70) return 'searching';
  return 'uncertain';
}

/**
 * Maps confidence score to badge variant and color
 */
function getConfidenceBadgeVariant(confidence: number): {
  variant: 'default' | 'secondary' | 'destructive';
  colorClass: string;
} {
  if (confidence >= 85) {
    return { variant: 'default', colorClass: 'bg-green-500 hover:bg-green-600' };
  }
  if (confidence >= 70) {
    return { variant: 'secondary', colorClass: 'bg-yellow-500 hover:bg-yellow-600' };
  }
  return { variant: 'destructive', colorClass: 'bg-red-500 hover:bg-red-600' };
}

/**
 * Formats timestamp for display
 */
function formatTimestamp(timestamp?: string | Date): string {
  if (!timestamp) return '';
  const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
  return date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
}

// ============================================================================
// Sub-Components
// ============================================================================

/**
 * ConfidenceBadge - Color-coded confidence indicator
 */
interface ConfidenceBadgeProps {
  confidence: number;
  className?: string;
}

const ConfidenceBadge = React.memo<ConfidenceBadgeProps>(({ confidence, className }) => {
  const { variant, colorClass } = getConfidenceBadgeVariant(confidence);

  return (
    <Badge
      variant={variant}
      className={cn('text-xs font-medium text-white', colorClass, className)}
      aria-label={`Confidence: ${confidence}%`}
    >
      {confidence}%
    </Badge>
  );
});
ConfidenceBadge.displayName = 'ConfidenceBadge';

/**
 * CitationLink - Clickable citation badge
 */
interface CitationLinkProps {
  citation: Citation;
  onClick?: (citationId: string) => void;
  className?: string;
}

const CitationLink = React.memo<CitationLinkProps>(({ citation, onClick, className }) => {
  const handleClick = () => {
    onClick?.(citation.id);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick?.(citation.id);
    }
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      className={cn(
        'inline-flex items-center gap-1 px-2 py-0.5 text-xs font-medium',
        'bg-orange-500 hover:bg-orange-600 text-white rounded-md',
        'transition-colors focus:outline-none focus:ring-2 focus:ring-orange-400',
        'cursor-pointer',
        className
      )}
      aria-label={`Citation: ${citation.label}${citation.page ? ` page ${citation.page}` : ''}`}
    >
      <span aria-hidden="true">📄</span>
      <span>
        {citation.label}
        {citation.page && ` p.${citation.page}`}
      </span>
    </button>
  );
});
CitationLink.displayName = 'CitationLink';

/**
 * TypingIndicator - Animated 3-dot loading indicator
 */
interface TypingIndicatorProps {
  className?: string;
}

const TypingIndicator = React.memo<TypingIndicatorProps>(({ className }) => {
  return (
    <div
      className={cn('flex items-center gap-1', className)}
      aria-label="AI is typing"
      aria-live="polite"
    >
      <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:-0.3s]" />
      <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:-0.15s]" />
      <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" />
    </div>
  );
});
TypingIndicator.displayName = 'TypingIndicator';

// ============================================================================
// Main Component
// ============================================================================

/**
 * ChatMessage - Message bubble with role-based layout
 *
 * @example
 * ```tsx
 * // AI message with high confidence
 * <ChatMessage
 *   role="assistant"
 *   content="Le risorse si piazzano sui territori..."
 *   confidence={95}
 *   citations={[{ id: '1', label: 'Regolamento', page: 5 }]}
 * />
 *
 * // User message
 * <ChatMessage
 *   role="user"
 *   content="Come si piazzano le risorse?"
 *   avatar={{ fallback: 'U' }}
 * />
 *
 * // AI typing indicator
 * <ChatMessage role="assistant" content="" isTyping />
 * ```
 */
export const ChatMessage = React.forwardRef<HTMLDivElement, ChatMessageProps>(
  (
    {
      role,
      content,
      confidence,
      citations,
      timestamp,
      avatar,
      isTyping = false,
      className,
      onCitationClick,
    },
    ref
  ) => {
    const isAssistant = role === 'assistant';
    const avatarState = isAssistant ? getAvatarState(confidence, isTyping) : undefined;

    return (
      <div
        ref={ref}
        className={cn(
          'flex gap-3 items-start mb-6',
          isAssistant ? 'flex-row' : 'flex-row-reverse',
          className
        )}
        role="article"
        aria-label={`${isAssistant ? 'AI' : 'User'} message`}
      >
        {/* Avatar */}
        {isAssistant ? (
          <MeepleAvatar state={avatarState!} size="md" className="shrink-0" />
        ) : (
          <Avatar className="shrink-0 w-10 h-10">
            {avatar?.src && <AvatarImage src={avatar.src} alt="User avatar" />}
            <AvatarFallback className="bg-primary text-primary-foreground">
              {avatar?.fallback || 'U'}
            </AvatarFallback>
          </Avatar>
        )}

        {/* Message Bubble */}
        <div className="flex-1 max-w-[75%]">
          <div
            className={cn(
              'rounded-lg p-4',
              isAssistant
                ? 'bg-muted text-muted-foreground'
                : 'bg-primary/10 text-foreground ml-auto'
            )}
          >
            {/* Message Content or Typing Indicator */}
            {isTyping ? (
              <TypingIndicator />
            ) : (
              <p className="text-sm leading-relaxed whitespace-pre-wrap">{content}</p>
            )}

            {/* Confidence Badge (AI only) */}
            {isAssistant && confidence !== undefined && !isTyping && (
              <div className="mt-3">
                <ConfidenceBadge confidence={confidence} />
              </div>
            )}

            {/* Citations (AI only) */}
            {isAssistant && citations && citations.length > 0 && !isTyping && (
              <div className="mt-3 flex flex-wrap gap-2" role="list">
                {citations.map(citation => (
                  <CitationLink key={citation.id} citation={citation} onClick={onCitationClick} />
                ))}
              </div>
            )}
          </div>

          {/* Timestamp */}
          {timestamp && !isTyping && (
            <div
              className={cn(
                'text-xs text-muted-foreground mt-1',
                isAssistant ? 'text-left' : 'text-right'
              )}
            >
              {formatTimestamp(timestamp)}
            </div>
          )}
        </div>
      </div>
    );
  }
);

ChatMessage.displayName = 'ChatMessage';

// ============================================================================
// Exports
// ============================================================================

export { ConfidenceBadge, CitationLink, TypingIndicator };
export type { ConfidenceBadgeProps, CitationLinkProps, TypingIndicatorProps };

/**
 * ChatBubble - Enhanced message card (Task #5, Issue #10)
 *
 * Features:
 * - User vs Agent styling (right vs left aligned)
 * - Agent avatar with type-specific colors
 * - Timestamps (relative + absolute on hover)
 * - Confidence scores for agent messages
 * - Citation links
 * - Markdown rendering
 */

'use client';

import { Bot, User, Sparkles, ExternalLink } from 'lucide-react';
import ReactMarkdown from 'react-markdown';

import { Badge } from '@/components/ui/data-display/badge';
import { sanitizeHtml } from '@/lib/security/sanitize';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface Citation {
  documentId: string;
  pageNumber?: number;
  text: string;
  confidence: number;
}

export interface ChatBubbleProps {
  /** Message type */
  type: 'user' | 'agent' | 'system';
  /** Message content */
  content: string;
  /** Timestamp */
  timestamp: Date;
  /** Agent type (for agent messages) */
  agentType?: 'tutor' | 'arbitro' | 'decisore';
  /** Confidence score (for agent messages) */
  confidence?: number;
  /** Citations (for agent messages) */
  citations?: Citation[];
  /** Additional class name */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

const AGENT_COLORS = {
  tutor: 'bg-blue-500',
  arbitro: 'bg-purple-500',
  decisore: 'bg-orange-500',
};

const AGENT_NAMES = {
  tutor: 'Tutor',
  arbitro: 'Arbitro',
  decisore: 'Decisore',
};

/**
 * Format timestamp as relative time
 * "2 minutes ago", "1 hour ago", etc.
 */
function formatRelativeTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHour = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHour / 24);

  if (diffSec < 60) return 'Just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  if (diffHour < 24) return `${diffHour}h ago`;
  return `${diffDay}d ago`;
}

/**
 * Format timestamp as absolute time
 * "Today at 10:30 AM"
 */
function formatAbsoluteTime(date: Date): string {
  const now = new Date();
  const isToday = date.toDateString() === now.toDateString();

  const timeStr = date.toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  if (isToday) return `Today at ${timeStr}`;

  return date.toLocaleDateString('it-IT', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ============================================================================
// Main Component
// ============================================================================

export function ChatBubble({
  type,
  content,
  timestamp,
  agentType = 'tutor',
  confidence,
  citations = [],
  className,
}: ChatBubbleProps) {
  const isUser = type === 'user';
  const isAgent = type === 'agent';
  const isSystem = type === 'system';

  return (
    <div
      className={cn(
        'flex gap-3 mb-4 animate-in fade-in-0 slide-in-from-bottom-2',
        isUser && 'flex-row-reverse',
        className
      )}
      data-testid={`chat-bubble-${type}`}
    >
      {/* Avatar */}
      <div className="shrink-0">
        {isUser && (
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary">
            <User className="h-4 w-4 text-primary-foreground" />
          </div>
        )}
        {isAgent && (
          <div
            className={cn(
              'flex h-8 w-8 items-center justify-center rounded-full',
              AGENT_COLORS[agentType]
            )}
            title={AGENT_NAMES[agentType]}
          >
            <Bot className="h-4 w-4 text-white" />
          </div>
        )}
        {isSystem && (
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted">
            <Sparkles className="h-4 w-4 text-muted-foreground" />
          </div>
        )}
      </div>

      {/* Message Content */}
      <div className={cn('flex-1 min-w-0', isUser && 'flex flex-col items-end')}>
        {/* Agent Name (for agent messages) */}
        {isAgent && (
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs font-medium text-muted-foreground">
              {AGENT_NAMES[agentType]}
            </span>
            {confidence !== undefined && (
              <Badge variant="outline" className="text-xs">
                <Sparkles className="h-3 w-3 mr-1" />
                {(confidence * 100).toFixed(0)}%
              </Badge>
            )}
          </div>
        )}

        {/* Message Bubble */}
        <div
          className={cn(
            'rounded-lg px-4 py-3 max-w-[85%] shadow-sm',
            isUser && 'bg-primary text-primary-foreground',
            isAgent && 'bg-muted/50 border border-border',
            isSystem && 'bg-muted/30 border border-dashed border-border text-muted-foreground'
          )}
        >
          {/* Content with Markdown (XSS-safe) */}
          <div className="prose prose-sm dark:prose-invert max-w-none">
            <ReactMarkdown
              components={{
                p: ({ children }) => <p className="mb-2 last:mb-0">{children}</p>,
                code: ({ children }) => (
                  <code className="bg-background/50 px-1 py-0.5 rounded text-xs">
                    {children}
                  </code>
                ),
              }}
            >
              {sanitizeHtml(content)}
            </ReactMarkdown>
          </div>

          {/* Citations */}
          {citations.length > 0 && (
            <div className="mt-3 pt-3 border-t border-border/50 space-y-1">
              <p className="text-xs font-medium text-muted-foreground">Sources:</p>
              {citations.map((citation, idx) => (
                <a
                  key={idx}
                  href={`/documents/${citation.documentId}`}
                  className="flex items-center gap-2 text-xs text-primary hover:underline"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <ExternalLink className="h-3 w-3" />
                  {citation.pageNumber && `Page ${citation.pageNumber}`}
                  {citation.confidence && (
                    <span className="text-muted-foreground">
                      ({(citation.confidence * 100).toFixed(0)}% match)
                    </span>
                  )}
                </a>
              ))}
            </div>
          )}
        </div>

        {/* Timestamp */}
        <div
          className={cn(
            'mt-1 text-xs text-muted-foreground',
            isUser && 'text-right'
          )}
          title={formatAbsoluteTime(timestamp)}
        >
          {formatRelativeTime(timestamp)}
        </div>
      </div>
    </div>
  );
}

export default ChatBubble;

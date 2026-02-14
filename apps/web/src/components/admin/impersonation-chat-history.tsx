/**
 * ImpersonationChatHistory Component (Issue #3700)
 *
 * Displays a user's AI chat sessions during admin impersonation.
 * Reuses useRecentChatSessions hook for data fetching.
 *
 * Features:
 * - Shows chat session list with title, timestamp, message count
 * - Read-only view (no delete/create during impersonation)
 * - Loading skeleton and empty state
 * - Accessible with WCAG 2.1 AA compliance
 */

'use client';

import { useMemo } from 'react';

import { Bot, MessageSquare, Clock, Hash } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useRecentChatSessions } from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ImpersonationChatHistoryProps {
  /** The impersonated user's ID */
  userId: string;
  /** Maximum sessions to show (default: 20) */
  limit?: number;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatRelativeTime(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
  });
}

// ============================================================================
// Skeleton
// ============================================================================

function ChatHistorySkeleton() {
  return (
    <div className="space-y-3" data-testid="impersonation-chat-skeleton">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex items-center gap-3 p-3 rounded-lg border border-border/40">
          <Skeleton className="h-8 w-8 rounded-lg shrink-0" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
          </div>
          <Skeleton className="h-3 w-16" />
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// Empty State
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-8 text-center"
      data-testid="impersonation-chat-empty"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Bot className="h-6 w-6 text-muted-foreground/50" aria-hidden="true" />
      </div>
      <p className="text-sm text-muted-foreground">
        This user has no AI chat sessions.
      </p>
    </div>
  );
}

// ============================================================================
// Session Item
// ============================================================================

interface SessionItemProps {
  id: string;
  title: string;
  lastMessageAt: string;
  messageCount: number;
  gameTitle?: string;
}

function SessionItem({ title, lastMessageAt, messageCount, gameTitle }: SessionItemProps) {
  return (
    <div
      role="listitem"
      className="flex items-center gap-3 p-3 rounded-lg border border-border/40 bg-card/50 hover:bg-muted/30 transition-colors"
      data-testid="impersonation-chat-session"
    >
      <div className="h-8 w-8 rounded-lg bg-blue-500/10 flex items-center justify-center shrink-0">
        <MessageSquare className="h-4 w-4 text-blue-600 dark:text-blue-400" aria-hidden="true" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium truncate">{title}</p>
        {gameTitle && (
          <p className="text-xs text-muted-foreground truncate">{gameTitle}</p>
        )}
      </div>
      <div className="flex items-center gap-3 shrink-0 text-xs text-muted-foreground">
        <span className="flex items-center gap-1" title="Messages">
          <Hash className="h-3 w-3" aria-hidden="true" />
          <span aria-label={`${messageCount} messages`}>{messageCount}</span>
        </span>
        <span className="flex items-center gap-1" title="Last activity">
          <Clock className="h-3 w-3" aria-hidden="true" />
          {formatRelativeTime(lastMessageAt)}
        </span>
      </div>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function ImpersonationChatHistory({
  userId,
  limit = 20,
  className,
}: ImpersonationChatHistoryProps) {
  const { data, isLoading, error } = useRecentChatSessions(userId, {
    limit,
    enabled: !!userId,
  });

  const sessions = useMemo(() => {
    if (!data?.sessions) return [];
    return data.sessions.map((session) => ({
      id: session.id,
      title: session.title || `Chat ${new Date(session.createdAt).toLocaleDateString()}`,
      lastMessageAt: session.lastMessageAt || session.createdAt,
      messageCount: session.messageCount,
      gameTitle: session.gameTitle || undefined,
    }));
  }, [data]);

  if (isLoading) {
    return (
      <div className={cn('space-y-4', className)} data-testid="impersonation-chat-history">
        <ChatHistorySkeleton />
      </div>
    );
  }

  if (error) {
    return (
      <div className={cn('space-y-4', className)} data-testid="impersonation-chat-history">
        <div role="alert" className="text-sm text-destructive text-center py-4">
          Failed to load chat history.
        </div>
      </div>
    );
  }

  return (
    <div className={cn('space-y-4', className)} data-testid="impersonation-chat-history">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {sessions.length === 0
            ? 'No conversations found'
            : `${sessions.length} conversation${sessions.length !== 1 ? 's' : ''} found`}
        </p>
      </div>

      {sessions.length === 0 ? (
        <EmptyState />
      ) : (
        <div role="list" aria-label="Chat sessions" className="space-y-2">
          {sessions.map((session) => (
            <SessionItem key={session.id} {...session} />
          ))}
        </div>
      )}
    </div>
  );
}

export default ImpersonationChatHistory;

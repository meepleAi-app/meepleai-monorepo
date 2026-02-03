/**
 * ChatHistorySection - Dashboard Widget for Recent AI Chats
 * Issue #3312 - Implement ChatHistorySection
 * Issue #3484 - Backend Integration
 *
 * Features:
 * - Shows up to 4 most recent chat sessions from backend
 * - Displays title, relative timestamp, message count
 * - Click navigates to chat session
 * - "New Chat" button to start new conversation
 * - "View All" link to chat history
 * - Empty state with "Start Conversation" CTA
 * - Loading skeleton state
 * - Delete session with confirmation
 *
 * @example
 * ```tsx
 * <ChatHistorySection userId={currentUser.id} />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Bot,
  ChevronRight,
  MessageSquare,
  Plus,
  Trash2,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import {
  useDeleteChatSession,
  useRecentChatSessions,
} from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatThread {
  id: string;
  title: string;
  lastMessageAt: string;
  messageCount: number;
  gameId?: string;
  gameTitle?: string;
}

export interface ChatHistorySectionProps {
  /** User ID for fetching sessions (required for backend integration) */
  userId?: string;
  /** Chat threads data (optional - for testing or manual override) */
  threads?: ChatThread[];
  /** Total count of threads (for "View All" logic) */
  totalCount?: number;
  /** Loading state (optional - auto-detected from query) */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function formatRelativeTime(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();

  // Check if same day
  const isToday =
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear();

  // Check if yesterday
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const isYesterday =
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear();

  if (isToday) {
    return `Oggi ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  if (isYesterday) {
    return `Ieri ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  // More than yesterday, show date
  return date.toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ============================================================================
// Skeleton Component
// ============================================================================

function ChatHistorySectionSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="chat-history-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-40" />
        </div>
        <Skeleton className="h-8 w-24 rounded-full" />
      </div>

      {/* Threads Skeleton */}
      <div className="space-y-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex items-center justify-between p-2">
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-3 w-24" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="chat-history-empty"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Bot className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <p className="text-sm text-muted-foreground mb-3">
        Nessuna conversazione
      </p>
      <Link href="/chat">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="start-chat-cta"
        >
          <Plus className="h-4 w-4 mr-1" />
          Inizia Conversazione
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// Thread Item Component
// ============================================================================

interface ThreadItemProps {
  thread: ChatThread;
  index: number;
  onDelete?: (threadId: string, gameId?: string) => void;
  isDeleting?: boolean;
}

function ThreadItem({ thread, index, onDelete, isDeleting }: ThreadItemProps) {
  const handleDelete = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onDelete?.(thread.id, thread.gameId);
  };

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(isDeleting && 'opacity-50')}
    >
      <Link
        href={`/chat/${thread.id}`}
        className="flex items-center justify-between p-2 rounded-lg hover:bg-muted/30 transition-colors group"
        data-testid={`chat-thread-${thread.id}`}
      >
        <div className="flex items-center gap-2 min-w-0 flex-1">
          <MessageSquare className="h-4 w-4 text-blue-600 dark:text-blue-400 shrink-0" />
          <div className="min-w-0 flex-1">
            <span
              className="text-sm truncate block group-hover:text-foreground transition-colors"
              data-testid={`chat-title-${thread.id}`}
            >
              {thread.title}
            </span>
            {thread.gameTitle && (
              <span className="text-xs text-muted-foreground truncate block">
                {thread.gameTitle}
              </span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0 ml-2">
          <span
            className="text-xs text-muted-foreground"
            data-testid={`chat-time-${thread.id}`}
          >
            {formatRelativeTime(thread.lastMessageAt)}
          </span>
          {onDelete && (
            <Button
              variant="ghost"
              size="icon"
              className="h-6 w-6 opacity-0 group-hover:opacity-100 transition-opacity text-muted-foreground hover:text-destructive"
              onClick={handleDelete}
              disabled={isDeleting}
              data-testid={`delete-thread-${thread.id}`}
            >
              <Trash2 className="h-3 w-3" />
              <span className="sr-only">Elimina</span>
            </Button>
          )}
        </div>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// ChatHistorySection Component
// ============================================================================

export function ChatHistorySection({
  userId,
  threads: propThreads,
  totalCount: propTotalCount,
  isLoading: propIsLoading,
  className,
}: ChatHistorySectionProps) {
  // Fetch from backend if userId provided and no threads prop
  const {
    data: backendData,
    isLoading: queryLoading,
    error,
  } = useRecentChatSessions(userId, {
    limit: 10,
    enabled: !!userId && !propThreads,
  });

  // Delete mutation
  const deleteMutation = useDeleteChatSession();

  // Map backend data to ChatThread format
  const threads = useMemo(() => {
    if (propThreads) return propThreads;
    if (!backendData?.sessions) return [];

    return backendData.sessions.map((session) => ({
      id: session.id,
      title: session.title || `Chat del ${new Date(session.createdAt).toLocaleDateString('it-IT')}`,
      lastMessageAt: session.lastMessageAt || session.createdAt,
      messageCount: session.messageCount,
      gameId: session.gameId,
      gameTitle: session.gameTitle || undefined,
    }));
  }, [propThreads, backendData]);

  const displayThreads = useMemo(() => threads.slice(0, 4), [threads]);
  const total = propTotalCount ?? backendData?.totalCount ?? threads.length;
  const hasMore = total > 4;
  const isEmpty = threads.length === 0;
  const isLoading = propIsLoading ?? queryLoading;

  const handleDelete = (threadId: string, gameId?: string) => {
    if (!userId) return;
    deleteMutation.mutate({
      sessionId: threadId,
      userId,
      gameId: gameId ?? '',
    });
  };

  if (isLoading) {
    return <ChatHistorySectionSkeleton className={className} />;
  }

  // Show error state
  if (error && !propThreads) {
    return (
      <section
        className={cn(
          'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
          className
        )}
        data-testid="chat-history-error"
      >
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-destructive/20 flex items-center justify-center">
              <MessageSquare className="h-4 w-4 text-destructive" />
            </div>
            <h3 className="font-semibold text-sm">Conversazioni AI Recenti</h3>
          </div>
        </div>
        <p className="text-sm text-muted-foreground text-center py-4">
          Impossibile caricare le conversazioni
        </p>
      </section>
    );
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="chat-history-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
            <MessageSquare className="h-4 w-4 text-blue-600 dark:text-blue-400" />
          </div>
          <h3 className="font-semibold text-sm" data-testid="chat-history-title">
            Conversazioni AI Recenti
          </h3>
        </div>

        {/* New Chat Button */}
        <Link href="/chat">
          <Button
            size="sm"
            variant="outline"
            className="rounded-full"
            data-testid="new-chat-button"
          >
            <Plus className="h-4 w-4 mr-1" />
            Nuova Chat
          </Button>
        </Link>
      </div>

      {/* Content */}
      {isEmpty ? (
        <EmptyState />
      ) : (
        <>
          {/* Threads List */}
          <div className="space-y-1" data-testid="threads-list">
            {displayThreads.map((thread, index) => (
              <ThreadItem
                key={thread.id}
                thread={thread}
                index={index}
                onDelete={userId ? handleDelete : undefined}
                isDeleting={deleteMutation.isPending && deleteMutation.variables?.sessionId === thread.id}
              />
            ))}
          </div>

          {/* View All Link */}
          {hasMore && (
            <div className="mt-4 pt-3 border-t border-border/40">
              <Link
                href="/chat/history"
                className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors justify-center"
                data-testid="view-all-chats"
              >
                Vedi Tutte le Chat
                <ChevronRight className="h-3 w-3" />
              </Link>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default ChatHistorySection;

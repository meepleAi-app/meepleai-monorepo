/**
 * ChatHistorySection - Dashboard Widget for Recent AI Chats
 * Issue #3312 - Implement ChatHistorySection
 *
 * Features:
 * - Shows up to 4 most recent chat threads
 * - Displays title, relative timestamp
 * - Click navigates to chat thread
 * - "New Chat" button to start new conversation
 * - "View All" link to chat history
 * - Empty state with "Start Conversation" CTA
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <ChatHistorySection />
 * ```
 */

'use client';

import { useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  MessageSquare,
  Plus,
  ChevronRight,
  Bot,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

export interface ChatThread {
  id: string;
  title: string;
  lastMessageAt: string;
  messageCount: number;
}

export interface ChatHistorySectionProps {
  /** Chat threads data */
  threads?: ChatThread[];
  /** Total count of threads (for "View All" logic) */
  totalCount?: number;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_THREADS: ChatThread[] = [
  {
    id: 'thread-1',
    title: 'Regole Wingspan - Setup iniziale',
    lastMessageAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), // 2 hours ago
    messageCount: 12,
  },
  {
    id: 'thread-2',
    title: 'Strategie Catan - Espansione Marinai',
    lastMessageAt: new Date(Date.now() - 1000 * 60 * 60 * 28).toISOString(), // Yesterday
    messageCount: 8,
  },
  {
    id: 'thread-3',
    title: 'FAQ Ticket to Ride - Carte duplicate e regole tunnel',
    lastMessageAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(), // 2 days ago
    messageCount: 5,
  },
  {
    id: 'thread-4',
    title: 'Setup Azul - Modalità 2 giocatori',
    lastMessageAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(), // 3 days ago
    messageCount: 3,
  },
];

// ============================================================================
// Helper Functions
// ============================================================================

function formatRelativeTime(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

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

function ThreadItem({ thread, index }: { thread: ChatThread; index: number }) {
  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.05 }}
    >
      <Link
        href={`/chat/${thread.id}`}
        className="flex items-center justify-between p-2 rounded-lg hover:bg-muted/30 transition-colors group"
        data-testid={`chat-thread-${thread.id}`}
      >
        <div className="flex items-center gap-2 min-w-0 flex-1">
          <MessageSquare className="h-4 w-4 text-blue-600 dark:text-blue-400 shrink-0" />
          <span
            className="text-sm truncate group-hover:text-foreground transition-colors"
            data-testid={`chat-title-${thread.id}`}
          >
            {thread.title}
          </span>
        </div>
        <span
          className="text-xs text-muted-foreground shrink-0 ml-2"
          data-testid={`chat-time-${thread.id}`}
        >
          {formatRelativeTime(thread.lastMessageAt)}
        </span>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// ChatHistorySection Component
// ============================================================================

export function ChatHistorySection({
  threads = MOCK_THREADS,
  totalCount,
  isLoading = false,
  className,
}: ChatHistorySectionProps) {
  const displayThreads = useMemo(() => threads.slice(0, 4), [threads]);
  const total = totalCount ?? threads.length;
  const hasMore = total > 4;
  const isEmpty = threads.length === 0;

  if (isLoading) {
    return <ChatHistorySectionSkeleton className={className} />;
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
              <ThreadItem key={thread.id} thread={thread} index={index} />
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

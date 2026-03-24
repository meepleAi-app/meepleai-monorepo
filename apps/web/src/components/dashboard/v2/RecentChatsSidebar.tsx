'use client';

/**
 * RecentChatsSidebar — Dashboard v2 "Il Tavolo"
 *
 * Compact sidebar section showing up to 4 recently active chat threads.
 */

import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ChatThread {
  id: string;
  title: string;
  messageCount: number;
  agentName: string;
  lastMessageAt: string;
}

export interface RecentChatsSidebarProps {
  threads: ChatThread[];
  loading?: boolean;
  className?: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function relativeTime(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'ora';
  if (mins < 60) return `${mins} min fa`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h fa`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'ieri';
  return `${days} giorni fa`;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function RecentChatsSidebar({ threads, loading, className }: RecentChatsSidebarProps) {
  if (!loading && threads.length === 0) return null;

  return (
    <div data-testid="sidebar-chats" className={cn('', className)}>
      <p className="text-[11px] font-bold uppercase tracking-wide text-[hsl(220,80%,55%)] mb-2">
        💬 Chat Recenti
      </p>

      {loading ? (
        <>
          <div className="h-[48px] rounded-[10px] bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[48px] rounded-[10px] bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[48px] rounded-[10px] bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
        </>
      ) : (
        threads.slice(0, 4).map(thread => (
          <div
            key={thread.id}
            className="p-2 rounded-[10px] bg-[rgba(255,255,255,0.5)] backdrop-blur-sm mb-1.5 cursor-pointer hover:bg-[rgba(255,255,255,0.75)] transition-colors"
          >
            <div className="text-xs font-semibold truncate">{thread.title}</div>
            <div className="text-[10px] text-muted-foreground mt-0.5">
              {thread.messageCount} msg · con {thread.agentName} ·{' '}
              {relativeTime(thread.lastMessageAt)}
            </div>
          </div>
        ))
      )}
    </div>
  );
}

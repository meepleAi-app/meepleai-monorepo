/**
 * ChatConversationList - Compact sidebar list of recent chat conversations
 *
 * Used in the desktop 2-panel chat layout as a persistent sidebar.
 * Shows recent sessions grouped by time with active thread highlight.
 */

'use client';

import { useMemo } from 'react';

import { Bot, MessageCircle, Plus } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import { cn } from '@/lib/utils';
import { formatRelativeTime } from '@/lib/utils/timeUtils';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function truncate(text: string, maxLen: number): string {
  return text.length > maxLen ? text.slice(0, maxLen) + '...' : text;
}

// ─── Session Item ────────────────────────────────────────────────────────────

function ConversationItem({
  session,
  isActive,
}: {
  session: ChatSessionSummaryDto;
  isActive: boolean;
}) {
  const title = session.title || 'Chat senza titolo';
  const subtitle = session.agentName ?? session.gameTitle ?? undefined;
  const time = session.lastMessageAt
    ? formatRelativeTime(new Date(session.lastMessageAt), false)
    : null;

  return (
    <Link
      href={`/chat/${session.id}`}
      className={cn(
        'flex flex-col gap-0.5 px-3 py-2.5 rounded-lg transition-colors',
        'hover:bg-muted/60',
        isActive &&
          'bg-amber-50 dark:bg-amber-950/30 border border-amber-200/60 dark:border-amber-800/40'
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span
          className={cn(
            'text-sm font-quicksand font-medium truncate',
            isActive ? 'text-amber-900 dark:text-amber-100' : 'text-foreground'
          )}
        >
          {truncate(title, 32)}
        </span>
        {time && (
          <span className="text-[11px] text-muted-foreground font-nunito shrink-0">{time}</span>
        )}
      </div>
      {subtitle && (
        <span className="text-xs text-muted-foreground font-nunito truncate flex items-center gap-1">
          <Bot className="h-3 w-3 shrink-0" />
          {truncate(subtitle, 28)}
        </span>
      )}
      {session.lastMessagePreview && (
        <span className="text-xs text-muted-foreground/70 font-nunito truncate mt-0.5">
          {truncate(session.lastMessagePreview, 50)}
        </span>
      )}
    </Link>
  );
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function ChatConversationList() {
  const pathname = usePathname();
  const { data, isLoading } = useRecentChatSessions(100);

  const sessions = useMemo(() => data?.sessions ?? [], [data]);

  // Extract active thread ID from pathname: /chat/[threadId]
  const activeThreadId = useMemo(() => {
    const match = pathname?.match(/^\/chat\/([a-f0-9-]+)/);
    return match?.[1] ?? null;
  }, [pathname]);

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border/40">
        <h2 className="text-sm font-quicksand font-bold text-foreground">Chat</h2>
        <Button variant="ghost" size="icon" className="h-7 w-7" asChild>
          <Link href="/chat/new" aria-label="Nuova chat">
            <Plus className="h-4 w-4" />
          </Link>
        </Button>
      </div>

      {/* Session list */}
      <div className="flex-1 overflow-y-auto px-2 py-2 space-y-0.5">
        {isLoading && (
          <div className="space-y-2 px-1">
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="h-14 rounded-lg bg-muted/30 animate-pulse"
                aria-hidden="true"
              />
            ))}
          </div>
        )}

        {!isLoading && sessions.length === 0 && (
          <div className="text-center py-8 text-muted-foreground font-nunito">
            <MessageCircle className="h-8 w-8 mx-auto mb-2 opacity-30" />
            <p className="text-xs">Nessuna chat</p>
          </div>
        )}

        {!isLoading &&
          sessions.map(session => (
            <ConversationItem
              key={session.id}
              session={session}
              isActive={session.id === activeThreadId}
            />
          ))}
      </div>
    </div>
  );
}

/**
 * ChatListMobile - Mobile chat session list grouped by game
 *
 * Displays recent chat sessions in a mobile-friendly list,
 * grouped by game title (or "Generale" if no game).
 * Uses MobileHeader for consistent mobile navigation.
 */

'use client';

import { useMemo } from 'react';

import { Bot, MessageCircle } from 'lucide-react';
import Link from 'next/link';

import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import { cn } from '@/lib/utils';
import { formatRelativeTime } from '@/lib/utils/timeUtils';

// ─── Helpers ────────────────────────────────────────────────────────────────

interface GameGroup {
  key: string;
  label: string;
  sessions: ChatSessionSummaryDto[];
}

function groupSessionsByGame(sessions: ChatSessionSummaryDto[]): GameGroup[] {
  const map = new Map<string, GameGroup>();

  for (const session of sessions) {
    const key = session.gameTitle ?? '__general__';
    const label = session.gameTitle ?? 'Generale';

    if (!map.has(key)) {
      map.set(key, { key, label, sessions: [] });
    }
    map.get(key)!.sessions.push(session);
  }

  // Sort groups: most recently active first
  return [...map.values()].sort((a, b) => {
    const latestA = a.sessions[0]?.lastMessageAt ?? a.sessions[0]?.createdAt ?? '';
    const latestB = b.sessions[0]?.lastMessageAt ?? b.sessions[0]?.createdAt ?? '';
    return new Date(latestB).getTime() - new Date(latestA).getTime();
  });
}

function truncate(text: string, maxLen: number): string {
  return text.length > maxLen ? text.slice(0, maxLen) + '...' : text;
}

// ─── Session Row ────────────────────────────────────────────────────────────

function SessionRow({ session }: { session: ChatSessionSummaryDto }) {
  const title = session.title || 'Chat senza titolo';
  const preview = session.lastMessagePreview
    ? truncate(session.lastMessagePreview, 60)
    : `${session.messageCount} messaggi`;
  const time = session.lastMessageAt
    ? formatRelativeTime(new Date(session.lastMessageAt), false)
    : null;

  return (
    <Link
      href={`/chat/${session.id}`}
      className={cn(
        'flex items-start gap-3 px-4 py-3',
        'active:bg-muted/60 transition-colors',
        'border-b border-border/20'
      )}
    >
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-purple-100 dark:bg-purple-900/30">
        <Bot className="h-4 w-4 text-purple-600 dark:text-purple-400" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm font-quicksand font-semibold text-foreground truncate">
            {truncate(title, 30)}
          </span>
          {time && (
            <span className="text-[11px] text-muted-foreground font-nunito shrink-0">{time}</span>
          )}
        </div>
        <p className="text-xs text-muted-foreground font-nunito truncate mt-0.5">{preview}</p>
      </div>
    </Link>
  );
}

// ─── Empty State ────────────────────────────────────────────────────────────

function ChatEmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-6 text-center">
      <MessageCircle className="h-12 w-12 text-muted-foreground/30 mb-4" />
      <h3 className="text-base font-quicksand font-semibold text-foreground mb-1">Nessuna chat</h3>
      <p className="text-sm text-muted-foreground font-nunito">
        Inizia una nuova conversazione con un agente AI!
      </p>
    </div>
  );
}

// ─── Skeleton Loading ───────────────────────────────────────────────────────

function SessionSkeleton() {
  return (
    <div className="flex items-start gap-3 px-4 py-3 border-b border-border/20">
      <div className="h-9 w-9 rounded-full bg-muted/40 animate-pulse shrink-0" />
      <div className="flex-1 space-y-2">
        <div className="h-4 w-3/4 bg-muted/40 animate-pulse rounded" />
        <div className="h-3 w-1/2 bg-muted/30 animate-pulse rounded" />
      </div>
    </div>
  );
}

// ─── Main Component ─────────────────────────────────────────────────────────

export function ChatListMobile() {
  const { data, isLoading, error } = useRecentChatSessions(100);

  const groups = useMemo(() => {
    const sessions = data?.sessions ?? [];
    return groupSessionsByGame(sessions);
  }, [data]);

  return (
    <div className="flex flex-col h-full bg-background">
      <MobileHeader title="Chat" />

      <div className="flex-1 overflow-y-auto">
        {/* Loading */}
        {isLoading && (
          <div>
            {Array.from({ length: 4 }).map((_, i) => (
              <SessionSkeleton key={i} />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="px-4 py-8 text-center">
            <p className="text-sm text-red-500 font-nunito">Errore nel caricamento delle chat.</p>
          </div>
        )}

        {/* Empty */}
        {!isLoading && !error && groups.length === 0 && <ChatEmptyState />}

        {/* Grouped list */}
        {!isLoading &&
          !error &&
          groups.map(group => (
            <div key={group.key}>
              {/* Group header */}
              <div className="sticky top-0 z-10 px-4 py-2 bg-muted/50 backdrop-blur-sm border-b border-border/20">
                <span className="text-xs font-quicksand font-bold text-muted-foreground uppercase tracking-wider">
                  {group.label}
                </span>
              </div>
              {group.sessions.map(session => (
                <SessionRow key={session.id} session={session} />
              ))}
            </div>
          ))}
      </div>
    </div>
  );
}

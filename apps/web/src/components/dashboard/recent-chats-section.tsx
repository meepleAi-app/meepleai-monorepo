/**
 * RecentChatsDashboardSection — Issue #5097, Epic #5094
 *
 * Dashboard section: 2 recent chat sessions as compact list-cards.
 * Warm brown accent to match dashboard palette.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { ArrowRight, MessageSquare } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

// ─── Section header ───────────────────────────────────────────────────────────

function SectionHeader() {
  return (
    <div className="flex items-center justify-between mb-3">
      <h3 className="font-quicksand text-sm font-bold text-foreground">💬 Chat recenti</h3>
      <Link
        href="/chat"
        className="flex items-center gap-1 text-xs font-semibold font-nunito text-muted-foreground hover:text-foreground transition-colors"
      >
        Vedi tutte
        <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}

// ─── Chat list card ───────────────────────────────────────────────────────────

function ChatListCard({ chat }: { chat: ChatSessionSummaryDto }) {
  const lastMessageLabel = chat.lastMessageAt
    ? formatDistanceToNow(new Date(chat.lastMessageAt), {
        addSuffix: true,
        locale: it,
      })
    : chat.createdAt
      ? formatDistanceToNow(new Date(chat.createdAt), {
          addSuffix: true,
          locale: it,
        })
      : null;

  const title = chat.title ?? (chat.gameTitle ? `Chat · ${chat.gameTitle}` : 'Chat senza titolo');

  return (
    <Link
      href={`/chat/${chat.id}`}
      className="flex items-center gap-3 px-3 py-2.5 rounded-xl border border-border bg-surface hover:bg-accent/40 transition-colors group"
      style={{ borderLeftWidth: 3, borderLeftColor: 'hsl(25,50%,45%)' }}
    >
      {/* Icon */}
      <div className="w-10 h-10 rounded-lg bg-[hsl(25,40%,92%)] dark:bg-[hsl(25,30%,20%)] shrink-0 flex items-center justify-center">
        <MessageSquare className="h-4 w-4 text-[hsl(25,50%,45%)]" />
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="font-quicksand font-bold text-sm text-foreground truncate group-hover:text-[hsl(25,70%,40%)] transition-colors">
          {title}
        </p>
        <p className="text-xs text-muted-foreground font-nunito mt-0.5 truncate">
          {[
            chat.gameTitle,
            chat.messageCount > 0 ? `${chat.messageCount} messaggi` : null,
            lastMessageLabel,
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
      </div>

      <ArrowRight className="h-3.5 w-3.5 text-muted-foreground shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" />
    </Link>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

function ChatListCardSkeleton() {
  return (
    <div className="flex items-center gap-3 px-3 py-2.5 rounded-xl border border-border">
      <Skeleton className="w-10 h-10 rounded-lg shrink-0" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="h-3 w-1/2" />
      </div>
    </div>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────

function EmptyChats() {
  return (
    <div className="flex flex-col items-center gap-2 py-6 text-center text-muted-foreground">
      <MessageSquare className="h-7 w-7 opacity-30" />
      <p className="text-sm font-nunito">Nessuna chat recente</p>
      <Link
        href="/chat/new"
        className="text-xs font-semibold text-[hsl(25,80%,45%)] hover:underline"
      >
        Inizia una conversazione →
      </Link>
    </div>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

export function RecentChatsDashboardSection() {
  const { data, isLoading } = useRecentChatSessions(2);
  const chats = data?.sessions?.slice(0, 2) ?? [];

  return (
    <section>
      <SectionHeader />
      <div className="space-y-2">
        {isLoading ? (
          <>
            <ChatListCardSkeleton />
            <ChatListCardSkeleton />
          </>
        ) : chats.length === 0 ? (
          <EmptyChats />
        ) : (
          chats.map(chat => <ChatListCard key={chat.id} chat={chat} />)
        )}
      </div>
    </section>
  );
}

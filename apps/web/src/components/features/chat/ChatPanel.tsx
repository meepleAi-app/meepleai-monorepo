'use client';

/**
 * ChatPanel — Chat tab panel for AlphaShell.
 *
 * Displays a list of recent chat sessions as MeepleCards.
 * Each card shows agent name, last message preview, and message count.
 * Includes a FAB for creating new chats.
 */

import { MessageCircle, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyStateCard, SkeletonCardGrid } from '@/components/features/common';
import { MeepleCard, entityColors } from '@/components/ui/data-display/meeple-card';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';

export function ChatPanel() {
  const router = useRouter();

  const { data: chatSessions, isLoading } = useRecentChatSessions(30);

  const chats = chatSessions?.sessions ?? [];

  return (
    <div className="p-4 sm:p-6 relative">
      <h2 className="font-quicksand font-bold text-lg mb-4">Le Tue Chat</h2>

      {isLoading ? (
        <SkeletonCardGrid count={4} />
      ) : chats.length === 0 ? (
        <EmptyStateCard
          title="Nessuna chat"
          description="Inizia una conversazione con un agente AI per ricevere assistenza sulle regole e strategie dei tuoi giochi."
          ctaLabel="Nuova Chat"
          onCtaClick={() => router.push('/chat/new')}
          icon={MessageCircle}
          entityColor={entityColors.chatSession.hsl}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
          {chats.map(chat => {
            const timeAgo = chat.lastMessageAt ? formatRelativeTime(chat.lastMessageAt) : undefined;

            return (
              <MeepleCard
                key={chat.id}
                id={chat.id}
                entity="chatSession"
                variant="list"
                title={chat.agentName ?? chat.title ?? 'Chat'}
                subtitle={chat.gameTitle ?? undefined}
                chatPreview={
                  chat.lastMessagePreview
                    ? { lastMessage: chat.lastMessagePreview, sender: 'agent' }
                    : undefined
                }
                chatStats={{ messageCount: chat.messageCount }}
                metadata={[
                  {
                    icon: MessageCircle,
                    label: `${chat.messageCount} msg`,
                  },
                  ...(timeAgo ? [{ label: timeAgo }] : []),
                ]}
                onClick={() => router.push(`/chat/${chat.id}`)}
              />
            );
          })}
        </div>
      )}

      {/* FAB: Nuova Chat */}
      <button
        onClick={() => router.push('/chat/new')}
        className="fixed bottom-20 right-4 lg:bottom-6 lg:right-6 z-40
                   w-14 h-14 rounded-full flex items-center justify-center
                   text-white shadow-lg hover:shadow-xl
                   transition-all duration-200 hover:scale-105 active:scale-95"
        style={{ backgroundColor: `hsl(${entityColors.chatSession.hsl})` }}
        aria-label="Nuova Chat"
      >
        <Plus className="w-6 h-6" />
      </button>
    </div>
  );
}

/**
 * Formats a datetime string as a relative time label (e.g. "2h fa", "3g fa").
 */
function formatRelativeTime(dateStr: string): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diffMs = now - then;

  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return 'ora';
  if (minutes < 60) return `${minutes}m fa`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h fa`;

  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}g fa`;

  const weeks = Math.floor(days / 7);
  if (weeks < 4) return `${weeks}s fa`;

  return new Date(dateStr).toLocaleDateString('it-IT', {
    day: 'numeric',
    month: 'short',
  });
}

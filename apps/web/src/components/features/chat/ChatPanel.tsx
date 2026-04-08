'use client';

/**
 * ChatPanel — Chat tab panel for AlphaShell.
 *
 * Displays a list of recent chat sessions as MeepleChatCard adapters.
 * Each card shows agent name, last message preview, and message count.
 * Includes a FAB for creating new chats.
 */

import { MessageCircle, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MeepleChatCard } from '@/components/chat-unified/MeepleChatCard';
import { EmptyStateCard, SkeletonCardGrid } from '@/components/features/common';
import { entityHsl } from '@/components/ui/data-display/meeple-card';
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
          entityColor={entityHsl('chat')}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
          {chats.map(chat => (
            <MeepleChatCard
              key={chat.id}
              chat={{
                id: chat.id,
                title: chat.agentName ?? chat.title ?? 'Chat',
                lastMessageAt: chat.lastMessageAt ?? new Date().toISOString(),
                messageCount: chat.messageCount,
                agentId: chat.agentId ?? null,
              }}
              variant="list"
              onClick={() => router.push(`/chat/${chat.id}`)}
            />
          ))}
        </div>
      )}

      {/* FAB: Nuova Chat */}
      <button
        onClick={() => router.push('/chat/new')}
        className="absolute bottom-20 right-4 lg:bottom-6 lg:right-6 z-40
                   w-14 h-14 rounded-full flex items-center justify-center
                   text-white shadow-lg hover:shadow-xl
                   transition-all duration-200 hover:scale-105 active:scale-95"
        style={{ backgroundColor: `hsl(${entityHsl('chat')})` }}
        aria-label="Nuova Chat"
      >
        <Plus className="w-6 h-6" />
      </button>
    </div>
  );
}

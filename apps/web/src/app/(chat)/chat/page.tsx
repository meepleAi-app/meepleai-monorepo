/**
 * Chat Sessions List Page - /chat
 *
 * Lists all user's chat sessions using EntityListView with MeepleCard.
 * Navigation footer links to Game, Agent, Session.
 *
 * @see Issue #4695
 */

'use client';

import { useMemo } from 'react';

import { MessageCircle, Plus } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { EntityListView } from '@/components/ui/data-display/entity-list-view';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { getNavigationLinks } from '@/config/entity-navigation';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

function formatRelativeDate(dateString: string | null): string {
  if (!dateString) return 'Mai';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60_000);
  if (diffMins < 1) return 'Adesso';
  if (diffMins < 60) return `${diffMins}m fa`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h fa`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays}g fa`;
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

function renderChatSessionCard(
  session: ChatSessionSummaryDto,
): Omit<MeepleCardProps, 'entity' | 'variant'> {
  const navLinks = getNavigationLinks('chatSession', {
    id: session.id,
    gameId: session.gameId,
  });

  return {
    title: session.title || 'Chat senza titolo',
    subtitle: session.gameTitle || undefined,
    metadata: [
      { icon: MessageCircle, value: `${session.messageCount} messaggi` },
    ],
    badge: session.isArchived ? 'Archiviata' : undefined,
    chatPreview: session.lastMessagePreview
      ? { lastMessage: session.lastMessagePreview, sender: 'agent' as const }
      : undefined,
    navigateTo: navLinks,
  };
}

export default function ChatListPage() {
  const router = useRouter();
  const { data, isLoading, error } = useRecentChatSessions(100);

  const sessions = useMemo(() => data?.sessions ?? [], [data]);

  if (error) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Alert variant="destructive">
            <AlertDescription>
              Errore nel caricamento delle sessioni chat.
            </AlertDescription>
          </Alert>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-quicksand font-bold text-foreground">
              Le tue Chat
            </h1>
            <p className="text-muted-foreground font-nunito mt-1">
              Tutte le conversazioni con gli agenti AI
            </p>
          </div>
          <Button asChild className="font-nunito">
            <Link href="/chat/new">
              <Plus className="mr-2 h-4 w-4" />
              Nuova Chat
            </Link>
          </Button>
        </div>

        {/* List */}
        <EntityListView<ChatSessionSummaryDto>
          items={sessions}
          entity="chatSession"
          persistenceKey="chat-sessions-list"
          loading={isLoading}
          renderItem={renderChatSessionCard}
          onItemClick={(session) => router.push(`/chat/${session.id}`)}
          searchable
          searchPlaceholder="Cerca nelle chat..."
          searchFields={['title', 'gameTitle', 'lastMessagePreview']}
          emptyMessage="Nessuna chat trovata. Inizia una nuova conversazione!"
          title="Sessioni Chat"
          showViewSwitcher
          gridColumns={{ default: 1, sm: 2, lg: 3, xl: 4 }}
        />
      </div>
    </div>
  );
}

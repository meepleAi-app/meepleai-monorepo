'use client';

/**
 * HomeFeed — Home tab panel for AlphaShell.
 *
 * Displays a vertical feed of sections:
 * 1. Sessioni Attive — active game sessions
 * 2. I Tuoi Giochi Recenti — recently added library games
 * 3. Serate di Gioco — upcoming game nights
 * 4. Chat Recenti — recent chat threads
 *
 * Each section uses MeepleCard for data items and EmptyStateCard for empty states.
 */

import { Gamepad2, BookOpen, CalendarDays, MessageCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyStateCard } from '@/components/features/common';
import { SkeletonCardGrid } from '@/components/features/common';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { entityColors } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import { useAlphaNav } from '@/hooks/useAlphaNav';

export function HomeFeed() {
  const router = useRouter();
  const { openDetail } = useAlphaNav();

  const { data: activeSessions, isLoading: sessionsLoading } = useActiveSessions(5);
  const { data: recentGames, isLoading: gamesLoading } = useRecentlyAddedGames(6);
  const { data: gameNights, isLoading: nightsLoading } = useUpcomingGameNights();
  const { data: chatSessions, isLoading: chatsLoading } = useRecentChatSessions(5);

  const sessions = activeSessions?.sessions ?? [];
  const games = recentGames?.items ?? [];
  const nights = gameNights ?? [];
  const chats = chatSessions?.sessions ?? [];

  return (
    <div className="p-4 sm:p-6 space-y-8">
      {/* Sessioni Attive */}
      <section>
        <h2 className="font-quicksand font-bold text-lg mb-4">Sessioni Attive</h2>
        {sessionsLoading ? (
          <SkeletonCardGrid count={2} />
        ) : sessions.length === 0 ? (
          <EmptyStateCard
            title="Nessuna sessione attiva"
            description="Avvia una nuova sessione di gioco per iniziare a tracciare le tue partite."
            ctaLabel="Nuova Sessione"
            onCtaClick={() => router.push('/sessions/new')}
            icon={Gamepad2}
            entityColor={entityColors.session.hsl}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {sessions.map(session => (
              <MeepleCard
                key={session.id}
                id={session.id}
                entity="session"
                variant="list"
                title={session.notes || `Sessione ${session.id.slice(0, 8)}`}
                subtitle={`${session.playerCount} giocatori`}
                badge={session.status}
                metadata={[
                  {
                    icon: Gamepad2,
                    label: `${session.durationMinutes} min`,
                  },
                ]}
                onClick={() => {
                  if (session.status === 'Setup' || session.status === 'Paused') {
                    router.push(`/sessions/live/${session.id}`);
                  } else {
                    openDetail(session.id, 'session');
                  }
                }}
              />
            ))}
          </div>
        )}
      </section>

      {/* I Tuoi Giochi Recenti */}
      <section>
        <h2 className="font-quicksand font-bold text-lg mb-4">I Tuoi Giochi Recenti</h2>
        {gamesLoading ? (
          <SkeletonCardGrid count={3} />
        ) : games.length === 0 ? (
          <EmptyStateCard
            title="Nessun gioco in libreria"
            description="Aggiungi i tuoi giochi preferiti alla libreria per accedervi rapidamente."
            ctaLabel="Esplora Catalogo"
            onCtaClick={() => router.push('/discover')}
            icon={BookOpen}
            entityColor={entityColors.game.hsl}
          />
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
            {games.map(entry => (
              <MeepleCard
                key={entry.gameId}
                id={entry.gameId}
                entity="game"
                variant="grid"
                title={entry.gameTitle ?? 'Gioco senza nome'}
                subtitle={entry.gamePublisher ?? undefined}
                imageUrl={entry.gameImageUrl ?? entry.gameIconUrl ?? undefined}
                rating={entry.averageRating ?? undefined}
                ratingMax={10}
                onClick={() => openDetail(entry.gameId, 'game')}
              />
            ))}
          </div>
        )}
      </section>

      {/* Serate di Gioco */}
      <section>
        <h2 className="font-quicksand font-bold text-lg mb-4">Serate di Gioco</h2>
        {nightsLoading ? (
          <SkeletonCardGrid count={2} />
        ) : nights.length === 0 ? (
          <EmptyStateCard
            title="Nessuna serata in programma"
            description="Organizza una serata di gioco e invita i tuoi amici."
            ctaLabel="Crea Serata"
            onCtaClick={() => router.push('/game-nights/new')}
            icon={CalendarDays}
            entityColor={entityColors.event.hsl}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {nights.map(night => (
              <MeepleCard
                key={night.id}
                id={night.id}
                entity="event"
                variant="list"
                title={night.title}
                subtitle={night.location ?? undefined}
                badge={night.status}
                metadata={[
                  {
                    icon: CalendarDays,
                    label: new Date(night.scheduledAt).toLocaleDateString('it-IT', {
                      day: 'numeric',
                      month: 'short',
                      hour: '2-digit',
                      minute: '2-digit',
                    }),
                  },
                ]}
                onClick={() => openDetail(night.id, 'event')}
              />
            ))}
          </div>
        )}
      </section>

      {/* Chat Recenti */}
      <section>
        <h2 className="font-quicksand font-bold text-lg mb-4">Chat Recenti</h2>
        {chatsLoading ? (
          <SkeletonCardGrid count={2} />
        ) : chats.length === 0 ? (
          <EmptyStateCard
            title="Nessuna chat recente"
            description="Inizia una conversazione con un agente AI per ricevere assistenza sui tuoi giochi."
            ctaLabel="Nuova Chat"
            onCtaClick={() => router.push('/chat/new')}
            icon={MessageCircle}
            entityColor={entityColors.chatSession.hsl}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {chats.map(chat => (
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
                    label: `${chat.messageCount} messaggi`,
                  },
                ]}
                onClick={() => router.push(`/chat/${chat.id}`)}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

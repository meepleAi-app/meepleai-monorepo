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

import { MeepleChatCard } from '@/components/chat-unified/MeepleChatCard';
import { EmptyStateCard } from '@/components/features/common';
import { SkeletonCardGrid } from '@/components/features/common';
import { MeepleEventCard } from '@/components/game-night/MeepleEventCard';
import { MeepleLibraryGameCard } from '@/components/library/MeepleLibraryGameCard';
import { MeepleCard, entityHsl } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import { useNavigation } from '@/hooks/useNavigation';

export function HomeFeed() {
  const router = useRouter();
  const { openDetail } = useNavigation();

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
            entityColor={entityHsl('session')}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            {sessions.map(session => (
              <MeepleCard
                key={session.id}
                entity="session"
                variant="list"
                title={session.notes || `Sessione ${session.id.slice(0, 8)}`}
                subtitle={`${session.playerCount} giocatori`}
                badge={session.status}
                metadata={[{ label: `${session.durationMinutes} min` }]}
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
            onCtaClick={() => router.push('/library')}
            icon={BookOpen}
            entityColor={entityHsl('game')}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
            {games.map(entry => (
              <MeepleLibraryGameCard
                key={entry.gameId}
                game={entry}
                variant="grid"
                onConfigureAgent={() => {}}
                onUploadPdf={() => {}}
                onEditNotes={() => {}}
                onRemove={() => {}}
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
            entityColor={entityHsl('event')}
          />
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            {nights.map(night => (
              <MeepleEventCard
                key={night.id}
                event={{
                  id: night.id,
                  title: night.title,
                  scheduledAt: night.scheduledAt,
                  location: night.location ?? null,
                  participantCount: 0,
                  gameCount: 0,
                }}
                variant="list"
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
      </section>
    </div>
  );
}

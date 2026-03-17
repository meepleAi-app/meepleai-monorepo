/**
 * CardsZone — Dashboard zone with horizontal-scroll card sections.
 *
 * Three sections of MeepleCards:
 * 1. Recent Games (entity="game") — from user's library
 * 2. Recent Sessions (entity="session") — completed + active sessions
 * 3. Active Chats (entity="chatSession") — recent AI conversations
 */

'use client';

import { Gamepad2, Clock, MessageSquare } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import type { GameSessionDto } from '@/lib/api/schemas';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ---------------------------------------------------------------------------
// Section container with horizontal scroll
// ---------------------------------------------------------------------------

interface CardSectionProps {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  'data-testid'?: string;
}

function CardSection({ title, icon, children, 'data-testid': testId }: CardSectionProps) {
  return (
    <section data-testid={testId}>
      <div className="flex items-center gap-2 mb-3">
        {icon}
        <h3 className="font-quicksand text-sm font-bold text-foreground">{title}</h3>
      </div>
      {children}
    </section>
  );
}

function HorizontalScroll({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="overflow-x-auto flex gap-4 snap-x snap-mandatory pb-2 scrollbar-hide"
      style={{ WebkitOverflowScrolling: 'touch' }}
    >
      {children}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Empty states
// ---------------------------------------------------------------------------

function EmptyState({
  icon: Icon,
  message,
  linkHref,
  linkLabel,
}: {
  icon: React.ComponentType<{ className?: string }>;
  message: string;
  linkHref: string;
  linkLabel: string;
}) {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center text-muted-foreground">
      <Icon className="h-8 w-8 opacity-30" />
      <p className="text-sm font-nunito">{message}</p>
      <Link
        href={linkHref}
        className="text-xs font-semibold text-[hsl(25,95%,45%)] hover:underline"
      >
        {linkLabel}
      </Link>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Skeleton row
// ---------------------------------------------------------------------------

function SkeletonRow({ count }: { count: number }) {
  return (
    <HorizontalScroll>
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
          <MeepleCardSkeleton variant="grid" />
        </div>
      ))}
    </HorizontalScroll>
  );
}

// ---------------------------------------------------------------------------
// Recent Games section
// ---------------------------------------------------------------------------

function RecentGamesCards() {
  const { data, isLoading } = useRecentlyAddedGames(8);
  const games = data?.items ?? [];

  if (isLoading) {
    return <SkeletonRow count={4} />;
  }

  if (games.length === 0) {
    return (
      <EmptyState
        icon={Gamepad2}
        message="Nessun gioco in libreria"
        linkHref="/library?action=add"
        linkLabel="Aggiungi il tuo primo gioco →"
      />
    );
  }

  return (
    <HorizontalScroll>
      {games.map((game: UserLibraryEntry) => (
        <div key={game.id} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
          <MeepleCard
            id={game.gameId}
            entity="game"
            variant="grid"
            title={game.gameTitle}
            subtitle={game.gamePublisher ?? undefined}
            imageUrl={game.gameImageUrl ?? undefined}
            data-testid={`cards-zone-game-${game.gameId}`}
          />
        </div>
      ))}
    </HorizontalScroll>
  );
}

// ---------------------------------------------------------------------------
// Recent Sessions section
// ---------------------------------------------------------------------------

function RecentSessionsCards() {
  const { data: activeData, isLoading: activeLoading } = useActiveSessions(8);
  const sessions = activeData?.sessions ?? [];

  if (activeLoading) {
    return <SkeletonRow count={4} />;
  }

  if (sessions.length === 0) {
    return (
      <EmptyState
        icon={Clock}
        message="Nessuna sessione recente"
        linkHref="/sessions"
        linkLabel="Inizia una partita →"
      />
    );
  }

  return (
    <HorizontalScroll>
      {sessions.map((session: GameSessionDto) => (
        <div key={session.id} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
          <MeepleCard
            id={session.id}
            entity="session"
            variant="grid"
            title={session.status === 'Completed' ? 'Partita completata' : `Partita in corso`}
            subtitle={`${session.playerCount} giocatori`}
            badge={session.status}
            data-testid={`cards-zone-session-${session.id}`}
          />
        </div>
      ))}
    </HorizontalScroll>
  );
}

// ---------------------------------------------------------------------------
// Active Chats section
// ---------------------------------------------------------------------------

function ActiveChatsCards() {
  const { data, isLoading } = useRecentChatSessions(6);
  const chats = data?.sessions ?? [];

  if (isLoading) {
    return <SkeletonRow count={3} />;
  }

  if (chats.length === 0) {
    return (
      <EmptyState
        icon={MessageSquare}
        message="Nessuna chat recente"
        linkHref="/chat/new"
        linkLabel="Inizia una conversazione →"
      />
    );
  }

  return (
    <HorizontalScroll>
      {chats.map((chat: ChatSessionSummaryDto) => {
        const title =
          chat.title ?? (chat.gameTitle ? `Chat · ${chat.gameTitle}` : 'Chat senza titolo');
        return (
          <div key={chat.id} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
            <MeepleCard
              id={chat.id}
              entity="chatSession"
              variant="grid"
              title={title}
              subtitle={chat.gameTitle ?? undefined}
              badge={chat.messageCount > 0 ? `${chat.messageCount} msg` : undefined}
              data-testid={`cards-zone-chat-${chat.id}`}
            />
          </div>
        );
      })}
    </HorizontalScroll>
  );
}

// ---------------------------------------------------------------------------
// CardsZone (public)
// ---------------------------------------------------------------------------

export function CardsZone() {
  return (
    <div data-testid="cards-zone" className="space-y-6">
      <CardSection
        title="Giochi recenti"
        icon={<Gamepad2 className="h-4 w-4 text-[hsl(25,95%,45%)]" />}
        data-testid="cards-zone-games"
      >
        <RecentGamesCards />
      </CardSection>

      <CardSection
        title="Sessioni recenti"
        icon={<Clock className="h-4 w-4 text-[hsl(262,83%,58%)]" />}
        data-testid="cards-zone-sessions"
      >
        <RecentSessionsCards />
      </CardSection>

      <CardSection
        title="Chat attive"
        icon={<MessageSquare className="h-4 w-4 text-[hsl(200,80%,50%)]" />}
        data-testid="cards-zone-chats"
      >
        <ActiveChatsCards />
      </CardSection>
    </div>
  );
}

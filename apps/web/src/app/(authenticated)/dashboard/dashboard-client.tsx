'use client';

import { useEffect } from 'react';

import { BookOpen, Dice5, FileText, MessageCircle, Search } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { DashboardScrollRow } from '@/components/dashboard/DashboardScrollRow';
import { HeroBanner } from '@/components/dashboard/HeroBanner';
import { QuickActionsRow } from '@/components/dashboard/QuickActionsRow';
import { EmptyState } from '@/components/layout/Layout';
import { useLayout } from '@/components/layout/LayoutProvider';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card-parts';
import type { TrendingGameDto, UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

export function DashboardClient() {
  const router = useRouter();
  const { user } = useAuth();
  const { responsive } = useLayout();
  const isMobile = responsive.isMobile;

  const {
    stats: _stats,
    isLoadingStats: _isLoadingStats,
    fetchStats,
    recentSessions,
    isLoadingSessions,
    fetchRecentSessions,
    updateFilters,
    games,
    isLoadingGames,
    trendingGames,
    isLoadingTrending,
    fetchTrendingGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(8);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const userName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';
  const nextSession = recentSessions[0];

  const quickActions = [
    {
      icon: <BookOpen className="w-5 h-5" />,
      label: 'Libreria',
      href: '/library',
      entityColor: '25 95% 45%',
    },
    {
      icon: <Dice5 className="w-5 h-5" />,
      label: 'Nuova partita',
      href: '/sessions/new',
      entityColor: '240 60% 55%',
    },
    {
      icon: <MessageCircle className="w-5 h-5" />,
      label: 'Chat AI',
      href: '/chat',
      entityColor: '220 80% 55%',
    },
    {
      icon: <FileText className="w-5 h-5" />,
      label: 'Regole',
      href: '/library?tab=private',
      entityColor: '174 60% 40%',
    },
    { icon: <Search className="w-5 h-5" />, label: 'Scopri', href: '/games' },
  ];

  return (
    <div className="flex flex-col gap-5 w-full max-w-7xl mx-auto px-3 sm:px-4 py-4 sm:py-5">
      {/* Hero Banner */}
      <HeroBanner
        title={nextSession ? `Prossima: ${nextSession.gameName}` : `Ciao ${userName}!`}
        subtitle={
          nextSession ? `${nextSession.playerCount} giocatori` : 'Esplora la tua libreria di giochi'
        }
        badge={nextSession ? { text: 'LIVE', variant: 'live' as const } : undefined}
        cta={
          nextSession
            ? { label: 'Entra nella sessione', href: `/sessions/${nextSession.id}` }
            : { label: 'Aggiungi un gioco', href: '/library' }
        }
      />

      {/* Quick Actions */}
      <QuickActionsRow actions={quickActions} />

      {/* Recent Sessions -- horizontal scroll */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-quicksand font-bold text-sm text-foreground flex items-center gap-1.5">
            <span className="text-primary">&#x1F550;</span> Giocati di recente
          </h2>
          <Link href="/sessions" className="text-xs text-primary font-semibold">
            Tutte &rarr;
          </Link>
        </div>
        {isLoadingSessions ? (
          <DashboardScrollRow>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="w-[140px] sm:w-[160px] shrink-0 snap-start">
                <MeepleCardSkeleton variant="grid" />
              </div>
            ))}
          </DashboardScrollRow>
        ) : recentSessions.length === 0 ? (
          <EmptyState
            title="Nessuna partita ancora"
            description="Inizia una sessione di gioco per vederla qui"
            action={
              <Link href="/sessions/new" className="text-sm text-primary font-semibold">
                Nuova partita &rarr;
              </Link>
            }
          />
        ) : (
          <DashboardScrollRow>
            {recentSessions.map(session => (
              <div key={session.id} className="w-[140px] sm:w-[160px] shrink-0 snap-start">
                <MeepleCard
                  entity="game"
                  variant="grid"
                  title={session.gameName}
                  subtitle={session.winnerName ? `Vincitore: ${session.winnerName}` : undefined}
                  imageUrl={session.gameImageUrl}
                  metadata={[
                    { label: `${session.playerCount} giocatori` },
                    ...(session.duration
                      ? [{ label: session.duration.match(/(\d+:\d+)/)?.[1] ?? session.duration }]
                      : []),
                  ]}
                  onClick={() => router.push(`/sessions/${session.id}`)}
                />
              </div>
            ))}
          </DashboardScrollRow>
        )}
      </section>

      {/* Library section -- grid desktop, list mobile */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-quicksand font-bold text-sm text-foreground flex items-center gap-1.5">
            <span className="text-primary">&#x1F4DA;</span> La Tua Libreria
          </h2>
          <Link href="/library" className="text-xs text-primary font-semibold">
            {games.length} giochi &rarr;
          </Link>
        </div>
        {isLoadingGames ? (
          <div
            className={
              isMobile
                ? 'flex flex-col gap-2'
                : 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'
            }
          >
            {Array.from({ length: isMobile ? 4 : 8 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant={isMobile ? 'list' : 'grid'} />
            ))}
          </div>
        ) : (
          <div
            className={
              isMobile
                ? 'flex flex-col gap-2'
                : 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'
            }
          >
            {games.slice(0, isMobile ? 6 : 8).map((game: UserGameDto) => (
              <MeepleCard
                key={game.id}
                entity="game"
                variant={isMobile ? 'list' : 'grid'}
                title={game.title}
                subtitle={game.publisher}
                imageUrl={game.imageUrl ?? game.thumbnailUrl}
                rating={game.averageRating}
                ratingMax={10}
                metadata={[
                  ...(game.minPlayers && game.maxPlayers
                    ? [{ label: `${game.minPlayers}-${game.maxPlayers}` }]
                    : []),
                  ...(game.playingTimeMinutes ? [{ label: `${game.playingTimeMinutes}'` }] : []),
                ]}
                onClick={() => router.push(`/library/${game.id}`)}
              />
            ))}
          </div>
        )}
      </section>

      {/* Popolari questa settimana — horizontal scroll */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-quicksand font-bold text-sm text-foreground flex items-center gap-1.5">
            <span className="text-primary">🔥</span> Popolari questa settimana
          </h2>
          <Link href="/games" className="text-xs text-primary font-semibold">
            Scopri &rarr;
          </Link>
        </div>
        {isLoadingTrending ? (
          <DashboardScrollRow>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="w-[140px] sm:w-[160px] shrink-0 snap-start">
                <MeepleCardSkeleton variant="grid" />
              </div>
            ))}
          </DashboardScrollRow>
        ) : trendingGames.length === 0 ? (
          <EmptyState
            title="Nessun trend ancora"
            description="I giochi più popolari appariranno qui"
            action={
              <Link href="/games" className="text-sm text-primary font-semibold">
                Esplora il catalogo &rarr;
              </Link>
            }
          />
        ) : (
          <DashboardScrollRow>
            {trendingGames.map((game: TrendingGameDto) => (
              <div
                key={`trending-${game.gameId}`}
                className="w-[140px] sm:w-[160px] shrink-0 snap-start"
              >
                <MeepleCard
                  entity="game"
                  variant="grid"
                  title={game.title}
                  imageUrl={game.thumbnailUrl}
                  coverLabels={
                    game.rank <= 3 ? [{ text: `#${game.rank}`, primary: true }] : undefined
                  }
                  metadata={[
                    ...(game.playCount > 0 ? [{ label: `🎲 ${game.playCount}` }] : []),
                    ...(game.libraryAddCount > 0 ? [{ label: `📚 ${game.libraryAddCount}` }] : []),
                  ]}
                  onClick={() => router.push(`/games/${game.gameId}`)}
                />
              </div>
            ))}
          </DashboardScrollRow>
        )}
      </section>
    </div>
  );
}

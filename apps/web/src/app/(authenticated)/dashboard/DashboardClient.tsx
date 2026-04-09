'use client';

import { useEffect, useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useCardHand } from '@/stores/use-card-hand';

import { ChatRecentCards, type ChatRecentPreview } from './sections/ChatRecentCards';
import { ContinueCarousel, type ContinueCarouselGame } from './sections/ContinueCarousel';
import { FriendsRow, type FriendPreview } from './sections/FriendsRow';
import { GreetingRow } from './sections/GreetingRow';
import { HeroLiveSession, type LiveSessionPreview } from './sections/HeroLiveSession';
import { KpiStrip } from './sections/KpiStrip';

/**
 * Phase 2 dashboard client — new layout with greeting, hero, KPI strip, carousel,
 * chat cards, and friends row. Reads data from the existing useDashboardStore
 * (fields not yet present in the store default to empty arrays / zeros).
 */
export function DashboardClient() {
  const { user } = useAuth();
  const router = useRouter();
  const drawCard = useCardHand(s => s.drawCard);

  // TODO(Phase 3): read from useDashboardStore once it exposes Phase 2 fields.
  // For now we render the layout with empty defaults so the shell + hero empty
  // state are visible and the page compiles without depending on data shape.
  const liveSession: LiveSessionPreview | null = null;
  const continueGames: ContinueCarouselGame[] = [];
  const recentChats: ChatRecentPreview[] = [];
  const activeFriends: FriendPreview[] = [];
  const kpis = { games: 0, sessions: 0, friends: 0, chats: 0 };

  // Memoize the mini-nav config so the structural key doesn't change across renders.
  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Home · Gaming Hub',
      tabs: [{ id: 'overview', label: 'Panoramica', href: '/dashboard' }],
      activeTabId: 'overview',
      primaryAction: {
        label: 'Nuova partita',
        icon: '＋',
        onClick: () => router.push('/sessions/new'),
      },
    }),
    [router]
  );
  useMiniNavConfig(miniNavConfig);

  // Draw this page as a hand card for cross-page context memory
  useEffect(() => {
    drawCard({
      id: 'section-dashboard',
      entity: 'game',
      title: 'Home',
      href: '/dashboard',
    });
  }, [drawCard]);

  const handleContinueSession = () => {
    const session = liveSession as LiveSessionPreview | null;
    if (session) {
      router.push(`/sessions/live/${session.id}`);
    } else {
      router.push('/sessions/new');
    }
  };

  const displayName = user?.displayName ?? 'giocatore';

  return (
    <div className="mx-auto max-w-[1440px] p-7 pb-12">
      <GreetingRow
        displayName={displayName}
        subtitle="Ecco cosa succede nella tua tavola oggi"
        stats={[
          { label: 'Partite mese', value: String(kpis.sessions) },
          { label: 'Giochi', value: String(kpis.games) },
        ]}
      />

      <HeroLiveSession session={liveSession} onContinue={handleContinueSession} />

      <KpiStrip kpis={kpis} />

      <ContinueCarousel games={continueGames} />
      <ChatRecentCards chats={recentChats} />
      <FriendsRow friends={activeFriends} />
    </div>
  );
}

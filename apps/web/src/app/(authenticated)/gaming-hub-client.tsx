'use client';

import { useEffect, useMemo } from 'react';

import { motion } from 'framer-motion';

import {
  AgentsDashboardSection,
  HeroZone,
  QuickCardsCarousel,
  QuickStats,
  RecentChatsDashboardSection,
  RecentGamesSection,
  SessionModeDashboard,
} from '@/components/dashboard';
import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { useOnboardingStatus } from '@/components/onboarding/use-onboarding-status';
import { WelcomeChecklist } from '@/components/onboarding/WelcomeChecklist';
import { WelcomeWizard } from '@/components/onboarding/WelcomeWizard';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useDashboardContext } from '@/hooks/useDashboardContext';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { useCardHand } from '@/stores/use-card-hand';

function fadeUp(delay: number) {
  return {
    initial: { opacity: 0, y: 12 },
    animate: { opacity: 1, y: 0 },
    transition: { delay: delay * 0.08, duration: 0.3, ease: [0.25, 0.46, 0.45, 0.94] },
  } as const;
}

export function GamingHubClient() {
  const { user } = useAuthUser();
  const { openWizard: _openWizard } = useAddGameWizard();
  const { showChecklist, isLoading: isLoadingOnboarding } = useOnboardingStatus();

  const { drawCard, protectCard } = useCardHand();
  const {
    stats,
    recentSessions,
    games,
    isLoadingStats,
    isLoadingGames,
    fetchStats,
    fetchRecentSessions,
    fetchGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(1);
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  useEffect(() => {
    drawCard({ id: 'section-dashboard', entity: 'custom', title: 'Dashboard', href: '/library' });
  }, [drawCard]);

  // Upcoming game night
  const { data: upcomingNights } = useUpcomingGameNights();
  const upcomingGameNight = upcomingNights?.[0] ?? null;

  // Context detection
  const dashCtx = useDashboardContext({
    recentSessions,
    games,
    upcomingGameNight,
    incompleteSessions: [],
  });

  // Protect session card from eviction when in session mode
  useEffect(() => {
    if (dashCtx.isSessionMode && dashCtx.hero.data) {
      const sessionData = dashCtx.hero.data as { id: string };
      protectCard(sessionData.id);
    }
  }, [dashCtx.isSessionMode, dashCtx.hero.data, protectCard]);

  // Quick cards: recent games
  const quickCards = useMemo(() => {
    return games.slice(0, 3).map(g => ({
      id: g.id,
      title: g.title,
      imageUrl: g.imageUrl ?? g.thumbnailUrl,
      entity: 'game' as const,
    }));
  }, [games]);

  const displayName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';
  const greeting = getGreeting();

  // ── Session Mode: simplified layout ───────────────────────────────────────
  if (dashCtx.isSessionMode && dashCtx.hero.data) {
    return (
      <div className="py-6 space-y-4 w-full max-w-screen-xl mx-auto">
        <SessionModeDashboard
          session={
            dashCtx.hero.data as {
              id: string;
              gameId: string;
              playerCount: number;
              durationMinutes: number;
            }
          }
        />
      </div>
    );
  }

  // ── Normal Mode: full contextual dashboard ────────────────────────────────
  return (
    <div className="py-6 space-y-6 w-full max-w-screen-xl mx-auto">
      <WelcomeWizard />
      {!isLoadingOnboarding && showChecklist && <WelcomeChecklist />}

      {/* 1. Greeting + Stats */}
      <motion.section {...fadeUp(0)}>
        <div className="mb-4">
          <p className="text-sm font-nunito font-medium text-muted-foreground mb-0.5">{greeting}</p>
          <h1 className="font-quicksand text-2xl font-bold text-foreground">{displayName}</h1>
        </div>
        <QuickStats stats={stats} isLoading={isLoadingStats} />
      </motion.section>

      {/* 2. Contextual Hero Zone */}
      <motion.section {...fadeUp(1)}>
        <HeroZone hero={dashCtx.hero} lastSession={recentSessions[0]} />
      </motion.section>

      {/* 3. Quick Cards Carousel */}
      {quickCards.length > 0 && (
        <motion.section {...fadeUp(2)}>
          <h2 className="font-quicksand text-base font-bold text-foreground mb-2">
            Accesso rapido
          </h2>
          <QuickCardsCarousel items={quickCards} />
        </motion.section>
      )}

      {/* 4. Content grid */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_300px] xl:grid-cols-[1fr_340px] gap-6">
        <div className="space-y-6">
          <motion.section {...fadeUp(3)}>
            <RecentGamesSection games={games} isLoading={isLoadingGames} />
          </motion.section>
        </div>
        <div className="space-y-6">
          <motion.section {...fadeUp(4)}>
            <AgentsDashboardSection />
          </motion.section>
          <motion.section {...fadeUp(5)}>
            <RecentChatsDashboardSection />
          </motion.section>
        </div>
      </div>
    </div>
  );
}

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Buongiorno,';
  if (hour < 18) return 'Buon pomeriggio,';
  return 'Buonasera,';
}

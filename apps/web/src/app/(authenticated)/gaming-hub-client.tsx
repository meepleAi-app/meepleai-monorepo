/**
 * Gaming Hub Client — Issue #5098, Epic #5094
 *
 * Dashboard redesign: Hero + 2-column content layout.
 *
 * Sections:
 *   1. Greeting + QuickStats (full width)
 *   2. DashboardSessionHero (full width)
 *   3. 2-column grid:
 *      - Left: RecentGamesSection (3 list-cards)
 *      - Right: AgentsDashboardSection + RecentChatsDashboardSection
 */

'use client';

import { useEffect } from 'react';

import { motion } from 'framer-motion';

import {
  AgentsDashboardSection,
  DashboardSessionHero,
  QuickStats,
  RecentChatsDashboardSection,
  RecentGamesSection,
} from '@/components/dashboard-v2';
import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { useOnboardingStatus } from '@/components/onboarding/use-onboarding-status';
import { WelcomeChecklist } from '@/components/onboarding/WelcomeChecklist';
import { WelcomeWizard } from '@/components/onboarding/WelcomeWizard';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { useCardHand } from '@/stores/use-card-hand';

// ─── Animation factory ───────────────────────────────────────────────────────

function fadeUp(delay: number) {
  return {
    initial: { opacity: 0, y: 12 },
    animate: { opacity: 1, y: 0 },
    transition: { delay: delay * 0.08, duration: 0.3, ease: [0.25, 0.46, 0.45, 0.94] },
  } as const;
}

// ─── Main component ───────────────────────────────────────────────────────────

export function GamingHubClient() {
  const { user } = useAuthUser();
  const { openWizard: _openWizard } = useAddGameWizard();
  const { showChecklist, isLoading: isLoadingOnboarding } = useOnboardingStatus();

  const { drawCard } = useCardHand();
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
    fetchRecentSessions(1); // used as "last session" fallback in hero
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  useEffect(() => {
    drawCard({
      id: 'section-dashboard',
      entity: 'custom',
      title: 'Dashboard',
      href: '/dashboard',
    });
  }, [drawCard]);

  const displayName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';

  const greeting = getGreeting();

  return (
    <div className="py-6 space-y-6 w-full max-w-screen-xl mx-auto">
      {/* ── 0. Onboarding (first-time users) ─────────────────────── */}
      <WelcomeWizard />
      {!isLoadingOnboarding && showChecklist && <WelcomeChecklist />}

      {/* ── 1. Greeting + QuickStats ────────────────────────────── */}
      <motion.section {...fadeUp(0)}>
        <div className="mb-4">
          <p className="text-sm font-nunito font-medium text-muted-foreground mb-0.5">{greeting}</p>
          <h1 className="font-quicksand text-2xl font-bold text-foreground">{displayName}</h1>
        </div>
        <QuickStats stats={stats} isLoading={isLoadingStats} />
      </motion.section>

      {/* ── 2. Session Hero ─────────────────────────────────────── */}
      <motion.section {...fadeUp(1)}>
        <DashboardSessionHero lastSession={recentSessions[0]} />
      </motion.section>

      {/* ── 3. Content grid: 2-col on desktop ──────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_300px] xl:grid-cols-[1fr_340px] gap-6">
        {/* Left column: Recent games */}
        <motion.section {...fadeUp(2)}>
          <RecentGamesSection games={games} isLoading={isLoadingGames} />
        </motion.section>

        {/* Right column: Agents + Chats stacked */}
        <div className="space-y-6">
          <motion.section {...fadeUp(3)}>
            <AgentsDashboardSection />
          </motion.section>

          <motion.section {...fadeUp(4)}>
            <RecentChatsDashboardSection />
          </motion.section>
        </div>
      </div>
    </div>
  );
}

// ─── Time-based greeting ─────────────────────────────────────────────────────

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Buongiorno,';
  if (hour < 18) return 'Buon pomeriggio,';
  return 'Buonasera,';
}

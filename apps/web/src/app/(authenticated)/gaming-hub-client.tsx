/**
 * Gaming Hub Client — Issue #5098, Epic #5094
 *
 * Dashboard redesign: Hero + mixed sections layout.
 *
 * Sections:
 *   1. Greeting + QuickStats
 *   2. DashboardSessionHero (active session or empty state)
 *   3. RecentGamesSection (3 list-cards)
 *   4. AgentsDashboardSection (2 agent cards + CTA)
 *   5. RecentChatsDashboardSection (2 list-cards)
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
import { Layout } from '@/components/layout';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

// ─── Animation factory ───────────────────────────────────────────────────────

function fadeUp(delay: number) {
  return {
    initial: { opacity: 0, y: 10 },
    animate: { opacity: 1, y: 0 },
    transition: { delay: delay * 0.07, duration: 0.25 },
  } as const;
}

// ─── Main component ───────────────────────────────────────────────────────────

export function GamingHubClient() {
  const { user } = useAuthUser();

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
    fetchRecentSessions(1);           // used as "last session" fallback in hero
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  const displayName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';

  return (
    <Layout showActionBar>
      <div className="py-6 space-y-8 max-w-4xl">

        {/* ── 1. Greeting + QuickStats ────────────────────────────── */}
        <motion.section {...fadeUp(0)}>
          <p className="font-quicksand text-2xl font-bold text-foreground mb-4">
            Ciao, {displayName}! 👋
          </p>
          <QuickStats stats={stats} isLoading={isLoadingStats} />
        </motion.section>

        {/* ── 2. Session Hero ─────────────────────────────────────── */}
        <motion.section {...fadeUp(1)}>
          <DashboardSessionHero lastSession={recentSessions[0]} />
        </motion.section>

        {/* ── 3. Giochi recenti ───────────────────────────────────── */}
        <motion.section {...fadeUp(2)}>
          <RecentGamesSection games={games} isLoading={isLoadingGames} />
        </motion.section>

        {/* ── 4. Agenti ───────────────────────────────────────────── */}
        <motion.section {...fadeUp(3)}>
          <AgentsDashboardSection />
        </motion.section>

        {/* ── 5. Chat recenti ─────────────────────────────────────── */}
        <motion.section {...fadeUp(4)}>
          <RecentChatsDashboardSection />
        </motion.section>

      </div>
    </Layout>
  );
}

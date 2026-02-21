/**
 * Gaming Hub Client - Issue #4936 (redesigned)
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 *
 * Full visual overhaul aligned to mockup style:
 * - Glassmorphism welcome banner with warm palette
 * - Horizontal 4-KPI stats row
 * - MeepleUserLibraryCard game grid
 * - SessionRow recent sessions (warm card style)
 * - Dark mode consistent with mockup tokens
 */

'use client';

import { useEffect } from 'react';

import { motion } from 'framer-motion';
import { ArrowRight, Plus } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import {
  QuickStats,
  RecentSessions,
  GameCollectionGrid,
  FilterBar,
  EmptyState,
} from '@/components/dashboard-v2';
import { Layout } from '@/components/layout';
import { Button } from '@/components/ui/primitives/button';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

// ─── Animation factory ───────────────────────────────────────────────────────

function fadeUpProps(delayIndex: number) {
  return {
    initial: { opacity: 0, y: 12 },
    animate: { opacity: 1, y: 0 },
    transition: { delay: delayIndex * 0.06, duration: 0.28 },
  } as const;
}

// ─── Section header ───────────────────────────────────────────────────────────

function SectionHeader({
  title,
  subtitle,
  href,
  hrefLabel,
}: {
  title: string;
  subtitle?: string;
  href?: string;
  hrefLabel?: string;
}) {
  return (
    <div className="flex items-end justify-between mb-4">
      <div>
        <h2 className="text-lg font-quicksand font-bold text-foreground">{title}</h2>
        {subtitle && (
          <p className="text-xs text-muted-foreground font-nunito mt-0.5">{subtitle}</p>
        )}
      </div>
      {href && hrefLabel && (
        <Link
          href={href}
          className="text-sm font-nunito text-[hsl(25,95%,45%)] hover:underline flex items-center gap-1"
        >
          {hrefLabel}
          <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      )}
    </div>
  );
}

// ─── Main component ───────────────────────────────────────────────────────────

export function GamingHubClient() {
  const { user } = useAuthUser();
  const router = useRouter();

  const {
    stats,
    recentSessions,
    games,
    filters,
    isLoadingStats,
    isLoadingSessions,
    isLoadingGames,
    fetchStats,
    fetchRecentSessions,
    fetchGames,
    updateFilters,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(3);
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  return (
    <Layout showActionBar>
      <div className="py-6 space-y-8 max-w-7xl">
        {/* ── Page Header ──────────────────────────────────────────── */}
        <motion.div {...fadeUpProps(0)}>
          <div className="flex items-start justify-between">
            <div>
              <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
                Gaming Hub
              </h1>
              <p className="text-sm text-muted-foreground mt-1">
                Bentornato, {user?.displayName || 'Giocatore'} · partite, collezione e statistiche di gioco
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <Button variant="ghost" size="sm" asChild className="gap-1.5 font-nunito">
                <Link href="/library">
                  La mia libreria
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              </Button>
              <Button
                size="sm"
                className="gap-1.5 font-nunito bg-[hsl(25,95%,45%)] hover:bg-[hsl(25,95%,40%)] text-white shadow-sm"
                asChild
              >
                <Link href="/play-records/new">
                  <Plus className="h-3.5 w-3.5" />
                  Nuova partita
                </Link>
              </Button>
            </div>
          </div>
        </motion.div>

        {/* ── KPI Stats Row ────────────────────────────────────────── */}
        <motion.section {...fadeUpProps(1)}>
          <QuickStats stats={stats} isLoading={isLoadingStats} />
        </motion.section>

        {/* ── Recent Sessions ──────────────────────────────────────── */}
        <motion.section {...fadeUpProps(2)}>
          <RecentSessions
            sessions={recentSessions}
            isLoading={isLoadingSessions}
            onViewAll={() => router.push('/play-records')}
            onNewSession={() => router.push('/play-records/new')}
          />
        </motion.section>

        {/* ── Game Collection ──────────────────────────────────────── */}
        <motion.section {...fadeUpProps(3)}>
          <SectionHeader
            title="I Miei Giochi"
            subtitle={stats ? `${stats.totalGames} giochi in collezione` : undefined}
            href="/library"
            hrefLabel="Vedi tutti"
          />

          <FilterBar
            categories={['all', 'strategy', 'family', 'party', 'solo', 'cooperative']}
            currentCategory={filters.category}
            currentSort={filters.sort}
            onCategoryChange={(cat) => updateFilters({ category: cat })}
            onSortChange={(sort) =>
              updateFilters({ sort: sort as 'alphabetical' | 'playCount' })
            }
          />

          {games.length === 0 && !isLoadingGames ? (
            <EmptyState variant="no-games" />
          ) : (
            <GameCollectionGrid
              games={games}
              isLoading={isLoadingGames}
              onGameClick={(id) => router.push(`/games/${id}`)}
            />
          )}
        </motion.section>
      </div>
    </Layout>
  );
}

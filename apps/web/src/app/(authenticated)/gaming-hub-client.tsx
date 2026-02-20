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
import { ArrowRight, Gamepad2, Plus } from 'lucide-react';
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
import { cn } from '@/lib/utils';

// ─── Animation factory ───────────────────────────────────────────────────────

function fadeUpProps(delayIndex: number) {
  return {
    initial: { opacity: 0, y: 12 },
    animate: { opacity: 1, y: 0 },
    transition: { delay: delayIndex * 0.06, duration: 0.28 },
  } as const;
}

// ─── Hero Banner ─────────────────────────────────────────────────────────────

interface HeroBannerProps {
  displayName: string | undefined;
  monthlyPlays: number;
  monthlyPlaysChange: number;
}

function HeroBanner({ displayName, monthlyPlays, monthlyPlaysChange }: HeroBannerProps) {
  const getChangeText = (change: number) => {
    if (change === 0) return null;
    const sign = change > 0 ? '+' : '';
    return `${sign}${change}%`;
  };
  const changeText = getChangeText(monthlyPlaysChange);

  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-2xl px-6 py-5',
        // Glassmorphism — warm palette
        'bg-[rgba(255,248,240,0.85)] dark:bg-[rgba(30,24,18,0.80)]',
        'backdrop-blur-md',
        'border border-[rgba(200,160,100,0.25)] dark:border-[rgba(100,80,50,0.30)]',
        'shadow-[0_8px_32px_rgba(180,120,60,0.10)] dark:shadow-[0_8px_32px_rgba(0,0,0,0.25)]'
      )}
    >
      {/* Subtle decorative gradient */}
      <div
        className="pointer-events-none absolute inset-0 opacity-30"
        style={{
          background:
            'radial-gradient(ellipse at top left, hsl(25 95% 45% / 0.15), transparent 60%)',
        }}
        aria-hidden="true"
      />

      <div className="relative flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-quicksand font-bold text-[hsl(25,60%,25%)] dark:text-[hsl(25,80%,85%)]">
            Bentornato, {displayName || 'Giocatore'} 👋
          </h1>
          {monthlyPlays > 0 && (
            <p className="mt-1 text-sm font-nunito text-[rgba(100,60,20,0.70)] dark:text-[rgba(240,210,170,0.65)]">
              {monthlyPlays} partite questo mese
              {changeText && (
                <span
                  className={cn(
                    'ml-1.5 text-xs font-semibold px-1.5 py-0.5 rounded-full',
                    monthlyPlaysChange > 0
                      ? 'bg-emerald-100/70 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400'
                      : 'bg-red-100/70 text-red-700 dark:bg-red-900/40 dark:text-red-400'
                  )}
                >
                  {changeText} vs mese scorso
                </span>
              )}
            </p>
          )}
        </div>

        {/* CTA buttons */}
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
    </div>
  );
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
        {/* ── Hero Banner ─────────────────────────────────────────── */}
        <motion.div {...fadeUpProps(0)}>
          <HeroBanner
            displayName={user?.displayName ?? undefined}
            monthlyPlays={stats?.monthlyPlays ?? 0}
            monthlyPlaysChange={stats?.monthlyPlaysChange ?? 0}
          />
        </motion.div>

        {/* ── KPI Stats Row ────────────────────────────────────────── */}
        <motion.section {...fadeUpProps(1)}>
          <SectionHeader title="Panoramica" subtitle="Il tuo riepilogo di gioco" />
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

        {/* ── Upcoming Games ───────────────────────────────────────── */}
        <motion.section {...fadeUpProps(4)}>
          <SectionHeader title="Prossime Partite" subtitle="Sessioni programmate" />
          <EmptyState variant="no-upcoming" />
        </motion.section>
      </div>
    </Layout>
  );
}

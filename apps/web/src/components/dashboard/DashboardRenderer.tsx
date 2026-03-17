'use client';

import { Suspense } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { useDashboardMode } from './useDashboardMode';
import { HeroZone, StatsZone, CardsZone, SessionBar, ScoreboardZone } from './zones';
import './dashboard-transitions.css';

/** Skeleton fallback for lazy-loaded zones. */
function ZoneSkeleton({ testId }: { testId: string }) {
  return <div data-testid={testId} className="animate-pulse rounded-2xl bg-muted h-32 w-full" />;
}

/**
 * Main dashboard layout renderer.
 *
 * Reads the current mode from `useDashboardMode()` and renders
 * either exploration zones or game-mode zones with animated
 * transitions via framer-motion `AnimatePresence`.
 */
export function DashboardRenderer() {
  const { state, isGameMode, isExploration } = useDashboardMode();

  return (
    <div data-testid="dashboard-renderer" className="flex flex-col gap-6 w-full">
      <AnimatePresence mode="wait">
        {(isExploration || state === 'transitioning') && !isGameMode && (
          <motion.div
            key="exploration"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col gap-6 w-full"
          >
            <motion.div layoutId="hero">
              <Suspense fallback={<ZoneSkeleton testId="hero-skeleton" />}>
                <HeroZone />
              </Suspense>
            </motion.div>

            <Suspense fallback={<ZoneSkeleton testId="stats-skeleton" />}>
              <StatsZone />
            </Suspense>

            <div className="w-full">
              <Suspense fallback={<ZoneSkeleton testId="cards-skeleton" />}>
                <CardsZone />
              </Suspense>
            </div>
          </motion.div>
        )}

        {isGameMode && (
          <motion.div
            key="gameMode"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="flex flex-col gap-6 w-full"
          >
            <motion.div layoutId="hero">
              <Suspense fallback={<ZoneSkeleton testId="session-bar-skeleton" />}>
                <SessionBar />
              </Suspense>
            </motion.div>

            <Suspense fallback={<ZoneSkeleton testId="scoreboard-skeleton" />}>
              <ScoreboardZone />
            </Suspense>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

'use client';

import { Suspense, useCallback } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card';
import { OverlayHybrid } from '@/components/ui/overlays';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useDashboardSearchStore } from '@/stores/useDashboardSearchStore';

import { AddToLibraryModal } from './AddToLibraryModal';
import { TavoloZone } from './TavoloZone';
import { useDashboardMode } from './useDashboardMode';
import {
  HeroZone,
  SessionBar,
  ScoreboardZone,
  ActiveSessionZone,
  GameNightZone,
  AgentZone,
  StatsZone,
  FeedZone,
  SuggestedZone,
} from './zones';
import './dashboard-transitions.css';

// ─── Skeleton fallback ────────────────────────────────────────────────────────

function ZoneSkeleton({ testId }: { testId: string }) {
  return <div data-testid={testId} className="animate-pulse rounded-2xl bg-muted h-32 w-full" />;
}

// ─── Exploration mode with zone components ────────────────────────────────────

function ExplorationView() {
  const { data: activeData } = useActiveSessions(5);
  const hasActiveSessions = (activeData?.sessions?.length ?? 0) > 0;

  return (
    <>
      <Suspense fallback={<ZoneSkeleton testId="hero-skeleton" />}>
        <HeroZone />
      </Suspense>

      <TavoloZone isEmpty={!hasActiveSessions}>
        <div className="space-y-4 md:flex md:gap-4 md:space-y-0">
          <div className="flex-1">
            <ActiveSessionZone />
          </div>
          <div className="flex-1">
            <GameNightZone />
          </div>
        </div>
      </TavoloZone>

      <AgentZone />
      <StatsZone />

      <div className="md:flex md:gap-6">
        <FeedZone />
        <SuggestedZone />
      </div>
    </>
  );
}

// ─── DashboardRenderer ────────────────────────────────────────────────────────

/**
 * Main dashboard layout renderer.
 *
 * Reads the current mode from `useDashboardMode()` and renders
 * either the zone-based exploration view or game-mode zones with
 * animated transitions via framer-motion `AnimatePresence`.
 */
export function DashboardRenderer() {
  const { state, isGameMode, isExploration } = useDashboardMode();
  const { selectedGame, setSelectedGame, openChatDrawer, drawerState, closeChatDrawer } =
    useDashboardSearchStore();

  const handleModalSuccess = useCallback(
    ({
      gameId,
      threadId,
      agentId,
      gameName,
    }: {
      gameId: string;
      threadId: string;
      agentId: string;
      gameName: string;
    }) => {
      setSelectedGame(null);
      openChatDrawer({ threadId, agentId, gameId, gameName });
    },
    [setSelectedGame, openChatDrawer]
  );

  return (
    <div data-testid="dashboard-renderer" className="env-hub flex flex-col gap-6 w-full p-4 md:p-6">
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
            <ExplorationView />
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
            <Suspense fallback={<ZoneSkeleton testId="session-bar-skeleton" />}>
              <SessionBar />
            </Suspense>
            <Suspense fallback={<ZoneSkeleton testId="scoreboard-skeleton" />}>
              <ScoreboardZone />
            </Suspense>
          </motion.div>
        )}
      </AnimatePresence>

      <OverlayHybrid enableDeepLink>
        {({ entityType, entityId }) => (
          <div className="p-4">
            <p className="text-sm text-muted-foreground">
              {entityType}: {entityId}
            </p>
          </div>
        )}
      </OverlayHybrid>

      <AddToLibraryModal
        game={selectedGame}
        isOpen={selectedGame !== null}
        onClose={() => setSelectedGame(null)}
        onSuccess={handleModalSuccess}
      />

      {drawerState && (
        <ExtraMeepleCardDrawer
          entityType="chatSession"
          entityId={drawerState.threadId}
          open={true}
          onClose={closeChatDrawer}
          liveChatData={drawerState}
          data-testid="dashboard-chat-drawer"
        />
      )}
    </div>
  );
}

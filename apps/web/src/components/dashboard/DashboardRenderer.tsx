'use client';

import { useCallback } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card';
import { OverlayHybrid } from '@/components/ui/overlays';
import { useDashboardSearchStore } from '@/stores/useDashboardSearchStore';

import { AddToLibraryModal } from './AddToLibraryModal';
import { ExplorationView } from './exploration/ExplorationView';
import { SessionSheet } from './sheet/SessionSheet';
import { SheetBreadcrumb } from './sheet/SheetBreadcrumb';
import { SheetContent } from './sheet/SheetContent';
import { TavoloView } from './tavolo/TavoloView';
import { useDashboardMode } from './useDashboardMode';
import './dashboard-transitions.css';

// ─── DashboardRenderer ────────────────────────────────────────────────────────

/**
 * Main dashboard layout renderer.
 *
 * Reads the current mode from `useDashboardMode()` and renders
 * either the zone-based exploration view or game-mode layout with
 * animated transitions via framer-motion `AnimatePresence`.
 */
export function DashboardRenderer() {
  const {
    state,
    isGameMode,
    isExploration,
    activeSessionId,
    activeSheet,
    breadcrumb,
    closeSheet,
    backCardLink,
    send,
  } = useDashboardMode();

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
            <TavoloView sessionId={activeSessionId} />
            <SessionSheet isOpen={activeSheet !== null} onClose={closeSheet}>
              <SheetBreadcrumb
                entries={breadcrumb}
                onNavigate={i => {
                  const stepsBack = breadcrumb.length - 1 - i;
                  for (let s = 0; s < stepsBack; s++) backCardLink();
                }}
              />
              {activeSheet && (
                <SheetContent context={activeSheet} sessionId={activeSessionId ?? ''} />
              )}
            </SessionSheet>
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

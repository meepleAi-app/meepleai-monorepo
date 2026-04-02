/**
 * Live Session Layout — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15 / Task 17
 * Game Session Flow v2.0 — Tasks 9, 11
 *
 * Wraps live session pages with session-specific MiniNav tabs:
 * Partita · Chat AI · Punteggi · Foto · Giocatori
 *
 * Includes a back button to return to the dashboard/play tab.
 * Context bar callbacks are wired to the overlay store (Task 17).
 *
 * Also provides:
 * - useSyncWorker: offline queue processing
 * - OfflineBanner: shown when offline or queue is non-empty
 * - TurnStateHeader (desktop only): advance turn/phase controls
 */

'use client';

import { type ReactNode, use, useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { OfflineBanner } from '@/components/session/live/OfflineBanner';
import { SessionNavConfig } from '@/components/session/live/SessionNavConfig';
import { TurnStateHeader } from '@/components/session/live/TurnStateHeader';
import { LiveSessionContextBarConnected } from '@/components/session/LiveSessionContextBarConnected';
import { OverlayHybrid } from '@/components/ui/overlays';
import { api } from '@/lib/api';
import { useSyncWorker } from '@/lib/domain-hooks/useSyncWorker';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useSyncQueueStore } from '@/lib/stores/sync-queue-store';

interface LiveSessionLayoutProps {
  children: ReactNode;
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionLayout({ children, params }: LiveSessionLayoutProps) {
  const { sessionId } = use(params);

  // Offline sync worker — processes queued operations when back online
  useSyncWorker();

  // Live session state (from SignalR-driven store)
  const status = useLiveSessionStore(s => s.status);
  const currentTurn = useLiveSessionStore(s => s.currentTurn);
  const currentPhase = useLiveSessionStore(s => s.currentPhase);
  const players = useLiveSessionStore(s => s.players);

  // Sync queue — for offline fallback
  const enqueue = useSyncQueueStore(s => s.enqueue);

  // Phase data (desktop TurnStateHeader)
  const { data: phases } = useQuery({
    queryKey: ['session-phases', sessionId],
    queryFn: () => api.liveSessions.getPhases(sessionId),
    enabled: !!sessionId,
    refetchInterval: 15_000,
  });

  // Active player name derived from players list (first player as safe fallback;
  // isOnline is a network-presence flag, not a turn indicator)
  const activePlayerName = players[0]?.name ?? null;

  const handleAdvanceTurn = useCallback(async () => {
    try {
      await api.liveSessions.advanceTurn(sessionId);
    } catch {
      enqueue({ type: 'advanceTurn', sessionId, payload: {} });
    }
  }, [sessionId, enqueue]);

  const handleAdvancePhase = useCallback(async () => {
    try {
      await api.liveSessions.advancePhase(sessionId);
    } catch {
      enqueue({ type: 'advancePhase', sessionId, payload: {} });
    }
  }, [sessionId, enqueue]);

  return (
    <>
      <ContextBarRegistrar alwaysVisible>
        <LiveSessionContextBarConnected sessionId={sessionId} />
      </ContextBarRegistrar>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/40">
        <Link
          href="/library"
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Torna alla dashboard"
        >
          <ArrowLeft className="w-4 h-4" />
          <span>Torna al Play</span>
        </Link>
      </div>

      {/* Offline / sync banner — all pages, all viewports */}
      <OfflineBanner />

      {/* Turn state header — desktop only (mobile handled inside PlayModeMobile) */}
      <div className="hidden lg:block">
        <TurnStateHeader
          currentTurn={currentTurn}
          currentPhase={currentPhase}
          phaseCount={phases?.totalPhases ?? 0}
          currentPhaseIndex={phases?.currentPhaseIndex ?? 0}
          activePlayerName={activePlayerName}
          canAdvanceTurn={status === 'InProgress'}
          canAdvancePhase={(phases?.hasPhases ?? false) && status === 'InProgress'}
          onAdvanceTurn={handleAdvanceTurn}
          onAdvancePhase={handleAdvancePhase}
        />
      </div>

      <SessionNavConfig sessionId={sessionId} />
      {children}

      <OverlayHybrid enableDeepLink>
        {({ entityType, entityId }) => (
          <div className="p-4">
            <p className="text-sm text-muted-foreground">
              {entityType}: {entityId}
            </p>
          </div>
        )}
      </OverlayHybrid>
    </>
  );
}

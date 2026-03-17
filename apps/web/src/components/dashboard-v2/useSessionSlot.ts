'use client';

import { useMemo } from 'react';

import { useGame } from '@/hooks/queries/useGames';
import { useSessionStore } from '@/lib/stores/sessionStore';

import { useDashboardMode } from './useDashboardMode';

// ============================================================================
// Types
// ============================================================================

export interface SessionSlotPlayer {
  name: string;
  score: number;
}

export interface SessionSlotData {
  isVisible: boolean;
  gameName: string;
  gameImageUrl?: string;
  sessionId: string;
  duration: number;
  players: SessionSlotPlayer[];
  sessionStatus: 'inProgress' | 'paused' | 'completed';
  gameId: string;
}

// ============================================================================
// Status mapping
// ============================================================================

function mapStatus(status: string): SessionSlotData['sessionStatus'] {
  switch (status) {
    case 'Paused':
      return 'paused';
    case 'Completed':
      return 'completed';
    default:
      return 'inProgress';
  }
}

// ============================================================================
// Duration calculation
// ============================================================================

function computeDurationMinutes(startedAt: string | null): number {
  if (!startedAt) return 0;
  const elapsed = Date.now() - new Date(startedAt).getTime();
  return Math.max(0, Math.floor(elapsed / 60_000));
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Bridge hook that combines dashboard mode, session store, and game query
 * into the data shape needed by SessionPanel / SessionPanelCollapsed.
 */
export function useSessionSlot(): SessionSlotData {
  const { isGameMode, activeSessionId } = useDashboardMode();
  const activeSession = useSessionStore(s => s.activeSession);

  const gameId = activeSession?.gameId ?? undefined;
  const { data: gameData } = useGame(gameId ?? '', !!gameId);

  return useMemo(() => {
    if (!isGameMode || !activeSession) {
      return {
        isVisible: false,
        gameName: '',
        sessionId: '',
        duration: 0,
        players: [],
        sessionStatus: 'inProgress' as const,
        gameId: '',
      };
    }

    const top3 = [...activeSession.players]
      .sort((a, b) => a.currentRank - b.currentRank)
      .slice(0, 3)
      .map(p => ({ name: p.displayName, score: p.totalScore }));

    return {
      isVisible: true,
      gameName: gameData?.title ?? activeSession.gameName,
      gameImageUrl: gameData?.imageUrl ?? undefined,
      sessionId: activeSession.id,
      duration: computeDurationMinutes(activeSession.startedAt),
      players: top3,
      sessionStatus: mapStatus(activeSession.status),
      gameId: activeSession.gameId ?? '',
    };
  }, [isGameMode, activeSession, gameData]);
}

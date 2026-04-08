'use client';

import { useCallback } from 'react';

import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';
import { useGameNightStore } from '@/stores/game-night/store';
import type { GameNightActiveSession } from '@/stores/game-night/types';

export function useGameNightMultiSession(gameNightId: string) {
  const activeSessions = useGameNightStore(s => s.activeSessions);
  const setActiveSessions = useGameNightStore(s => s.setActiveSessions);
  const addDiaryEntry = useGameNightStore(s => s.addDiaryEntry);

  const startNextGame = useCallback(
    async (gameId: string, gameTitle: string) => {
      const data = await gameNightSessionClient.startSession(gameNightId, gameId, gameTitle);

      const newSession: GameNightActiveSession = {
        id: data.sessionId,
        gameNightSessionId: data.gameNightSessionId,
        gameId,
        gameTitle,
        playOrder: data.playOrder,
        status: 'in_progress',
        startedAt: new Date().toISOString(),
      };

      // Read latest state atomically to avoid stale closure
      const current = useGameNightStore.getState().activeSessions;
      setActiveSessions([...current, newSession]);

      addDiaryEntry({
        id: crypto.randomUUID(),
        sessionId: data.sessionId,
        gameNightId,
        eventType: 'game_started',
        description: `🎲 ${gameTitle}: partita iniziata`,
        timestamp: new Date().toISOString(),
      });

      return data;
    },
    [gameNightId, setActiveSessions, addDiaryEntry]
  );

  const completeCurrentGame = useCallback(
    async (winnerId?: string, winnerName?: string) => {
      await gameNightSessionClient.completeSession(gameNightId, winnerId);

      const current = useGameNightStore.getState().activeSessions;
      const inProgressSession = current.find(s => s.status === 'in_progress');
      const gameTitle = inProgressSession?.gameTitle ?? 'Gioco';

      const updated = current.map(s =>
        s.status === 'in_progress'
          ? { ...s, status: 'completed' as const, winnerId, completedAt: new Date().toISOString() }
          : s
      );
      setActiveSessions(updated);

      addDiaryEntry({
        id: crypto.randomUUID(),
        sessionId: inProgressSession?.id ?? '',
        gameNightId,
        eventType: 'game_completed',
        description: `🏆 ${gameTitle}: ${winnerName ? `${winnerName} vince!` : 'completato'}`,
        timestamp: new Date().toISOString(),
      });
    },
    [gameNightId, setActiveSessions, addDiaryEntry]
  );

  const finalizeNight = useCallback(async () => {
    await gameNightSessionClient.finalizeNight(gameNightId);

    const current = useGameNightStore.getState().activeSessions;
    addDiaryEntry({
      id: crypto.randomUUID(),
      sessionId: current[current.length - 1]?.id ?? '',
      gameNightId,
      eventType: 'night_finalized',
      description: '📊 Serata completata!',
      timestamp: new Date().toISOString(),
    });
  }, [gameNightId, addDiaryEntry]);

  return {
    activeSessions,
    currentSession: activeSessions.find(s => s.status === 'in_progress'),
    completedCount: activeSessions.filter(s => s.status === 'completed').length,
    startNextGame,
    completeCurrentGame,
    finalizeNight,
  };
}

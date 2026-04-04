'use client';
import { useCallback, useEffect, useRef } from 'react';

import { rollDice, updateScore } from '@/lib/api/clients/gameSessionsClient';
import { queueOperation, syncPendingOperations } from '@/lib/offline/toolkitDb';

export function useOfflineToolkit(sessionId: string | null) {
  // SSR-safe: navigator doesn't exist on the server in Next.js App Router
  const isOnlineRef = useRef(typeof navigator !== 'undefined' ? navigator.onLine : true);
  const isSyncingRef = useRef(false);
  const isMountedRef = useRef(true);

  useEffect(() => {
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    const handleOnline = async () => {
      isOnlineRef.current = true;
      if (!sessionId || isSyncingRef.current) return;
      isSyncingRef.current = true;
      try {
        await syncPendingOperations(sessionId, async op => {
          switch (op.type) {
            case 'dice_roll':
              await rollDice(sessionId, op.payload.diceType as string, op.payload.count as number);
              break;
            case 'score_update':
              await updateScore(
                sessionId,
                op.payload.playerId as string,
                op.payload.score as number
              );
              break;
          }
        });
      } finally {
        isSyncingRef.current = false;
      }
    };

    const handleOffline = () => {
      isOnlineRef.current = false;
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [sessionId]);

  const rollDiceOfflineAware = useCallback(
    async (diceType: string, count = 1): Promise<number[]> => {
      // Local result always available immediately — no async dependency on the server
      const sides = parseInt(diceType.replace(/[Dd]/, ''), 10) || 6;
      const results = Array.from({ length: count }, () => Math.floor(Math.random() * sides) + 1);

      if (sessionId) {
        if (isOnlineRef.current) {
          // Online: fire-and-forget API call; queue on failure
          rollDice(sessionId, diceType, count).catch(() => {
            if (!isMountedRef.current) return;
            void queueOperation({
              id: crypto.randomUUID(),
              sessionId,
              type: 'dice_roll',
              payload: { diceType, count },
              timestamp: new Date().toISOString(),
              synced: false,
            });
          });
        } else {
          await queueOperation({
            id: crypto.randomUUID(),
            sessionId,
            type: 'dice_roll',
            payload: { diceType, count },
            timestamp: new Date().toISOString(),
            synced: false,
          });
        }
      }

      return results;
    },
    [sessionId]
  );

  const updateScoreOfflineAware = useCallback(
    async (playerId: string, score: number): Promise<void> => {
      if (!sessionId) return;

      if (isOnlineRef.current) {
        updateScore(sessionId, playerId, score).catch(() => {
          if (!isMountedRef.current) return;
          void queueOperation({
            id: crypto.randomUUID(),
            sessionId,
            type: 'score_update',
            payload: { playerId, score },
            timestamp: new Date().toISOString(),
            synced: false,
          });
        });
      } else {
        await queueOperation({
          id: crypto.randomUUID(),
          sessionId,
          type: 'score_update',
          payload: { playerId, score },
          timestamp: new Date().toISOString(),
          synced: false,
        });
      }
    },
    [sessionId]
  );

  return {
    rollDice: rollDiceOfflineAware,
    updateScore: updateScoreOfflineAware,
  };
}

'use client';
import { useCallback } from 'react';

import { useSessionStore } from '@/stores/session/store';
import type { ActivityEvent } from '@/stores/session/types';

/**
 * Interpreta gli eventi SSE di tipo score_update e li applica allo store.
 * Si integra con useSessionSSE esistente — passa handleSseEvent come callback.
 */
export function useSessionScoreSync() {
  const updateScore = useSessionStore(s => s.updateScore);
  const addEvent = useSessionStore(s => s.addEvent);

  const handleSseEvent = useCallback(
    (event: ActivityEvent) => {
      // Sempre aggiunge all'activity feed
      addEvent(event);

      switch (event.type) {
        case 'score_update': {
          const playerId = event.playerId;
          const score = event.data?.score as number | undefined;
          if (playerId && typeof score === 'number') {
            updateScore(playerId, score);
          }
          break;
        }
      }
    },
    [updateScore, addEvent]
  );

  return { handleSseEvent };
}

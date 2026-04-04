'use client';
import { useCallback } from 'react';

import { useSessionStore } from '@/stores/session/store';
import type { ActivityEvent } from '@/stores/session/types';

/**
 * Interpreta gli eventi SSE di tipo score_update e li applica allo store.
 * Si integra con useSessionSSE esistente — passa handleSseEvent come callback.
 *
 * Note: addEvent is intentionally NOT called here. useSessionSSE already calls
 * addEvent before firing onEvent, so calling it again would write every event
 * to the activity feed twice.
 */
export function useSessionScoreSync() {
  const updateScore = useSessionStore(s => s.updateScore);

  const handleSseEvent = useCallback(
    (event: ActivityEvent) => {
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
    [updateScore]
  );

  return { handleSseEvent };
}

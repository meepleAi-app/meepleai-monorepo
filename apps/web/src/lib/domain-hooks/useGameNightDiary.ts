'use client';

import { useCallback, useEffect } from 'react';

import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';
import { useGameNightStore } from '@/stores/game-night/store';
import type { DiaryEntry } from '@/stores/game-night/types';

export function useGameNightDiary(gameNightId: string) {
  const diary = useGameNightStore(s => s.diary);
  const setDiary = useGameNightStore(s => s.setDiary);
  const addDiaryEntry = useGameNightStore(s => s.addDiaryEntry);

  useEffect(() => {
    if (!gameNightId) return;

    gameNightSessionClient
      .getDiary(gameNightId)
      .then(data => {
        if (data?.entries) {
          setDiary(
            data.entries.map(e => ({
              id: e.id,
              sessionId: e.sessionId,
              gameNightId,
              eventType: e.eventType as DiaryEntry['eventType'],
              description: e.description ?? '',
              payload: e.payload ? JSON.parse(e.payload) : undefined,
              actorId: e.actorId,
              timestamp: e.timestamp,
            }))
          );
        }
      })
      .catch(() => {
        // Silently fail on initial load — diary is not critical
      });
  }, [gameNightId, setDiary]);

  const handleSseEvent = useCallback(
    (entry: DiaryEntry) => {
      addDiaryEntry(entry);
    },
    [addDiaryEntry]
  );

  return { entries: diary, handleSseEvent };
}

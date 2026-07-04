'use client';

import { useCallback, useEffect } from 'react';

import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';
import { useGameNightStore } from '@/stores/game-night/store';
import type { DiaryEntry } from '@/stores/game-night/types';

/** #2633 C2: parse a diary payload without ever throwing — a malformed row must not vanish the diary. */
function safeParsePayload(payload: string | undefined): Record<string, unknown> | undefined {
  if (!payload) return undefined;
  try {
    const parsed: unknown = JSON.parse(payload);
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Hook for managing game night diary entries.
 *
 * Usage: The consuming component should connect handleSseEvent to useSessionSSE:
 * ```
 * const { entries, handleSseEvent } = useGameNightDiary(gameNightId);
 * useSessionSSE(sessionId, { onEvent: (evt) => {
 *   if (evt.type === 'session:gamenight') handleSseEvent(evt.data);
 * }});
 * ```
 */
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
              // #2633 C2 (panel must-fix #2): guard the parse per-row — a single malformed payload
              // must NOT throw and vanish the whole diary (it did, silently, via the outer .catch).
              payload: safeParsePayload(e.payload),
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

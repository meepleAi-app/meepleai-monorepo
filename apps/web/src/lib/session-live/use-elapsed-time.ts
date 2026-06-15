'use client';

/**
 * useElapsedTime — wire G4 LiveTopBar timer chip (Issue #2355).
 *
 * Returns elapsed milliseconds since `startedAt`, ticking every `intervalMs`
 * (default 1000ms). Returns `undefined` when `startedAt` is null/undefined
 * so the timer chip is hidden gracefully during loading or for sessions
 * without a start time.
 *
 * The interval is paused via cleanup on unmount; visibility-change isn't
 * required because LiveTopBar mounts inside the session-live shell which
 * itself remounts on tab visibility transitions.
 *
 * @see apps/web/src/lib/session-live/format-elapsed-time.ts (formatting layer)
 * @see Issue #2355 (G4 wiring), parent #2352 (G4 primitive shipped)
 */

import { useEffect, useState } from 'react';

export function useElapsedTime(
  startedAt: string | null | undefined,
  intervalMs = 1000
): number | undefined {
  const [tick, setTick] = useState<number>(() => Date.now());

  useEffect(() => {
    if (!startedAt) return;
    const id = setInterval(() => setTick(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [startedAt, intervalMs]);

  if (!startedAt) return undefined;

  const startMs = Date.parse(startedAt);
  if (Number.isNaN(startMs)) return undefined;

  return Math.max(0, tick - startMs);
}

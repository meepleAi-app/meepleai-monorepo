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
 * #4750: pass `endedAt` (the session's `completedAt`) and the value stops there — no interval
 * runs and the duration is `endedAt - startedAt` — instead of a timer that keeps climbing for a
 * concluded session. An `endedAt` that does not parse is ignored (the timer keeps ticking).
 *
 * @see apps/web/src/lib/session-live/format-elapsed-time.ts (formatting layer)
 * @see Issue #2355 (G4 wiring), parent #2352 (G4 primitive shipped)
 */

import { useEffect, useState } from 'react';

export function useElapsedTime(
  startedAt: string | null | undefined,
  intervalMs = 1000,
  endedAt?: string | null
): number | undefined {
  const [tick, setTick] = useState<number>(() => Date.now());

  const endMs = endedAt ? Date.parse(endedAt) : Number.NaN;
  const isFrozen = !Number.isNaN(endMs);

  useEffect(() => {
    if (!startedAt || isFrozen) return;
    const id = setInterval(() => setTick(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [startedAt, intervalMs, isFrozen]);

  if (!startedAt) return undefined;

  const startMs = Date.parse(startedAt);
  if (Number.isNaN(startMs)) return undefined;

  return Math.max(0, (isFrozen ? endMs : tick) - startMs);
}

'use client';

import { useEffect, useState } from 'react';

/**
 * A ticking clock hook — #2633 Slice B (LD-7).
 *
 * Returns the current `Date`, re-rendering every `intervalMs` (default 60s, which
 * matches the minute-granularity `'Hh Mm'` / `'Nm'` formats the night-live view
 * renders). Injecting this value into the pure `mapNightLiveToViewModel(dto, now)`
 * keeps the elapsed/actual times live while leaving the mapper deterministic.
 *
 * @param intervalMs tick interval in milliseconds (default 60000).
 */
export function useNow(intervalMs: number = 60_000): Date {
  const [now, setNow] = useState<Date>(() => new Date());

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);

  return now;
}

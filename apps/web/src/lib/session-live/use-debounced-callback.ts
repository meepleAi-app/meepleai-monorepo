/**
 * useDebouncedCallback — debounced callback with explicit flush().
 *
 * Issue #2430 Block B+ (T3): hoisted out of `scores/page.tsx` and extended
 * with a `flush()` method exposed via tuple return so `ScoreTabContent` can
 * invoke pending callbacks during unmount cleanup (DEC-4 flush-on-unmount).
 *
 * Semantics:
 *   - debouncedFn(...args): schedule callback after `delay` ms of silence.
 *     Subsequent calls within the window reset the timer.
 *   - flush(): invoke the pending callback immediately if any. No-op when
 *     nothing is pending. Caller may call multiple times safely.
 *   - Cleanup on unmount: timer is cleared but flush is NOT called
 *     automatically — callers opt in via the returned `flush` ref.
 */

import { useCallback, useEffect, useRef } from 'react';

export function useDebouncedCallback<TArgs extends readonly unknown[]>(
  callback: (...args: TArgs) => void,
  delay: number
): readonly [debouncedFn: (...args: TArgs) => void, flush: () => void] {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pendingArgsRef = useRef<TArgs | null>(null);
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(
    () => () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    },
    []
  );

  const debouncedFn = useCallback(
    (...args: TArgs) => {
      if (timerRef.current) clearTimeout(timerRef.current);
      pendingArgsRef.current = args;
      timerRef.current = setTimeout(() => {
        timerRef.current = null;
        const pending = pendingArgsRef.current;
        pendingArgsRef.current = null;
        if (pending) callbackRef.current(...pending);
      }, delay);
    },
    [delay]
  );

  const flush = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    const pending = pendingArgsRef.current;
    pendingArgsRef.current = null;
    if (pending) callbackRef.current(...pending);
  }, []);

  return [debouncedFn, flush] as const;
}

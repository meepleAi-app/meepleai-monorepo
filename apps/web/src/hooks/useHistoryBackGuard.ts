'use client';

import { useEffect, useRef } from 'react';

/**
 * useHistoryBackGuard — 1-level Android/browser Back guard for transient overlays.
 *
 * When `open` is true it pushes exactly one history entry so the hardware/gesture
 * Back button (and the browser Back button) pops that entry and closes the overlay
 * via `onClose` instead of navigating away from the current route.
 *
 * On a non-Back close (ESC / backdrop / programmatic — `open` flips to false while
 * the guard entry is still on the stack) the pushed entry is reconciled with
 * `history.back()` so the history stack stays balanced (no phantom entry). The
 * popstate listener is removed *before* that reconciling pop, so it does not
 * re-fire `onClose` (no double-close).
 *
 * Extracted from the inline pattern in `OverlayHybrid.tsx` (gap A-03, issue #3197).
 *
 * @param open    whether the guarded overlay is currently open
 * @param onClose called when the user dismisses the overlay via Back/popstate
 */
export function useHistoryBackGuard(open: boolean, onClose: () => void): void {
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (typeof window === 'undefined' || !open) return;

    // Push exactly one guard entry per open transition (deps: [open]).
    let poppedByBack = false;
    window.history.pushState({ backGuard: true }, '');

    const handlePopState = () => {
      poppedByBack = true;
      onCloseRef.current();
    };
    window.addEventListener('popstate', handlePopState);

    return () => {
      window.removeEventListener('popstate', handlePopState);
      // Non-Back close: the guard entry is still on the stack — pop it to keep
      // history balanced. The listener is already gone, so this reconciling pop
      // does not re-trigger onClose.
      if (!poppedByBack) {
        window.history.back();
      }
    };
  }, [open]);
}

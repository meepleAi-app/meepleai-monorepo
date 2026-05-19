'use client';

import { useCallback, useState } from 'react';

export const SOFT_WARNING_STORAGE_KEY = 'gamebook.soft-warning.dismissed-at';

const SOFT_THRESHOLD = 0.9;

/**
 * Hook that decides whether to render the SoftWarningCredits component
 * and exposes a dismiss callback.
 *
 * - shouldShow = true ⇔ 0.9 <= used/total < 1.0 AND no prior dismissal in
 *   the current browser session.
 * - dismiss() writes an ISO timestamp to sessionStorage; subsequent mounts
 *   in the same session will return shouldShow=false.
 * - Cross-session: opening a new tab resets dismissal (sessionStorage, not local).
 * - Defensive: returns shouldShow=false when total <= 0.
 *
 * Must be invoked from a client component ('use client').
 */
export function useSoftWarningDismissal(
  used: number,
  total: number
): { readonly shouldShow: boolean; readonly dismiss: () => void } {
  // Lazy initializer so the sessionStorage read happens only on the client.
  // During SSR the arrow function never runs and `dismissed` defaults to false,
  // preventing the hydration mismatch that would occur if a prior tab session
  // had stored a dismissal timestamp.
  const [dismissed, setDismissed] = useState<boolean>(
    () =>
      typeof sessionStorage !== 'undefined' &&
      sessionStorage.getItem(SOFT_WARNING_STORAGE_KEY) !== null
  );

  const dismiss = useCallback(() => {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(SOFT_WARNING_STORAGE_KEY, new Date().toISOString());
    }
    setDismissed(true);
  }, []);

  if (total <= 0) {
    return { shouldShow: false, dismiss };
  }
  const ratio = used / total;
  const inSoftRange = ratio >= SOFT_THRESHOLD && ratio < 1;
  return { shouldShow: inSoftRange && !dismissed, dismiss };
}

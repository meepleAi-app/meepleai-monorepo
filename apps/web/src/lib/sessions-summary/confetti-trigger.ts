/**
 * sessionStorage-backed first-load flag for the WCAG-2.3.3-compliant confetti
 * animation on `/sessions/[id]` summary (Wave D.3, Phase 0.5 contract §9).
 *
 * Behaviour:
 *   - First call to `shouldShowConfetti(sessionId)` for a given id returns
 *     `true` and writes a sentinel value to `sessionStorage`.
 *   - Subsequent calls (same tab/session) return `false`. This prevents the
 *     animation re-triggering on tab switches, browser back/forward, and
 *     React StrictMode double-mounts.
 *   - SSR safe: if `window` is undefined (server render), returns `false`.
 *
 * `clearConfettiFlag(sessionId)` exists for tests and dev-tools only — it is
 * NOT wired into the production UX. Use it from `beforeEach` in unit tests
 * to reset state between cases.
 *
 * Note: the actual reduced-motion handling is the Confetti component's
 * responsibility (`@media (prefers-reduced-motion: reduce)` CSS gate). This
 * module only governs whether the animation can RUN at all; the component
 * decides whether to RENDER the falling pieces vs the static medal fallback.
 */

const CONFETTI_KEY_PREFIX = 'meeplai-d3-confetti-';

/**
 * Returns true the first time it is called for `sessionId` in the current
 * browser session, then false on every subsequent call. Always returns false
 * in SSR (no `window`).
 *
 * @param sessionId — session UUID; used as the sessionStorage key suffix
 * @returns whether the confetti animation should play this mount
 */
export function shouldShowConfetti(sessionId: string): boolean {
  if (typeof window === 'undefined') return false;
  // Defensive: if sessionStorage is disabled by the browser (private mode in
  // some browsers), reading it throws. Treat that as "do not show" (least
  // surprising — ensures no animation runs without persistence guard).
  try {
    const key = `${CONFETTI_KEY_PREFIX}${sessionId}`;
    if (window.sessionStorage.getItem(key)) return false;
    window.sessionStorage.setItem(key, '1');
    return true;
  } catch {
    return false;
  }
}

/**
 * Removes the sessionStorage flag for `sessionId`. Used by tests and
 * dev-tools to reset state between runs. SSR safe.
 */
export function clearConfettiFlag(sessionId: string): void {
  if (typeof window === 'undefined') return;
  try {
    window.sessionStorage.removeItem(`${CONFETTI_KEY_PREFIX}${sessionId}`);
  } catch {
    // Storage write threw — nothing to do, state is effectively cleared anyway.
  }
}

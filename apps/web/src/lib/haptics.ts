/**
 * Haptic Feedback System
 * Mobile UX Epic — Issue 14
 *
 * Provides tactile feedback on mobile devices using the Vibration API.
 * Falls back silently on unsupported browsers/devices.
 *
 * Usage:
 *   haptic.tap()       // Light tap (10ms) — button press, tab switch
 *   haptic.success()   // Double pulse — action completed
 *   haptic.error()     // Long buzz (200ms) — error/destructive
 *   haptic.selection() // Micro pulse (5ms) — selection change
 *
 * Notes:
 * - Respects prefers-reduced-motion (disabled when set)
 * - No-op on desktop browsers (navigator.vibrate undefined)
 * - iOS Safari does NOT support Vibration API (no-op)
 */

function canVibrate(): boolean {
  return typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function';
}

function prefersReducedMotion(): boolean {
  if (typeof window === 'undefined') return false;
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function vibrate(pattern: number | number[]): void {
  if (!canVibrate() || prefersReducedMotion()) return;
  try {
    navigator.vibrate(pattern);
  } catch {
    // Silently ignore — vibration is non-critical UX enhancement
  }
}

export const haptic = {
  /** Light tap feedback (10ms) — button press, tab switch, FAB tap */
  tap: () => vibrate(10),

  /** Success feedback — double pulse for completed actions */
  success: () => vibrate([10, 50, 10]),

  /** Error feedback — longer buzz for errors or destructive actions */
  error: () => vibrate(200),

  /** Micro selection feedback (5ms) — checkbox, toggle, picker change */
  selection: () => vibrate(5),

  /** Context change feedback — medium pulse for navigation transitions */
  contextChange: () => vibrate(15),
} as const;

/**
 * Format milliseconds elapsed into `HH:MM:SS` (or `HHH:MM:SS` for sessions ≥100h).
 *
 * Used by `LiveTopBar` (G4 — Issue #2352) to render the live elapsed-time chip
 * since the session's `startedAt`. Negative or non-finite inputs collapse to
 * `00:00:00` — defensive default for clock skew or transient loading states.
 *
 * @see docs/superpowers/specs/2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md §G4
 */
export function formatElapsedTime(ms: number): string {
  if (!Number.isFinite(ms) || ms <= 0) {
    return '00:00:00';
  }

  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  const pad = (n: number): string => String(n).padStart(2, '0');

  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

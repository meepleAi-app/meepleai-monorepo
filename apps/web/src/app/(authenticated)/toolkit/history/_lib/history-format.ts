/**
 * history-format — shared formatting helpers for /toolkit/history
 * (Issue #3010, Task 5).
 *
 * `formatDuration`, `getInitials`, and `MAX_AVATARS` were byte-identical
 * copies duplicated across `HistoryTable.tsx` (owns `MAX_AVATARS` too),
 * `HistoryCards.tsx`, `HistoryDetailModal.tsx`, and `client.tsx`
 * (`formatDuration` only). This module is the single source of truth so all
 * four call sites render identical labels — pure, no React, no i18n.
 */

/** Max number of player avatars shown before collapsing the rest into a "+N" pill. */
export const MAX_AVATARS = 3;

/** Formats a session duration in minutes as `"{h}h {m}m"`. */
export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h}h ${m}m`;
}

/** Derives up to 2 uppercase initials from a player's display name. */
export function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return (parts[0] as string).slice(0, 2).toUpperCase();
  const first = parts[0] as string;
  const last = parts[parts.length - 1] as string;
  return `${first[0]}${last[0]}`.toUpperCase();
}

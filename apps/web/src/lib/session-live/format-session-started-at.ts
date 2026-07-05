/**
 * formatSessionStartedAt — SI-4 #2635.
 *
 * Formats a live Session's `startedAt` (a UTC instant, derived from "Apri Live mode" —
 * Invariante 5, never user input) as a human date+time in the VIEWER's local timezone. Used for
 * the read-only "▶ Ora di inizio {…} · derivata" chip. Absolute (not elapsed) + local so a
 * multi-day campaign is unambiguous; the raw UTC instant is pinned, only the display is localized.
 *
 * The BE may serialize the instant without an offset (bare `DateTime`) — append `Z` so it parses
 * as UTC, not browser-local. Returns '' for an unparseable value (chip suppressed by the caller).
 */
const OFFSET_RE = /([zZ]|[+-]\d{2}:?\d{2})$/;

export function formatSessionStartedAt(
  startedAt: string,
  opts?: { readonly locale?: string; readonly timeZone?: string }
): string {
  const iso = OFFSET_RE.test(startedAt) ? startedAt : `${startedAt}Z`;
  const instant = new Date(iso);
  if (Number.isNaN(instant.getTime())) return '';

  return new Intl.DateTimeFormat(opts?.locale ?? 'it-IT', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: opts?.timeZone,
  }).format(instant);
}

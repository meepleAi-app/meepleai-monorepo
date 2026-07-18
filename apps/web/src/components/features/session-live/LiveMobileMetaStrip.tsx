/**
 * LiveMobileMetaStrip — #3146 Slice 1 (session-live mobile parity).
 *
 * A thin, always-visible meta strip rendered directly under the shared
 * LiveTopBar on the MOBILE layout (<lg). It surfaces the session-state chips
 * — turn progress, elapsed time (#11), and the read-only derived start time
 * (#14, Invariante 5) — which the LiveTopBar hides below lg to save space.
 *
 * On desktop (lg+) the chips live in the LiveTopBar itself, so this strip is
 * `lg:hidden`. Presentational only: the orchestrator (SessionLiveView) resolves
 * all labels (turn ICU plural, formatted elapsed, localized start time) and
 * passes them in. Renders nothing when it has no content (no empty chrome).
 *
 * Gate C: live/real-time meta surface, not a MeepleCard pattern.
 * Token discipline: semantic tokens only (bg-card / border-border /
 * text-muted-foreground / text-foreground), no raw color literals.
 */

import type { ReactElement } from 'react';

export interface LiveMobileMetaStripLabels {
  /** aria-label for the elapsed-time chip (required when elapsedLabel is set). */
  readonly elapsedAriaLabel?: string;
  /** aria-label for the read-only derived start-time chip. */
  readonly startedAtAriaLabel?: string;
}

export interface LiveMobileMetaStripProps {
  /** Resolved turn label (ICU plural "Turno {n}/{total}") — omit/empty to hide. */
  readonly turnLabel?: string;
  /** Pre-formatted elapsed time (HH:MM:SS) — omit to hide. */
  readonly elapsedLabel?: string;
  /** Pre-formatted read-only derived start-time ("▶ Ora di inizio … · derivata") — omit to hide. */
  readonly startedAtLabel?: string;
  readonly labels: LiveMobileMetaStripLabels;
}

export function LiveMobileMetaStrip({
  turnLabel,
  elapsedLabel,
  startedAtLabel,
  labels,
}: LiveMobileMetaStripProps): ReactElement | null {
  const hasTurn = Boolean(turnLabel);
  const hasElapsed = elapsedLabel != null;
  const hasStartedAt = Boolean(startedAtLabel);

  // No content → render nothing (avoid an empty strip on layouts/states where
  // none of the session-state chips apply).
  if (!hasTurn && !hasElapsed && !hasStartedAt) return null;

  return (
    <div
      data-slot="live-mobile-meta-strip"
      className="flex shrink-0 flex-wrap items-center gap-x-3 gap-y-1 border-b border-border/60
        bg-card/40 px-4 py-1.5 text-xs text-muted-foreground lg:hidden"
    >
      {hasTurn && (
        <span data-slot="live-mobile-meta-strip-turn" className="shrink-0 font-medium">
          {turnLabel}
        </span>
      )}

      {hasElapsed && (
        <span
          data-slot="live-mobile-meta-strip-elapsed"
          aria-label={labels.elapsedAriaLabel}
          className="shrink-0 font-mono tabular-nums text-foreground/90"
        >
          {elapsedLabel}
        </span>
      )}

      {hasStartedAt && (
        <span
          data-slot="live-mobile-meta-strip-started-at"
          aria-label={labels.startedAtAriaLabel}
          className="min-w-0 truncate"
        >
          {startedAtLabel}
        </span>
      )}
    </div>
  );
}

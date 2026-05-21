/**
 * LiveTopBar — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Sticky header for the /sessions/[id]/live view.
 * Shows session name, status, turn info, and role-based CTAs.
 *
 * Gate C: DIVERGES from MeepleCard — live/real-time UI, not a card pattern.
 *
 * Foundation mode: onPause/onResume/onEndgame absent (aria-disabled).
 * Interactions sub-PR wires actual handlers.
 *
 * data-slot="session-live-top-bar" — required by unit tests.
 * data-viewer-role={viewerRole} — role variant assertion in unit tests.
 * data-status={status} — status assertion in unit tests.
 */

import type { ReactElement } from 'react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface LiveTopBarLabels {
  /** Pre-resolved by orchestrator (Gate A). */
  readonly sessionTitleAriaLabel: string;
  /** Pre-resolved ICU plural from orchestrator (Gate A: "Turno {n}/{total}"). */
  readonly turnLabelResolved: string;
  readonly statusInProgress: string;
  readonly statusPaused: string;
  readonly pauseCta: string;
  readonly resumeCta: string;
  readonly endgameCta: string;
  readonly exitAriaLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface LiveTopBarProps {
  readonly sessionName: string;
  readonly status: 'InProgress' | 'Paused';
  readonly viewerRole: ParticipantRole;
  readonly onPause?: () => void;
  readonly onResume?: () => void;
  readonly onEndgame?: () => void;
  readonly onExit: () => void;
  readonly labels: LiveTopBarLabels;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function LiveTopBar({
  sessionName,
  status,
  viewerRole,
  onPause,
  onResume,
  onEndgame,
  onExit,
  labels,
}: LiveTopBarProps): ReactElement {
  const isHost = viewerRole === 'Host';
  const isPaused = status === 'Paused';

  const statusLabel = isPaused ? labels.statusPaused : labels.statusInProgress;

  return (
    <header
      data-slot="session-live-top-bar"
      data-viewer-role={viewerRole}
      data-status={status}
      aria-label={labels.sessionTitleAriaLabel}
      className="flex h-14 shrink-0 items-center justify-between gap-3 border-b border-border/60
        bg-[hsl(240,40%,12%)] px-4"
    >
      {/* Left: session name + status */}
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <span className="truncate text-sm font-semibold text-foreground" aria-hidden="true">
          {sessionName}
        </span>
        <span
          className={[
            'shrink-0 rounded-full px-2 py-0.5 text-xs font-medium',
            isPaused ? 'bg-amber-500/20 text-amber-300' : 'bg-emerald-500/20 text-emerald-300',
          ].join(' ')}
        >
          {statusLabel}
        </span>
        {labels.turnLabelResolved && (
          <span className="hidden shrink-0 text-xs text-muted-foreground sm:inline">
            {labels.turnLabelResolved}
          </span>
        )}
      </div>

      {/* Right: role-based CTAs */}
      <div className="flex items-center gap-2">
        {/* Host-only: Pause (shown when InProgress) */}
        {isHost && !isPaused && (
          <button
            type="button"
            data-slot="session-live-top-bar-pause"
            onClick={onPause}
            aria-disabled={onPause == null ? 'true' : undefined}
            className="rounded-lg border border-amber-700/60 bg-transparent px-3 py-1.5
              text-xs font-medium text-amber-300
              hover:bg-amber-500/10 focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-amber-500/50
              aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
          >
            {labels.pauseCta}
          </button>
        )}

        {/* Host-only: Resume (shown when Paused) */}
        {isHost && isPaused && (
          <button
            type="button"
            data-slot="session-live-top-bar-resume"
            onClick={onResume}
            aria-disabled={onResume == null ? 'true' : undefined}
            className="rounded-lg border border-emerald-700/60 bg-transparent px-3 py-1.5
              text-xs font-medium text-emerald-300
              hover:bg-emerald-500/10 focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-emerald-500/50
              aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
          >
            {labels.resumeCta}
          </button>
        )}

        {/* Host-only: End game */}
        {isHost && (
          <button
            type="button"
            data-slot="session-live-top-bar-endgame"
            onClick={onEndgame}
            aria-disabled={onEndgame == null ? 'true' : undefined}
            className="rounded-lg bg-rose-700 px-3 py-1.5 text-xs font-medium text-white
              hover:bg-rose-600 focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-rose-400
              aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
          >
            {labels.endgameCta}
          </button>
        )}

        {/* Exit (all roles) */}
        <button
          type="button"
          data-slot="session-live-top-bar-exit"
          onClick={onExit}
          aria-label={labels.exitAriaLabel}
          className="rounded-lg border border-border bg-transparent p-1.5 text-muted-foreground
            hover:bg-card hover:text-foreground focus-visible:outline-none
            focus-visible:ring-2 focus-visible:ring-ring"
        >
          {/* X icon */}
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M18 6 6 18" />
            <path d="m6 6 12 12" />
          </svg>
        </button>
      </div>
    </header>
  );
}

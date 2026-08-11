'use client';

/**
 * ClaimFeedbackControls — the per-claim 👍 / 👎 feedback row (ME-M3.1, Issue #533).
 *
 * Additive under each claim on the public mechanic card. Two labelled, keyboard-
 * operable buttons:
 *   - 👍 "Helpful"  → submits a positive vote immediately (parent owns the mutation).
 *   - 👎 "Report a problem" → asks the parent to open the report-error modal.
 *
 * The current vote (`'up' | 'down' | undefined`) is owned by the parent
 * (MechanicCardView keeps a single card-scoped claimId→vote map). This component
 * is purely presentational + dispatches intents; it renders a subtle confirmation
 * once a vote is recorded and reflects the active state on the pressed button.
 *
 * The whole row is `print:hidden` — a printed reference card shouldn't show
 * interactive controls.
 */

import { type ReactElement } from 'react';

import { ThumbsDown, ThumbsUp } from 'lucide-react';

import { cn } from '@/lib/utils';

export type ClaimVote = 'up' | 'down';

export interface ClaimFeedbackControlsProps {
  /** The claim this row belongs to (used for stable test ids + labels). */
  readonly claimId: string;
  /** The current recorded vote for this claim, if any. */
  readonly vote: ClaimVote | undefined;
  /** True while a submission for THIS claim is in flight — disables the buttons. */
  readonly isPending: boolean;
  /** Record a 👍 (submit immediately). */
  readonly onThumbUp: () => void;
  /** Request the report-error modal for a 👎. */
  readonly onThumbDown: () => void;
}

export function ClaimFeedbackControls({
  claimId,
  vote,
  isPending,
  onThumbUp,
  onThumbDown,
}: ClaimFeedbackControlsProps): ReactElement {
  const upActive = vote === 'up';
  const downActive = vote === 'down';

  return (
    <div
      data-slot="mechanic-card-claim-feedback"
      className="mt-1 flex items-center gap-2 print:hidden"
    >
      <span className="sr-only">Was this claim helpful?</span>

      <button
        type="button"
        onClick={onThumbUp}
        disabled={isPending}
        aria-pressed={upActive}
        aria-label="Helpful"
        data-testid={`mechanic-card-thumb-up-${claimId}`}
        className={cn(
          'inline-flex h-8 w-8 items-center justify-center rounded-full border transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50',
          upActive
            ? 'border-entity-game/40 bg-entity-game/15 text-entity-game'
            : 'border-border bg-transparent text-muted-foreground hover:bg-muted hover:text-foreground'
        )}
      >
        <ThumbsUp className="h-4 w-4" aria-hidden="true" />
      </button>

      <button
        type="button"
        onClick={onThumbDown}
        disabled={isPending}
        aria-pressed={downActive}
        aria-label="Report a problem"
        data-testid={`mechanic-card-thumb-down-${claimId}`}
        className={cn(
          'inline-flex h-8 w-8 items-center justify-center rounded-full border transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50',
          downActive
            ? 'border-destructive/40 bg-destructive/10 text-destructive'
            : 'border-border bg-transparent text-muted-foreground hover:bg-muted hover:text-foreground'
        )}
      >
        <ThumbsDown className="h-4 w-4" aria-hidden="true" />
      </button>

      {/* Confirmation copy — announced politely once a vote lands. */}
      {vote !== undefined && (
        <span
          role="status"
          data-testid={`mechanic-card-feedback-thanks-${claimId}`}
          className="text-xs font-medium text-muted-foreground"
        >
          {upActive ? 'Thanks!' : 'Thanks — report received'}
        </span>
      )}
    </div>
  );
}

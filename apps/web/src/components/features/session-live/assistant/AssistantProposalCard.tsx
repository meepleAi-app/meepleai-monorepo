'use client';

/**
 * AssistantProposalCard — one proposal of the in-game assistant (#4238 A4, spec 2026-10-11 §5.3).
 *
 * Derived from `ScoreProposalCard`: the assistant proposes, the host confirms, corrects or rejects.
 * Only a `Pending` card acts. Every other status is shown with its reason, because a card that
 * looks confirmable but is not would send the host to record the score twice.
 *
 * `confirmBlocked` is the one-at-a-time rule from the review of #4301: while another card is
 * being confirmed, this one cannot start a confirmation.
 */

import { useId, useState, type FormEvent, type JSX } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import {
  ASSISTANT_PROPOSAL_KIND,
  ASSISTANT_PROPOSAL_STATUS,
  type AssistantProposalDto,
} from '@/lib/api/schemas/live-sessions.schemas';

/** Mirrors the backend limit of a proposal and of the host's correction (spec §5.1). */
const MAX_POINTS = 100_000;

const KNOWN_STATUSES = new Set<string>(Object.values(ASSISTANT_PROPOSAL_STATUS));
const KNOWN_FAILURES = new Set([
  'session_state_invalid',
  'proposal_not_applicable',
  'session_state_unavailable',
]);
const KNOWN_ERRORS = new Set([
  'proposal_stale',
  'proposal_not_pending',
  'session_state_invalid',
  'proposal_not_applicable',
]);

export interface AssistantProposalCardProps {
  readonly proposal: AssistantProposalDto;
  /** Another card is being confirmed: this one cannot start a confirmation. */
  readonly confirmBlocked: boolean;
  readonly confirming: boolean;
  readonly rejecting: boolean;
  /** The code of the last confirm or reject of this card that failed, if any. */
  readonly errorCode: string | null;
  readonly onConfirm: (points?: number) => void;
  readonly onReject: () => void;
}

function signed(points: number): string {
  return points > 0 ? `+${points}` : `${points}`;
}

export function AssistantProposalCard({
  proposal,
  confirmBlocked,
  confirming,
  rejecting,
  errorCode,
  onConfirm,
  onReject,
}: AssistantProposalCardProps): JSX.Element {
  const { t } = useTranslation();
  const inputId = useId();
  const [correcting, setCorrecting] = useState(false);
  const [draft, setDraft] = useState('');

  const isScore = proposal.kind === ASSISTANT_PROPOSAL_KIND.score;
  const isPending = proposal.status === ASSISTANT_PROPOSAL_STATUS.pending;
  const busy = confirming || rejecting;
  const canConfirm = isPending && !busy && !confirmBlocked;

  const points = proposal.confirmedPoints ?? proposal.points ?? 0;
  const description = isScore
    ? t('pages.sessionLive.assistant.card.score', {
        points: signed(points),
        dimension: proposal.dimension ?? '',
        player: proposal.playerName ?? '',
        round: proposal.round ?? 1,
      })
    : t('pages.sessionLive.assistant.card.advanceTurn');

  const statusKey = KNOWN_STATUSES.has(proposal.status) ? proposal.status : 'other';
  const failureKey =
    proposal.failureCode && KNOWN_FAILURES.has(proposal.failureCode)
      ? proposal.failureCode
      : 'other';
  const errorKey = errorCode && KNOWN_ERRORS.has(errorCode) ? errorCode : 'other';

  const parsedDraft = Number(draft);
  const draftValid =
    draft.trim() !== '' && Number.isInteger(parsedDraft) && Math.abs(parsedDraft) <= MAX_POINTS;

  function submitCorrection(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (!draftValid || !canConfirm) return;
    setCorrecting(false);
    onConfirm(parsedDraft);
  }

  return (
    <article
      data-slot="assistant-proposal-card"
      data-status={proposal.status}
      data-kind={proposal.kind}
      aria-busy={busy}
      className="rounded-md border border-entity-agent/40 bg-entity-agent/10 px-3 py-2 text-sm"
    >
      <div className="flex items-start justify-between gap-2">
        <p className="font-medium text-foreground">{description}</p>
        <span
          data-slot="assistant-proposal-status"
          className="shrink-0 rounded bg-background px-1.5 py-0.5 text-xs text-entity-agent-text"
        >
          {t(`pages.sessionLive.assistant.card.status.${statusKey}`)}
        </span>
      </div>

      {isScore && proposal.resultingValue != null ? (
        <p className="mt-1 text-xs text-muted-foreground">
          {t('pages.sessionLive.assistant.card.resulting', { value: proposal.resultingValue })}
        </p>
      ) : null}

      {proposal.status === ASSISTANT_PROPOSAL_STATUS.failed ? (
        <p data-slot="assistant-proposal-failure" className="mt-1 text-xs text-muted-foreground">
          {t(`pages.sessionLive.assistant.card.failure.${failureKey}`)}
        </p>
      ) : null}

      {errorCode !== null ? (
        <p
          role="alert"
          data-slot="assistant-proposal-error"
          className="mt-1 text-xs text-destructive"
        >
          {t(`pages.sessionLive.assistant.card.error.${errorKey}`)}
        </p>
      ) : null}

      {isPending && correcting ? (
        <form onSubmit={submitCorrection} className="mt-2 flex flex-wrap items-end gap-2">
          <label htmlFor={inputId} className="text-xs text-muted-foreground">
            {t('pages.sessionLive.assistant.card.correctLabel')}
          </label>
          <input
            id={inputId}
            type="number"
            inputMode="numeric"
            step={1}
            min={-MAX_POINTS}
            max={MAX_POINTS}
            value={draft}
            onChange={event => setDraft(event.target.value)}
            className="h-8 w-24 rounded-md border border-border bg-background px-2 text-sm text-foreground"
          />
          <Button type="submit" size="sm" disabled={!draftValid || !canConfirm}>
            {t('pages.sessionLive.assistant.card.applyCorrection')}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setCorrecting(false)}>
            {t('pages.sessionLive.assistant.card.cancel')}
          </Button>
        </form>
      ) : null}

      {isPending && !correcting ? (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <Button
            type="button"
            size="sm"
            data-slot="assistant-proposal-confirm"
            disabled={!canConfirm}
            onClick={() => onConfirm()}
          >
            {t('pages.sessionLive.assistant.card.confirm')}
          </Button>
          {isScore ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              data-slot="assistant-proposal-correct"
              disabled={!canConfirm}
              onClick={() => {
                setDraft(String(points));
                setCorrecting(true);
              }}
            >
              {t('pages.sessionLive.assistant.card.correct')}
            </Button>
          ) : null}
          <Button
            type="button"
            size="sm"
            variant="ghost"
            data-slot="assistant-proposal-reject"
            disabled={busy}
            onClick={onReject}
          >
            {t('pages.sessionLive.assistant.card.reject')}
          </Button>
          {confirmBlocked && !confirming ? (
            <span className="text-xs text-muted-foreground">
              {t('pages.sessionLive.assistant.card.busy')}
            </span>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

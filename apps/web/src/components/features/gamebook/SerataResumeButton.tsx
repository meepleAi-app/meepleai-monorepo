'use client';

/**
 * SerataResumeButton — SI-4 (#2635, step 3).
 *
 * Rendered on the gamebook play page under the SerataSpineStrip when the campaign's owning
 * game-night is resumable (see {@link isSerataResumable}). Opens a NEW live sitting via the Attach
 * path and routes to the freshly-opened live session. A max-1-live 409 / a 403 surface distinct
 * inline feedback instead of a silent no-op. Picker navigation (solo reading) is unchanged — this
 * is the game-night resume entry, deliberately organizer + resumable-only.
 */

import { useCallback, type ReactElement } from 'react';

import { useRouter } from 'next/navigation';

import { ConflictError, ForbiddenError } from '@/lib/api/core/errors';
import type { GamebookCampaignSpine } from '@/lib/api/gamebook-campaigns';
import { isMaxLiveBlockedError } from '@/lib/game-nights/hooks/useStartNextGame';
import { useResumeGamebookSitting } from '@/lib/gamebook/hooks/useResumeGamebookSitting';

const RESUMABLE_STATUSES = new Set(['Published', 'InProgress']);

/**
 * A campaign's game-night is "resumable" (a new live sitting can be opened from the play page) when
 * the viewer is the organizer, the night is Published/InProgress (not a terminal Completed/Cancelled
 * or an unpublished Draft), and no sitting is currently live (an existing live one → jump, not
 * resume). A Completed night is intentionally excluded: the BE rejects resume-from-Completed at the
 * command boundary (409) and the product answer is "start a new session".
 */
export function isSerataResumable(
  spine: GamebookCampaignSpine | null | undefined,
  currentUserId: string | undefined
): boolean {
  if (!spine || !currentUserId) return false;
  if (spine.organizerId !== currentUserId) return false;
  if (spine.hasLiveSession) return false;
  return RESUMABLE_STATUSES.has(spine.gameNightStatus);
}

function resumeErrorMessage(error: unknown): string {
  if (isMaxLiveBlockedError(error)) {
    return 'C’è già una partita live in questa serata.';
  }
  if (error instanceof ForbiddenError) {
    return 'Solo l’organizzatore può riprendere la serata.';
  }
  if (error instanceof ConflictError) {
    return 'Non è possibile riprendere la serata ora.';
  }
  return 'Impossibile riprendere la serata. Riprova.';
}

export interface SerataResumeButtonProps {
  readonly gameNightId: string;
  readonly campaignId: string;
}

export function SerataResumeButton({
  gameNightId,
  campaignId,
}: SerataResumeButtonProps): ReactElement {
  const router = useRouter();
  const resume = useResumeGamebookSitting(gameNightId);

  const handleClick = useCallback(() => {
    resume.mutate(
      { campaignId },
      {
        onSuccess: result => {
          // The freshly-opened session is in-progress → /sessions/{id} forks to the live view
          // (where the SI-4 startedAt chip renders).
          router.push(`/sessions/${result.sessionId}`);
        },
      }
    );
  }, [resume, campaignId, router]);

  return (
    <div className="flex flex-col gap-1">
      <button
        type="button"
        onClick={handleClick}
        disabled={resume.isPending}
        data-testid="serata-resume-button"
        className="inline-flex items-center justify-center gap-2 self-start rounded-md bg-entity-event px-4 py-2 font-display text-sm font-semibold text-white shadow-sm transition hover:bg-entity-event/90 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {resume.isPending ? 'Avvio…' : '▶ Riprendi la serata'}
      </button>
      {resume.isError ? (
        <span role="alert" className="text-xs font-medium text-[hsl(var(--c-danger))]">
          {resumeErrorMessage(resume.error)}
        </span>
      ) : null}
    </div>
  );
}

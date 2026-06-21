/**
 * ScoreTabContent — polymorphic score tab content for SessionLiveView.
 *
 * Owns ALL polymorphic scoring logic for the score tab:
 *   - Block B (read-only): store selectors + REST hydration race guard +
 *     scoringPanelData adapter memo + a11y placeholder.
 *   - Block B+ (mutable): role-based mount (Host=editor, others=renderer),
 *     debounced useUpdateSessionScores wire, optimistic local override,
 *     5-kind error mapper, retry button, 30s rate-limit countdown
 *     persisted in useLiveSessionStore.
 *
 * Issue #2430 Block B+.
 */

'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from 'react';

import { toast } from 'sonner';

import {
  ScoringPanelRenderer,
  type ScoringPanelData,
  type ScoringPanelRendererLabels,
} from '@/components/features/session-live';
import { PolymorphicScoreEditor } from '@/components/sessions';
import type { ScoreChangePayload } from '@/components/sessions/PolymorphicScoreEditor';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import {
  UpdateSessionScoresError,
  useUpdateSessionScores,
  type UpdateSessionScoresPayload,
} from '@/hooks/use-update-session-scores';
import { useGameObjectivesCatalogue } from '@/hooks/useGameObjectivesCatalogue';
import { useTranslation } from '@/hooks/useTranslation';
import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const RATE_LIMIT_WINDOW_MS = 30_000;

// ─── Local normalized error type ─────────────────────────────────────────────

type ScoredErrorKind = 'forbidden' | 'rate-limited' | 'validation' | 'server' | 'network';

interface ScoredError {
  readonly kind: ScoredErrorKind;
  readonly status: number;
  readonly message: string;
  readonly details?: unknown;
}

function mapMutationError(err: unknown): ScoredError {
  if (err instanceof UpdateSessionScoresError) {
    if (err.status === 429) {
      return { kind: 'rate-limited', status: 429, message: err.message };
    }
    return {
      kind: err.kind,
      status: err.status,
      message: err.message,
      details: err.details,
    };
  }
  return {
    kind: 'network',
    status: 0,
    message: err instanceof Error ? err.message : 'Network error',
  };
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ScoreTabContentProps {
  readonly sessionId: string;
  readonly viewerRole: 'Host' | 'Player' | 'Spectator';
  readonly players: ReadonlyArray<{
    readonly id: string;
    readonly name: string;
    readonly displayName?: string;
  }>;
  readonly labels: ScoringPanelRendererLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ScoreTabContent(props: ScoreTabContentProps): ReactElement {
  const { sessionId, viewerRole, players, labels, className } = props;
  const { t } = useTranslation();

  // Store selectors
  const scoringType = useLiveSessionStore(s => s.scoringType);
  const scoreData = useLiveSessionStore(s => s.scoreData);
  const rateLimitedUntil = useLiveSessionStore(s => s.rateLimitedUntil);
  const setRateLimitedUntil = useLiveSessionStore(s => s.setRateLimitedUntil);

  // Mutation hook
  const mutation = useUpdateSessionScores();

  // Refs (unmount safety).
  // isMountedRef alone is sufficient to guard against post-unmount setState;
  // the host-transfer mid-mutation case is covered by the flush effect's
  // [viewerRole, flush] deps which fires cleanup on role change.
  // The retry button captures the payload via closure (handleMutationError's
  // second arg), so no lastPayloadRef is needed.
  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // Optimistic local override (cleared on success/error)
  const [localScoreOverride, setLocalScoreOverride] = useState<ScoreDataByType[ScoreType] | null>(
    null
  );

  // Tick state for 429 countdown
  const [, setTick] = useState(0);
  useEffect(() => {
    if (rateLimitedUntil == null) return;
    const intervalId = setInterval(() => {
      setTick(n => n + 1);
      if (Date.now() >= rateLimitedUntil) {
        setRateLimitedUntil(null);
      }
    }, 1000);
    return () => clearInterval(intervalId);
  }, [rateLimitedUntil, setRateLimitedUntil]);

  const isRateLimited = rateLimitedUntil != null && Date.now() < rateLimitedUntil;
  const rateLimitRemainingSec =
    isRateLimited && rateLimitedUntil != null
      ? Math.max(0, Math.ceil((rateLimitedUntil - Date.now()) / 1000))
      : 0;

  // Effective score data: localOverride wins during pending debounce
  const effectiveScoreData = localScoreOverride ?? scoreData;

  // #2432: catalogue lookup centralised at the hook so a future per-game
  // BE endpoint becomes an internal swap. The store does not yet carry
  // `gameId` (#1899-followup, same TODO that gates `scoringType` wiring),
  // so we pass null today and let the hook resolve to the default catalogue.
  const availableObjectives = useGameObjectivesCatalogue(null);

  // Renderer data (Block B path, used for non-Host roles + Host's null fallback)
  const scoringPanelData = useMemo<ScoringPanelData | null>(
    () =>
      mapScoreDataToPanelData(scoringType, effectiveScoreData, players, {
        availableObjectives,
      }),
    [scoringType, effectiveScoreData, players, availableObjectives]
  );

  // Error handler — normalized via mapMutationError
  const handleMutationError = useCallback(
    (err: unknown, payload: UpdateSessionScoresPayload | null) => {
      if (!isMountedRef.current) return;
      const scored = mapMutationError(err);
      setLocalScoreOverride(null); // rollback to store
      switch (scored.kind) {
        case 'forbidden':
          toast.error(t('pages.sessionLive.scoring.forbiddenToast'), {
            id: 'score-403',
          });
          break;
        case 'rate-limited': {
          const deadline = Date.now() + RATE_LIMIT_WINDOW_MS;
          setRateLimitedUntil(deadline);
          toast.warning(
            t('pages.sessionLive.scoring.rateLimitedToast', {
              seconds: RATE_LIMIT_WINDOW_MS / 1000,
            }),
            { id: 'score-429' }
          );
          break;
        }
        case 'validation': {
          toast.error(
            t('pages.sessionLive.scoring.validationFailedTemplate', {
              message: JSON.stringify(scored.details ?? scored.message),
            }),
            { id: 'score-400' }
          );
          break;
        }
        case 'server':
          toast.error(t('pages.sessionLive.scoring.serverErrorToast'), {
            id: 'score-5xx',
            action: {
              label: t('pages.sessionLive.scoring.retryCta'),
              onClick: () => {
                if (payload) {
                  mutation.mutate(payload, {
                    onError: e => handleMutationError(e, payload),
                  });
                }
              },
            },
          });
          break;
        case 'network':
          toast.error(t('pages.sessionLive.scoring.networkErrorToast'), {
            id: 'score-network',
            action: {
              label: t('pages.sessionLive.scoring.retryCta'),
              onClick: () => {
                if (payload) {
                  mutation.mutate(payload, {
                    onError: e => handleMutationError(e, payload),
                  });
                }
              },
            },
          });
          break;
      }
    },
    // `mutation.mutate` is referentially stable across renders (TanStack Query
    // guarantee), so we depend on the method, not the whole mutation object.
    // Whole `mutation` would change identity on pending/idle flips, forcing
    // handleMutationError → submitMutation → debouncedSubmit recreation
    // mid-edit and potentially dropping in-flight debounced calls.
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mutation.mutate is stable per TanStack Query v5
    [t, mutation.mutate, setRateLimitedUntil]
  );

  // Debounced mutation dispatch.
  // Hook-level useUpdateSessionScores.onSuccess invalidates queries; the
  // inline onSuccess below ADDS the local-override clear — TanStack Query v5
  // merges per-mutate callbacks with hook-level ones (both fire).
  const submitMutation = useCallback(
    (payload: UpdateSessionScoresPayload) => {
      mutation.mutate(payload, {
        onSuccess: () => {
          if (!isMountedRef.current) return;
          setLocalScoreOverride(null);
        },
        onError: err => handleMutationError(err, payload),
      });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mutation.mutate is stable per TanStack Query v5
    [mutation.mutate, handleMutationError]
  );

  const [debouncedSubmit, flush] = useDebouncedCallback(submitMutation, 500);

  // Flush-on-unmount + flush-on-role-change (DEC-4).
  // [viewerRole, flush] deps ensure cleanup fires on in-tree role transitions
  // (Host → Player without a full unmount).
  useEffect(() => {
    return () => {
      flush();
    };
  }, [viewerRole, flush]);

  // onChange handler for the editor: optimistic UI + debounced submit
  const handleScoreChange = useCallback(
    (payload: ScoreChangePayload) => {
      setLocalScoreOverride(payload.data);
      debouncedSubmit({
        sessionId,
        scoringType: payload.scoringType,
        scoreData: payload.data,
      });
    },
    [sessionId, debouncedSubmit]
  );

  // ── Render ────────────────────────────────────────────────────────────────

  const playerOptions = useMemo(
    () => players.map(p => ({ id: p.id, displayName: p.displayName ?? p.name })),
    [players]
  );

  const hostEditing = viewerRole === 'Host' && scoringType !== null;

  if (hostEditing && scoringType !== null) {
    return (
      <div className={className}>
        <PolymorphicScoreEditor
          scoringType={scoringType}
          players={playerOptions}
          initialData={effectiveScoreData ?? undefined}
          availableObjectives={availableObjectives}
          onChange={handleScoreChange}
          disabled={isRateLimited || mutation.isPending}
        />
        {isRateLimited && (
          <div
            role="status"
            aria-live="polite"
            data-slot="score-rate-limit-countdown"
            className="mt-1 text-xs text-amber-500"
          >
            {t('pages.sessionLive.scoring.rateLimitedTemplate', {
              seconds: rateLimitRemainingSec,
            })}
          </div>
        )}
      </div>
    );
  }

  if (scoringPanelData != null) {
    return <ScoringPanelRenderer data={scoringPanelData} labels={labels} className={className} />;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className={`${className ?? ''} text-xs text-muted-foreground`.trim()}
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  );
}

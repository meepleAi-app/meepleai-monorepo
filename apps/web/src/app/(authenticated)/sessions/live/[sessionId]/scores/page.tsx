/**
 * Live Session Scores Page — /sessions/live/[sessionId]/scores
 *
 * Game Night Improvvisata — Task 16.
 *
 * Asse D follow-up P1 (#1899) T6: wire `PolymorphicScoreEditor` for non-`Points`
 * scoring types and host edit mode, while keeping the legacy `ScoreBoard` as the
 * backward-compatible default for read-only `Points` sessions. The page debounces
 * the strategy-specific change payload before invoking the
 * `useUpdateSessionScores` mutation so rapid edits collapse to a single autosave.
 */

'use client';

import { use, useCallback, useEffect, useRef } from 'react';

import { AutosaveIndicator } from '@/components/session/live/AutosaveIndicator';
import { ScoreBoard } from '@/components/session/live/ScoreBoard';
import { PolymorphicScoreEditor, type ScoreChangePayload } from '@/components/sessions';
import {
  UpdateSessionScoresError,
  useUpdateSessionScores,
} from '@/hooks/use-update-session-scores';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

/**
 * Generic debounced-callback helper. Inlined because `use-debounce` is not part
 * of the workspace and the existing `use-debounce` value-hook under
 * `entity-list-view/hooks` debounces *values*, not *callbacks*.
 */
function useDebouncedCallback<TArgs extends readonly unknown[]>(
  callback: (...args: TArgs) => void,
  delay: number
): (...args: TArgs) => void {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(
    () => () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    },
    []
  );

  return useCallback(
    (...args: TArgs) => {
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => {
        callbackRef.current(...args);
      }, delay);
    },
    [delay]
  );
}

interface LiveSessionScoresPageProps {
  params: Promise<{ sessionId: string }>;
}

/**
 * MVP fallback objectives catalogue.
 *
 * The full game-catalogue wiring (Toolkit / SessionDetail) is out of scope for
 * P1; once available, replace this constant with a selector on
 * `useLiveSessionStore` (or a TanStack Query against the game detail endpoint).
 *
 * TODO(#1899-followup): wire real catalogue via live-session-store extension.
 */
const MVP_OBJECTIVES_CATALOGUE: readonly string[] = [
  'Vittoria',
  'Sopravvivenza',
  'Tesoro',
  'Boss',
  'Quest',
];

export default function LiveSessionScoresPage({ params }: LiveSessionScoresPageProps) {
  const { sessionId } = use(params);
  const players = useLiveSessionStore(s => s.players);

  /**
   * The live-session store does NOT yet expose `scoringType`. Until the asse-D
   * follow-up wiring lands the session-detail hydration extension, every live
   * session is treated as `Points` for backward compatibility with the existing
   * `ScoreBoard` UX.
   *
   * TODO(#1899-followup): replace with `useLiveSessionStore(s => s.scoringType)`
   * once the store is extended (see backlog item under epic #1895).
   */
  const scoringType = 'Points' as const;

  const isHost = players.find(p => p.isHost)?.isHost ?? false;
  const mutation = useUpdateSessionScores();

  const debouncedSave = useDebouncedCallback((payload: ScoreChangePayload) => {
    mutation.mutate({
      sessionId,
      scoringType: payload.scoringType,
      scoreData: payload.data,
    });
  }, 500);

  // Backward-compat: a non-host viewer on a `Points` session sees the legacy
  // ScoreBoard, which is read-only and preserves the original autosave UX.
  if (scoringType === 'Points' && !isHost) {
    return (
      <div className="space-y-2">
        <div className="flex justify-end px-4 pt-2">
          <AutosaveIndicator />
        </div>
        <ScoreBoard sessionId={sessionId} isHost={isHost} />
      </div>
    );
  }

  // Polymorphic edit path: host on `Points` sessions, OR any user on a non-Points
  // scoring type (BinaryWin / Objectives / Ranking).
  return (
    <div className="space-y-4 p-4" data-testid="polymorphic-scores-page">
      <div className="flex justify-end">
        <AutosaveIndicator />
      </div>
      <PolymorphicScoreEditor
        scoringType={scoringType}
        players={players.map(p => ({
          id: p.id,
          // `PlayerInfo` exposes `name` (not `displayName`); the strategy
          // primitives accept the `PlayerOption.displayName` field so we map
          // here. When the store gains explicit display-name handling this
          // adapter can be removed.
          displayName: p.name,
        }))}
        availableObjectives={MVP_OBJECTIVES_CATALOGUE}
        onChange={debouncedSave}
        disabled={mutation.isPending}
      />
      {mutation.isError && mutation.error instanceof UpdateSessionScoresError && (
        <div
          role="alert"
          className="rounded-md border border-[hsl(var(--c-danger))] p-3 text-sm text-[hsl(var(--c-danger))]"
          data-testid="scores-error-banner"
        >
          {mutation.error.kind === 'forbidden' &&
            'Non sei autorizzato a modificare gli score (solo host).'}
          {mutation.error.kind === 'validation' && 'Score data non valido.'}
          {mutation.error.kind === 'server' && 'Errore di salvataggio. Riprova.'}
        </div>
      )}
    </div>
  );
}

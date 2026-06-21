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

import { use } from 'react';

import { AutosaveIndicator } from '@/components/session/live/AutosaveIndicator';
import { ScoreBoard } from '@/components/session/live/ScoreBoard';
import { PolymorphicScoreEditor, type ScoreChangePayload } from '@/components/sessions';
import {
  UpdateSessionScoresError,
  useUpdateSessionScores,
} from '@/hooks/use-update-session-scores';
import { useGameObjectivesCatalogue } from '@/hooks/useGameObjectivesCatalogue';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

interface LiveSessionScoresPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionScoresPage({ params }: LiveSessionScoresPageProps) {
  const { sessionId } = use(params);
  const players = useLiveSessionStore(s => s.players);

  // #2432: catalogue lookup is centralised at the hook layer so the future
  // per-game endpoint swap is invisible to this page. Today the store does
  // not expose gameId here (#1899-followup, same TODO that gates scoringType
  // wiring); pass null to opt into the default catalogue until the wiring
  // lands.
  const availableObjectives = useGameObjectivesCatalogue(null);

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

  const [debouncedSave] = useDebouncedCallback((payload: ScoreChangePayload) => {
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
        <ScoreBoard sessionId={sessionId} />
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
        availableObjectives={availableObjectives}
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

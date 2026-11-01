/**
 * Create Play Record Page
 *
 * Multi-step wizard for creating new play records.
 * Issue #3892: Play Records Frontend UI
 *
 * #2348: Reads ?gameNightId= from URL and prefills the form via
 * useGameNightPrefill (date/location/game/roster from completed GameNight).
 * Next.js 16 requires useSearchParams under <Suspense> — inner content
 * component reads params; default export wraps it.
 */

'use client';

import { Suspense, useEffect, useRef, useState } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';

import { FormPageContainer } from '@/components/layout/PageContainer';
import {
  SessionCreateForm,
  type PlayerEntry,
  type SessionCreateFormProps,
} from '@/components/play-records/SessionCreateForm';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { playRecordsApi } from '@/lib/api/play-records.api';
import type { SessionCreateForm as SessionFormData } from '@/lib/api/schemas/play-records.schemas';
import { useGameNightPrefill } from '@/lib/domain-hooks/useGameNightPrefill';
import { useCreatePlayRecord } from '@/lib/domain-hooks/usePlayRecords';

/** A player's name the way the server compares it: trimmed and lowercased (`PlayRecord.AddPlayer`). */
const norm = (name: string): string => name.trim().toLowerCase();

/**
 * What a record is, for the purpose of resuming a save that stopped half way: the fields `CreatePlayRecord`
 * takes. They cannot all be corrected on the record afterwards (the game, the visibility, the scoring
 * dimensions), so a save whose inputs changed has to be a new record (#4800).
 */
function createKey(data: SessionFormData): string {
  return JSON.stringify([
    data.gameId ?? null,
    data.gameName,
    data.sessionDate.toISOString(),
    data.visibility,
    data.groupId ?? null,
    data.enableScoring ? (data.scoringDimensions ?? null) : null,
    data.enableScoring ? (data.dimensionUnits ?? null) : null,
  ]);
}

/**
 * Issue #2847 (#BB): the create wizard collected players, per-player scores and a location, but
 * `CreatePlayRecord` does not accept them, so they are persisted with the dedicated follow-up endpoints
 * (addPlayer / recordScore / update) after the record exists.
 *
 * #4800: this can stop half way, with the record already on the server, and blind repetition does not work
 * (`AddPlayer` refuses a name that is already there). So each step looks at what the server holds and does
 * only what is missing; `resumed` says the record may not be empty. A first save skips the read.
 */
async function persistFollowUps(
  recordId: string,
  data: SessionFormData,
  players: PlayerEntry[],
  resumed: boolean
): Promise<void> {
  const existing = resumed ? await playRecordsApi.getRecord(recordId) : null;

  // Persist the roster. addPlayer returns 204 (no id), so re-fetch afterwards
  // to resolve the created player ids before recording scores.
  const alreadyThere = new Set((existing?.players ?? []).map(p => norm(p.displayName)));
  // A name that is in the form twice is the server's to refuse, as it always was: counting the second one
  // as «already there» too would turn that error into a silent success on the retry.
  const taken = new Set<string>();
  for (const player of players) {
    const name = norm(player.name);
    if (alreadyThere.has(name) && !taken.has(name)) {
      taken.add(name);
      continue;
    }
    await playRecordsApi.addPlayer(recordId, { displayName: player.name });
  }

  // Per-player score → first scoring dimension (or "points"). Only when
  // scoring is enabled and a numeric value was entered.
  const scoredPlayers = players.filter(
    p => p.score.trim() !== '' && !Number.isNaN(Number(p.score))
  );
  if (data.enableScoring && scoredPlayers.length > 0) {
    const dimension = data.scoringDimensions?.[0] ?? 'points';
    const unit = data.dimensionUnits?.[dimension];
    const record = await playRecordsApi.getRecord(recordId);
    for (const player of scoredPlayers) {
      const persisted = record.players.find(rp => norm(rp.displayName) === norm(player.name));
      if (!persisted) continue;
      const value = Number(player.score);
      // Already recorded with this very value by the attempt that stopped half way: sending it again would
      // only repeat the event. A different value is a correction made since, and goes through: the server
      // replaces the score of a dimension (`RecordPlayer.RecordScore`).
      if (persisted.scores.find(score => score.dimension === dimension)?.value === value) continue;
      await playRecordsApi.recordScore(recordId, {
        playerId: persisted.id,
        dimension,
        value,
        unit,
      });
    }
  }

  // Location is not part of CreatePlayRecordRequest — persist it via update.
  const location = data.location?.trim();
  if (location && existing?.location !== location) {
    await playRecordsApi.updateRecord(recordId, { location });
  }
}

function NewPlayRecordContent() {
  const router = useRouter();
  const { t } = useTranslation();
  const createRecord = useCreatePlayRecord();
  const searchParams = useSearchParams();
  const gameNightId = searchParams.get('gameNightId');
  const { prefill, isLoading } = useGameNightPrefill(gameNightId);
  // #2847 (#BB): the roster is persisted after create via extra calls, which are
  // not part of the create mutation — track our own saving flag so the wizard
  // stays disabled for the whole sequence.
  const [isSaving, setIsSaving] = useState(false);
  // A save that created the record and then stopped (#4800): the next save with the same inputs finishes that
  // record instead of creating another one. Not state: nothing is drawn from it, and it does not survive a
  // reload of the page (the half record stays in the list, as it would after any other abandoned save).
  const stoppedHalfWay = useRef<{ key: string; recordId: string; roster: Set<string> } | null>(
    null
  );
  // The save ends with a navigation: the page may be gone by the time `finally` runs.
  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  // `false` tells the form nothing was saved, so it keeps what the user typed and the draft stays (#4800).
  // It used to catch every error and resolve, which the form took for «saved»: wizard back to the first
  // step, roster gone and the draft — the safety net for exactly this — deleted.
  //
  // Two kinds of failure. If the record could not be created, a retry is a normal second attempt (an error
  // that arrives after the server committed is indistinguishable from this one: `CreatePlayRecord` takes no
  // id from the caller, so that retry makes a second record). If it was created and a later step failed, the
  // record is on the server: the retry resumes it (`persistFollowUps` does only what is missing) as long as
  // what the record is made of is the same — the inputs that define it, and a roster that only grew: the
  // server has no way to remove or rename a player, so a player taken out or renamed since is a new record.
  const handleSubmit: SessionCreateFormProps['onSubmit'] = async (data, players) => {
    setIsSaving(true);
    try {
      const key = createKey(data);
      const roster = new Set(players.map(p => norm(p.name)));
      const earlier = stoppedHalfWay.current;
      if (
        earlier &&
        (earlier.key !== key || ![...earlier.roster].every(name => roster.has(name)))
      ) {
        // Changed after a save that stopped half way: that record cannot be corrected into this one, and
        // deleting it on the user's behalf is not ours to do. Say it is still there.
        stoppedHalfWay.current = null;
        toast.warning(t('playRecords.new.error.strayRecord'), {
          description: t('playRecords.new.error.strayRecordDescription'),
          action: {
            label: t('playRecords.new.error.strayRecordOpen'),
            onClick: () => router.push(`/play-records/${earlier.recordId}`),
          },
        });
      }

      const resumed = stoppedHalfWay.current !== null;
      if (stoppedHalfWay.current) stoppedHalfWay.current.roster = roster;
      let recordId = stoppedHalfWay.current?.recordId;
      if (recordId === undefined) {
        try {
          recordId = await createRecord.mutateAsync({
            gameId: data.gameId,
            gameName: data.gameName,
            sessionDate: data.sessionDate.toISOString(),
            visibility: data.visibility,
            groupId: data.groupId,
            scoringDimensions: data.enableScoring ? data.scoringDimensions : undefined,
            dimensionUnits: data.enableScoring ? data.dimensionUnits : undefined,
          });
        } catch (error) {
          toast.error(t('playRecords.new.error.saveFailed'), {
            description:
              error instanceof Error
                ? error.message
                : t('playRecords.new.error.saveFailedDescription'),
          });
          return false;
        }
        stoppedHalfWay.current = { key, recordId, roster };
      }

      try {
        await persistFollowUps(recordId, data, players, resumed);
      } catch (error) {
        toast.error(t('playRecords.new.error.partialSaveFailed'), {
          description:
            error instanceof Error
              ? error.message
              : t('playRecords.new.error.partialSaveFailedDescription'),
        });
        return false;
      }
      stoppedHalfWay.current = null;

      toast.success(t('playRecords.new.success.toast'), {
        description: t('playRecords.new.success.toastDescription'),
      });

      router.push(`/play-records/${recordId}`);
    } finally {
      if (isMountedRef.current) setIsSaving(false);
    }
  };

  const handleCancel = () => {
    router.push('/play-records');
  };

  // While the GameNight prefill is loading, show a skeleton.
  // Only shown when a gameNightId is present (backward-compat: no-op when absent).
  if (gameNightId && isLoading) {
    return (
      <FormPageContainer className="p-6 space-y-4">
        <div className="h-8 bg-muted animate-pulse rounded w-48" />
        <div className="h-12 bg-muted animate-pulse rounded" />
        <div className="h-32 bg-muted animate-pulse rounded" />
      </FormPageContainer>
    );
  }

  return (
    <FormPageContainer className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={handleCancel}
          aria-label={t('playRecords.new.a11y.backToList')}
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">{t('playRecords.new.pageTitle')}</h1>
          <p className="text-muted-foreground mt-1">{t('playRecords.new.pageSubtitle')}</p>
        </div>
      </div>

      {/* Form — prefill?.initialValues/initialPlayers are undefined when no gameNightId */}
      <SessionCreateForm
        initialValues={prefill?.initialValues}
        initialPlayers={prefill?.initialPlayers}
        onSubmit={handleSubmit}
        onCancel={handleCancel}
        isSubmitting={createRecord.isPending || isSaving}
      />
    </FormPageContainer>
  );
}

export default function NewPlayRecordPage() {
  return (
    <Suspense
      fallback={
        <FormPageContainer className="p-6">
          <div className="h-8 bg-muted animate-pulse rounded w-48" />
        </FormPageContainer>
      }
    >
      <NewPlayRecordContent />
    </Suspense>
  );
}

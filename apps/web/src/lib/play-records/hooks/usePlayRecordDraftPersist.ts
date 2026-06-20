// apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.ts
'use client';

import { useEffect, useMemo, useRef, useState } from 'react';

import {
  PLAY_RECORD_DRAFT_SCHEMA_VERSION,
  persistedPlayRecordDraftSchema,
  type PersistedPlayRecordDraft,
  type PlayRecordDraftState,
} from '@/lib/play-records/draft-types';

const STORAGE_PREFIX = 'meepleai:play-record-create-draft:';
const DEBOUNCE_MS = 800;
const TTL_MS = 7 * 24 * 60 * 60 * 1000; // 7 days

interface StoredEnvelope {
  readonly savedAt: number;
  readonly draft: PersistedPlayRecordDraft;
}

function storageKey(userId: string): string {
  return `${STORAGE_PREFIX}${userId}`;
}

function readDraft(userId: string): PersistedPlayRecordDraft | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    if (raw == null) return null;
    const parsed = JSON.parse(raw) as StoredEnvelope;
    if (Date.now() - parsed.savedAt > TTL_MS) {
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    const validated = persistedPlayRecordDraftSchema.safeParse(parsed.draft);
    if (!validated.success) {
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    return validated.data as PersistedPlayRecordDraft;
  } catch {
    return null;
  }
}

function toPersisted(state: PlayRecordDraftState): PersistedPlayRecordDraft {
  return {
    schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
    currentStep: state.currentStep,
    gameType: state.gameType,
    gameId: state.gameId,
    gameName: state.gameName,
    sessionDate:
      state.sessionDate instanceof Date
        ? state.sessionDate.toISOString()
        : String(state.sessionDate),
    visibility: state.visibility,
    enableScoring: state.enableScoring,
    scoringDimensions: state.scoringDimensions,
    dimensionUnits: state.dimensionUnits,
    notes: state.notes,
    location: state.location,
    players: state.players,
  };
}

function writeDraft(userId: string, state: PlayRecordDraftState): void {
  if (typeof window === 'undefined') return;
  const envelope: StoredEnvelope = { savedAt: Date.now(), draft: toPersisted(state) };
  try {
    window.localStorage.setItem(storageKey(userId), JSON.stringify(envelope));
  } catch {
    // localStorage full or denied — best-effort persistence (spec AC-A1 risks).
  }
}

function deleteDraft(userId: string): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.removeItem(storageKey(userId));
  } catch {
    // ignore
  }
}

export interface UsePlayRecordDraftPersistOptions {
  readonly userId: string | null;
  readonly state: PlayRecordDraftState;
  /** Skip autosave/restore entirely (e.g. edit mode). */
  readonly enabled?: boolean;
}

export interface UsePlayRecordDraftPersistResult {
  /** Draft loaded from storage on first render, or null if none / stale. */
  readonly initialDraft: PersistedPlayRecordDraft | null;
  /** Imperative clear (call after successful submit / explicit discard). */
  readonly clear: () => void;
  /** Whether a save is currently scheduled (debounced). */
  readonly isPending: boolean;
  /** Epoch ms of the last successful write, or null. */
  readonly lastSavedAt: number | null;
}

export function usePlayRecordDraftPersist({
  userId,
  state,
  enabled = true,
}: UsePlayRecordDraftPersistOptions): UsePlayRecordDraftPersistResult {
  // Snapshot the initial draft ONCE so consumers restore exactly once.
  const [initialDraft] = useState<PersistedPlayRecordDraft | null>(() =>
    userId && enabled ? readDraft(userId) : null
  );

  const [isPending, setIsPending] = useState(false);
  const [lastSavedAt, setLastSavedAt] = useState<number | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Latest state read by the debounced writer (avoids stale closures).
  const stateRef = useRef(state);
  stateRef.current = state;

  // Skip the first effect run so a pristine form does not persist on mount.
  const firstRun = useRef(true);

  const signature = useMemo(
    () =>
      JSON.stringify({
        s: state.currentStep,
        gt: state.gameType,
        gi: state.gameId ?? null,
        gn: state.gameName,
        sd: state.sessionDate instanceof Date ? state.sessionDate.getTime() : state.sessionDate,
        v: state.visibility,
        es: state.enableScoring,
        sdim: state.scoringDimensions,
        du: state.dimensionUnits,
        n: state.notes ?? null,
        l: state.location ?? null,
        p: state.players,
      }),
    [state]
  );

  useEffect(() => {
    if (!enabled || !userId) return undefined;
    if (firstRun.current) {
      firstRun.current = false;
      return undefined;
    }
    setIsPending(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      writeDraft(userId, stateRef.current);
      setIsPending(false);
      setLastSavedAt(Date.now());
    }, DEBOUNCE_MS);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
    // signature captures every persisted field; stateRef supplies the value.
  }, [enabled, userId, signature]);

  return {
    initialDraft,
    clear: () => {
      if (timer.current) clearTimeout(timer.current);
      setIsPending(false);
      setLastSavedAt(null);
      if (userId) deleteDraft(userId);
    },
    isPending,
    lastSavedAt,
  };
}

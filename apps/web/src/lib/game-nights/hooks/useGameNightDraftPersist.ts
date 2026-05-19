/**
 * useGameNightDraftPersist — autosave + restore for the SP7 wizard.
 *
 * Issue #950 W3 Commit 3. Spec §9 (draft autosave policy):
 *   - Debounce 800ms after any reducer action that mutates form state.
 *   - localStorage keyed `meepleai:gamenight-create-draft:<userId>`
 *     (avoid cross-user leak).
 *   - Schema version guard via `WIZARD_DRAFT_SCHEMA_VERSION` (drop on bump).
 *   - 7-day TTL — drafts older than 7 days are discarded on read.
 *   - Submit-success clears the draft via the returned `clear()` callback.
 */

'use client';

import { useEffect, useRef, useState } from 'react';

import {
  WIZARD_DRAFT_SCHEMA_VERSION,
  type PersistedDraft,
  type WizardState,
} from '@/lib/game-nights/wizard-types';
import { persistedDraftSchema } from '@/lib/game-nights/wizard-validators';

const STORAGE_PREFIX = 'meepleai:gamenight-create-draft:';
const DEBOUNCE_MS = 800;
const TTL_MS = 7 * 24 * 60 * 60 * 1000; // 7 days

interface StoredEnvelope {
  readonly savedAt: number;
  readonly draft: PersistedDraft;
}

function storageKey(userId: string): string {
  return `${STORAGE_PREFIX}${userId}`;
}

function readDraft(userId: string): PersistedDraft | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    if (raw == null) return null;
    const parsed = JSON.parse(raw) as StoredEnvelope;
    if (Date.now() - parsed.savedAt > TTL_MS) {
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    const validated = persistedDraftSchema.safeParse(parsed.draft);
    if (!validated.success) {
      // Schema version mismatch or corrupted payload — discard.
      window.localStorage.removeItem(storageKey(userId));
      return null;
    }
    return validated.data as PersistedDraft;
  } catch {
    return null;
  }
}

function writeDraft(userId: string, state: WizardState): void {
  if (typeof window === 'undefined') return;
  // PersistedDraft is the WizardState minus the `draft` branch + schemaVersion.
  const draft: PersistedDraft = {
    schemaVersion: WIZARD_DRAFT_SCHEMA_VERSION,
    step: state.step,
    date: state.date,
    location: state.location,
    invitees: state.invitees,
    games: state.games,
  };
  const envelope: StoredEnvelope = { savedAt: Date.now(), draft };
  try {
    window.localStorage.setItem(storageKey(userId), JSON.stringify(envelope));
  } catch {
    // localStorage full or denied — swallow; the spec accepts best-effort
    // persistence (Nygard §9 risks).
  }
}

function deleteDraft(userId: string): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.removeItem(storageKey(userId));
  } catch {
    // Ignore.
  }
}

export interface UseGameNightDraftPersistOptions {
  readonly userId: string | null;
  readonly state: WizardState;
  /**
   * Skip autosave entirely (e.g. when the visual-test fixture short-circuits
   * the orchestrator to a deterministic state).
   */
  readonly enabled?: boolean;
}

export interface UseGameNightDraftPersistResult {
  /** The draft loaded from storage on mount, or null if none / stale. */
  readonly initialDraft: PersistedDraft | null;
  /** Imperative clear (call after successful submit / explicit discard). */
  readonly clear: () => void;
  /** Whether a save is currently scheduled (debounced). */
  readonly isPending: boolean;
}

export function useGameNightDraftPersist({
  userId,
  state,
  enabled = true,
}: UseGameNightDraftPersistOptions): UseGameNightDraftPersistResult {
  // Snapshot the initial draft on the FIRST render so consumers can dispatch
  // `restoreFromDraft` exactly once. Subsequent state writes don't re-load
  // (the user's edits would otherwise be overwritten on re-render).
  const [initialDraft] = useState<PersistedDraft | null>(() =>
    userId && enabled ? readDraft(userId) : null
  );

  const [isPending, setIsPending] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Capture the latest `state` in a ref so the effect's deps array can list
  // only the persisted slices (and skip `state.draft`, which is autosave
  // status — including it would trigger an infinite re-save loop). The
  // effect reads through the ref, so react-hooks/exhaustive-deps stays happy.
  const stateRef = useRef(state);
  stateRef.current = state;

  const { step, date, location, invitees, games } = state;

  useEffect(() => {
    if (!enabled || !userId) return undefined;

    setIsPending(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      writeDraft(userId, stateRef.current);
      setIsPending(false);
    }, DEBOUNCE_MS);

    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, [enabled, userId, step, date, location, invitees, games]);

  return {
    initialDraft,
    clear: () => {
      if (timer.current) clearTimeout(timer.current);
      setIsPending(false);
      if (userId) deleteDraft(userId);
    },
    isPending,
  };
}

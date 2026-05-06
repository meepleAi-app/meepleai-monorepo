/**
 * 6-cell FSM derivation for `/sessions/[id]` summary (Wave D.3).
 *
 * Pure function — no React, no API client. Composes 4 query inputs (session,
 * diary, snapshots, achievements) into a single discriminated union output
 * cell. The orchestrator (Task 3) renders the appropriate UI per cell kind.
 *
 * Schema reality v1 carryover (Gate B):
 *   - The Phase 0.5 contract (sessions-id-summary-hooks.md §4.1) refers to
 *     `SessionDetailDto`, `SessionDiaryEvent`, `SessionVisionSnapshot`. The
 *     real schema names in the repo are `SessionDetailsDto` (note the extra
 *     'S' in `Details`), `DiaryEntryDto` (from `session-flow/types.ts`), and
 *     `SessionSnapshotDto` (from `sessionSnapshotsClient.ts`). The orchestrator
 *     (Task 3) is responsible for mapping `useSessionDetail`'s
 *     `GameSessionDto` (from `games.schemas.ts`) into a `SessionDetailsDto`-
 *     shaped value when needed (or composing the two if both are required).
 *
 *   - For this foundation file, we use the canonical contract types from
 *     `session-tracking.schemas.ts` (`ParticipantDto` is the source of
 *     `displayName`, `totalScore`, `finalRank`) and adapt at orchestrator
 *     boundary. `AchievementDto` is a frontend-only stub (see schemas.ts).
 *
 * Cell precedence (highest → lowest):
 *   1. loading       — ANY of 4 queries pending
 *   2. error         — ANY query errored (session error preferred for messaging)
 *   3. not-found     — session query returned null (404 or invalid id)
 *   4. not-completed — session.status !== 'Completed'
 *   5. partial       — all queries success but ≥1 sub-array (diary/snapshots/
 *                       achievements) is empty → render with placeholders
 *   6. default       — all queries success and all sub-arrays non-empty
 */

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';
import type { SessionDetailsDto } from '@/lib/api/schemas/session-tracking.schemas';
import type { DiaryEntryDto } from '@/lib/api/session-flow/types';

import type { AchievementDto } from './schemas';

// ---------------------------------------------------------------------------
// Cell discriminated union
// ---------------------------------------------------------------------------

export type SessionSummaryFSMCell =
  | { kind: 'loading' }
  | { kind: 'error'; error: Error }
  | { kind: 'not-found' }
  | {
      kind: 'not-completed';
      status: SessionDetailsDto['status'];
      sessionId: string;
    }
  | {
      kind: 'default';
      session: SessionDetailsDto;
      diary: readonly DiaryEntryDto[];
      snapshots: readonly SessionSnapshotDto[];
      achievements: readonly AchievementDto[];
    }
  | {
      kind: 'partial';
      session: SessionDetailsDto;
      diary: readonly DiaryEntryDto[];
      snapshots: readonly SessionSnapshotDto[];
      achievements: readonly AchievementDto[];
      /** Names of sections that are empty and need a placeholder. */
      missing: readonly ('diary' | 'snapshots' | 'achievements' | 'chat')[];
    };

// ---------------------------------------------------------------------------
// Input shapes (mirror TanStack Query's UseQueryResult slice)
// ---------------------------------------------------------------------------

/**
 * Minimal slice of TanStack `UseQueryResult` we actually need.
 * Independent type so this module stays decoupled from `@tanstack/react-query`.
 */
export interface QueryLike<TData> {
  readonly isPending: boolean;
  readonly isError: boolean;
  readonly error?: Error | null;
  readonly data?: TData | null;
}

export interface DeriveStateInput {
  readonly sessionQuery: QueryLike<SessionDetailsDto>;
  readonly diaryQuery: QueryLike<readonly DiaryEntryDto[]>;
  readonly snapshotsQuery: QueryLike<readonly SessionSnapshotDto[]>;
  readonly achievementsQuery: QueryLike<readonly AchievementDto[]>;
  readonly sessionId: string;
}

// ---------------------------------------------------------------------------
// Derivation
// ---------------------------------------------------------------------------

/**
 * Status string considered "completed" for the summary view. The backend may
 * return either `'Completed'` (capital C) or `'Abandoned'` (which renders the
 * abandoned variant of the summary, NOT the live UI). Both qualify for the
 * default/partial cells; only `InProgress` / `Paused` / `Setup` etc. fall
 * into not-completed.
 */
const COMPLETED_STATUSES = new Set(['Completed', 'Abandoned']);

/**
 * Derive the 6-cell FSM cell from query state.
 *
 * Cell kinds are mutually exclusive — the function returns exactly one cell.
 * The discriminant `kind` is narrow-typeable in an exhaustive switch.
 *
 * Edge cases:
 *   - Empty `sessionId` is NOT validated here. The orchestrator (Task 3)
 *     resolves the route param before calling derive; if the param is empty
 *     it short-circuits to the not-found cell BEFORE calling this function.
 *     This file assumes `sessionId` is a non-empty string.
 *
 *   - The `error` cell prefers `sessionQuery.error` over sub-query errors
 *     because the session is the parent (sub-queries are gated on its
 *     success). If the parent succeeded but a sub-query errored, we still
 *     prefer sessionQuery's error field (which will be null/undefined) and
 *     fall through to the first errored sub-query.
 */
export function deriveSessionSummaryState(input: DeriveStateInput): SessionSummaryFSMCell {
  const { sessionQuery, diaryQuery, snapshotsQuery, achievementsQuery, sessionId } = input;

  // Cell 1: loading — ANY query pending
  if (
    sessionQuery.isPending ||
    diaryQuery.isPending ||
    snapshotsQuery.isPending ||
    achievementsQuery.isPending
  ) {
    return { kind: 'loading' };
  }

  // Cell 2: error — ANY query errored (prefer session error for surfaced messaging)
  if (sessionQuery.isError) {
    return { kind: 'error', error: sessionQuery.error ?? new Error('Session query failed') };
  }
  if (diaryQuery.isError) {
    return { kind: 'error', error: diaryQuery.error ?? new Error('Diary query failed') };
  }
  if (snapshotsQuery.isError) {
    return { kind: 'error', error: snapshotsQuery.error ?? new Error('Snapshots query failed') };
  }
  if (achievementsQuery.isError) {
    return {
      kind: 'error',
      error: achievementsQuery.error ?? new Error('Achievements query failed'),
    };
  }

  // Cell 3: not-found — session resolved with null/undefined data
  const session = sessionQuery.data;
  if (session === null || session === undefined) {
    return { kind: 'not-found' };
  }

  // Cell 4: not-completed — session exists but status is in-progress/paused/setup
  if (!COMPLETED_STATUSES.has(session.status)) {
    return { kind: 'not-completed', status: session.status, sessionId };
  }

  // From here on, all queries succeeded with data. Default sub-arrays to []
  // (TanStack returns `data: undefined` when query disabled — defensive guard).
  const diary = diaryQuery.data ?? [];
  const snapshots = snapshotsQuery.data ?? [];
  const achievements = achievementsQuery.data ?? [];

  // Cell 6 vs 5: partial when ANY of the optional sections is empty
  const missing: ('diary' | 'snapshots' | 'achievements' | 'chat')[] = [];
  if (diary.length === 0) missing.push('diary');
  if (snapshots.length === 0) missing.push('snapshots');
  if (achievements.length === 0) missing.push('achievements');
  // Note: `chat` not represented in the 4-query input set in v1 (chat highlights
  // are a stub-only fixture per contract §4.3). The orchestrator will append
  // `'chat'` to `missing` when the chat highlights fixture is empty. We surface
  // the type member here to keep the union closed.

  if (missing.length > 0) {
    return {
      kind: 'partial',
      session,
      diary,
      snapshots,
      achievements,
      missing,
    };
  }

  // Cell 5: default
  return {
    kind: 'default',
    session,
    diary,
    snapshots,
    achievements,
  };
}

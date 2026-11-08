/**
 * The stable codes of the 409s a live session answers when an action does not fit its status (#4844),
 * and the i18n key each one reads as. The server's sentence is English and for the logs: it is never
 * shown. A code the client does not know falls back to the generic sentence, never to the text.
 *
 * Mirror of `LiveSessionConflictCodes` (apps/api, GameManagement/Domain/Exceptions). Lower snake case,
 * like `turn_changed`, `score_changed` and `kb_not_ready`; `MAX_LIVE_SESSIONS_EXCEEDED` predates the
 * rule and is compared as is in `useStartNextGame`.
 */

import { ConflictError } from '@/lib/api/core/errors';

export const LIVE_SESSION_CONFLICT_CODES = {
  /** Created or Setup, on an action that wants the game started. */
  notStarted: 'session_not_started',
  /** Paused, on an action that wants the game running (a rule dispute, a turn). */
  paused: 'session_paused',
  /** Completed: nothing changes a finished game. */
  completed: 'session_completed',
  /** InProgress on start. */
  alreadyStarted: 'session_already_started',
  /** InProgress on resume. */
  notPaused: 'session_not_paused',
} as const;

export type LiveSessionConflictCode =
  (typeof LIVE_SESSION_CONFLICT_CODES)[keyof typeof LIVE_SESSION_CONFLICT_CODES];

const KEY_BY_CODE: Readonly<Record<LiveSessionConflictCode, string>> = {
  session_not_started: 'pages.sessionLive.conflicts.notStarted',
  session_paused: 'pages.sessionLive.conflicts.paused',
  session_completed: 'pages.sessionLive.conflicts.completed',
  session_already_started: 'pages.sessionLive.conflicts.alreadyStarted',
  session_not_paused: 'pages.sessionLive.conflicts.notPaused',
};

/** The sentence for a 409 whose code the client does not know, or that carries none. */
export const LIVE_SESSION_CONFLICT_GENERIC_KEY = 'pages.sessionLive.conflicts.generic';

/**
 * True for a 409, whichever class carries it: `ConflictError` from the http client, or anything with
 * `statusCode: 409` (the live page already discriminated that way before the codes existed).
 */
export function isLiveSessionConflict(error: unknown): boolean {
  if (error instanceof ConflictError) return true;
  return (
    typeof error === 'object' &&
    error !== null &&
    (error as { statusCode?: unknown }).statusCode === 409
  );
}

/** The known code on a 409, or `null` when it has none or an unknown one. */
export function liveSessionConflictCode(error: unknown): LiveSessionConflictCode | null {
  if (!(error instanceof ConflictError)) return null;
  const code = error.code;
  return code != null && Object.hasOwn(KEY_BY_CODE, code)
    ? (code as LiveSessionConflictCode)
    : null;
}

/**
 * The i18n key a live-session 409 reads as, or `null` when `error` is not a 409 at all — the caller
 * keeps its own message for those. An unknown code gives the generic key: never the server's text.
 */
export function liveSessionConflictKey(error: unknown): string | null {
  if (!isLiveSessionConflict(error)) return null;
  const code = liveSessionConflictCode(error);
  return code == null ? LIVE_SESSION_CONFLICT_GENERIC_KEY : KEY_BY_CODE[code];
}

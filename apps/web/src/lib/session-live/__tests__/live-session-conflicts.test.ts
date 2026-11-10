import { describe, expect, it } from 'vitest';

import { ApiError, ConflictError, NotFoundError } from '@/lib/api/core/errors';

import {
  CONCURRENT_EDIT_CODE,
  LIVE_SESSION_CONCURRENT_EDIT_KEY,
  LIVE_SESSION_CONFLICT_CODES,
  LIVE_SESSION_CONFLICT_GENERIC_KEY,
  isLiveSessionConflict,
  liveSessionConflictCode,
  liveSessionConflictKey,
} from '../live-session-conflicts';

// #4844: the 409 of an action the game's status does not admit reads in the user's language, by code.
describe('liveSessionConflictKey', () => {
  it.each([
    [LIVE_SESSION_CONFLICT_CODES.notStarted, 'pages.sessionLive.conflicts.notStarted'],
    [LIVE_SESSION_CONFLICT_CODES.paused, 'pages.sessionLive.conflicts.paused'],
    [LIVE_SESSION_CONFLICT_CODES.completed, 'pages.sessionLive.conflicts.completed'],
    [LIVE_SESSION_CONFLICT_CODES.alreadyStarted, 'pages.sessionLive.conflicts.alreadyStarted'],
    [LIVE_SESSION_CONFLICT_CODES.notPaused, 'pages.sessionLive.conflicts.notPaused'],
  ])('%s reads as %s', (code, key) => {
    const error = new ConflictError({ message: 'Cannot record scores in Created status.', code });
    expect(liveSessionConflictKey(error)).toBe(key);
    expect(liveSessionConflictCode(error)).toBe(code);
  });

  it('a code the client does not know reads as the generic sentence, never as the server text', () => {
    const error = new ConflictError({ message: 'Some new refusal', code: 'session_frozen' });
    expect(liveSessionConflictKey(error)).toBe(LIVE_SESSION_CONFLICT_GENERIC_KEY);
    expect(liveSessionConflictCode(error)).toBeNull();
  });

  // ADR-060: the xmin 409 is the one conflict worth retrying — it must not read as «not allowed in
  // the game's current state», which would tell the player the game forbids scoring while it runs.
  it('the concurrent-edit 409 reads as «try again», not as a state refusal', () => {
    const error = new ConflictError({
      message: 'The record was modified',
      code: CONCURRENT_EDIT_CODE,
    });
    expect(liveSessionConflictKey(error)).toBe(LIVE_SESSION_CONCURRENT_EDIT_KEY);
    expect(liveSessionConflictCode(error)).toBeNull();
  });

  it('a 409 without a code is generic too', () => {
    expect(liveSessionConflictKey(new ConflictError({ message: 'conflict' }))).toBe(
      LIVE_SESSION_CONFLICT_GENERIC_KEY
    );
  });

  it('the codes are lower snake case, like turn_changed and kb_not_ready', () => {
    for (const code of Object.values(LIVE_SESSION_CONFLICT_CODES)) {
      expect(code).toMatch(/^[a-z][a-z0-9]*(_[a-z0-9]+)*$/);
    }
  });

  it('is not its business when the error is not a 409: the caller keeps its own message', () => {
    expect(liveSessionConflictKey(new NotFoundError({ message: 'gone' }))).toBeNull();
    expect(liveSessionConflictKey(new ApiError({ message: 'boom', statusCode: 500 }))).toBeNull();
    expect(liveSessionConflictKey(new Error('Network error'))).toBeNull();
    expect(liveSessionConflictKey(undefined)).toBeNull();
  });

  it('anything carrying statusCode 409 counts as a conflict, as the live page already assumed', () => {
    expect(isLiveSessionConflict({ statusCode: 409 })).toBe(true);
    expect(liveSessionConflictKey({ statusCode: 409 })).toBe(LIVE_SESSION_CONFLICT_GENERIC_KEY);
    expect(isLiveSessionConflict({ statusCode: 404 })).toBe(false);
  });
});

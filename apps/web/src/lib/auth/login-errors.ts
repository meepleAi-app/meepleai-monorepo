/**
 * #4991: which message the login page shows for a failed attempt.
 *
 * The server's sentence is English and for the logs: it is never shown. The response code chooses
 * the translated message — the same rule as `lib/session-live/live-session-conflicts.ts`. An unknown
 * code, a network error or a rate limit get the generic message.
 */
import { ApiError } from '@/lib/api/core/errors';

export const LOGIN_GENERIC_ERROR_KEY = 'auth.login.genericError';

const KEY_BY_CODE: ReadonlyMap<string, string> = new Map([
  ['invalid_credentials', 'auth.login.errors.invalidCredentials'],
  ['account_locked', 'auth.login.errors.accountLocked'],
  ['account_unavailable', 'auth.login.errors.accountUnavailable'],
]);

export function loginErrorKey(error: unknown): string {
  if (error instanceof ApiError && error.code) {
    return KEY_BY_CODE.get(error.code) ?? LOGIN_GENERIC_ERROR_KEY;
  }
  return LOGIN_GENERIC_ERROR_KEY;
}

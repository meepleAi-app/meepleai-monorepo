/**
 * #4991: the login page showed the server's English sentence ("Invalid email or password"). The
 * text shown is chosen by the response code; the sentence is for the logs.
 */
import { describe, expect, it } from 'vitest';

import { ApiError, RateLimitError } from '@/lib/api/core/errors';

import { LOGIN_GENERIC_ERROR_KEY, loginErrorKey } from '../login-errors';

function apiError(code: string | undefined, statusCode = 400): ApiError {
  return new ApiError({ message: 'Invalid email or password', statusCode, code });
}

describe('loginErrorKey', () => {
  it.each([
    ['invalid_credentials', 'auth.login.errors.invalidCredentials'],
    ['account_locked', 'auth.login.errors.accountLocked'],
    ['account_unavailable', 'auth.login.errors.accountUnavailable'],
  ])('maps the code %s to its own message', (code, key) => {
    expect(loginErrorKey(apiError(code))).toBe(key);
  });

  it.each([
    ['the old undifferentiated code', apiError('domain_error')],
    ['a code inherited from Object.prototype', apiError('constructor')],
    ['an error without code', apiError(undefined, 500)],
    [
      'a rate limit',
      new RateLimitError({ message: 'Too Many Requests', code: 'Too Many Requests' }),
    ],
    ['a plain Error carrying the server sentence', new Error('Invalid email or password')],
    ['a non-Error value', 'boom'],
  ])('falls back to the generic message for %s', (_label, error) => {
    expect(loginErrorKey(error)).toBe(LOGIN_GENERIC_ERROR_KEY);
  });
});

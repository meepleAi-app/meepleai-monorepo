/**
 * Regression tests for LoginResponseSchema parsing — issue #1811.
 *
 * The BE serializes the 2FA temp token as `sessionToken` (Models/TwoFactorDto.cs:62);
 * the FE historically reads it as `tempSessionToken` (_content.tsx, AuthModal.tsx).
 * The schema must accept both field names and normalize to `tempSessionToken` so the
 * 2FA challenge branch in the login flow stays reachable.
 *
 * Bug repro before the fix: zod parsing silently dropped `sessionToken`, leaving
 * `tempSessionToken: undefined`. The `if (requiresTwoFactor && tempSessionToken)`
 * check in _content.tsx:81 fell through to the generic error, locking out every
 * user with 2FA enabled.
 */

import { describe, it, expect } from 'vitest';

import { LoginResponseSchema } from '../auth.schemas';

describe('LoginResponseSchema — 2FA challenge field name compatibility', () => {
  it('normalizes BE-shaped response (sessionToken) to tempSessionToken (issue #1811)', () => {
    // Exact shape returned by POST /api/v1/auth/login when 2FA is enabled,
    // as observed on staging 2026-06-02. The BE DTO uses `SessionToken` →
    // camelCase JSON `sessionToken`.
    const beResponse = {
      requiresTwoFactor: true,
      sessionToken: 'EigeLf9W3VDVUs8KwA9Vb0SMgIXAWBo2O/2Y1WXl9YE=',
      message: 'Two-factor authentication required',
    };

    const parsed = LoginResponseSchema.parse(beResponse);

    expect(parsed.requiresTwoFactor).toBe(true);
    expect(parsed.tempSessionToken).toBe('EigeLf9W3VDVUs8KwA9Vb0SMgIXAWBo2O/2Y1WXl9YE=');
    expect(parsed.user).toBeNull();
  });

  it('still accepts the legacy FE-shaped response (tempSessionToken)', () => {
    // Existing tests in _content.test.tsx and AuthModal.test.tsx use this shape
    // when mocking api.auth.login directly. Preserve backward compatibility.
    const feResponse = {
      requiresTwoFactor: true,
      tempSessionToken: 'temp-token-abc',
    };

    const parsed = LoginResponseSchema.parse(feResponse);

    expect(parsed.requiresTwoFactor).toBe(true);
    expect(parsed.tempSessionToken).toBe('temp-token-abc');
  });

  it('prefers tempSessionToken over sessionToken when both are present', () => {
    // Defensive: if a future BE change starts sending both for transition, the
    // FE name wins to avoid surprising downstream code that reads .tempSessionToken.
    const bothFields = {
      requiresTwoFactor: true,
      tempSessionToken: 'fe-shape',
      sessionToken: 'be-shape',
    };

    const parsed = LoginResponseSchema.parse(bothFields);

    expect(parsed.tempSessionToken).toBe('fe-shape');
  });

  it('parses successful login response (no 2FA) with user', () => {
    const successResponse = {
      user: {
        id: '11111111-1111-4111-8111-111111111111',
        email: 'user@example.com',
        role: 'User',
        onboardingCompleted: true,
        onboardingSkipped: false,
      },
      requiresTwoFactor: false,
    };

    const parsed = LoginResponseSchema.parse(successResponse);

    expect(parsed.user).not.toBeNull();
    expect(parsed.user?.email).toBe('user@example.com');
    expect(parsed.requiresTwoFactor).toBe(false);
    expect(parsed.tempSessionToken).toBeNull();
  });

  it('defaults requiresTwoFactor to false when the field is missing', () => {
    const minimalResponse = {
      user: {
        id: '22222222-2222-4222-9222-222222222222',
        email: 'min@example.com',
        role: 'User',
      },
    };

    const parsed = LoginResponseSchema.parse(minimalResponse);

    expect(parsed.requiresTwoFactor).toBe(false);
    expect(parsed.tempSessionToken).toBeNull();
  });

  it('returns tempSessionToken: null when neither field is present', () => {
    const noToken = {
      requiresTwoFactor: false,
      user: null,
    };

    const parsed = LoginResponseSchema.parse(noToken);

    expect(parsed.tempSessionToken).toBeNull();
  });
});

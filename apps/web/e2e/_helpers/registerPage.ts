/**
 * The `/register` page as it is (#4248): `RegisterForm`, no auth modal.
 *
 * The form has email (`#register-email`), password (`#register-password`), the Terms
 * checkbox (`register-terms`, required) and a honeypot; no password confirmation and no
 * display name. The page asks `GET /api/v1/auth/registration-mode` from the browser and,
 * when registration is invite-only — the default of a fresh database — shows
 * `RequestAccessForm` instead.
 *
 * Browser calls go through the Next proxy with relative paths (`getApiBase()` returns ''),
 * so routes are relative globs: `http://localhost:8080/api/v1/…` would never match.
 */

import { expect } from '@playwright/test';

import type { Page } from '@playwright/test';

/** Simulates the registration mode in the browser only: the backend keeps its own. */
export async function mockRegistrationMode(
  page: Page,
  publicRegistrationEnabled: boolean
): Promise<void> {
  await page.route('**/api/v1/auth/registration-mode', route =>
    route.fulfill({ json: { publicRegistrationEnabled, oauthEnabled: false } })
  );
}

/**
 * Answers `POST /api/v1/auth/register` with the real contract (`RegisterResponseSchema`:
 * `{ user }`). No session cookie is set: pages that need one redirect to `/login`.
 */
export async function mockRegisterResponse(page: Page, email: string): Promise<void> {
  await page.route('**/api/v1/auth/register', route =>
    route.fulfill({
      json: {
        user: {
          id: '00000000-0000-4000-8000-000000004248',
          email,
          displayName: null,
          role: 'User',
          onboardingCompleted: false,
          onboardingSkipped: false,
          emailVerified: false,
        },
        expiresAt: '2099-01-01T00:00:00Z',
      },
    })
  );
}

/** Fills the form, accepts the Terms and submits it. */
export async function submitRegistration(
  page: Page,
  { email, password }: { email: string; password: string }
): Promise<void> {
  const form = page.getByTestId('register-form');
  await expect(form).toBeVisible();
  await form.locator('#register-email').fill(email);
  await form.locator('#register-password').fill(password);
  await form.getByTestId('register-terms').check();
  await form.locator('button[type="submit"]').click();
}

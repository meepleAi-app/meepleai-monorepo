/**
 * Auth Registration → Dashboard Flow Tests - REAL BACKEND
 *
 * The bug these tests were written for: after registering, the dashboard showed a
 * "no user" state, because the forms and `AuthProvider` kept separate user states.
 * #4228 made `AuthProvider` read the `['user','current']` query; its unit test mounts
 * the real provider (`components/auth/__tests__/AuthProvider.single-source.test.tsx`).
 *
 * #4248: rewritten on the real pages and the real backend. There is no auth modal: the
 * landing's CTA leads to `/register`, the form has email, password and the Terms, and a
 * successful registration opens the session and lands on `/verification-pending`. The old
 * tests set a fake session cookie and expected the dashboard straight after registering.
 *
 * Real APIs Used:
 * - GET /api/v1/auth/registration-mode, PUT /api/v1/admin/settings/registration-mode
 * - POST /api/v1/auth/register (user registration, session cookie)
 * - GET /api/v1/auth/me (session on reload)
 */

import {
  ensurePublicRegistration,
  uncommonRegistrationPassword,
  uniqueRegistrationEmail,
  type PublicRegistration,
} from './_helpers/publicRegistration';
import { submitRegistration } from './_helpers/registerPage';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import { test, expect } from './fixtures';

test.describe('Registration → Dashboard Flow', () => {
  test.describe.configure({ mode: 'serial' });

  let registration: PublicRegistration;

  test.beforeAll(async () => {
    registration = await ensurePublicRegistration();
  });

  test.afterAll(async () => {
    if (registration?.available) await registration.restore();
  });

  test('after registering, the dashboard greets the new user by name, not as a guest', async ({
    page,
  }) => {
    if (!registration.available) test.skip(true, registration.reason);
    await seedCookieConsent(page);
    const email = uniqueRegistrationEmail('e2e-reg-dashboard');

    // 1. From the landing to the register page
    await page.goto('/');
    await page.getByRole('link', { name: 'Inizia gratis' }).first().click();
    await page.waitForURL(/\/register$/);

    // 2. Register for real
    const registered = page.waitForResponse(
      r => r.url().includes('/api/v1/auth/register') && r.request().method() === 'POST'
    );
    await submitRegistration(page, { email, password: uncommonRegistrationPassword() });
    const response = await registered;
    expect(response.status()).toBe(200);
    const { user } = (await response.json()) as { user: { email: string; displayName: string } };
    expect(user.email).toBe(email);

    // 3. The page asks to verify the email; the session is already open
    await page.waitForURL('**/verification-pending**', { timeout: 15000 });
    await expect(page.getByTestId('verification-pending-page')).toBeVisible();

    // 4. A full reload of the dashboard reads the user from the session (`/auth/me`)
    await page.goto('/dashboard');
    const greeting = page.locator('[data-slot="dashboard-hero"] h1');
    await expect(greeting).toContainText(user.displayName, { timeout: 15000 });
  });
});

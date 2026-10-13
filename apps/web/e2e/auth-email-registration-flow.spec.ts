/**
 * E2E Test: Email Registration → Session → Logout Flow - REAL BACKEND
 *
 * #4248: rewritten on the real pages. The old journey opened an auth modal from a
 * "Get Started" button, filled fields that `RegisterForm` does not have, expected the
 * dashboard straight after registering, and checked most of its steps in `if` blocks that
 * asserted nothing (the class of #4127). Today:
 * 1. the landing's «Inizia gratis» leads to `/register`;
 * 2. registering (email, password, Terms) opens an HttpOnly session and lands on
 *    `/verification-pending`;
 * 3. the dashboard greets the new user by name, not as a guest;
 * 4. logging out from the user menu closes the session: the library sends to `/login` again.
 *
 * Step 3 is what `auth-registration-dashboard.spec.ts` was written for: after registering,
 * the dashboard showed a "no user" state, because the forms and `AuthProvider` kept separate
 * user states (fixed by #4228, whose unit test mounts the real provider). That spec set a
 * fake session cookie; its check lives here, on the one real registration, so that a single
 * file opens and restores the registration mode (see `_helpers/publicRegistration.ts`).
 *
 * Real APIs Used:
 * - GET /api/v1/auth/registration-mode, PUT /api/v1/admin/settings/registration-mode
 * - POST /api/v1/auth/register (user registration, session cookie)
 * - GET /api/v1/auth/me (session verification)
 * - POST /api/v1/auth/logout (session destruction, via the logout server action)
 *
 * @see Issue #2700 - Full registration flow E2E test with header/cookie validation
 */

import { test, expect } from '@playwright/test';

import {
  ensurePublicRegistration,
  uncommonRegistrationPassword,
  uniqueRegistrationEmail,
  type PublicRegistration,
} from './_helpers/publicRegistration';
import { submitRegistration } from './_helpers/registerPage';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

test.describe('Email Registration → Session → Logout Flow', () => {
  test.describe.configure({ mode: 'serial' });

  let registration: PublicRegistration;

  test.beforeAll(async () => {
    registration = await ensurePublicRegistration();
  });

  test.afterAll(async () => {
    if (registration?.available) await registration.restore();
  });

  test('Register with email → session open on verification-pending → dashboard greets by name → logout → library protected again', async ({
    page,
    context,
  }) => {
    if (!registration.available) test.skip(true, registration.reason);
    await seedCookieConsent(page);
    const email = uniqueRegistrationEmail('e2e-reg-flow');
    let displayName = '';

    await test.step('Landing CTA leads to the register page', async () => {
      await page.goto('/');
      await page.getByRole('link', { name: 'Inizia gratis' }).first().click();
      await page.waitForURL(url => url.pathname === '/register');
    });

    await test.step('Registering opens an HttpOnly session', async () => {
      const registered = page.waitForResponse(
        r => r.url().includes('/api/v1/auth/register') && r.request().method() === 'POST'
      );
      await submitRegistration(page, { email, password: uncommonRegistrationPassword() });
      const response = await registered;
      expect(response.status()).toBe(200);
      const { user } = (await response.json()) as { user: { email: string; displayName: string } };
      expect(user.email).toBe(email);
      // The form sends no name: the backend assigns one (`Player-…`).
      expect(user.displayName).not.toBe('');
      displayName = user.displayName;

      const session = (await context.cookies()).find(c => c.name === 'meepleai_session');
      expect(session, 'the registration must set the session cookie').toBeDefined();
      expect(session?.httpOnly).toBe(true);
    });

    await test.step('The page asks to verify the email', async () => {
      await page.waitForURL('**/verification-pending**', { timeout: 15000 });
      await expect(page.getByTestId('verification-pending-page')).toBeVisible();
    });

    await test.step('The dashboard greets the new user by name, not as a guest', async () => {
      // A full reload: the user comes from the session (`/auth/me`), not from client state.
      await page.goto('/dashboard');
      await expect(page.locator('[data-slot="dashboard-hero"] h1')).toContainText(displayName, {
        timeout: 15000,
      });
    });

    await test.step('The new user reaches a protected page', async () => {
      await page.goto('/library');
      expect(new URL(page.url()).pathname).toBe('/library');
      await expect(page.getByTestId('user-menu-trigger').first()).toBeVisible({ timeout: 15000 });
    });

    await test.step('Logout from the user menu closes the session', async () => {
      await page.getByTestId('user-menu-trigger').first().click();
      await page.getByTestId('logout-menu-item').click();
      await page.waitForURL(url => url.pathname === '/login', { timeout: 15000 });

      await page.goto('/library');
      await page.waitForURL(url => url.pathname === '/login', { timeout: 15000 });
    });
  });

  test('Verify frontend-backend header transmission', async ({ page, context }) => {
    // This test focuses specifically on validating headers in API calls
    const capturedRequests: { url: string; headers: Record<string, string> }[] = [];

    // Capture requests
    page.on('request', request => {
      if (request.url().includes('/api/')) {
        capturedRequests.push({
          url: request.url(),
          headers: request.headers(),
        });
      }
    });

    // Navigate to trigger auth check
    await page.goto('/dashboard');

    // Wait for potential redirect or auth check
    await page.waitForLoadState('networkidle');

    // Analyze captured requests
    console.log('\n🔍 HEADER ANALYSIS:');
    for (const req of capturedRequests) {
      console.log(`\nRequest: ${req.url}`);
      console.log('Headers:');

      // Check important headers
      const importantHeaders = [
        'accept',
        'content-type',
        'cookie',
        'authorization',
        'x-requested-with',
        'origin',
        'referer',
      ];

      for (const header of importantHeaders) {
        if (req.headers[header]) {
          const value =
            header === 'cookie'
              ? req.headers[header].substring(0, 80) + '...'
              : req.headers[header];
          console.log(`  ${header}: ${value}`);
        }
      }
    }

    // Verify at least one API call was made
    expect(capturedRequests.length).toBeGreaterThan(0);
  });
});

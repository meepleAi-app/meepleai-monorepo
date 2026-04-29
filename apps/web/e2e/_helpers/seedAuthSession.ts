/**
 * Auth session seeding helper for Playwright specs targeting `(authenticated)/*` routes.
 *
 * **Why this helper exists** (Wave B.1 lesson learned, Issue #633):
 *   The `proxy.ts` middleware redirects unauthenticated users away from `PROTECTED_ROUTES`
 *   (which includes `/games`, `/library`, `/admin`, etc.) to `/login`. The
 *   `PLAYWRIGHT_AUTH_BYPASS=true` env var (set in `playwright.config.ts:434`)
 *   short-circuits the BACKEND validation step but still requires a session cookie
 *   to be PRESENT on the request — otherwise the proxy treats the user as
 *   unauthenticated and emits a 307 to `/login`, breaking visual regression specs
 *   for any `(authenticated)` route.
 *
 *   Wave A.1-A.5 routes were all in `(public)` — they did not need this helper.
 *   Wave B.1 (`/games?tab=library`) is the first brownfield migration of an
 *   `(authenticated)` route, so it must seed cookies before navigating.
 *
 * **Contract** (must match `proxy.ts` exactly):
 *   - `meepleai_session` cookie — opaque token, validated server-side normally,
 *     but trusted as-is when `PLAYWRIGHT_AUTH_BYPASS=true && NODE_ENV !== 'production'`
 *   - `meepleai_user_role` cookie — drives admin redirects (default `'user'`)
 *
 * **Defaults**: role `'user'` (non-admin) — admin redirects (`/admin → /` for non-admins)
 * are out of scope for visual specs of regular `(authenticated)` routes. Override only if
 * the spec specifically tests admin-only views.
 *
 * **Usage**:
 *   ```ts
 *   import { seedAuthSession } from '../_helpers/seedAuthSession';
 *   test('default state', async ({ page }) => {
 *     await seedAuthSession(page);
 *     await page.goto('/games?tab=library');
 *     // ...
 *   });
 *   ```
 *
 * Cookies are applied via `context.addCookies()` so they are present on the very first
 * request (before any redirect logic runs). Domain `localhost` matches the dev server
 * baked by `playwright.config.ts` webServer.
 *
 * Companion to `seedCookieConsent` (which seeds banner-dismissal localStorage).
 * For most `(authenticated)` specs, call BOTH helpers in sequence.
 */

import type { Page } from '@playwright/test';

const SESSION_COOKIE_NAME = 'meepleai_session';
const USER_ROLE_COOKIE = 'meepleai_user_role';

// Opaque test token — proxy never validates this when PLAYWRIGHT_AUTH_BYPASS=true.
// Value chosen to be obviously synthetic for triage runs.
const FIXTURE_SESSION_TOKEN = 'playwright-fixture-session-token';

export type AuthSessionRole = 'user' | 'admin';

export async function seedAuthSession(
  page: Page,
  options: { role?: AuthSessionRole } = {}
): Promise<void> {
  const role = options.role ?? 'user';
  await page.context().addCookies([
    {
      name: SESSION_COOKIE_NAME,
      value: FIXTURE_SESSION_TOKEN,
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
    {
      name: USER_ROLE_COOKIE,
      value: role,
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

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

import type { Page, Route } from '@playwright/test';

const SESSION_COOKIE_NAME = 'meepleai_session';
const USER_ROLE_COOKIE = 'meepleai_user_role';

// Opaque test token — proxy never validates this when PLAYWRIGHT_AUTH_BYPASS=true.
// Value chosen to be obviously synthetic for triage runs.
const FIXTURE_SESSION_TOKEN = 'playwright-fixture-session-token';

export type AuthSessionRole = 'user' | 'admin';

/** Role names used by the mock-auth helpers (AdminHelper, AuthHelper, fixtures). */
export type MockAuthRole = 'Admin' | 'Editor' | 'User';

/**
 * Builds the two cookies proxy.ts needs to resolve an authenticated session AND
 * its role under the E2E auth bypass. `role` must already be lower-cased to match
 * proxy.ts's isAdminRole() expectation.
 */
function buildBypassAuthCookies(role: string) {
  return [
    {
      name: SESSION_COOKIE_NAME,
      value: FIXTURE_SESSION_TOKEN,
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax' as const,
    },
    {
      name: USER_ROLE_COOKIE,
      value: role,
      domain: 'localhost',
      path: '/',
      httpOnly: false,
      secure: false,
      sameSite: 'Lax' as const,
    },
  ];
}

export async function seedAuthSession(
  page: Page,
  options: { role?: AuthSessionRole } = {}
): Promise<void> {
  const role = options.role ?? 'user';
  await page.context().addCookies(buildBypassAuthCookies(role));
}

/**
 * Seeds the session + role cookies for the PascalCase-role mock-auth helpers so
 * navigating to /admin/** actually resolves the admin role under the E2E bypass
 * (proxy.ts #2784), instead of the middleware falling back to 'user' and
 * redirecting away. Call BEFORE the first page.goto in a mock-auth helper.
 *
 * The meepleai_user_role cookie is honored by proxy.ts ONLY while the bypass is
 * engaged (dev/CI); production ignores it, so it cannot escalate privileges.
 */
export async function seedMockRoleCookies(page: Page, role: MockAuthRole): Promise<void> {
  await page.context().addCookies(buildBypassAuthCookies(role.toLowerCase()));
}

/**
 * Mocks client-side auth endpoints required to bypass `RequireRole` (and any
 * provider that calls `api.auth.getMe()` / `api.auth.getSessionStatus()`) in
 * the visual-test prod build where the backend at `:8080` is unreachable.
 *
 * **Why this is needed beyond `seedAuthSession`** (Wave B.1 lesson learned,
 * Issue #633):
 *   `seedAuthSession` only seeds cookies that satisfy the `proxy.ts` middleware
 *   gate (server-side redirect). It does NOT satisfy the React-side
 *   `RequireRole` / `AuthProvider` flows that issue `GET /api/v1/auth/me` on
 *   mount via `useEffect`. In the migrated visual-test build there is no
 *   backend; the request 401s (or hangs), and the React tree gets stuck on
 *   the "Verifica autorizzazioni…" spinner inside `<main>` — even though no
 *   direct grep on the route's component tree finds an explicit guard import.
 *
 * **Routes mocked**:
 *   - `GET /api/v1/auth/me` — returns a synthetic user DTO matching the shape
 *     consumed by `AuthProvider` (`{ user: {...} }`)
 *   - `GET /api/v1/auth/session/status` — returns a far-future expiry so the
 *     `useSessionCheck` 5-min polling hook does not auto-logout
 *
 * **Pattern**: same as `admin-mechanic-extractor-validation/load-existing-analysis.spec.ts`
 * which solved the identical "Verifica autorizzazioni…" hang when added.
 *
 * Regex (not glob `**`) is used because Playwright's `**` glob anchored to
 * scheme is unreliable across mixed absolute/relative request URLs. The `httpClient`
 * issues relative URLs in the browser, which go through Next.js proxy at
 * `localhost:3000`, NOT to `localhost:8080`. The host-agnostic regex matches both.
 */
export async function mockAuthEndpoints(
  page: Page,
  options: {
    role?: AuthSessionRole;
    userId?: string;
    email?: string;
    /**
     * Override the `user.onboardingCompleted` flag returned by
     * `GET /api/v1/auth/me`. Default `true` because most authenticated specs
     * want to land on their target route without bouncing through the
     * onboarding wizard. Specs that need to exercise `/onboarding` itself
     * (e.g. asse-d-p3) must pass `onboardingCompleted: false`, otherwise the
     * page redirects to `/library`.
     */
    onboardingCompleted?: boolean;
  } = {}
): Promise<void> {
  const role = options.role ?? 'user';
  const userId = options.userId ?? '00000000-0000-4000-8000-000000000fff';
  const email = options.email ?? 'fixture-user@meepleai.test';
  const onboardingCompleted = options.onboardingCompleted ?? true;

  const authMeDto = {
    user: {
      id: userId,
      email,
      role: role === 'admin' ? 'Admin' : 'User',
      displayName: 'Fixture User',
      onboardingCompleted,
      onboardingSkipped: false,
    },
  };

  await page.context().route(/\/api\/v1\/auth\/me(\?.*)?$/, async (route: Route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(authMeDto),
      });
      return;
    }
    await route.continue();
  });

  await page.context().route(/\/api\/v1\/auth\/session\/status(\?.*)?$/, async (route: Route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          expiresAt: '2099-12-31T23:59:59Z',
          lastSeenAt: '2026-04-30T12:00:00Z',
          remainingMinutes: 60,
        }),
      });
      return;
    }
    await route.continue();
  });
}

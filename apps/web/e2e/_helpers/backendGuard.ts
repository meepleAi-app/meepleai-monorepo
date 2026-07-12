import type { Page } from '@playwright/test';

/**
 * Real-backend reachability guard for admin E2E specs that perform a real UI
 * login (fill the /login form + submit against the API).
 *
 * Such specs cannot run under the local E2E auth bypass (`next dev`,
 * PLAYWRIGHT_AUTH_BYPASS=true) because there is no backend at :8080 to validate
 * the login — they hard-fail at the login step. This is a pre-existing
 * backend-dependency, distinct from the #2784 role-cookie regression (which is
 * fixed for mock-based specs via `seedMockRoleCookies`).
 *
 * Use in a spec's `test.beforeEach` BEFORE the login:
 *   test.beforeEach(async ({ page }) => {
 *     test.skip(!(await isBackendReachable(page)), NO_BACKEND_SKIP_REASON);
 *     // ...existing real login...
 *   });
 *
 * Locally (no backend) → probe fails → skip. In CI (real backend up) → probe
 * succeeds → the spec runs normally. See issue #2784.
 */
const API_HEALTH_URL = `${
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'
}/health/live`;

export const NO_BACKEND_SKIP_REASON =
  'Requires a real backend (real UI login) — skipped without one at :8080. #2784';

/**
 * Returns true only when the real backend `/health/live` endpoint responds OK.
 * Never throws: any network/timeout error resolves to false (treat as
 * unreachable → skip).
 */
export async function isBackendReachable(page: Page): Promise<boolean> {
  try {
    const res = await page.request.get(API_HEALTH_URL, { timeout: 3000 });
    return res.ok();
  } catch {
    return false;
  }
}

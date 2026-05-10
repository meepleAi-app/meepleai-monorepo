/**
 * Real-backend auth helper for smoke tests.
 *
 * The CI workflow seeds an initial admin user via API container env vars
 * INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD matching the SMOKE_USER_*
 * env vars below. We login directly with those credentials — no register
 * step needed (registration is fail-closed gated by Registration:PublicEnabled
 * config and we don't enable it in CI).
 *
 * Used by smoke-real-backend specs to detect endpoint binding regressions
 * (404, schema mismatch) that visual-test fixtures hide.
 */
import type { APIRequestContext, Page } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';
const SEED_EMAIL = process.env.SMOKE_USER_EMAIL ?? 'smoke-user@meepleai.test';
const SEED_PASSWORD = process.env.SMOKE_USER_PASSWORD ?? 'SmokeUser1!!';

export async function smokeLogin(request: APIRequestContext): Promise<{ cookieHeader: string }> {
  const res = await request.post(`${API_BASE}/api/v1/auth/login`, {
    data: { email: SEED_EMAIL, password: SEED_PASSWORD },
  });
  if (!res.ok()) {
    throw new Error(`smokeLogin failed ${res.status()} for ${SEED_EMAIL}`);
  }
  const cookies = res.headersArray().filter(h => h.name.toLowerCase() === 'set-cookie');
  const session = cookies.find(c => c.value.startsWith('meepleai_session='));
  if (!session) throw new Error('No meepleai_session cookie returned');
  return { cookieHeader: session.value.split(';')[0] };
}

export async function applySessionToPage(page: Page, cookieHeader: string): Promise<void> {
  const eq = cookieHeader.indexOf('=');
  const name = cookieHeader.slice(0, eq);
  const value = cookieHeader.slice(eq + 1);
  // Cookie cross-port (#960 H4): tests run frontend on :3000 + backend on :8080.
  // Using `url: API_BASE` binds the cookie to :8080 only — browser does NOT send
  // it during hydration fetches initiated by pages loaded from :3000. Switching
  // to `domain: 'localhost'` makes the cookie shared across all ports of the
  // host, matching how a real browser receives Set-Cookie from the backend
  // (where domain is the eTLD+1).
  await page.context().addCookies([
    {
      name,
      value,
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

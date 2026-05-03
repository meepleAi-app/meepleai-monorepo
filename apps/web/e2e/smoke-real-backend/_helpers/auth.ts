/**
 * Real-backend auth helper for smoke tests.
 *
 * Differs from visual-test fixtures: makes a REAL POST /api/v1/auth/register
 * (idempotent — ignores 409) + POST /api/v1/auth/login against backend at :8080
 * with a known smoke user. Cookie returned applies to subsequent page nav.
 *
 * Used by smoke-real-backend specs to detect endpoint binding regressions
 * (404, schema mismatch) that visual-test fixtures hide.
 */
import type { APIRequestContext, Page } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';
const SEED_EMAIL = process.env.SMOKE_USER_EMAIL ?? 'smoke-user@meepleai.test';
const SEED_PASSWORD = process.env.SMOKE_USER_PASSWORD ?? 'SmokeUser1!';

/**
 * Idempotent register — returns regardless of whether user exists.
 */
async function ensureUser(request: APIRequestContext): Promise<void> {
  const res = await request.post(`${API_BASE}/api/v1/auth/register`, {
    data: { email: SEED_EMAIL, password: SEED_PASSWORD, displayName: 'Smoke User' },
    failOnStatusCode: false,
  });
  // 200/201 OK or 409/422 already-exists — both acceptable.
  if (res.status() >= 500) {
    throw new Error(`smoke ensureUser failed ${res.status()} for ${SEED_EMAIL}`);
  }
}

export async function smokeLogin(request: APIRequestContext): Promise<{ cookieHeader: string }> {
  await ensureUser(request);
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
  await page.context().addCookies([
    {
      name,
      value,
      url: API_BASE,
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}

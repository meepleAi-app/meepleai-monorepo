/**
 * proxy.ts middleware — admin-role resolution under the E2E auth bypass (#2784)
 *
 * After the v1 plaintext role-cookie sunset (2026-05-13), proxy.ts resolved the
 * admin role only from the backend-validated session cache. Under the E2E auth
 * bypass (PLAYWRIGHT_AUTH_BYPASS=true) the cache is never warmed, so the role
 * fell back to 'user' and every admin E2E spec redirected away from /admin.
 *
 * These tests pin the fix: when the bypass is engaged, the role is resolved from
 * the plaintext meepleai_user_role cookie seeded by the E2E auth helpers — AND
 * that this bypass path is unreachable in production, so the cookie can never
 * escalate a real user to admin.
 */
import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const ADMIN_URL = 'http://localhost/admin';

function makeRequest(cookies: Record<string, string>): NextRequest {
  const cookieHeader = Object.entries(cookies)
    .map(([name, value]) => `${name}=${value}`)
    .join('; ');
  return new NextRequest(ADMIN_URL, {
    headers: cookieHeader ? { cookie: cookieHeader } : {},
  });
}

/** True when the middleware let the request continue (no redirect). */
function wasAllowed(res: Response): boolean {
  return res.headers.get('location') === null;
}

describe('proxy() admin-role resolution under the E2E auth bypass (#2784)', () => {
  beforeEach(() => {
    // Reset the module-level sessionValidationCache + cachedApiOrigin between tests.
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('grants /admin when the bypass is engaged and the role cookie is admin', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    expect(wasAllowed(res)).toBe(true);
    expect(res.status).toBe(200);
  });

  it('redirects /admin away when the bypass is engaged but the role cookie is user', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'user' })
    );

    // Authenticated non-admin → redirected to home (not admin).
    expect(res.headers.get('location')).toBe('http://localhost/');
  });

  it('redirects /admin away when the bypass is engaged but no role cookie is present', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');

    const { proxy } = await import('@/proxy');
    const res = await proxy(makeRequest({ meepleai_session: 'fixture-token' }));

    // Never elevate by guessing: default role 'user' → redirected away.
    expect(res.headers.get('location')).toBe('http://localhost/');
  });

  it('does NOT trust the role cookie in production — role is sourced only from /auth/me', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    // Bypass flag is set but MUST be ignored: production build, visual flag off.
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');
    vi.stubEnv('API_BASE_URL', 'http://api:8080');

    // /auth/me reports the real role as 'user'; the admin role COOKIE must not win.
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ user: { role: 'user' } }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'real-token', meepleai_user_role: 'admin' })
    );

    // Cookie escalation blocked: authenticated non-admin → redirected away from /admin.
    expect(res.headers.get('location')).toBe('http://localhost/');
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it('does not engage the bypass without PLAYWRIGHT_AUTH_BYPASS even with an admin role cookie', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', '');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');
    vi.stubEnv('API_BASE_URL', 'http://api:8080');

    // No bypass → real session validation runs; backend rejects the fixture token.
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({}),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    // Unauthenticated on a protected route → redirected to /login (not admin).
    expect(res.headers.get('location')).toContain('/login');
  });
});

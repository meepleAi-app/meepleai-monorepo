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

/**
 * #3498 — this middleware emits its own CSP alongside the one from next.config.js, and a browser
 * enforces the INTERSECTION of every policy it receives. The cover R2-strict E2E burned a full CI
 * run on exactly that: next.config.js had already been widened, this header had not, so the MinIO
 * presigned cover stayed blocked and the card fell back to its emoji placeholder — a symptom
 * indistinguishable from a missing object. These tests pin both directions.
 */
describe('proxy() CSP img-src — MinIO presign opt-in (#3498)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  /** Returns the `img-src` directive of the response's CSP header. */
  async function imgSrcDirective(optIn: string | undefined): Promise<string> {
    vi.stubEnv('NODE_ENV', 'development');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');
    vi.stubEnv('NEXT_PUBLIC_CSP_ALLOW_LOCAL_BLOB', optIn ?? '');

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    const csp = res.headers.get('content-security-policy') ?? '';
    return csp.split('; ').find(directive => directive.startsWith('img-src')) ?? '';
  }

  it('widens img-src to the MinIO presign host when the opt-in is on', async () => {
    expect(await imgSrcDirective('true')).toBe("img-src 'self' data: https: http://localhost:9000");
  });

  it.each([
    ['unset', undefined],
    ['false', 'false'],
    // Only the literal "true" opts in — anything else keeps the closed default, so a stray or
    // half-configured value can never widen the policy in prod.
    ['a non-literal truthy value', '1'],
  ])('keeps the closed default when the opt-in is %s', async (_label, value) => {
    expect(await imgSrcDirective(value)).toBe("img-src 'self' data: https:");
  });
});

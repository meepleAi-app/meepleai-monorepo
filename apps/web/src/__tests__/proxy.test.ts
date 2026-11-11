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
 * that under NODE_ENV=production this path stays closed unless an allow variable
 * is set (see the #4980 block below), so the cookie can never escalate a real
 * user to admin.
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
    const mockFetch = vi.fn();
    vi.stubGlobal('fetch', mockFetch);

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    expect(wasAllowed(res)).toBe(true);
    expect(res.status).toBe(200);
    // The seeded role cookie answers the question: no backend round-trip.
    expect(mockFetch).not.toHaveBeenCalled();
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
    vi.stubEnv('API_BASE_URL', 'http://api:8080');
    // No role cookie → the role is asked to /auth/me (#4980), which rejects a fake token.
    const mockFetch = vi.fn().mockResolvedValue({ ok: false, status: 401, json: async () => ({}) });
    vi.stubGlobal('fetch', mockFetch);

    const { proxy } = await import('@/proxy');
    const res = await proxy(makeRequest({ meepleai_session: 'fixture-token' }));

    // Never elevate by guessing: default role 'user' → redirected away.
    expect(res.headers.get('location')).toBe('http://localhost/');
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it('does NOT trust the role cookie in production — role is sourced only from /auth/me', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    // Bypass flag is set but MUST be ignored: production server, no allow variable.
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION', '');
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
 * #4980 — `test-e2e.yml` serves the suite with `next start`, where NODE_ENV is 'production', so
 * the bypass stayed off and every spec that seeds a fake session cookie landed on /login (263 of
 * the 267 failures in shard 3 of PR #4897). Under NODE_ENV=production the bypass is allowed only
 * when an allow variable says so — read at runtime, see the comment in proxy.ts. These tests pin
 * both directions for each allow variable: it opens the bypass, and nothing else does.
 */
describe('proxy() — what allows the E2E auth bypass under NODE_ENV=production (#4980)', () => {
  const ALLOW_VARS = [
    ['PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION', 'true'],
    ['NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED (#960)', '1'],
  ] as const;
  const allowVarName = (label: string) => label.split(' ')[0];

  /** /auth/me as a real backend answers a fake cookie: 401. */
  function backendRejectsFakeCookie() {
    const mockFetch = vi.fn().mockResolvedValue({ ok: false, status: 401, json: async () => ({}) });
    vi.stubGlobal('fetch', mockFetch);
    return mockFetch;
  }

  /** A `next start` server: NODE_ENV=production, and only the given allow variable set. */
  function productionServer(allow: Record<string, string> = {}) {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('API_BASE_URL', 'http://api:8080');
    for (const [label] of ALLOW_VARS) vi.stubEnv(allowVarName(label), '');
    for (const [name, value] of Object.entries(allow)) vi.stubEnv(name, value);
  }

  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it.each(ALLOW_VARS)(
    'with %s=%s, trusts the fake cookie and its admin role without asking the backend',
    async (label, value) => {
      productionServer({ [allowVarName(label)]: value });
      vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
      const mockFetch = backendRejectsFakeCookie();

      const { proxy } = await import('@/proxy');
      const res = await proxy(
        makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
      );

      expect(wasAllowed(res)).toBe(true);
      expect(mockFetch).not.toHaveBeenCalled();
    }
  );

  it('without an allow variable, sends the fake cookie to the backend and redirects to /login', async () => {
    productionServer();
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    const mockFetch = backendRejectsFakeCookie();

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    expect(res.headers.get('location')).toBe('http://localhost/login?from=%2Fadmin');
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it.each(ALLOW_VARS)(
    'with %s=%s but without PLAYWRIGHT_AUTH_BYPASS, still validates against the backend',
    async (label, value) => {
      productionServer({ [allowVarName(label)]: value });
      vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', '');
      const mockFetch = backendRejectsFakeCookie();

      const { proxy } = await import('@/proxy');
      const res = await proxy(
        makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
      );

      expect(res.headers.get('location')).toBe('http://localhost/login?from=%2Fadmin');
      expect(mockFetch).toHaveBeenCalledTimes(1);
    }
  );

  it('with the bypass open, still asks /auth/me for the role of a real login (no role cookie)', async () => {
    // Specs that log in for real get only the HMAC v2 role cookie from the backend: the role
    // must come from /auth/me, or an admin who logged in for real is sent away from /admin.
    productionServer({ PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION: 'true' });
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ user: { role: 'Admin' } }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'real-token', meepleai_user_role_v2: 'opaque-hmac' })
    );

    expect(wasAllowed(res)).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION', '1'],
    ['PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION', 'TRUE'],
    ['PLAYWRIGHT_AUTH_BYPASS_ALLOW_PRODUCTION', 'yes'],
    ['NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', 'true'],
  ])('treats %s=%s as off: only the documented literal allows the bypass', async (name, value) => {
    productionServer({ [name]: value });
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', 'true');
    backendRejectsFakeCookie();

    const { proxy } = await import('@/proxy');
    const res = await proxy(
      makeRequest({ meepleai_session: 'fixture-token', meepleai_user_role: 'admin' })
    );

    expect(res.headers.get('location')).toBe('http://localhost/login?from=%2Fadmin');
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

// #4985: an unverified user past the grace period logged in (200), /auth/me answered 403 with the
// reason, and the proxy read it as "no session": back to /login with no message. The login button
// looked dead.
describe('proxy() — session blocked by email verification (#4985)', () => {
  const LIBRARY_URL = 'http://localhost/library';
  const EMAIL = 'test@meepleai.com';

  function libraryRequest(): NextRequest {
    return new NextRequest(LIBRARY_URL, { headers: { cookie: 'meepleai_session=real-token' } });
  }

  function meAnswers(...responses: Array<{ status: number; body: unknown }>) {
    const mockFetch = vi.fn();
    for (const { status, body } of responses) {
      mockFetch.mockResolvedValueOnce({
        ok: status >= 200 && status < 300,
        status,
        json: async () => body,
      });
    }
    vi.stubGlobal('fetch', mockFetch);
    return mockFetch;
  }

  const verificationRequired = {
    status: 403,
    body: {
      error: 'Email verification required',
      message: 'Please verify your email address to access this feature.',
      verificationStatus: {
        isVerified: false,
        emailAddress: EMAIL,
        gracePeriodEnded: null,
        resendEndpoint: '/api/v1/auth/email/resend',
      },
    },
  };

  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('PLAYWRIGHT_AUTH_BYPASS', '');
    vi.stubEnv('NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED', '');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it('sends the user to /verification-pending with their email, not to /login', async () => {
    meAnswers(verificationRequired);

    const { proxy } = await import('@/proxy');
    const res = await proxy(libraryRequest());

    expect(res.headers.get('location')).toBe(
      `http://localhost/verification-pending?email=${encodeURIComponent(EMAIL)}`
    );
  });

  it('does not cache the blocked state: once verified, the next request goes through', async () => {
    const mockFetch = meAnswers(verificationRequired, {
      status: 200,
      body: { user: { id: 'u1', email: EMAIL, role: 'user' } },
    });

    const { proxy } = await import('@/proxy');
    await proxy(libraryRequest());
    const res = await proxy(libraryRequest());

    expect(res.headers.get('location')).toBeNull();
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['a 401', { status: 401, body: {} }],
    ['a 403 without verificationStatus', { status: 403, body: { error: 'Forbidden' } }],
  ])('keeps sending %s to /login', async (_label, response) => {
    meAnswers(response);

    const { proxy } = await import('@/proxy');
    const res = await proxy(libraryRequest());

    expect(res.headers.get('location')).toBe('http://localhost/login?from=%2Flibrary');
  });
});

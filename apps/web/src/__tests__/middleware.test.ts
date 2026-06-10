/**
 * Middleware unit tests (#2118).
 *
 * Validate the auth gate behaviour without spinning up a full Next.js runtime:
 *  - public paths pass through regardless of session presence
 *  - authenticated paths require the `meepleai_session` cookie
 *  - missing cookie → redirect to `/login?redirect=<original>`
 *  - search params are preserved across the redirect
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { middleware } from '../middleware';

// We cannot import the real `NextRequest` class in jsdom (it relies on a
// Node-only `Request` extension), so we hand-roll a minimal shape that
// matches what the middleware actually reads.
interface FakeNextRequest {
  nextUrl: {
    pathname: string;
    search: string;
  };
  cookies: {
    has: (name: string) => boolean;
  };
  url: string;
}

function makeRequest(pathname: string, options: { search?: string; cookies?: string[] } = {}) {
  const { search = '', cookies = [] } = options;
  return {
    nextUrl: { pathname, search },
    cookies: { has: (name: string) => cookies.includes(name) },
    url: `http://localhost:3000${pathname}${search}`,
  } as unknown as FakeNextRequest;
}

// Patch `next/server` so we can detect `next()` vs. `redirect()` without
// hitting the real Edge runtime.
vi.mock('next/server', async () => {
  const NextResponse = {
    next: vi.fn(() => ({ type: 'next' as const })),
    redirect: vi.fn((url: URL) => ({ type: 'redirect' as const, url })),
  };
  return { NextResponse };
});

describe('middleware (#2118)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('public paths pass through', () => {
    const publicPaths: ReadonlyArray<string> = [
      '/',
      '/login',
      '/register',
      '/reset-password',
      '/verify-email',
      '/oauth-callback',
      '/about',
      '/contact',
      '/pricing',
      '/faq',
      '/how-it-works',
      '/shared-games',
      '/shared-games/some-id',
      '/library/shared/some-token',
      '/join',
      '/join/session/code123',
      '/invites/token123',
      '/accept-invite',
      '/offline',
    ];

    it.each(publicPaths)('%s without session → next()', pathname => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const result = middleware(makeRequest(pathname) as any) as { type: string };
      expect(result.type).toBe('next');
    });

    it.each(publicPaths)('%s WITH session → next() (no double-redirect)', pathname => {
      const result = middleware(
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        makeRequest(pathname, { cookies: ['meepleai_session'] }) as any
      ) as { type: string };
      expect(result.type).toBe('next');
    });
  });

  describe('authenticated paths', () => {
    const authPaths: ReadonlyArray<string> = [
      '/dashboard',
      '/hub',
      '/hub/games',
      '/hub/games/some-id',
      '/hub/agents',
      '/hub/toolkits',
      '/library',
      '/library/some-game-id',
      '/sessions',
      '/agents',
      '/profile',
      '/chat',
      '/game-nights',
      '/notifications',
      '/discover',
    ];

    it.each(authPaths)('%s with session → next()', pathname => {
      const result = middleware(
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        makeRequest(pathname, { cookies: ['meepleai_session'] }) as any
      ) as { type: string };
      expect(result.type).toBe('next');
    });

    it.each(authPaths)('%s without session → redirect to /login', pathname => {
      const result = middleware(
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        makeRequest(pathname) as any
      ) as { type: string; url: URL };
      expect(result.type).toBe('redirect');
      expect(result.url.pathname).toBe('/login');
      expect(result.url.searchParams.get('redirect')).toBe(pathname);
    });
  });

  it('preserves search params in the redirect target', () => {
    const result = middleware(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      makeRequest('/hub/games', { search: '?tab=featured&q=catan' }) as any
    ) as { type: string; url: URL };
    expect(result.type).toBe('redirect');
    expect(result.url.searchParams.get('redirect')).toBe('/hub/games?tab=featured&q=catan');
  });

  it('does NOT treat /login_attempt as public (prefix match must require a trailing slash)', () => {
    // Guard against a regression where `/login_attempt` would be public just
    // because it starts with `/login`.
    const result = middleware(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      makeRequest('/login_attempt') as any
    ) as { type: string; url?: URL };
    expect(result.type).toBe('redirect');
  });

  it('does NOT pass through `/` prefix to every URL (root match must be exact)', () => {
    // `'/' !== prefix` guard ensures `/anything` is not auto-public.
    const result = middleware(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      makeRequest('/dashboard') as any
    ) as { type: string; url?: URL };
    expect(result.type).toBe('redirect');
  });
});

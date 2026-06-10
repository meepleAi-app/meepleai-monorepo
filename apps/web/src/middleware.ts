/**
 * Next.js middleware — auth gate for the `(authenticated)` and `(chat)` route
 * groups (#2118).
 *
 * Before this middleware existed, the `(authenticated)` layout (`UserShell`)
 * had no auth check. Anonymous visitors who hit `/hub/games`, `/dashboard`,
 * `/library`, etc. would see the authenticated chrome with an empty session,
 * relying on individual page components to handle the redirect. That worked
 * by coincidence (most data hooks 401 → redirect) but produced a flash of
 * broken chrome and inconsistent UX.
 *
 * Policy: whitelist-based. Anything NOT matched as public requires the
 * `meepleai_session` cookie. Cookie presence is checked here (cheap); full
 * session validation happens server-side via `getServerUser()` (see
 * `apps/web/src/lib/auth/server.ts`).
 */

import { NextResponse, type NextRequest } from 'next/server';

/** Matches `proxy.ts`, `lib/auth/server.ts`, and backend `CookieHelpers.cs`. */
const SESSION_COOKIE_NAME = 'meepleai_session';

/**
 * Path prefixes that DO NOT require auth. A pathname is public when it equals
 * one of these entries OR starts with `<entry>/`. The `/` root is treated as
 * exact-only (otherwise every path would match).
 */
const PUBLIC_PATH_PREFIXES: readonly string[] = [
  // Welcome / landing
  '/',
  // Auth flows — must be reachable without a session by definition
  '/login',
  '/register',
  '/reset-password',
  '/verify-email',
  '/verification-pending',
  '/verification-success',
  '/invitation-expired',
  '/welcome',
  '/oauth-callback',
  '/setup-account',
  // Marketing & legal
  '/about',
  '/contact',
  '/pricing',
  '/legal',
  '/terms',
  '/privacy',
  '/cookies',
  '/cookie-settings',
  // Help
  '/faq',
  '/how-it-works',
  // Public catalog (SSR / SEO) — anon mirror of the auth-gated `/hub/games`
  '/shared-games',
  // Token-based share & invite flows
  '/library/shared',
  '/join',
  '/invites',
  '/accept-invite',
  // PWA offline shell
  '/offline',
  // Dev playgrounds (unreachable in production builds; included to avoid
  // surprise redirects during local development)
  '/dev',
];

function isPublicPath(pathname: string): boolean {
  for (const prefix of PUBLIC_PATH_PREFIXES) {
    if (pathname === prefix) return true;
    if (prefix !== '/' && pathname.startsWith(`${prefix}/`)) return true;
  }
  return false;
}

export function middleware(req: NextRequest) {
  const { pathname, search } = req.nextUrl;

  if (isPublicPath(pathname)) return NextResponse.next();

  if (req.cookies.has(SESSION_COOKIE_NAME)) return NextResponse.next();

  const loginUrl = new URL('/login', req.url);
  loginUrl.searchParams.set('redirect', pathname + search);
  return NextResponse.redirect(loginUrl);
}

export const config = {
  // Skip middleware on:
  //   - API routes (auth enforced by backend per-endpoint)
  //   - Next.js internals (`_next/static`, `_next/image`, `_next/data`)
  //   - Static asset shortcuts (favicon, manifest, robots, sitemap, service worker)
  //   - Common image / font / asset extensions
  matcher: [
    '/((?!api|_next/static|_next/image|_next/data|favicon\\.ico|manifest\\.json|robots\\.txt|sitemap\\.xml|sw\\.js|workbox-.*\\.js|.*\\.(?:png|jpg|jpeg|gif|svg|webp|ico|css|js|woff|woff2|ttf|otf|map)).*)',
  ],
};

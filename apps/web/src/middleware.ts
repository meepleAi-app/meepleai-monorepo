import { NextResponse } from 'next/server';

import type { NextRequest } from 'next/server';

/**
 * Next.js Middleware — Onboarding Guard
 * Issue #325: Redirect users who haven't completed onboarding.
 *
 * Uses a cookie set by the auth provider when session data includes
 * onboardingCompleted=false. The cookie avoids per-request API calls.
 *
 * Cookie lifecycle:
 * - Set by frontend auth provider after session validation
 * - Cleared on logout or when onboarding is completed
 */

const PUBLIC_PATHS = new Set([
  '/',
  '/login',
  '/register',
  '/forgot-password',
  '/reset-password',
  '/games',
  '/accept-invite',
  '/verify-email',
]);

const EXCLUDED_PREFIXES = [
  '/api/',
  '/_next/',
  '/static/',
  '/favicon',
  '/og-image',
  '/twitter-card',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Skip excluded prefixes
  if (EXCLUDED_PREFIXES.some(prefix => pathname.startsWith(prefix))) {
    return NextResponse.next();
  }

  // Skip public paths (exact match)
  if (PUBLIC_PATHS.has(pathname)) {
    return NextResponse.next();
  }

  // Check for onboarding status cookie
  // Set to 'false' by auth provider when user needs onboarding
  const onboardingComplete = request.cookies.get('onboarding_completed')?.value;

  if (onboardingComplete === 'false') {
    const onboardingUrl = new URL('/accept-invite', request.url);
    onboardingUrl.searchParams.set('redirect', pathname);
    return NextResponse.redirect(onboardingUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};

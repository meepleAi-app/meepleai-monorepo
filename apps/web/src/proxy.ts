/**
 * Next.js 16 Proxy - Authentication Gating
 *
 * Issue #1080: FE-IMP-004 — AuthContext + Edge Middleware
 *
 * Provides server-side authentication checks and redirects for protected routes.
 * This prevents flash of unauthorized content by checking authentication
 * status before rendering pages.
 *
 * Protected Routes:
 * - /library - Game library (post-login default)
 * - /chat - Main chat interface
 * - /upload - Document upload page
 * - /admin - Administration panel
 * - /editor - Editor interface
 * - /settings - User settings
 *
 * Public Routes:
 * - / - Home page
 * - /login - Login page
 * - /register - Registration page
 *
 * Cookie-Based Auth:
 * The proxy checks for the presence of authentication session cookies
 * that are set by the backend API after successful login.
 */

import { NextResponse } from 'next/server';

import * as metrics from '@/lib/metrics/session-cache-metrics';
import { isAdminRole } from '@/lib/utils/roles';

import type { NextRequest } from 'next/server';

// Issue #3797: Import metrics manager (works in Edge Runtime)

// ============================================================================
// Configuration
// ============================================================================

/**
 * Protected routes that require authentication
 * Unauthenticated users will be redirected to /login
 */
const PROTECTED_ROUTES = [
  '/library',
  '/chat',
  '/upload',
  '/admin',
  '/editor',
  '/settings',
  '/games',
  '/agents',
];

/**
 * Admin-only routes that require admin role
 */
const ADMIN_ONLY_ROUTES = ['/admin'];

/**
 * Public routes that don't require authentication
 * Authenticated users will be redirected to /chat
 */
const PUBLIC_AUTH_ROUTES = ['/login', '/register'];

/**
 * Session cookie name used by the backend API
 * This matches the cookie name set in CookieHelpers.cs (default: "meepleai_session")
 */
const SESSION_COOKIE_NAME = 'meepleai_session';

/**
 * User role cookie name
 * Used to check authorization for protected routes
 */
const USER_ROLE_COOKIE = 'meepleai_user_role';

// ============================================================================
// Proxy Function
// ============================================================================

// Cache API origin at module level for performance (only computed once)
let cachedApiOrigin: string | null = null;

// Simple session validation cache so we don't call the API on every request
type SessionValidationEntry = {
  valid: boolean;
  expiresAt: number;
};

const SESSION_CACHE_TTL_MS = 120 * 1000; // 2 minutes (optimized for performance)
const SESSION_CACHE_LIMIT = 200;
const sessionValidationCache = new Map<string, SessionValidationEntry>();

function cacheSessionValidation(cookieValue: string, valid: boolean) {
  if (!cookieValue) return;

  sessionValidationCache.set(cookieValue, {
    valid,
    expiresAt: Date.now() + SESSION_CACHE_TTL_MS,
  });

  if (sessionValidationCache.size > SESSION_CACHE_LIMIT) {
    const oldestKey = sessionValidationCache.keys().next().value;
    if (oldestKey) {
      sessionValidationCache.delete(oldestKey);
    }
  }
}

async function isSessionCookieValid(request: NextRequest, cookieValue: string): Promise<boolean> {
  const cached = sessionValidationCache.get(cookieValue);
  if (cached && cached.expiresAt > Date.now()) {
    // Metrics: Cache hit
    metrics.recordCacheHit();

    // Debug logging for session validation (use warn which is allowed by ESLint)
    // eslint-disable-next-line no-console
    console.log(
      `[proxy] Session validation CACHE HIT for ${cookieValue.substring(0, 10)}... valid=${cached.valid}`
    );
    return cached.valid;
  }

  // Metrics: Cache miss
  metrics.recordCacheMiss();

  const cookieHeader = request.headers.get('cookie');
  if (!cookieHeader) {
    // eslint-disable-next-line no-console
    console.log('[proxy] No cookie header found in request');
    cacheSessionValidation(cookieValue, false);
    return false;
  }

  try {
    // Use first API origin (server-side Docker network URL)
    const apiOrigins = getApiOrigins();
    const apiUrl = `${apiOrigins[0]}/api/v1/auth/me`;

    // Issue #3797: Add AbortController with 5s timeout to prevent indefinite hangs
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000);

    // eslint-disable-next-line no-console
    console.log(
      `[proxy] Validating session at ${apiUrl} with cookie: ${cookieHeader.substring(0, 50)}...`
    );

    try {
      const response = await fetch(apiUrl, {
        headers: {
          cookie: cookieHeader,
        },
        credentials: 'include',
        cache: 'no-store',
        signal: controller.signal, // Add abort signal for timeout
      });

      clearTimeout(timeoutId); // Clear timeout on success

      // Metrics: Validation success or failure
      if (response.ok) {
        metrics.recordValidationSuccess();
      } else {
        metrics.recordValidationFailure();
      }

      // eslint-disable-next-line no-console
      console.log(`[proxy] Session validation response: ${response.status} ok=${response.ok}`);
      cacheSessionValidation(cookieValue, response.ok);
      return response.ok;
    } catch (fetchError) {
      clearTimeout(timeoutId);

      // Metrics: Track timeout vs other errors
      if (fetchError instanceof Error && fetchError.name === 'AbortError') {
        metrics.recordValidationTimeout();
        console.error('[proxy] Session validation TIMEOUT after 5s');
      } else {
        metrics.recordValidationFailure();
        console.error('[proxy] Session validation fetch error:', fetchError);
      }

      cacheSessionValidation(cookieValue, false);
      return false;
    }
  } catch (error) {
    console.error('[proxy] Failed to validate session cookie:', error);
    cacheSessionValidation(cookieValue, false);
    return false;
  }
}

/**
 * Get API base URL origins for CSP
 * Returns array of unique origins that need to be allowed in connect-src
 * Results are cached for performance
 *
 * Uses API_BASE_URL for server-side (Docker service name like http://api:8080)
 * and NEXT_PUBLIC_API_BASE for client-side (http://localhost:8080)
 *
 * Issue: Client-side JavaScript may use NEXT_PUBLIC_API_BASE while proxy
 * uses API_BASE_URL - CSP must allow both origins
 */
function getApiOrigins(): string[] {
  if (cachedApiOrigin !== null) {
    // Return cached result split by space (may contain multiple origins)
    return cachedApiOrigin.split(' ').filter(Boolean);
  }

  const origins = new Set<string>();

  // Server-side API base (Docker service name)
  const serverApiBase = process.env.API_BASE_URL?.trim();
  if (serverApiBase) {
    try {
      origins.add(new URL(serverApiBase).origin);
    } catch {
      // Ignore invalid URLs
    }
  }

  // Client-side API base (what JavaScript in browser will use)
  const clientApiBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (clientApiBase) {
    try {
      origins.add(new URL(clientApiBase).origin);
    } catch {
      // Ignore invalid URLs
    }
  }

  // Always include localhost:8080 as fallback for development
  origins.add('http://localhost:8080');

  // Cache as space-separated string
  cachedApiOrigin = Array.from(origins).join(' ');
  return Array.from(origins);
}

/**
 * Security headers configuration
 * Applied to all responses for defense-in-depth protection
 *
 * See: docs/06-security/client-side-encryption.md
 *
 * @param requestOrigin - The origin of the incoming request (for CSP deduplication)
 */
function getSecurityHeaders(requestOrigin?: string) {
  const apiOrigins = getApiOrigins();

  // Build connect-src with all API origins that differ from request origin
  // This ensures both server-side (Docker) and client-side origins are allowed
  const connectSrcParts = ["'self'"];
  for (const apiOrigin of apiOrigins) {
    if (!requestOrigin || apiOrigin !== requestOrigin) {
      connectSrcParts.push(apiOrigin);
    }
  }
  return {
    // Content Security Policy - XSS protection
    'Content-Security-Policy': [
      "default-src 'self'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval'", // Required for Next.js hydration
      "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com", // Required for Tailwind CSS + Google Fonts
      "img-src 'self' data: https:", // Allow images from data URIs and HTTPS
      "font-src 'self' data: https://fonts.gstatic.com", // Allow Google Fonts files
      `connect-src ${connectSrcParts.join(' ')} https://fonts.googleapis.com https://fonts.gstatic.com`, // Allow API + Google Fonts
      "worker-src 'self' blob:", // Required for PDF.js web workers
      "frame-ancestors 'none'", // Prevent clickjacking
      "base-uri 'self'",
      "form-action 'self'",
    ].join('; '),

    // Prevent MIME sniffing
    'X-Content-Type-Options': 'nosniff',

    // Clickjacking protection
    'X-Frame-Options': 'DENY',

    // XSS filter for legacy browsers
    'X-XSS-Protection': '1; mode=block',

    // Referrer policy
    'Referrer-Policy': 'strict-origin-when-cross-origin',

    // Permissions policy
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=(), payment=(), usb=()',
  };
}

/**
 * Add security headers to the response
 * @param response - The response to add headers to
 * @param requestOrigin - Optional request origin for CSP deduplication
 */
function addSecurityHeaders(response: NextResponse, requestOrigin?: string): NextResponse {
  const headers = getSecurityHeaders(requestOrigin);
  Object.entries(headers).forEach(([key, value]) => {
    response.headers.set(key, value);
  });
  return response;
}

/**
 * Proxy function that runs on every request
 * Checks authentication status and redirects as needed
 */
export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const requestOrigin = request.nextUrl.origin;
  const hostname = request.nextUrl.hostname;

  // ============================================================================
  // Domain Normalization: Redirect 127.0.0.1 to localhost
  // ============================================================================
  // Cookies are domain-specific. If the backend sets cookies for 'localhost',
  // they won't be sent when accessing via '127.0.0.1'. This redirect ensures
  // consistent cookie handling by normalizing all requests to use 'localhost'.
  if (hostname === '127.0.0.1') {
    const normalizedUrl = new URL(request.url);
    normalizedUrl.hostname = 'localhost';
    return NextResponse.redirect(normalizedUrl);
  }

  // Check if user has a session cookie
  const sessionCookie = request.cookies.get(SESSION_COOKIE_NAME);
  const sessionCookieValue = sessionCookie?.value;
  let isAuthenticated = false;

  // ============================================================================
  // E2E Test Auth Bypass (development only)
  // ============================================================================
  // When PLAYWRIGHT_AUTH_BYPASS=true (dev only), trust session cookies without
  // server-side backend validation. This allows Playwright E2E tests to mock
  // auth at the browser level via page.route() without needing a running backend.
  // Safety: Guarded by NODE_ENV !== 'production' - cannot activate in production.
  if (
    process.env.NODE_ENV !== 'production' &&
    process.env.PLAYWRIGHT_AUTH_BYPASS === 'true' &&
    sessionCookieValue
  ) {
    isAuthenticated = true;
  } else if (sessionCookieValue) {
    isAuthenticated = await isSessionCookieValid(request, sessionCookieValue);
  }

  // Check user role (only trusted when we know the session is valid)
  const userRoleCookie = request.cookies.get(USER_ROLE_COOKIE);
  const userRole = isAuthenticated ? userRoleCookie?.value || 'user' : 'user';
  const isAdmin = isAuthenticated && isAdminRole(userRole);

  // Check if the current route is protected or public auth route
  const isProtectedRoute = PROTECTED_ROUTES.some(route => pathname.startsWith(route));
  const isPublicAuthRoute = PUBLIC_AUTH_ROUTES.some(route => pathname === route);
  const isAdminRoute = ADMIN_ONLY_ROUTES.some(route => pathname.startsWith(route));
  const isHomePage = pathname === '/';

  // Redirect unauthenticated users from protected routes to login
  if (isProtectedRoute && !isAuthenticated) {
    const loginUrl = new URL('/login', request.url);
    // Preserve the intended destination for redirect after login
    loginUrl.searchParams.set('from', pathname);
    const response = NextResponse.redirect(loginUrl);
    return addSecurityHeaders(response, requestOrigin);
  }

  // Redirect non-admin users from admin routes
  if (isAdminRoute && isAuthenticated && !isAdmin) {
    const homeUrl = new URL('/', request.url);
    const response = NextResponse.redirect(homeUrl);
    return addSecurityHeaders(response, requestOrigin);
  }

  // Redirect authenticated users from login/register pages to dashboard
  if (isPublicAuthRoute && isAuthenticated) {
    // Check if there's a 'from' parameter to redirect back to
    const fromParam = request.nextUrl.searchParams.get('from');
    const defaultDest = isAdmin ? '/admin' : '/library';
    const redirectUrl =
      fromParam && PROTECTED_ROUTES.some(route => fromParam.startsWith(route))
        ? new URL(fromParam, request.url)
        : new URL(defaultDest, request.url);
    const response = NextResponse.redirect(redirectUrl);
    return addSecurityHeaders(response, requestOrigin);
  }

  // Redirect authenticated users from homepage to dashboard
  if (isHomePage && isAuthenticated) {
    const defaultDest = isAdmin ? '/admin' : '/library';
    const response = NextResponse.redirect(new URL(defaultDest, request.url));
    return addSecurityHeaders(response, requestOrigin);
  }

  // ============================================================================
  // Alpha Zero Route Guard
  // ============================================================================
  // When NEXT_PUBLIC_ALPHA_MODE=true, only alpha-allowed routes are accessible.
  // Non-alpha routes redirect to /dashboard. This prevents users from reaching
  // features that are not yet ready for alpha testing.
  const IS_ALPHA_MODE = process.env.NEXT_PUBLIC_ALPHA_MODE === 'true';

  if (IS_ALPHA_MODE && isAuthenticated) {
    const ALPHA_ROUTE_PREFIXES = [
      '/dashboard',
      '/library',
      '/chat',
      '/discover',
      '/games',
      '/profile',
      '/settings',
      '/admin', // admin root redirects to /admin/overview via page.tsx
      '/admin/overview',
      '/admin/shared-games',
      '/admin/knowledge-base',
      '/admin/content',
      '/admin/users',
      '/onboarding',
      '/upload',
      '/setup',
      '/join',
      '/agents',
    ];

    const isAlphaRoute = ALPHA_ROUTE_PREFIXES.some(prefix => pathname.startsWith(prefix));
    const isAuthRoute =
      pathname.startsWith('/login') ||
      pathname.startsWith('/register') ||
      pathname.startsWith('/reset-password') ||
      pathname.startsWith('/verify-email') ||
      pathname.startsWith('/oauth-callback') ||
      pathname.startsWith('/setup-account') ||
      pathname.startsWith('/welcome');
    const isPublicRoute =
      pathname === '/' ||
      pathname.startsWith('/api/') ||
      pathname.startsWith('/health') ||
      pathname.startsWith('/offline') ||
      pathname.startsWith('/_next/');

    if (!isAlphaRoute && !isAuthRoute && !isPublicRoute) {
      const response = NextResponse.redirect(new URL('/dashboard', request.url));
      return addSecurityHeaders(response, requestOrigin);
    }
  }

  // Allow the request to continue with security headers
  const response = NextResponse.next();
  return addSecurityHeaders(response, requestOrigin);
}

// ============================================================================
// Proxy Configuration
// ============================================================================

/**
 * Configure which routes the proxy should run on
 *
 * Matcher patterns:
 * - Include: Protected routes and public auth routes
 * - Exclude: API routes, static files, and Next.js internals
 */
export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - api routes (/api/*)
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - favicon.ico
     * - public folder files (*.png, *.jpg, *.svg, etc.)
     */
    '/((?!api|_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)',
  ],
};

/**
 * Next.js 15 Middleware - Authentication Gating
 *
 * Issue #1080: FE-IMP-004 — AuthContext + Edge Middleware
 *
 * Provides server-side authentication checks and redirects for protected routes.
 * This prevents flash of unauthorized content by checking authentication
 * status before rendering pages.
 *
 * Protected Routes:
 * - /dashboard - Main dashboard (post-login default)
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
 * The middleware checks for the presence of authentication session cookies
 * that are set by the backend API after successful login.
 */

import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

// ============================================================================
// Configuration
// ============================================================================

/**
 * Protected routes that require authentication
 * Unauthenticated users will be redirected to /login
 */
const PROTECTED_ROUTES = ['/dashboard', '/chat', '/upload', '/admin', '/editor', '/settings'];

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
// Middleware Function
// ============================================================================

// Cache API origin at module level for performance (only computed once)
let cachedApiOrigin: string | null = null;

// Simple session validation cache so we don't call the API on every request
type SessionValidationEntry = {
  valid: boolean;
  expiresAt: number;
};

const SESSION_CACHE_TTL_MS = 30 * 1000; // 30 seconds
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
    return cached.valid;
  }

  const cookieHeader = request.headers.get('cookie');
  if (!cookieHeader) {
    cacheSessionValidation(cookieValue, false);
    return false;
  }

  try {
    const apiUrl = `${getApiOrigin()}/api/v1/auth/me`;
    const response = await fetch(apiUrl, {
      headers: {
        cookie: cookieHeader,
      },
      credentials: 'include',
      cache: 'no-store',
    });

    cacheSessionValidation(cookieValue, response.ok);
    return response.ok;
  } catch (error) {
    cacheSessionValidation(cookieValue, false);
    console.error('[middleware] Failed to validate session cookie:', error);
    return false;
  }
}

/**
 * Get API base URL origin for CSP
 * Extracts the origin (protocol + host) from the API base URL
 * Results are cached for performance
 */
function getApiOrigin(): string {
  if (cachedApiOrigin !== null) {
    return cachedApiOrigin;
  }

  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  const apiBase =
    envBase && envBase !== 'undefined' && envBase !== 'null' ? envBase : 'http://localhost:8080';

  try {
    const url = new URL(apiBase);
    cachedApiOrigin = url.origin;
    return cachedApiOrigin;
  } catch {
    // Fallback if URL parsing fails
    cachedApiOrigin = 'http://localhost:8080';
    return cachedApiOrigin;
  }
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
  const apiOrigin = getApiOrigin();

  // Only add API origin to connect-src if it differs from the request origin
  // This prevents redundant CSP entries like "connect-src 'self' http://localhost:3000"
  // when the API is on the same origin as the frontend
  const connectSrcParts = ["'self'"];
  if (requestOrigin && apiOrigin !== requestOrigin) {
    connectSrcParts.push(apiOrigin);
  } else if (!requestOrigin) {
    // No request origin available (shouldn't happen), include API origin for safety
    connectSrcParts.push(apiOrigin);
  }

  return {
    // Content Security Policy - XSS protection
    'Content-Security-Policy': [
      "default-src 'self'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval'", // Required for Next.js hydration
      "style-src 'self' 'unsafe-inline'", // Required for Tailwind CSS
      "img-src 'self' data: https:", // Allow images from data URIs and HTTPS
      "font-src 'self' data:",
      `connect-src ${connectSrcParts.join(' ')}`, // Allow API backend requests
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
 * Middleware function that runs on every request
 * Checks authentication status and redirects as needed
 */
export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const requestOrigin = request.nextUrl.origin;

  // Check if user has a session cookie
  const sessionCookie = request.cookies.get(SESSION_COOKIE_NAME);
  const sessionCookieValue = sessionCookie?.value;
  let isAuthenticated = false;

  if (sessionCookieValue) {
    isAuthenticated = await isSessionCookieValid(request, sessionCookieValue);
  }

  // Check user role (only trusted when we know the session is valid)
  const userRoleCookie = request.cookies.get(USER_ROLE_COOKIE);
  const userRole = isAuthenticated ? userRoleCookie?.value || 'user' : 'user';
  const isAdmin = isAuthenticated && userRole === 'admin';

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
    const redirectUrl =
      fromParam && PROTECTED_ROUTES.some(route => fromParam.startsWith(route))
        ? new URL(fromParam, request.url)
        : new URL('/dashboard', request.url);
    const response = NextResponse.redirect(redirectUrl);
    return addSecurityHeaders(response, requestOrigin);
  }

  // Redirect authenticated users from homepage to dashboard
  if (isHomePage && isAuthenticated) {
    const response = NextResponse.redirect(new URL('/dashboard', request.url));
    return addSecurityHeaders(response, requestOrigin);
  }

  // Allow the request to continue with security headers
  const response = NextResponse.next();
  return addSecurityHeaders(response, requestOrigin);
}

// ============================================================================
// Middleware Configuration
// ============================================================================

/**
 * Configure which routes the middleware should run on
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

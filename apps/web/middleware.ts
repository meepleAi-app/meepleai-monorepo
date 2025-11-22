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
 * - /chat - Main chat interface
 * - /upload - Document upload page
 * - /admin - Administration panel
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
const PROTECTED_ROUTES = ['/chat', '/upload', '/admin', '/editor'];

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

// ============================================================================
// Middleware Function
// ============================================================================

/**
 * Security headers configuration
 * Applied to all responses for defense-in-depth protection
 *
 * See: docs/06-security/client-side-encryption.md
 */
const SECURITY_HEADERS = {
  // Content Security Policy - XSS protection
  'Content-Security-Policy': [
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline' 'unsafe-eval'", // Required for Next.js hydration
    "style-src 'self' 'unsafe-inline'", // Required for Tailwind CSS
    "img-src 'self' data: https:", // Allow images from data URIs and HTTPS
    "font-src 'self' data:",
    "connect-src 'self'",
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

/**
 * Add security headers to the response
 */
function addSecurityHeaders(response: NextResponse): NextResponse {
  Object.entries(SECURITY_HEADERS).forEach(([key, value]) => {
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

  // Check if user has a session cookie
  const sessionCookie = request.cookies.get(SESSION_COOKIE_NAME);
  const isAuthenticated = !!sessionCookie?.value;

  // Check if the current route is protected or public auth route
  const isProtectedRoute = PROTECTED_ROUTES.some(route => pathname.startsWith(route));
  const isPublicAuthRoute = PUBLIC_AUTH_ROUTES.some(route => pathname === route);

  // Redirect unauthenticated users from protected routes to login
  if (isProtectedRoute && !isAuthenticated) {
    const loginUrl = new URL('/login', request.url);
    // Preserve the intended destination for redirect after login
    loginUrl.searchParams.set('from', pathname);
    const response = NextResponse.redirect(loginUrl);
    return addSecurityHeaders(response);
  }

  // Redirect authenticated users from login/register pages to chat
  if (isPublicAuthRoute && isAuthenticated) {
    // Check if there's a 'from' parameter to redirect back to
    const fromParam = request.nextUrl.searchParams.get('from');
    const redirectUrl = fromParam && PROTECTED_ROUTES.some(route => fromParam.startsWith(route))
      ? new URL(fromParam, request.url)
      : new URL('/chat', request.url);
    const response = NextResponse.redirect(redirectUrl);
    return addSecurityHeaders(response);
  }

  // Allow the request to continue with security headers
  const response = NextResponse.next();
  return addSecurityHeaders(response);
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

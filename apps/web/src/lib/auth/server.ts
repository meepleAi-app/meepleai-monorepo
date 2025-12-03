/**
 * Server-Side Authentication Utilities
 *
 * Issue #1611: SSR Auth Protection Migration
 * ADR-015: Server-Side Rendering (SSR) Authentication Protection
 *
 * Provides utilities for server-side authentication and authorization
 * to be used with Next.js App Router Server Components.
 *
 * Benefits:
 * - Zero UI flash (auth before render)
 * - Server-side session validation
 * - Role-based access control (RBAC)
 * - E2E test compatible (HTTP-level mocking)
 */

import { cookies } from 'next/headers';
import type { AuthUser } from '@/types/auth';

// ============================================================================
// Constants
// ============================================================================

/**
 * Backend API base URL for server-to-server calls
 * Uses internal Docker service name in production, localhost in dev
 */
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Session cookie name (matches middleware.ts and backend CookieHelpers.cs)
 */
const SESSION_COOKIE_NAME = 'meepleai_session';

// ============================================================================
// Core Functions
// ============================================================================

/**
 * Get current authenticated user from server-side session
 *
 * This function performs a server-to-server API call to validate
 * the session cookie and retrieve user information.
 *
 * Works with Next.js App Router Server Components.
 *
 * @returns AuthUser if authenticated, null otherwise
 *
 * @example
 * ```typescript
 * // app/protected/page.tsx
 * import { getServerUser } from '@/lib/auth/server'
 * import { redirect } from 'next/navigation'
 *
 * export default async function ProtectedPage() {
 *   const user = await getServerUser()
 *   if (!user) redirect('/login')
 *   return <div>Welcome {user.email}</div>
 * }
 * ```
 */
export async function getServerUser(): Promise<AuthUser | null> {
  try {
    const cookieStore = await cookies();
    const sessionCookie = cookieStore.get(SESSION_COOKIE_NAME);

    if (!sessionCookie) {
      return null;
    }

    // Server-to-server API call (no CORS issues)
    // Uses session cookie for authentication
    const res = await fetch(`${API_BASE}/api/v1/auth/me`, {
      headers: {
        Cookie: `${SESSION_COOKIE_NAME}=${sessionCookie.value}`,
      },
      credentials: 'include',
      cache: 'no-store', // Always validate session, don't cache
    });

    if (!res.ok) {
      // Session invalid or expired
      return null;
    }

    const data = await res.json();

    return data.user || null;
  } catch (error) {
    // Network error or JSON parse error
    console.error('[getServerUser] Failed to validate session:', error);
    return null;
  }
}

// ============================================================================
// Role Authorization Helpers
// ============================================================================

/**
 * Check if user has admin role
 *
 * @param user - Authenticated user
 * @returns True if user is admin
 */
export function isAdmin(user: AuthUser): boolean {
  return user.role.toLowerCase() === 'admin';
}

/**
 * Check if user has editor role (or admin)
 *
 * @param user - Authenticated user
 * @returns True if user is admin or editor
 */
export function isEditor(user: AuthUser): boolean {
  const role = user.role.toLowerCase();
  return role === 'admin' || role === 'editor';
}

/**
 * Check if user has specific role
 *
 * @param user - Authenticated user
 * @param role - Required role (case-insensitive)
 * @returns True if user has role
 */
export function hasRole(user: AuthUser, role: string): boolean {
  return user.role.toLowerCase() === role.toLowerCase();
}

/**
 * Check if user has any of the specified roles
 *
 * @param user - Authenticated user
 * @param roles - Array of allowed roles (case-insensitive)
 * @returns True if user has any of the roles
 */
export function hasAnyRole(user: AuthUser, roles: string[]): boolean {
  const userRole = user.role.toLowerCase();
  return roles.some(role => role.toLowerCase() === userRole);
}

/**
 * RequireRole Client Component
 *
 * Issue #1608: Frontend Route Protection
 *
 * Client-side role-based access control component that works with E2E tests.
 * Uses getCurrentUser() action which is mockable by Playwright.
 *
 * Security Pattern:
 * - Layer 1: Middleware blocks unauthenticated users
 * - Layer 2: This component blocks unauthorized roles
 * - Works with E2E test mocks (client-side API calls)
 *
 * Usage:
 * ```tsx
 * export default function AdminPage() {
 *   return (
 *     <RequireRole allowedRoles={['Admin']}>
 *       <AdminClient />
 *     </RequireRole>
 *   );
 * }
 * ```
 */

'use client';

import { useEffect, useState, useRef } from 'react';
import type { ReactNode } from 'react';

import { useRouter } from 'next/navigation';

import { getCurrentUser } from '@/actions/auth';
import { logger } from '@/lib/logger';
import type { AuthUser } from '@/types/auth';

interface RequireRoleProps {
  /**
   * Array of allowed roles (e.g., ['Admin'], ['Admin', 'Editor'])
   * User must have at least one of these roles to access the content
   */
  allowedRoles: string[];

  /**
   * Content to render if user is authorized
   */
  children: ReactNode;

  /**
   * Optional: Custom redirect URL for unauthorized access
   * @default '/'
   */
  unauthorizedRedirect?: string;

  /**
   * Optional: Custom redirect URL for unauthenticated users
   * @default '/login'
   */
  unauthenticatedRedirect?: string;
}

/**
 * Client Component that enforces role-based access control
 *
 * This component:
 * 1. Fetches current user via getCurrentUser() action
 * 2. Shows loading state during auth check
 * 3. Redirects if unauthenticated or unauthorized
 * 4. Renders children only if authorized
 *
 * Compatible with E2E tests using Playwright mocks
 */
export function RequireRole({
  allowedRoles,
  children,
  unauthorizedRedirect = '/',
  unauthenticatedRedirect = '/login',
}: RequireRoleProps) {
  const router = useRouter();
  const [_user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const hasChecked = useRef(false);

  useEffect(() => {
    // Prevent duplicate checks
    if (hasChecked.current) return;
    hasChecked.current = true;

    async function checkAuth() {
      try {
        const result = await getCurrentUser();

        // Not authenticated
        if (!result.success || !result.user) {
          // Preserve the current path in 'from' parameter
          const currentPath = window.location.pathname;
          const redirectUrl = `${unauthenticatedRedirect}?from=${encodeURIComponent(currentPath)}`;
          router.replace(redirectUrl);
          return;
        }

        // Check authorization (role-based)
        const userRole = result.user.role.toLowerCase();
        const hasRequiredRole = allowedRoles.some(role => role.toLowerCase() === userRole);

        if (!hasRequiredRole) {
          // Authenticated but not authorized
          router.replace(unauthorizedRedirect);
          return;
        }

        // User is authenticated and authorized
        setUser(result.user);
        setLoading(false);
      } catch (error) {
        logger.error(
          'Auth check failed',
          error instanceof Error ? error : new Error(String(error)),
          { component: 'RequireRole' }
        );
        // Preserve the current path in 'from' parameter
        const currentPath = window.location.pathname;
        const redirectUrl = `${unauthenticatedRedirect}?from=${encodeURIComponent(currentPath)}`;
        router.replace(redirectUrl);
      }
    }

    checkAuth();
  }, [allowedRoles, router, unauthorizedRedirect, unauthenticatedRedirect]);

  // Show loading state while checking authentication
  // Note: We render a minimal loading indicator that doesn't hide the parent layout
  // This allows the navbar/layout to remain visible during auth check
  if (loading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-gray-900 mx-auto mb-4"></div>
          <p className="text-gray-600">Verifica autorizzazioni...</p>
        </div>
      </div>
    );
  }

  // User is authorized - render protected content
  return <>{children}</>;
}

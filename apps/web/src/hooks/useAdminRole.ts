/**
 * useAdminRole - Hook for admin role checks and UI hiding (Issue #3690)
 *
 * Provides role-based access control helpers for admin dashboard.
 * Use for conditional rendering of admin features based on role permissions.
 *
 * Example:
 * ```tsx
 * const { isSuperAdmin, isAdminOrAbove } = useAdminRole();
 *
 * return (
 *   <>
 *     {isSuperAdmin && <GlobalFeatureFlagsButton />}
 *     {isAdminOrAbove && <UserManagementPanel />}
 *   </>
 * );
 * ```
 */

import { useMemo } from 'react';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import type { AuthUser, UserRole } from '@/types/auth';
import { isSuperAdmin, isAdminOrAbove, isEditorOrAbove, hasRole } from '@/types/auth';

export interface AdminRoleResult {
  /** Current user data (null if not authenticated) */
  user: AuthUser | null;

  /** True if user is SuperAdmin */
  isSuperAdmin: boolean;

  /** True if user is Admin or SuperAdmin */
  isAdminOrAbove: boolean;

  /** True if user is Editor, Admin, or SuperAdmin */
  isEditorOrAbove: boolean;

  /** Check if user has a specific role */
  hasRole: (role: UserRole) => boolean;

  /** True if user data is still loading */
  isLoading: boolean;
}

/**
 * Hook to check admin role permissions for UI conditional rendering
 */
export function useAdminRole(): AdminRoleResult {
  const { data: user, isLoading } = useCurrentUser();

  const result = useMemo(
    (): AdminRoleResult => ({
      user: user ?? null,
      isSuperAdmin: isSuperAdmin(user ?? null),
      isAdminOrAbove: isAdminOrAbove(user ?? null),
      isEditorOrAbove: isEditorOrAbove(user ?? null),
      hasRole: (role: UserRole) => hasRole(user ?? null, role),
      isLoading,
    }),
    [user, isLoading]
  );

  return result;
}

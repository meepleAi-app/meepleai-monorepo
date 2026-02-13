/**
 * Role Gate Component
 * Epic #4068 - Issue #4178
 *
 * Conditional rendering based on user role requirement
 */

'use client';

import type { ReactNode } from 'react';

import { usePermissions } from '@/contexts/PermissionContext';
import type { UserRole } from '@/types/permissions';

interface RoleGateProps {
  /** Required role (or roles) */
  role: UserRole | UserRole[];
  /** Content to render when role matches */
  children: ReactNode;
  /** Optional fallback content when role doesn't match */
  fallback?: ReactNode;
}

/**
 * RoleGate conditionally renders children based on user role
 *
 * @example
 * <RoleGate role="admin">
 *   <AdminPanel />
 * </RoleGate>
 *
 * @example
 * <RoleGate role={['admin', 'creator']} fallback={<p>Access denied</p>}>
 *   <ContentManagement />
 * </RoleGate>
 */
export function RoleGate({ role, children, fallback }: RoleGateProps) {
  const { role: userRole } = usePermissions();

  const hasRole = Array.isArray(role)
    ? role.includes(userRole)
    : userRole === role;

  return hasRole ? <>{children}</> : <>{fallback}</>;
}

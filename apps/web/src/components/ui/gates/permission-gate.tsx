/**
 * Permission Gate Component
 * Epic #4068 - Issue #4178
 *
 * Conditional rendering based on feature access permissions
 */

'use client';

import type { ReactNode } from 'react';

import { usePermissions } from '@/contexts/PermissionContext';

interface PermissionGateProps {
  /** Feature name to check access for */
  feature: string;
  /** Content to render when permission granted */
  children: ReactNode;
  /** Optional fallback content when permission denied */
  fallback?: ReactNode;
}

/**
 * PermissionGate conditionally renders children based on feature access
 *
 * @example
 * <PermissionGate feature="bulk-select">
 *   <BulkActionsToolbar />
 * </PermissionGate>
 *
 * @example
 * <PermissionGate feature="analytics.view" fallback={<UpgradePrompt />}>
 *   <AnalyticsDashboard />
 * </PermissionGate>
 */
export function PermissionGate({ feature, children, fallback }: PermissionGateProps) {
  const { canAccess } = usePermissions();

  return canAccess(feature) ? <>{children}</> : <>{fallback}</>;
}

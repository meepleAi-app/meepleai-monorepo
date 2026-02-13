/**
 * Tier Gate Component
 * Epic #4068 - Issue #4178
 *
 * Conditional rendering based on minimum tier requirement
 */

'use client';

import type { ReactNode } from 'react';

import { usePermissions } from '@/contexts/PermissionContext';
import type { UserTier } from '@/types/permissions';

interface TierGateProps {
  /** Minimum tier required */
  tier: UserTier;
  /** Content to render when tier sufficient */
  children: ReactNode;
  /** Optional fallback content when tier insufficient */
  fallback?: ReactNode;
}

/**
 * TierGate conditionally renders children based on minimum tier level
 *
 * @example
 * <TierGate tier="pro">
 *   <AdvancedFeatures />
 * </TierGate>
 *
 * @example
 * <TierGate tier="normal" fallback={<UpgradePrompt requiredTier="normal" />}>
 *   <DragDropInterface />
 * </TierGate>
 */
export function TierGate({ tier, children, fallback }: TierGateProps) {
  const { hasTier } = usePermissions();

  return hasTier(tier) ? <>{children}</> : <>{fallback}</>;
}

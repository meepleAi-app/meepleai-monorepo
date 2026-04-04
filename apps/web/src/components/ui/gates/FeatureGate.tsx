'use client';

/**
 * FeatureGate
 *
 * Conditionally renders children only when a feature flag is enabled for the current user.
 *
 * @example
 * <FeatureGate feature="advanced_rag">
 *   <AdvancedRagPanel />
 * </FeatureGate>
 *
 * @example
 * <FeatureGate feature="premium_export" fallback={<UpgradePrompt />}>
 *   <ExportButton />
 * </FeatureGate>
 */

import { useFeatureFlag } from '@/providers/FeatureFlagProvider';

interface FeatureGateProps {
  /** Feature flag key to check (e.g., "advanced_rag") */
  feature: string;
  /** Optional fallback when feature is disabled (defaults to null) */
  fallback?: React.ReactNode;
  children: React.ReactNode;
}

export function FeatureGate({ feature, fallback = null, children }: FeatureGateProps) {
  const isEnabled = useFeatureFlag(feature);
  return isEnabled ? <>{children}</> : <>{fallback}</>;
}

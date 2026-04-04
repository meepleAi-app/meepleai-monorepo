'use client';

/**
 * FeatureFlagProvider
 *
 * React context provider for server-driven user feature flag access.
 * Fetches flags once per session (5 min stale) and exposes `isEnabled(key)`.
 */

import { createContext, useContext, useMemo } from 'react';

import { useUserFeatures } from '@/hooks/queries/useFeatureFlags';
import type { UserFeatureDto } from '@/lib/api/schemas/feature-flags.schemas';

interface FeatureFlagContextValue {
  flags: UserFeatureDto[];
  isLoading: boolean;
  isEnabled: (key: string) => boolean;
}

const FeatureFlagContext = createContext<FeatureFlagContextValue>({
  flags: [],
  isLoading: true,
  isEnabled: () => false,
});

export function FeatureFlagProvider({ children }: { children: React.ReactNode }) {
  const { data: flags = [], isLoading } = useUserFeatures();

  const value = useMemo<FeatureFlagContextValue>(
    () => ({
      flags,
      isLoading,
      isEnabled: (key: string) => flags.some(f => f.key === key && f.hasAccess),
    }),
    [flags, isLoading]
  );

  return <FeatureFlagContext value={value}>{children}</FeatureFlagContext>;
}

export function useFeatureFlag(key: string): boolean {
  const { isEnabled } = useContext(FeatureFlagContext);
  return isEnabled(key);
}

export function useFeatureFlagContext() {
  return useContext(FeatureFlagContext);
}

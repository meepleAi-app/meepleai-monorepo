/**
 * Feature Flags React Query Hooks
 *
 * Hooks for fetching user-facing feature flag access.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export const featureFlagKeys = {
  all: ['feature-flags'] as const,
  userFeatures: () => [...featureFlagKeys.all, 'user'] as const,
};

export function useUserFeatures() {
  return useQuery({
    queryKey: featureFlagKeys.userFeatures(),
    queryFn: () => api.featureFlags.getUserFeatures(),
    staleTime: 5 * 60 * 1000, // 5 min — flags change infrequently
  });
}

'use client';

/**
 * Issue #936 (G5) — Provider observability hooks.
 *
 * useProviderQuota: TanStack Query for GET /api/v1/admin/providers/{name}/quota.
 * Server caches 5min; staleTime 4min so client refetches just before server expiry.
 *
 * useProbeProviderMutation: POST /api/v1/admin/providers/{name}/probe (SuperAdmin).
 * Invalidates quota cache on success (probe is zero-cost but kept for consistency).
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ProviderName, ProviderProbeResult, ProviderQuota } from '@/lib/api/schemas/providers';

export const providerKeys = {
  all: ['admin', 'providers'] as const,
  quota: (name: ProviderName) => ['admin', 'providers', name, 'quota'] as const,
};

export function useProviderQuota(name: ProviderName, options?: { enabled?: boolean }) {
  return useQuery<ProviderQuota | null>({
    queryKey: providerKeys.quota(name),
    queryFn: () => api.admin.getProviderQuota(name),
    staleTime: 4 * 60 * 1000,
    enabled: options?.enabled ?? true,
  });
}

export function useProbeProviderMutation(name: ProviderName) {
  const qc = useQueryClient();
  return useMutation<ProviderProbeResult, Error, { model?: string } | void>({
    mutationFn: input => api.admin.probeProvider(name, input?.model),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: providerKeys.quota(name) });
    },
  });
}

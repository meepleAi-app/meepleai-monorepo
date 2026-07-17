'use client';

/**
 * Issue #936 (G5) — Provider observability hooks.
 *
 * useProviderQuota: TanStack Query for GET /api/v1/admin/providers/{name}/quota.
 * Server caches 5min; staleTime 4min so client refetches just before server expiry.
 *
 * useProbeProviderMutation: POST /api/v1/admin/providers/{name}/probe (SuperAdmin).
 * Invalidates quota cache on success (probe is zero-cost but kept for consistency).
 *
 * #1834 SP5 F4-C3 extension:
 * useCircuitBreakerStates: GET /api/v1/admin/circuit-breakers.
 * useLlmSystemConfig: GET /api/v1/admin/llm/config (for routing chain viz).
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CircuitBreakerState } from '@/lib/api/schemas/admin/admin-circuit-breakers.schemas';
import type { LlmSystemConfigDto } from '@/lib/api/schemas/llm-system-config.schemas';
import type { ProviderName, ProviderProbeResult, ProviderQuota } from '@/lib/api/schemas/providers';

export const providerKeys = {
  all: ['admin', 'providers'] as const,
  quota: (name: ProviderName) => ['admin', 'providers', name, 'quota'] as const,
  quotaAll: ['admin', 'providers', 'quota'] as const,
  circuitBreakers: ['admin', 'circuit-breakers'] as const,
  llmConfig: ['admin', 'llm', 'config'] as const,
};

export function useProviderQuota(name: ProviderName, options?: { enabled?: boolean }) {
  return useQuery<ProviderQuota | null>({
    queryKey: providerKeys.quota(name),
    queryFn: () => api.admin.getProviderQuota(name),
    staleTime: 4 * 60 * 1000,
    enabled: options?.enabled ?? true,
  });
}

/**
 * Issue #3043 — aggregated quota (single fetch) for the credits summary widget.
 * staleTime 4min, mirror of the per-provider hook (just under the 5min server TTL).
 */
export function useProvidersQuota(options?: { enabled?: boolean }) {
  return useQuery<ProviderQuota[]>({
    queryKey: providerKeys.quotaAll,
    queryFn: () => api.admin.getProvidersQuota(),
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

/**
 * #1834 — Circuit breaker states for all external services (LLM + others).
 * Polly-managed in-memory state; not cached server-side, but client staleTime
 * 30s avoids hammering during a fast-refresh session.
 */
export function useCircuitBreakerStates(options?: { enabled?: boolean }) {
  return useQuery<CircuitBreakerState[]>({
    queryKey: providerKeys.circuitBreakers,
    queryFn: () => api.admin.getCircuitBreakerStates(),
    staleTime: 30 * 1000,
    enabled: options?.enabled ?? true,
  });
}

/**
 * #1834 — LLM system configuration. Used by RoutingChainViz to render the
 * fallback chain from `fallbackChainJson`. 60s staleTime to mirror BE cache TTL.
 *
 * Note: the BE endpoint always returns 200 with the config object (DB or
 * appsettings fallback), so this hook's data is non-nullable once resolved.
 */
export function useLlmSystemConfig(options?: { enabled?: boolean }) {
  return useQuery<LlmSystemConfigDto>({
    queryKey: providerKeys.llmConfig,
    queryFn: () => api.admin.getLlmSystemConfig(),
    staleTime: 60 * 1000,
    enabled: options?.enabled ?? true,
  });
}

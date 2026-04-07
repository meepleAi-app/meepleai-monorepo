/**
 * React Query Hooks for AI Infrastructure Dashboard
 *
 * Hooks for infrastructure monitoring, service management,
 * pipeline testing, and configuration updates.
 */

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';

// ============================================================================
// Query Keys
// ============================================================================

const INFRA_KEYS = {
  services: ['admin', 'infrastructure', 'services'] as const,
  dependencies: (name: string) => ['admin', 'infrastructure', 'dependencies', name] as const,
  config: (name: string) => ['admin', 'infrastructure', 'config', name] as const,
  pipeline: ['admin', 'infrastructure', 'pipeline'] as const,
};

// ============================================================================
// Query Hooks
// ============================================================================

/**
 * Fetch status of all AI/infra services.
 * Auto-refreshes every 30 seconds.
 */
export function useInfraServices() {
  return useQuery({
    queryKey: INFRA_KEYS.services,
    queryFn: () => api.infrastructure.getServices(),
    staleTime: 30_000,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

/**
 * Fetch dependency tree for a specific service.
 * Only fires when a service name is provided.
 */
export function useServiceDependencies(name: string | null) {
  return useQuery({
    queryKey: INFRA_KEYS.dependencies(name ?? ''),
    queryFn: () => api.infrastructure.getServiceDependencies(name!),
    enabled: !!name,
    staleTime: 60_000,
  });
}

/**
 * Fetch configurable parameters for a specific service.
 * Only fires when a service name is provided.
 */
export function useServiceConfig(name: string | null) {
  return useQuery({
    queryKey: INFRA_KEYS.config(name ?? ''),
    queryFn: () => api.infrastructure.getServiceConfig(name!),
    enabled: !!name,
    staleTime: 60_000,
  });
}

// ============================================================================
// Mutation Hooks
// ============================================================================

/**
 * Test full RAG pipeline connectivity.
 * Invalidates service status on completion.
 */
export function usePipelineTest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.infrastructure.testPipeline(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

/**
 * Restart a specific service (with cooldown enforcement).
 * Invalidates service status on completion.
 */
export function useRestartService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.infrastructure.restartService(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

/**
 * Trigger an on-demand health check for a specific service.
 * Invalidates service status on completion.
 */
export function useTriggerHealthCheck() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => api.infrastructure.triggerHealthCheck(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

/**
 * Update configuration parameters for a service.
 * Invalidates both config and service status on completion.
 */
export function useUpdateServiceConfig() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ name, params }: { name: string; params: Record<string, string> }) =>
      api.infrastructure.updateServiceConfig(name, params),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.config(variables.name) });
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

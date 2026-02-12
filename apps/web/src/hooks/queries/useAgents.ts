/**
 * useAgents - React Query hook for agent catalog
 * Issue #4126: API Integration
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export function useAgents(options: { activeOnly?: boolean; type?: string } = {}) {
  return useQuery({
    queryKey: ['agents', options],
    queryFn: () => api.agents.getAll(options.activeOnly, options.type),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

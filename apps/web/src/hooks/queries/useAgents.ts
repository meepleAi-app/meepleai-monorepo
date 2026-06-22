/**
 * useAgents - React Query hook for agent catalog
 * Issue #4126: API Integration
 * Issue #1592 (Phase 2b): `scope?: 'my-library'` option forwards to BE-2 #1589.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface UseAgentsOptions {
  activeOnly?: boolean;
  type?: string;
  /** BE-2 #1589: `'my-library'` returns library-game agents + system agents. */
  scope?: 'my-library';
}

export function useAgents(options: UseAgentsOptions = {}) {
  return useQuery({
    queryKey: ['agents', options],
    queryFn: () => api.agents.getAll(options.activeOnly, options.type, options.scope),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

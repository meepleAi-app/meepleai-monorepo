/**
 * useRecentAgents - React Query hook for dashboard widget
 * Issue #4126: API Integration
 */

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useRecentAgents(limit: number = 3) {
  return useQuery({
    queryKey: ['agents', 'recent', limit],
    queryFn: () => api.agents.getRecent(limit),
    staleTime: 2 * 60 * 1000, // 2 minutes
  });
}

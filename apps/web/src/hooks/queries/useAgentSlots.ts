/**
 * useAgentSlots - React Query hook for agent slot allocation
 * Issue #4771: Agent Slots Endpoint + Quota System
 */

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

export const agentSlotsKeys = {
  all: ['agent-slots'] as const,
  user: () => [...agentSlotsKeys.all, 'user'] as const,
};

export type AgentSlot = {
  slotIndex: number;
  agentId: string | null;
  agentName: string | null;
  gameId: string | null;
  status: 'active' | 'available' | 'locked';
};

export type AgentSlotsData = {
  total: number;
  used: number;
  available: number;
  slots: AgentSlot[];
};

export function useAgentSlots(enabled: boolean = true) {
  return useQuery({
    queryKey: agentSlotsKeys.user(),
    queryFn: () => api.agents.getSlots(),
    enabled,
    staleTime: 30_000, // 30 seconds - slots change rarely
  });
}

/**
 * Helper hook that returns just the availability status.
 * Useful for conditionally showing "Create Agent" buttons.
 */
export function useHasAvailableSlots() {
  const { data, isLoading } = useAgentSlots();
  return {
    hasAvailableSlots: (data?.available ?? 0) > 0,
    isLoading,
    slotsData: data,
  };
}

/**
 * Hook to invalidate agent slots cache.
 * Call after agent creation/deletion.
 */
export function useInvalidateAgentSlots() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: agentSlotsKeys.all });
}

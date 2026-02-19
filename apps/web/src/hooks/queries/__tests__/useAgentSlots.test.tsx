/**
 * Tests for useAgentSlots hooks
 *
 * Issue #4773: Frontend Hooks (useAgentSlots + useCreateAgentFlow)
 *
 * Tests TanStack Query hooks for agent slot allocation and availability.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { useAgentSlots, useHasAvailableSlots, useInvalidateAgentSlots, agentSlotsKeys } from '../useAgentSlots';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type { Mock } from 'vitest';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getSlots: vi.fn(),
    },
  },
}));

describe('useAgentSlots hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('agentSlotsKeys', () => {
    it('generates correct base query keys', () => {
      expect(agentSlotsKeys.all).toEqual(['agent-slots']);
    });

    it('generates correct user query keys', () => {
      expect(agentSlotsKeys.user()).toEqual(['agent-slots', 'user']);
    });
  });

  // ==================== useAgentSlots ====================

  describe('useAgentSlots', () => {
    const mockSlotsData = {
      total: 3,
      used: 1,
      available: 2,
      slots: [
        {
          slotIndex: 1,
          agentId: 'agent-1',
          agentName: 'RulesMaster',
          gameId: 'game-1',
          status: 'active' as const,
        },
        {
          slotIndex: 2,
          agentId: null,
          agentName: null,
          gameId: null,
          status: 'available' as const,
        },
        {
          slotIndex: 3,
          agentId: null,
          agentName: null,
          gameId: null,
          status: 'available' as const,
        },
      ],
    };

    it('fetches agent slots successfully', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue(mockSlotsData);

      const { result } = renderHook(() => useAgentSlots(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.agents.getSlots).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockSlotsData);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useAgentSlots(false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.agents.getSlots).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch slots');
      (api.agents.getSlots as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAgentSlots(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns correct slot counts', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue(mockSlotsData);

      const { result } = renderHook(() => useAgentSlots(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.total).toBe(3);
      expect(result.current.data?.used).toBe(1);
      expect(result.current.data?.available).toBe(2);
      expect(result.current.data?.slots).toHaveLength(3);
    });
  });

  // ==================== useHasAvailableSlots ====================

  describe('useHasAvailableSlots', () => {
    it('returns true when slots are available', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue({
        total: 3,
        used: 1,
        available: 2,
        slots: [],
      });

      const { result } = renderHook(() => useHasAvailableSlots(), { wrapper });

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.hasAvailableSlots).toBe(true);
    });

    it('returns false when no slots available', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue({
        total: 3,
        used: 3,
        available: 0,
        slots: [],
      });

      const { result } = renderHook(() => useHasAvailableSlots(), { wrapper });

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.hasAvailableSlots).toBe(false);
    });

    it('returns false when loading (defaults to 0 available)', () => {
      (api.agents.getSlots as Mock).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      const { result } = renderHook(() => useHasAvailableSlots(), { wrapper });

      expect(result.current.isLoading).toBe(true);
      expect(result.current.hasAvailableSlots).toBe(false);
    });

    it('provides slotsData for detailed access', async () => {
      const mockData = {
        total: 10,
        used: 5,
        available: 5,
        slots: [],
      };
      (api.agents.getSlots as Mock).mockResolvedValue(mockData);

      const { result } = renderHook(() => useHasAvailableSlots(), { wrapper });

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.slotsData).toEqual(mockData);
    });
  });

  // ==================== useInvalidateAgentSlots ====================

  describe('useInvalidateAgentSlots', () => {
    it('returns a function that invalidates agent slots cache', async () => {
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useInvalidateAgentSlots(), { wrapper });

      result.current();

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agentSlotsKeys.all });
    });
  });
});

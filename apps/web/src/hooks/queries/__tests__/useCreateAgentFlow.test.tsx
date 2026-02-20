/**
 * Tests for useCreateAgentFlow mutation hook
 *
 * Issue #4773: Frontend Hooks (useAgentSlots + useCreateAgentFlow)
 *
 * Tests TanStack Query mutation hook for orchestrated agent creation.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useCreateAgentFlow, type CreateAgentFlowResult, AGENT_FLOW_MESSAGES } from '../useCreateAgentFlow';
import { agentSlotsKeys } from '../useAgentSlots';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      createWithSetup: vi.fn(),
    },
  },
}));

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

import { toast } from 'sonner';

describe('useCreateAgentFlow hook', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  const mockInput = {
    gameId: 'game-123',
    addToCollection: true,
    agentType: 'RAG',
    agentName: 'MyAgent',
  };

  const mockResult: CreateAgentFlowResult = {
    agentId: 'agent-456',
    agentName: 'MyAgent',
    threadId: 'thread-789',
    slotUsed: 2,
    gameAddedToCollection: true,
  };

  // ==================== Success Cases ====================

  describe('successful creation', () => {
    it('calls createWithSetup API', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(api.agents.createWithSetup).toHaveBeenCalledWith(mockInput);
    });

    it('shows success toast with agent name', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(toast.success).toHaveBeenCalledWith(AGENT_FLOW_MESSAGES.success('MyAgent'));
    });

    it('invalidates agent slots cache', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agentSlotsKeys.all });
    });

    it('invalidates agents list cache', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['agents'] });
    });

    it('invalidates user-library when game added to collection', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['user-library'] });
    });

    it('does NOT invalidate user-library when game not added', async () => {
      const resultNoCollection = { ...mockResult, gameAddedToCollection: false };
      (api.agents.createWithSetup as Mock).mockResolvedValue(resultNoCollection);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(invalidateSpy).not.toHaveBeenCalledWith({ queryKey: ['user-library'] });
    });

    it('calls onSuccess callback with result', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue(mockResult);
      const onSuccess = vi.fn();

      const { result } = renderHook(() => useCreateAgentFlow({ onSuccess }), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(mockInput);
      });

      expect(onSuccess).toHaveBeenCalledWith(mockResult);
    });
  });

  // ==================== Error Cases ====================

  describe('error handling', () => {
    it('shows slot limit error toast', async () => {
      const error = new Error('Agent limit reached for your tier');
      (api.agents.createWithSetup as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(mockInput);
        } catch {
          // Expected error
        }
      });

      expect(toast.error).toHaveBeenCalledWith(AGENT_FLOW_MESSAGES.slotLimit.title, {
        description: AGENT_FLOW_MESSAGES.slotLimit.description,
      });
    });

    it('shows name conflict error toast', async () => {
      const error = new Error('Agent with unique name already exists');
      (api.agents.createWithSetup as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(mockInput);
        } catch {
          // Expected error
        }
      });

      expect(toast.error).toHaveBeenCalledWith(AGENT_FLOW_MESSAGES.nameConflict.title, {
        description: AGENT_FLOW_MESSAGES.nameConflict.description,
      });
    });

    it('shows generic error toast for unknown errors', async () => {
      const error = new Error('Something unexpected happened');
      (api.agents.createWithSetup as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(mockInput);
        } catch {
          // Expected error
        }
      });

      expect(toast.error).toHaveBeenCalledWith(AGENT_FLOW_MESSAGES.genericError('Something unexpected happened').title, {
        description: AGENT_FLOW_MESSAGES.genericError('Something unexpected happened').description,
      });
    });

    it('shows fallback message when error has no message', async () => {
      const error = new Error('');
      (api.agents.createWithSetup as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useCreateAgentFlow(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(mockInput);
        } catch {
          // Expected error
        }
      });

      expect(toast.error).toHaveBeenCalledWith(AGENT_FLOW_MESSAGES.genericError(AGENT_FLOW_MESSAGES.fallbackErrorDesc).title, {
        description: AGENT_FLOW_MESSAGES.genericError(AGENT_FLOW_MESSAGES.fallbackErrorDesc).description,
      });
    });

    it('calls onError callback', async () => {
      const error = new Error('Test error');
      (api.agents.createWithSetup as Mock).mockRejectedValue(error);
      const onError = vi.fn();

      const { result } = renderHook(() => useCreateAgentFlow({ onError }), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(mockInput);
        } catch {
          // Expected error
        }
      });

      expect(onError).toHaveBeenCalledWith(error);
    });
  });
});

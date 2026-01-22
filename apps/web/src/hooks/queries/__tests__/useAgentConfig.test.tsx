/**
 * Tests for useAgentConfig hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for AI agent configuration.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useAgentConfig, useUpdateAgentConfig, agentConfigKeys } from '../useAgentConfig';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type { AgentConfigDto } from '@/lib/api';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getAgentConfig: vi.fn(),
      updateAgentConfig: vi.fn(),
    },
  },
}));

describe('useAgentConfig hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('agentConfigKeys', () => {
    it('generates correct base query keys', () => {
      expect(agentConfigKeys.all).toEqual(['agentConfig']);
    });

    it('generates correct byGame query keys', () => {
      const gameId = 'game-123';
      expect(agentConfigKeys.byGame(gameId)).toEqual(['agentConfig', gameId]);
    });

    it('generates unique keys for different games', () => {
      const key1 = agentConfigKeys.byGame('game-1');
      const key2 = agentConfigKeys.byGame('game-2');

      expect(key1).not.toEqual(key2);
    });
  });

  // ==================== useAgentConfig ====================

  describe('useAgentConfig', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockConfig: AgentConfigDto = {
      id: 'config-1',
      userId: 'user-1',
      gameId,
      personality: 'friendly',
      verbosity: 'normal',
      expertiseLevel: 'intermediate',
      languageStyle: 'casual',
      responseFormat: 'markdown',
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-15T00:00:00Z',
    };

    it('fetches agent config successfully', async () => {
      (api.library.getAgentConfig as Mock).mockResolvedValue(mockConfig);

      const { result } = renderHook(() => useAgentConfig(gameId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getAgentConfig).toHaveBeenCalledWith(gameId);
      expect(result.current.data).toEqual(mockConfig);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useAgentConfig(gameId, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getAgentConfig).not.toHaveBeenCalled();
    });

    it('does not fetch when gameId is empty', () => {
      const { result } = renderHook(() => useAgentConfig(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getAgentConfig).not.toHaveBeenCalled();
    });

    it('returns null when no config exists (uses default)', async () => {
      (api.library.getAgentConfig as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useAgentConfig(gameId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch config');
      (api.library.getAgentConfig as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAgentConfig(gameId), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useUpdateAgentConfig ====================

  describe('useUpdateAgentConfig', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const existingConfig: AgentConfigDto = {
      id: 'config-1',
      userId: 'user-1',
      gameId,
      personality: 'friendly',
      verbosity: 'normal',
      expertiseLevel: 'intermediate',
      languageStyle: 'casual',
      responseFormat: 'markdown',
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-15T00:00:00Z',
    };

    const updatedConfig: AgentConfigDto = {
      ...existingConfig,
      personality: 'professional',
      verbosity: 'detailed',
      updatedAt: new Date().toISOString(),
    };

    it('updates agent config successfully', async () => {
      (api.library.updateAgentConfig as Mock).mockResolvedValue(updatedConfig);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      const request = { personality: 'professional', verbosity: 'detailed' };
      await act(async () => {
        await result.current.mutateAsync({ gameId, request });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.library.updateAgentConfig).toHaveBeenCalledWith(gameId, request);
      expect(result.current.data).toEqual(updatedConfig);

      // Verify cache invalidation on settled
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: agentConfigKeys.byGame(gameId) });
    });

    it('performs optimistic update when config exists', async () => {
      // Pre-populate cache with existing config
      queryClient.setQueryData(agentConfigKeys.byGame(gameId), existingConfig);

      (api.library.updateAgentConfig as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(updatedConfig), 100))
      );

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      const request = { personality: 'professional' };

      // Start mutation (don't await yet)
      act(() => {
        result.current.mutate({ gameId, request });
      });

      // Check optimistic update applied immediately
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<AgentConfigDto>(agentConfigKeys.byGame(gameId));
        expect(cachedData?.personality).toBe('professional');
      });
    });

    it('creates optimistic config when none exists', async () => {
      // No existing config in cache
      queryClient.setQueryData(agentConfigKeys.byGame(gameId), null);

      (api.library.updateAgentConfig as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(updatedConfig), 100))
      );

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      const request = { personality: 'professional', verbosity: 'detailed' };

      act(() => {
        result.current.mutate({ gameId, request });
      });

      // Check optimistic config created with temp ID
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<AgentConfigDto>(agentConfigKeys.byGame(gameId));
        expect(cachedData?.id).toBe('temp-id');
        expect(cachedData?.personality).toBe('professional');
        expect(cachedData?.gameId).toBe(gameId);
      });
    });

    it('rolls back on error', async () => {
      // Pre-populate cache with existing config
      queryClient.setQueryData(agentConfigKeys.byGame(gameId), existingConfig);

      const error = new Error('Update failed');
      (api.library.updateAgentConfig as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      const request = { personality: 'professional' };

      await act(async () => {
        try {
          await result.current.mutateAsync({ gameId, request });
        } catch {
          // Expected error
        }
      });

      // Verify rollback to original config
      const cachedData = queryClient.getQueryData<AgentConfigDto>(agentConfigKeys.byGame(gameId));
      expect(cachedData?.personality).toBe('friendly'); // Original value
    });

    it('cancels outgoing queries before mutation', async () => {
      const cancelSpy = vi.spyOn(queryClient, 'cancelQueries');

      (api.library.updateAgentConfig as Mock).mockResolvedValue(updatedConfig);

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ gameId, request: { personality: 'professional' } });
      });

      expect(cancelSpy).toHaveBeenCalledWith({ queryKey: agentConfigKeys.byGame(gameId) });
    });

    it('updates multiple config fields at once', async () => {
      const multiFieldUpdate: AgentConfigDto = {
        ...existingConfig,
        personality: 'professional',
        verbosity: 'detailed',
        expertiseLevel: 'expert',
        languageStyle: 'formal',
      };
      (api.library.updateAgentConfig as Mock).mockResolvedValue(multiFieldUpdate);

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      const request = {
        personality: 'professional',
        verbosity: 'detailed',
        expertiseLevel: 'expert',
        languageStyle: 'formal',
      };

      await act(async () => {
        await result.current.mutateAsync({ gameId, request });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.library.updateAgentConfig).toHaveBeenCalledWith(gameId, request);
      expect(result.current.data).toEqual(multiFieldUpdate);
    });

    it('handles network errors gracefully', async () => {
      const networkError = new Error('Network error');
      (api.library.updateAgentConfig as Mock).mockRejectedValue(networkError);

      const { result } = renderHook(() => useUpdateAgentConfig(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ gameId, request: { personality: 'test' } });
        } catch (e) {
          expect(e).toEqual(networkError);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(networkError);
    });
  });
});

/**
 * Tests for useAgentDocuments hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for agent document selection.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useAgentDocuments, useUpdateAgentDocuments, agentDocumentsKeys } from '../useAgentDocuments';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type { AgentDocumentsDto, UpdateAgentDocumentsResponse } from '@/lib/api/schemas';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getDocuments: vi.fn(),
      updateDocuments: vi.fn(),
    },
  },
}));

describe('useAgentDocuments hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('agentDocumentsKeys', () => {
    it('generates correct base query keys', () => {
      expect(agentDocumentsKeys.all).toEqual(['agent-documents']);
    });

    it('generates correct byAgent query keys', () => {
      const agentId = 'agent-123';
      expect(agentDocumentsKeys.byAgent(agentId)).toEqual(['agent-documents', 'by-agent', agentId]);
    });

    it('generates unique keys for different agents', () => {
      const key1 = agentDocumentsKeys.byAgent('agent-1');
      const key2 = agentDocumentsKeys.byAgent('agent-2');

      expect(key1).not.toEqual(key2);
    });
  });

  // ==================== useAgentDocuments ====================

  describe('useAgentDocuments', () => {
    const agentId = 'agent-123';
    const mockDocuments: AgentDocumentsDto = {
      agentId,
      documents: [
        {
          id: 'doc-1',
          title: 'Catan Rulebook',
          type: 'rulebook',
          fileSize: 1024000,
          uploadedAt: '2024-01-01T00:00:00Z',
        },
        {
          id: 'doc-2',
          title: 'Catan FAQ',
          type: 'faq',
          fileSize: 512000,
          uploadedAt: '2024-01-02T00:00:00Z',
        },
      ],
      totalDocuments: 2,
      lastUpdated: '2024-01-02T00:00:00Z',
    };

    it('fetches agent documents successfully', async () => {
      (api.agents.getDocuments as Mock).mockResolvedValue(mockDocuments);

      const { result } = renderHook(() => useAgentDocuments({ agentId }), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.agents.getDocuments).toHaveBeenCalledWith(agentId);
      expect(result.current.data).toEqual(mockDocuments);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useAgentDocuments({ agentId, enabled: false }), {
        wrapper,
      });

      expect(result.current.isFetching).toBe(false);
      expect(api.agents.getDocuments).not.toHaveBeenCalled();
    });

    it('does not fetch when agentId is null', () => {
      const { result } = renderHook(() => useAgentDocuments({ agentId: null }), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.agents.getDocuments).not.toHaveBeenCalled();
    });

    it('throws error when agentId is required but null', async () => {
      // When agentId is null but enabled is true (default), it should not fetch
      const { result } = renderHook(() => useAgentDocuments({ agentId: null, enabled: true }), {
        wrapper,
      });

      // Should not be fetching because enabled condition includes agentId check
      expect(result.current.isFetching).toBe(false);
    });

    it('returns null when no documents exist', async () => {
      (api.agents.getDocuments as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useAgentDocuments({ agentId }), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch documents');
      (api.agents.getDocuments as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAgentDocuments({ agentId }), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty documents array when agent has no documents', async () => {
      const emptyDocuments: AgentDocumentsDto = {
        agentId,
        documents: [],
        totalDocuments: 0,
        lastUpdated: '2024-01-01T00:00:00Z',
      };
      (api.agents.getDocuments as Mock).mockResolvedValue(emptyDocuments);

      const { result } = renderHook(() => useAgentDocuments({ agentId }), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.documents).toHaveLength(0);
      expect(result.current.data?.totalDocuments).toBe(0);
    });
  });

  // ==================== useUpdateAgentDocuments ====================

  describe('useUpdateAgentDocuments', () => {
    const agentId = 'agent-123';
    const documentIds = ['doc-1', 'doc-2', 'doc-3'];

    const mockUpdateResponse: UpdateAgentDocumentsResponse = {
      success: true,
      agentId,
      documentIds,
      updatedAt: new Date().toISOString(),
    };

    it('updates agent documents successfully', async () => {
      (api.agents.updateDocuments as Mock).mockResolvedValue(mockUpdateResponse);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateAgentDocuments(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ agentId, documentIds });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.agents.updateDocuments).toHaveBeenCalledWith(agentId, documentIds);
      expect(result.current.data).toEqual(mockUpdateResponse);

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: agentDocumentsKeys.byAgent(agentId),
      });
    });

    it('calls onSuccess callback on successful update', async () => {
      (api.agents.updateDocuments as Mock).mockResolvedValue(mockUpdateResponse);
      const onSuccessMock = vi.fn();

      const { result } = renderHook(() => useUpdateAgentDocuments({ onSuccess: onSuccessMock }), {
        wrapper,
      });

      await act(async () => {
        await result.current.mutateAsync({ agentId, documentIds });
      });

      expect(onSuccessMock).toHaveBeenCalledWith(mockUpdateResponse);
    });

    it('calls onError callback on failed update', async () => {
      const error = new Error('Update failed');
      (api.agents.updateDocuments as Mock).mockRejectedValue(error);
      const onErrorMock = vi.fn();

      const { result } = renderHook(() => useUpdateAgentDocuments({ onError: onErrorMock }), {
        wrapper,
      });

      await act(async () => {
        try {
          await result.current.mutateAsync({ agentId, documentIds });
        } catch {
          // Expected error
        }
      });

      expect(onErrorMock).toHaveBeenCalledWith(error);
    });

    it('handles update errors', async () => {
      const error = new Error('Permission denied');
      (api.agents.updateDocuments as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useUpdateAgentDocuments(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ agentId, documentIds });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });

    it('updates with empty document list (clear all)', async () => {
      const clearResponse: UpdateAgentDocumentsResponse = {
        success: true,
        agentId,
        documentIds: [],
        updatedAt: new Date().toISOString(),
      };
      (api.agents.updateDocuments as Mock).mockResolvedValue(clearResponse);

      const { result } = renderHook(() => useUpdateAgentDocuments(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ agentId, documentIds: [] });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.agents.updateDocuments).toHaveBeenCalledWith(agentId, []);
      expect(result.current.data?.documentIds).toHaveLength(0);
    });

    it('handles concurrent updates correctly', async () => {
      (api.agents.updateDocuments as Mock).mockResolvedValue(mockUpdateResponse);

      const { result } = renderHook(() => useUpdateAgentDocuments(), { wrapper });

      // Start two updates
      await act(async () => {
        await Promise.all([
          result.current.mutateAsync({ agentId, documentIds: ['doc-1'] }),
          result.current.mutateAsync({ agentId, documentIds: ['doc-2'] }),
        ]);
      });

      // Both should have been called
      expect(api.agents.updateDocuments).toHaveBeenCalledTimes(2);
    });

    it('shows pending state during mutation', async () => {
      (api.agents.updateDocuments as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockUpdateResponse), 100))
      );

      const { result } = renderHook(() => useUpdateAgentDocuments(), { wrapper });

      act(() => {
        result.current.mutate({ agentId, documentIds });
      });

      // Check pending state - wait for it to transition to pending
      await waitFor(() => {
        expect(result.current.isPending).toBe(true);
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.isPending).toBe(false);
    });
  });
});

/**
 * Tests for useBulkCollectionActions hook
 * Issue #4268 - Phase 3: Bulk Collection Actions
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useBulkCollectionActions } from '../use-bulk-collection-actions';

import type { BulkOperationResult } from '@/lib/api/schemas/collections.schemas';

// Mock API client
vi.mock('@/lib/api/clients/collectionsClient', () => ({
  bulkAddToCollection: vi.fn(),
  bulkRemoveFromCollection: vi.fn(),
  getBulkAssociatedData: vi.fn(),
}));

// Mock toast
vi.mock('sonner', () => ({
  toast: {
    loading: vi.fn(),
    success: vi.fn(),
    warning: vi.fn(),
    error: vi.fn(),
    dismiss: vi.fn(),
  },
}));

import { toast } from 'sonner';

import {
  bulkAddToCollection,
  bulkRemoveFromCollection,
  getBulkAssociatedData,
} from '@/lib/api/clients/collectionsClient';

describe('useBulkCollectionActions', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  it('bulkAdd calls API and shows success toast on complete success', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 5,
      successCount: 5,
      failedCount: 0,
      errors: [],
    };

    vi.mocked(bulkAddToCollection).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    const request = {
      entityIds: ['id1', 'id2', 'id3', 'id4', 'id5'],
      isFavorite: false,
    };

    result.current.bulkAdd(request);

    await waitFor(() => {
      expect(bulkAddToCollection).toHaveBeenCalledWith('game', request);
      expect(toast.success).toHaveBeenCalledWith('5 giochi aggiunti!');
    });
  });

  it('bulkAdd shows warning toast on partial success', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 5,
      successCount: 3,
      failedCount: 2,
      errors: [
        { entityId: 'id4', error: 'Collection quota exceeded' },
        { entityId: 'id5', error: 'Entity not found' },
      ],
    };

    vi.mocked(bulkAddToCollection).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    result.current.bulkAdd({
      entityIds: ['id1', 'id2', 'id3', 'id4', 'id5'],
    });

    await waitFor(() => {
      expect(toast.warning).toHaveBeenCalledWith(
        '3/5 giochi aggiunti. 2 falliti.',
        expect.objectContaining({
          description: 'Collection quota exceeded',
        })
      );
    });
  });

  it('bulkAdd shows error toast on total failure', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 3,
      successCount: 0,
      failedCount: 3,
      errors: [
        { entityId: 'id1', error: 'Not found' },
        { entityId: 'id2', error: 'Not found' },
        { entityId: 'id3', error: 'Not found' },
      ],
    };

    vi.mocked(bulkAddToCollection).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    result.current.bulkAdd({ entityIds: ['id1', 'id2', 'id3'] });

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith(
        'Impossibile aggiungere 3 giochi',
        expect.objectContaining({
          description: 'Not found',
        })
      );
    });
  });

  it('bulkRemove calls API and shows success toast', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 10,
      successCount: 10,
      failedCount: 0,
      errors: [],
    };

    vi.mocked(bulkRemoveFromCollection).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    const request = {
      entityIds: Array.from({ length: 10 }, (_, i) => `id${i}`),
    };

    result.current.bulkRemove(request);

    await waitFor(() => {
      expect(bulkRemoveFromCollection).toHaveBeenCalledWith('game', request);
      expect(toast.success).toHaveBeenCalledWith('10 giochi rimossi!');
    });
  });

  it('fetchAggregatedData fetches bulk associated data', async () => {
    const mockData = {
      totalCustomAgents: 2,
      totalPrivatePdfs: 1,
      totalChatSessions: 5,
      totalGameSessions: 10,
      totalChecklistItems: 8,
      totalLabels: 3,
    };

    vi.mocked(getBulkAssociatedData).mockResolvedValue(mockData);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    const data = await result.current.fetchAggregatedData(['id1', 'id2', 'id3']);

    expect(getBulkAssociatedData).toHaveBeenCalledWith('game', {
      entityIds: ['id1', 'id2', 'id3'],
    });
    expect(data).toEqual(mockData);
  });

  it('handles different entity types correctly', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 2,
      successCount: 2,
      failedCount: 0,
      errors: [],
    };

    vi.mocked(bulkAddToCollection).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useBulkCollectionActions('player'), {
      wrapper,
    });

    result.current.bulkAdd({ entityIds: ['id1', 'id2'] });

    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith('2 giocatori aggiunti!');
    });
  });

  it('shows loading toast during bulk add', async () => {
    vi.mocked(bulkAddToCollection).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    result.current.bulkAdd({ entityIds: ['id1'] });

    await waitFor(() => {
      expect(toast.loading).toHaveBeenCalledWith('Aggiungendo alla collezione...');
    });
  });

  it('handles API errors gracefully', async () => {
    const mockError = new Error('Network error');
    vi.mocked(bulkAddToCollection).mockRejectedValue(mockError);

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    result.current.bulkAdd({ entityIds: ['id1'] });

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith(
        "Errore durante l'aggiunta alla collezione",
        expect.objectContaining({
          description: 'Network error',
        })
      );
    });
  });

  it('invalidates queries after successful bulk add', async () => {
    const mockResult: BulkOperationResult = {
      totalRequested: 2,
      successCount: 2,
      failedCount: 0,
      errors: [],
    };

    vi.mocked(bulkAddToCollection).mockResolvedValue(mockResult);

    const invalidateQueriesSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useBulkCollectionActions('game'), {
      wrapper,
    });

    result.current.bulkAdd({ entityIds: ['id1', 'id2'] });

    await waitFor(() => {
      expect(invalidateQueriesSpy).toHaveBeenCalledWith({
        queryKey: ['user-library'],
      });
      expect(invalidateQueriesSpy).toHaveBeenCalledWith({
        queryKey: ['library-status'],
      });
    });
  });
});

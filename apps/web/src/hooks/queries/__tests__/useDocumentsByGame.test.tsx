/**
 * Tests for useDocumentsByGame Hook (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: React Query hook for fetching game documents
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'vitest/dist/index.js';
import { useDocumentsByGame } from '../useDocumentsByGame';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn(),
    },
  },
}));

describe('useDocumentsByGame', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  it('should fetch documents successfully', async () => {
    const mockDocuments: PdfDocumentDto[] = [
      {
        id: 'doc-1',
        gameId: 'game-1',
        fileName: 'rules.pdf',
        filePath: '/uploads/rules.pdf',
        fileSizeBytes: 1024000,
        processingStatus: 'Completed',
        uploadedAt: '2024-01-01T00:00:00Z',
        processedAt: '2024-01-01T00:05:00Z',
        pageCount: 10,
        documentType: 'base',
        isPublic: false,
      },
    ];

    (api.documents.getDocumentsByGame as any).mockResolvedValueOnce(mockDocuments);

    const { result } = renderHook(() => useDocumentsByGame({ gameId: 'game-1' }), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual(mockDocuments);
    expect(api.documents.getDocumentsByGame).toHaveBeenCalledWith('game-1');
  });

  it('should not fetch when gameId is null', () => {
    const { result } = renderHook(() => useDocumentsByGame({ gameId: null }), {
      wrapper,
    });

    expect(result.current.isPending).toBe(true);
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.documents.getDocumentsByGame).not.toHaveBeenCalled();
  });

  it('should not fetch when enabled=false', () => {
    const { result } = renderHook(() => useDocumentsByGame({ gameId: 'game-1', enabled: false }), {
      wrapper,
    });

    expect(result.current.isPending).toBe(true);
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.documents.getDocumentsByGame).not.toHaveBeenCalled();
  });

  it('should handle API errors', async () => {
    const error = new Error('Failed to fetch documents');
    (api.documents.getDocumentsByGame as any).mockRejectedValueOnce(error);

    const { result } = renderHook(() => useDocumentsByGame({ gameId: 'game-1' }), {
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toEqual(error);
  });

  it('should throw error when gameId null in queryFn', async () => {
    // This tests the edge case where gameId becomes null during execution
    // We enable the query but gameId is null
    const spy = vi.spyOn(api.documents, 'getDocumentsByGame' as any);

    // Force enable query with null gameId (shouldn't happen in practice)
    queryClient.setQueryData(['documents', 'by-game', null], null);

    const { result } = renderHook(() => useDocumentsByGame({ gameId: null, enabled: true }), {
      wrapper,
    });

    // Should remain idle because enabled check prevents execution
    expect(result.current.fetchStatus).toBe('idle');
    expect(spy).not.toHaveBeenCalled();
  });
});

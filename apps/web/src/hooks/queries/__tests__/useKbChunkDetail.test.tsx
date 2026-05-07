/**
 * @vitest-environment jsdom
 *
 * useKbChunkDetail unit tests — Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.3).
 *
 * Coverage:
 *   - GET /kb-docs/{id}/chunks/{chunkId} URL shape.
 *   - 24h staleTime constant (chunks immutable post-ingest).
 *   - 200 → typed payload with prev/nextChunkId.
 *   - 401/404 → null fallback.
 *   - Missing docId / chunkId → no fire.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KB_CHUNK_DETAIL_STALE_TIME_MS, useKbChunkDetail } from '../useKbChunkDetail';

vi.mock('@/lib/api/client', () => ({
  apiClient: { get: vi.fn() },
}));

import { apiClient } from '@/lib/api/client';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const DOC_ID = '11111111-1111-1111-1111-111111111111';
const CHUNK_ID = '22222222-2222-2222-2222-222222222222';
const PREV_ID = '33333333-3333-3333-3333-333333333333';
const NEXT_ID = '44444444-4444-4444-4444-444444444444';

const SAMPLE = {
  id: CHUNK_ID,
  docId: DOC_ID,
  position: 5,
  headingPath: ['Setup', 'Players'],
  content: '# Hello',
  pageNumber: 2,
  prevChunkId: PREV_ID,
  nextChunkId: NEXT_ID,
  metadata: {},
};

describe('useKbChunkDetail — constants', () => {
  it('exports 24h staleTime (chunks immutable)', () => {
    expect(KB_CHUNK_DETAIL_STALE_TIME_MS).toBe(24 * 60 * 60 * 1000);
  });
});

describe('useKbChunkDetail — happy path', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE);
  });

  it('issues GET /kb-docs/{id}/chunks/{chunkId}', async () => {
    const { result } = renderHook(() => useKbChunkDetail({ docId: DOC_ID, chunkId: CHUNK_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      `/kb-docs/${DOC_ID}/chunks/${CHUNK_ID}`,
      expect.anything()
    );
    expect(result.current.data?.position).toBe(5);
    expect(result.current.data?.prevChunkId).toBe(PREV_ID);
    expect(result.current.data?.nextChunkId).toBe(NEXT_ID);
  });
});

describe('useKbChunkDetail — null fallback', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(null);
  });

  it('returns null when apiClient yields null (401/404)', async () => {
    const { result } = renderHook(() => useKbChunkDetail({ docId: DOC_ID, chunkId: CHUNK_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });
});

describe('useKbChunkDetail — guard rails', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE);
  });

  it('does not fire when docId is null', () => {
    renderHook(() => useKbChunkDetail({ docId: null, chunkId: CHUNK_ID }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when chunkId is null', () => {
    renderHook(() => useKbChunkDetail({ docId: DOC_ID, chunkId: null }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useKbChunkDetail({ docId: DOC_ID, chunkId: CHUNK_ID, enabled: false }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
  });
});

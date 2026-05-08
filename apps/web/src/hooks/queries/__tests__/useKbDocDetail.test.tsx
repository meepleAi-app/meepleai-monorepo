/**
 * @vitest-environment jsdom
 *
 * useKbDocDetail unit tests — Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.1).
 *
 * Coverage:
 *   - GET /kb-docs/{id} URL shape + 1h staleTime constant.
 *   - 200 → ready envelope with doc payload.
 *   - 423 Locked → locked envelope with parsed processingStatus.
 *   - 401/404 → null fallback.
 *   - enabled:false / missing docId → no fire.
 *   - Locked path does NOT throw (terminal state, no retry).
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/core/errors';

import { KB_DOC_DETAIL_STALE_TIME_MS, useKbDocDetail } from '../useKbDocDetail';

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

const READY_DOC = {
  id: DOC_ID,
  title: 'Catan rulebook',
  docType: 'rulebook',
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Catan',
  uploaderName: 'Marco',
  uploadedAt: '2026-04-01T12:00:00Z',
  lastIngestedAt: '2026-04-01T12:30:00Z',
  processingStatus: 'ready',
  chunkCount: 120,
  pageCount: 32,
  language: 'it',
  tags: [],
} as const;

describe('useKbDocDetail — constants', () => {
  it('exports a 1h staleTime to mirror backend HybridCache', () => {
    expect(KB_DOC_DETAIL_STALE_TIME_MS).toBe(60 * 60 * 1000);
  });
});

describe('useKbDocDetail — happy path', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(READY_DOC);
  });

  it('issues GET /kb-docs/{id} and returns ready envelope', async () => {
    const { result } = renderHook(() => useKbDocDetail({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(`/kb-docs/${DOC_ID}`, expect.anything());
    expect(result.current.data?.status).toBe('ready');
    if (result.current.data?.status === 'ready') {
      expect(result.current.data.doc.title).toBe('Catan rulebook');
    }
  });
});

describe('useKbDocDetail — 423 Locked', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns locked envelope when backend responds 423', async () => {
    const lockedError = new ApiError({
      message: "KB document xxx is in processing state 'processing' — not yet ready.",
      statusCode: 423,
      endpoint: `/kb-docs/${DOC_ID}`,
    });
    mockGet.mockRejectedValue(lockedError);

    const { result } = renderHook(() => useKbDocDetail({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.status).toBe('locked');
    if (result.current.data?.status === 'locked') {
      expect(result.current.data.processingStatus).toBe('processing');
      expect(result.current.data.doc).toBeNull();
    }
  });

  it('parses failed processingStatus from 423 message', async () => {
    mockGet.mockRejectedValue(
      new ApiError({
        message: "Doc is in state 'failed' — not ready.",
        statusCode: 423,
      })
    );

    const { result } = renderHook(() => useKbDocDetail({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    if (result.current.data?.status === 'locked') {
      expect(result.current.data.processingStatus).toBe('failed');
    }
  });
});

describe('useKbDocDetail — null fallback', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(null); // apiClient returns null on 401
  });

  it('returns null when apiClient yields null', async () => {
    const { result } = renderHook(() => useKbDocDetail({ docId: DOC_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });
});

describe('useKbDocDetail — guard rails', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(READY_DOC);
  });

  it('does not fire when docId is null', () => {
    renderHook(() => useKbDocDetail({ docId: null }), { wrapper: createWrapper() });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when docId is empty', () => {
    renderHook(() => useKbDocDetail({ docId: '' }), { wrapper: createWrapper() });
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useKbDocDetail({ docId: DOC_ID, enabled: false }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
  });
});

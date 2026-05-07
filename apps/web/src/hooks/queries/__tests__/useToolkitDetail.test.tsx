/**
 * @vitest-environment jsdom
 *
 * useToolkitDetail unit tests — Wave 3 Phase 2 (Issue #805 / PR #732 §5.3.1).
 *
 * Coverage:
 *   - Stable URL shape `/toolkits/{id}` and 10min staleTime mirror.
 *   - Schema validation forwards typed envelope.
 *   - 404 / 401 / null fallback (apiClient returns null) → null result.
 *   - `enabled: false` defers the request.
 *   - Empty / invalid toolkitId is a no-op.
 *   - Error path surfaces the rejection.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { TOOLKIT_DETAIL_STALE_TIME_MS, useToolkitDetail } from '../useToolkitDetail';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

import { apiClient } from '@/lib/api/client';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const TOOLKIT_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_DETAIL = {
  toolkit: {
    id: TOOLKIT_ID,
    name: 'Test Toolkit',
    description: 'Toolkit by Test Author.',
    authorId: '22222222-2222-2222-2222-222222222222',
    authorName: 'Test Author',
    authorAvatarUrl: null,
    coverImageUrl: null,
    agent: {
      id: TOOLKIT_ID,
      name: 'Test Toolkit',
      systemPromptPreview: '',
    },
    kbDocsCount: 0,
    toolsCount: 0,
    installCount: 0,
    ratingAverage: null,
    ratingCount: 0,
    createdAt: '2026-04-01T12:00:00Z',
    publishedAt: '2026-05-01T08:30:00Z',
    yankedAt: null,
    currentVersion: '1.0.1',
  },
  viewerContext: {
    isOwner: false,
    hasInstalled: false,
    canRate: false,
  },
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useToolkitDetail — constants', () => {
  it('exports a 10-minute staleTime to mirror the backend HybridCache TTL', () => {
    expect(TOOLKIT_DETAIL_STALE_TIME_MS).toBe(10 * 60 * 1000);
  });
});

describe('useToolkitDetail — request shape', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_DETAIL);
  });

  it('issues GET /toolkits/{id} for the supplied toolkitId', async () => {
    const { result } = renderHook(() => useToolkitDetail({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(`/toolkits/${TOOLKIT_ID}`, expect.anything());
  });

  it('does not fire when toolkitId is null', () => {
    renderHook(() => useToolkitDetail({ toolkitId: null }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when toolkitId is empty string', () => {
    renderHook(() => useToolkitDetail({ toolkitId: '' }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useToolkitDetail({ toolkitId: TOOLKIT_ID, enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });
});

describe('useToolkitDetail — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the parsed envelope on success', async () => {
    mockGet.mockResolvedValue(SAMPLE_DETAIL);

    const { result } = renderHook(() => useToolkitDetail({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_DETAIL);
  });

  it('returns null when the backend returns null (401/404/circuit-open)', async () => {
    mockGet.mockResolvedValue(null);

    const { result } = renderHook(() => useToolkitDetail({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('network down'));

    const { result } = renderHook(() => useToolkitDetail({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('network down');
  });
});

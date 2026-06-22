/**
 * @vitest-environment jsdom
 *
 * useToolkitVersions unit tests — Wave 3 Phase 2 (Issue #805 / PR #732 §5.3.2).
 *
 * Coverage:
 *   - Stable URL `/api/v1/toolkits/{id}/versions` and 10min staleTime mirror.
 *   - Schema validation forwards typed array (items unwrapped).
 *   - Empty / null fallback returns empty array.
 *   - `enabled: false` and missing toolkitId defer the request.
 *   - Error path surfaces the rejection.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { TOOLKIT_VERSIONS_STALE_TIME_MS, useToolkitVersions } from '../useToolkitVersions';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

const mockApi = vi.hoisted<MockedApiClient>(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  head: vi.fn(),
  options: vi.fn(),
}));
vi.mock('@/lib/api/client', () => ({ apiClient: mockApi }));

const mockGet = mockApi.get;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const TOOLKIT_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_VERSIONS = {
  items: [
    {
      version: '1.0.2',
      publishedAt: '2026-05-01T10:00:00Z',
      yankedAt: null,
      changelog: 'Initial version',
      isCurrent: true,
    },
  ],
};

describe('useToolkitVersions — constants', () => {
  it('exports a 10-minute staleTime to mirror the backend HybridCache TTL', () => {
    expect(TOOLKIT_VERSIONS_STALE_TIME_MS).toBe(10 * 60 * 1000);
  });
});

describe('useToolkitVersions — request shape', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGet.mockResolvedValue(SAMPLE_VERSIONS);
  });

  it('issues GET /toolkits/{id}/versions', async () => {
    const { result } = renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGet).toHaveBeenCalledWith(
      `/api/v1/toolkits/${TOOLKIT_ID}/versions`,
      expect.anything()
    );
  });

  it('does not fire when toolkitId is null', () => {
    renderHook(() => useToolkitVersions({ toolkitId: null }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID, enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGet).not.toHaveBeenCalled();
  });
});

describe('useToolkitVersions — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('unwraps items and returns the typed array', async () => {
    mockGet.mockResolvedValue(SAMPLE_VERSIONS);

    const { result } = renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_VERSIONS.items);
  });

  it('returns an empty array on null response (401/404/circuit-open)', async () => {
    mockGet.mockResolvedValue(null);

    const { result } = renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('returns an empty array on empty-state envelope', async () => {
    mockGet.mockResolvedValue({ items: [] });

    const { result } = renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('exposes error state when the API rejects', async () => {
    mockGet.mockRejectedValue(new Error('versions fetch failed'));

    const { result } = renderHook(() => useToolkitVersions({ toolkitId: TOOLKIT_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('versions fetch failed');
  });
});

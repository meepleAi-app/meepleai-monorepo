/**
 * @vitest-environment jsdom
 *
 * useStatusBanner unit tests — Issue #1089.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  STATUS_BANNER_REFETCH_INTERVAL_MS,
  STATUS_BANNER_STALE_TIME_MS,
  useStatusBanner,
} from '../useStatusBanner';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
  },
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

const SAMPLE_BANNER = {
  messageId: 'abc-123',
  message: 'Service degraded',
  severity: 'Warning' as const,
  updatedAt: '2026-05-17T10:00:00Z',
};

describe('useStatusBanner — constants', () => {
  it('exports a 60s stale time', () => {
    expect(STATUS_BANNER_STALE_TIME_MS).toBe(60_000);
  });

  it('exports a 60s refetch interval', () => {
    expect(STATUS_BANNER_REFETCH_INTERVAL_MS).toBe(60_000);
  });
});

describe('useStatusBanner — query behaviour', () => {
  beforeEach(() => {
    mockGet.mockReset();
  });

  it('returns the banner on success', async () => {
    mockGet.mockResolvedValueOnce(SAMPLE_BANNER);
    const { result } = renderHook(() => useStatusBanner(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(SAMPLE_BANNER);
    expect(mockGet).toHaveBeenCalledWith('/api/v1/status-banner', expect.anything());
  });

  it('returns null when backend responds with 204 (apiClient resolves to null)', async () => {
    mockGet.mockResolvedValueOnce(null);
    const { result } = renderHook(() => useStatusBanner(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });

  it('surfaces errors from the underlying fetch', async () => {
    const err = new Error('network down');
    // The HttpClient retries internally; reject all attempts.
    mockGet.mockRejectedValue(err);
    const { result } = renderHook(() => useStatusBanner(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 5_000 });
    expect(result.current.error).toBe(err);
  });

  it('hits the v1 prefix endpoint', async () => {
    mockGet.mockResolvedValueOnce(null);
    renderHook(() => useStatusBanner(), { wrapper: createWrapper() });
    await waitFor(() => expect(mockGet).toHaveBeenCalled());
    const path = mockGet.mock.calls[0][0];
    expect(path).toMatch(/^\/api\/v1\//);
  });

  it('uses a stable query key for cache reuse', async () => {
    mockGet.mockResolvedValue(SAMPLE_BANNER);
    const wrapper = createWrapper();
    const { result: r1 } = renderHook(() => useStatusBanner(), { wrapper });
    const { result: r2 } = renderHook(() => useStatusBanner(), { wrapper });
    await waitFor(() => expect(r1.current.isSuccess).toBe(true));
    await waitFor(() => expect(r2.current.isSuccess).toBe(true));
    // Same query key → both hooks resolve to the same shared data reference.
    expect(r1.current.data).toEqual(r2.current.data);
  });
});

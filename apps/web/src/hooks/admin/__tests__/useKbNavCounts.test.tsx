/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useKbNavCounts } from '../useKbNavCounts';
import { fetchKbNavCounts } from '@/lib/api/admin-kb-nav-counts';

vi.mock('@/lib/api/admin-kb-nav-counts', () => ({
  fetchKbNavCounts: vi.fn(),
}));

const mockedFetchKbNavCounts = vi.mocked(fetchKbNavCounts);

function makeWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('useKbNavCounts', () => {
  beforeEach(() => {
    mockedFetchKbNavCounts.mockReset();
  });

  it('returns queue and feedback from a successful fetch', async () => {
    mockedFetchKbNavCounts.mockResolvedValueOnce({
      processingQueue: 7,
      feedback7d: 23,
      asOf: '2026-05-30T12:00:00Z',
    });

    const { result } = renderHook(() => useKbNavCounts(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.queue).toBe(7);
    expect(result.current.feedback).toBe(23);
    expect(result.current.isError).toBe(false);
  });

  it('exposes loading=true and undefined values before first settle', () => {
    mockedFetchKbNavCounts.mockImplementation(() => new Promise(() => {}));

    const { result } = renderHook(() => useKbNavCounts(), { wrapper: makeWrapper() });

    expect(result.current.loading).toBe(true);
    expect(result.current.queue).toBeUndefined();
    expect(result.current.feedback).toBeUndefined();
  });

  it('sets isError=true when the fetch throws', async () => {
    mockedFetchKbNavCounts.mockRejectedValueOnce(new Error('boom'));

    const { result } = renderHook(() => useKbNavCounts(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.queue).toBeUndefined();
    expect(result.current.feedback).toBeUndefined();
  });
});

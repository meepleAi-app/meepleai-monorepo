/**
 * useSearchBggGames - Retry & Timeout Edge Cases Tests
 * Issue #4167: BGG search retry logic and timeout handling tests
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useSearchBggGames } from '../useSearchBggGames';
import { api } from '@/lib/api';

// Mock API and toast
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    error: vi.fn(),
  },
}));

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

describe('useSearchBggGames - Edge Cases', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should retry on network error up to 3 times', async () => {
    // Fail twice, succeed on 3rd
    vi.mocked(api.bgg.search)
      .mockRejectedValueOnce(new TypeError('Network error'))
      .mockRejectedValueOnce(new TypeError('Network error'))
      .mockResolvedValueOnce({
        results: [{ bggId: 13, name: 'Catan', type: 'boardgame' }],
        totalResults: 1,
        page: 1,
        pageSize: 20,
      });

    const { toast } = await import('sonner');

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Should have retried
    expect(api.bgg.search).toHaveBeenCalledTimes(3);
    expect(toast.info).toHaveBeenCalled();
  });

  it('should fail after 3 retry attempts', async () => {
    // Fail all 3 attempts
    vi.mocked(api.bgg.search).mockRejectedValue(new TypeError('Network error'));

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    }, { timeout: 10000 });

    // Should have tried 3 times
    expect(api.bgg.search).toHaveBeenCalledTimes(3);
  });

  it('should handle timeout error (AbortError)', async () => {
    const timeoutError = new Error('BGG search timed out after 30s');
    timeoutError.name = 'Error'; // Simulated timeout message

    vi.mocked(api.bgg.search).mockRejectedValue(timeoutError);

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error?.message).toContain('timed out');
  });

  it('should not retry on 4xx client errors', async () => {
    const clientError = new Error('Bad request');
    Object.assign(clientError, { statusCode: 400 });

    vi.mocked(api.bgg.search).mockRejectedValue(clientError);

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Should only try once (no retry for 4xx)
    expect(api.bgg.search).toHaveBeenCalledOnce();
  });

  it('should retry on 5xx server errors', async () => {
    const serverError = new Error('Internal server error');
    Object.assign(serverError, { statusCode: 500 });

    // Fail once, then succeed
    vi.mocked(api.bgg.search)
      .mockRejectedValueOnce(serverError)
      .mockResolvedValueOnce({
        results: [{ bggId: 13, name: 'Catan', type: 'boardgame' }],
        totalResults: 1,
        page: 1,
        pageSize: 20,
      });

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Should have retried once
    expect(api.bgg.search).toHaveBeenCalledTimes(2);
  });

  it('should not query if query length < 2', () => {
    const { result } = renderHook(() => useSearchBggGames({ query: 'C' }), {
      wrapper: createWrapper(),
    });

    expect(result.current.isPending).toBe(false);
    expect(api.bgg.search).not.toHaveBeenCalled();
  });

  it('should not query if disabled', () => {
    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan', enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(result.current.isPending).toBe(false);
    expect(api.bgg.search).not.toHaveBeenCalled();
  });
});

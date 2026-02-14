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

/**
 * Create a wrapper that does NOT override the retry option,
 * allowing the hook's own retry configuration to apply.
 * This is important for tests that verify retry behavior.
 */
const createRetryWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        // Do NOT set retry here - let the hook's retry config apply
        gcTime: 0,
        staleTime: 0,
        refetchOnMount: false,
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
      },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

/**
 * Create a wrapper that disables retries for tests that
 * do not need retry behavior (faster tests).
 */
const createNoRetryWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
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
    // isRetryableError checks for TypeError with 'fetch' in message,
    // or statusCode >= 500, or statusCode 408/429
    vi.mocked(api.bgg.search)
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce({
        results: [{ bggId: 13, name: 'Catan', type: 'boardgame' }],
        totalResults: 1,
        page: 1,
        pageSize: 20,
      });

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createRetryWrapper(),
    });

    await waitFor(
      () => {
        expect(result.current.isSuccess).toBe(true);
      },
      { timeout: 15000 }
    );

    // Should have retried: failed 2x, succeeded on 3rd
    expect(api.bgg.search).toHaveBeenCalledTimes(3);
  }, 20000);

  it('should fail after 3 retry attempts', async () => {
    // All attempts fail with a retryable error
    vi.mocked(api.bgg.search).mockRejectedValue(new TypeError('Failed to fetch'));

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createRetryWrapper(),
    });

    await waitFor(
      () => {
        expect(result.current.isError).toBe(true);
      },
      { timeout: 30000 }
    );

    // Initial attempt + 3 retries = 4 total calls
    // (retry callback returns false when failureCount >= 3)
    expect(api.bgg.search).toHaveBeenCalledTimes(4);
  }, 35000);

  it('should handle timeout error', async () => {
    const timeoutError = new Error('BGG search timed out after 30s');

    vi.mocked(api.bgg.search).mockRejectedValue(timeoutError);

    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan' }), {
      wrapper: createNoRetryWrapper(),
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
      wrapper: createRetryWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // isRetryableError returns false for 4xx, so no retries
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
      wrapper: createRetryWrapper(),
    });

    await waitFor(
      () => {
        expect(result.current.isSuccess).toBe(true);
      },
      { timeout: 15000 }
    );

    // Should have retried once
    expect(api.bgg.search).toHaveBeenCalledTimes(2);
  }, 20000);

  it('should not query if query length < 2', () => {
    const { result } = renderHook(() => useSearchBggGames({ query: 'C' }), {
      wrapper: createNoRetryWrapper(),
    });

    // When enabled=false, React Query v5 reports fetchStatus='idle'
    // and isPending=true (data is pending since it was never fetched).
    // The key indicator that the query is disabled is fetchStatus='idle'.
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.bgg.search).not.toHaveBeenCalled();
  });

  it('should not query if disabled', () => {
    const { result } = renderHook(() => useSearchBggGames({ query: 'Catan', enabled: false }), {
      wrapper: createNoRetryWrapper(),
    });

    // Query is disabled, so fetchStatus should be 'idle'
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.bgg.search).not.toHaveBeenCalled();
  });
});

/**
 * useUploadPdf - Retry Edge Cases Tests
 * Issue #4167: Network retry logic tests
 *
 * The hook uses retryWithBackoff internally (not React Query retry).
 * We mock retryWithBackoff to execute without delays and control retry behavior.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useUploadPdf } from '../useUploadPdf';
import { api } from '@/lib/api';

// Mock API - hook uses api.sharedGames.wizardUploadPdf (not api.pdf.uploadPdf)
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      wizardUploadPdf: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock retryWithBackoff to execute synchronously without delays
vi.mock('@/lib/retryUtils', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/lib/retryUtils')>();
  return {
    ...original,
    retryWithBackoff: async <T,>(
      fn: () => Promise<T>,
      options?: {
        maxAttempts?: number;
        shouldRetry?: (error: unknown, attempt: number) => boolean;
        onRetry?: (error: unknown, attempt: number, delayMs: number) => void;
      }
    ): Promise<T> => {
      const maxAttempts = options?.maxAttempts ?? 3;
      let lastError: unknown;

      for (let attempt = 0; attempt < maxAttempts; attempt++) {
        try {
          return await fn();
        } catch (error) {
          lastError = error;
          const isLastAttempt = attempt === maxAttempts - 1;

          if (isLastAttempt || (options?.shouldRetry && !options.shouldRetry(error, attempt))) {
            throw error;
          }

          // Call onRetry without delay
          options?.onRetry?.(error, attempt + 1, 0);
        }
      }

      throw lastError;
    },
  };
});

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

describe('useUploadPdf - Retry Edge Cases', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should retry on retryable error up to 3 times', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    // Fail twice, succeed on 3rd attempt
    // isRetryableError checks TypeError with message containing 'fetch'
    vi.mocked(api.sharedGames.wizardUploadPdf)
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce({ documentId: 'doc-123', fileName: 'test.pdf' });

    const { toast } = await import('sonner');

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Should have called wizardUploadPdf 3 times (2 failures + 1 success)
    expect(api.sharedGames.wizardUploadPdf).toHaveBeenCalledTimes(3);

    // Should have shown retry toast 2 times
    expect(toast.info).toHaveBeenCalledTimes(2);
    // Toast message format: "Upload failed. Retrying... (attempt N/3)"
    expect(toast.info).toHaveBeenCalledWith(expect.stringContaining('Retrying'));
  });

  it('should fail after 3 retry attempts', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    // Fail all 3 attempts with retryable error
    vi.mocked(api.sharedGames.wizardUploadPdf).mockRejectedValue(
      new TypeError('Failed to fetch')
    );

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Should have tried 3 times
    expect(api.sharedGames.wizardUploadPdf).toHaveBeenCalledTimes(3);
  });

  it('should not retry on non-retryable errors (4xx)', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    const clientError = new Error('Bad request');
    Object.assign(clientError, { statusCode: 400 });

    vi.mocked(api.sharedGames.wizardUploadPdf).mockRejectedValue(clientError);

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Should only try once (no retry for 4xx)
    expect(api.sharedGames.wizardUploadPdf).toHaveBeenCalledOnce();
  });

  it('should reset progress on error', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    // wizardUploadPdf signature: (file, onProgress?) => Promise
    vi.mocked(api.sharedGames.wizardUploadPdf).mockImplementation(
      async (_file: File, onProgress?: (percent: number) => void) => {
        onProgress?.(50);
        throw new Error('Upload failed');
      }
    );

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    // Error occurs and progress resets to 0
    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.progress).toBe(0);
  });
});

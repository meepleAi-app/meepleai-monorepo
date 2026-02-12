/**
 * useUploadPdf - Retry Edge Cases Tests
 * Issue #4167: Network retry logic tests
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useUploadPdf } from '../useUploadPdf';
import { api } from '@/lib/api';

// Mock API and toast
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      uploadPdf: vi.fn(),
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

  it('should retry on network error up to 3 times', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    // Fail twice, succeed on 3rd attempt
    vi.mocked(api.pdf.uploadPdf)
      .mockRejectedValueOnce(new TypeError('Network error'))
      .mockRejectedValueOnce(new TypeError('Network error'))
      .mockResolvedValueOnce({ documentId: 'doc-123', fileName: 'test.pdf' });

    const { toast } = await import('sonner');

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Should have called uploadPdf 3 times (2 failures + 1 success)
    expect(api.pdf.uploadPdf).toHaveBeenCalledTimes(3);

    // Should have shown retry toast 2 times
    expect(toast.info).toHaveBeenCalledTimes(2);
    expect(toast.info).toHaveBeenCalledWith(expect.stringContaining('Retrying'));
  });

  it('should fail after 3 retry attempts', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    // Fail all 3 attempts
    vi.mocked(api.pdf.uploadPdf).mockRejectedValue(new TypeError('Network error'));

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Should have tried 3 times
    expect(api.pdf.uploadPdf).toHaveBeenCalledTimes(3);
  });

  it('should not retry on non-retryable errors (4xx)', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    const clientError = new Error('Bad request');
    Object.assign(clientError, { statusCode: 400 });

    vi.mocked(api.pdf.uploadPdf).mockRejectedValue(clientError);

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Should only try once (no retry for 4xx)
    expect(api.pdf.uploadPdf).toHaveBeenCalledOnce();
  });

  it('should reset progress on error', async () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    vi.mocked(api.pdf.uploadPdf).mockImplementation(async (_, __, onProgress) => {
      // Simulate progress then fail
      onProgress?.(50);
      throw new Error('Upload failed');
    });

    const { result } = renderHook(() => useUploadPdf(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(file);

    // Progress should increase
    await waitFor(() => {
      expect(result.current.progress).toBeGreaterThan(0);
    });

    // Then error occurs
    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    // Progress should reset to 0
    expect(result.current.progress).toBe(0);
  });
});

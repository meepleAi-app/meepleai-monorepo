/**
 * Tests for useCustomCoverUpload + useRemoveCustomCover (L3 #1824).
 *
 * Covers:
 *   - Happy path upload (POST multipart → 201)
 *   - Happy path remove (DELETE → 204)
 *   - 403 error surface on upload
 *   - 404 error surface on remove
 *   - Query invalidation on success
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useCustomCoverUpload } from '../useCustomCoverUpload';
import { useRemoveCustomCover } from '../useRemoveCustomCover';
import { ApiError } from '@/lib/api/core/errors';

// ---------------------------------------------------------------------------
// Mock global fetch
// ---------------------------------------------------------------------------
const mockFetch = vi.fn();
global.fetch = mockFetch;

// ---------------------------------------------------------------------------
// Wrapper factory
// ---------------------------------------------------------------------------
function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const GAME_ID = '00000000-0000-0000-0000-000000000042';

// ---------------------------------------------------------------------------
// useCustomCoverUpload
// ---------------------------------------------------------------------------
describe('useCustomCoverUpload (L3 #1824)', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  it('uploads blob via POST multipart to correct endpoint', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () =>
        Promise.resolve({
          coverR2Key: `user-covers/u/${GAME_ID}/cover`,
          presignedUrl: 'https://r2.example.com/signed-url',
        }),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useCustomCoverUpload(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    const blob = new Blob([new ArrayBuffer(10_000)], { type: 'image/webp' });

    await act(async () => {
      await result.current.mutateAsync(blob);
    });

    expect(mockFetch).toHaveBeenCalledWith(
      `/api/v1/library/${GAME_ID}/cover`,
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: expect.any(FormData),
      })
    );
  });

  it('resolves with coverR2Key and presignedUrl on success', async () => {
    const expected = {
      coverR2Key: `user-covers/u/${GAME_ID}/cover`,
      presignedUrl: 'https://r2.example.com/signed-url',
    };
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () => Promise.resolve(expected),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useCustomCoverUpload(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    const blob = new Blob([new ArrayBuffer(10_000)], { type: 'image/webp' });
    let data: { coverR2Key: string; presignedUrl: string } | undefined;

    await act(async () => {
      data = await result.current.mutateAsync(blob);
    });

    expect(data).toEqual(expected);
  });

  it('throws ApiError with status 403 when game not in library', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 403,
      json: () => Promise.resolve({ error: 'Game not in library' }),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useCustomCoverUpload(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    const blob = new Blob([new ArrayBuffer(100)], { type: 'image/webp' });

    await act(async () => {
      try {
        await result.current.mutateAsync(blob);
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).statusCode).toBe(403);
  });

  it('invalidates library queries on success', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () =>
        Promise.resolve({
          coverR2Key: `user-covers/u/${GAME_ID}/cover`,
          presignedUrl: 'https://r2.example.com/signed-url',
        }),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useCustomCoverUpload(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    const blob = new Blob([new ArrayBuffer(10_000)], { type: 'image/webp' });

    await act(async () => {
      await result.current.mutateAsync(blob);
    });

    // Should invalidate both specific game detail and the broad library key
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: expect.arrayContaining(['library', 'detail', GAME_ID]) })
    );
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: expect.arrayContaining(['library']) })
    );
  });

  it('does NOT invalidate queries on error', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 413,
      json: () => Promise.resolve({ error: 'File too large' }),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useCustomCoverUpload(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    const blob = new Blob([new ArrayBuffer(100)], { type: 'image/webp' });

    await act(async () => {
      try {
        await result.current.mutateAsync(blob);
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(invalidateSpy).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// useRemoveCustomCover
// ---------------------------------------------------------------------------
describe('useRemoveCustomCover (L3 #1824)', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  it('sends DELETE request to correct endpoint', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      json: () => Promise.reject(new Error('no body')), // 204 has no body
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useRemoveCustomCover(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(mockFetch).toHaveBeenCalledWith(
      `/api/v1/library/${GAME_ID}/cover`,
      expect.objectContaining({
        method: 'DELETE',
        credentials: 'include',
      })
    );
  });

  it('throws ApiError with status 404 when cover not found', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 404,
      json: () => Promise.resolve({ error: 'Custom cover not found' }),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useRemoveCustomCover(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync();
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ApiError);
    expect((result.current.error as ApiError).statusCode).toBe(404);
  });

  it('invalidates library queries on success', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 204,
      json: () => Promise.reject(new Error('no body')),
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useRemoveCustomCover(GAME_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: expect.arrayContaining(['library', 'detail', GAME_ID]) })
    );
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: expect.arrayContaining(['library']) })
    );
  });
});

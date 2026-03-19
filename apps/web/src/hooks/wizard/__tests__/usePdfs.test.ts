/**
 * usePdfs Hook Tests
 * Issue #3005: Frontend Test Coverage Improvement
 *
 * Tests for PDF documents fetching and management hook.
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

const mockLoggerError = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  },
  getLogger: () => ({
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

import { usePdfs } from '../usePdfs';

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('usePdfs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset process.env
    vi.stubEnv('NEXT_PUBLIC_API_BASE', 'http://localhost:8080');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  describe('Initial State', () => {
    it('should return empty pdfs array initially', () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: [] }),
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      expect(result.current.pdfs).toEqual([]);
      expect(result.current.error).toBeNull();
    });

    it('should set loading to true while fetching', async () => {
      let resolvePromise: () => void;
      const fetchPromise = new Promise<void>(resolve => {
        resolvePromise = resolve;
      });

      mockFetch.mockImplementationOnce(async () => {
        await fetchPromise;
        return {
          ok: true,
          json: async () => ({ pdfs: [] }),
        };
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      expect(result.current.loading).toBe(true);

      await act(async () => {
        resolvePromise!();
      });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });

    it('should not fetch when gameId is null', () => {
      const { result } = renderHook(() => usePdfs(null));

      expect(result.current.pdfs).toEqual([]);
      expect(result.current.loading).toBe(false);
      expect(result.current.error).toBeNull();
      expect(mockFetch).not.toHaveBeenCalled();
    });
  });

  describe('fetchPdfs', () => {
    it('should fetch PDFs for a game', async () => {
      const mockPdfs = [
        {
          id: 'pdf-1',
          fileName: 'rules.pdf',
          fileSizeBytes: 1024,
          uploadedAt: '2024-01-01T00:00:00Z',
          uploadedByUserId: 'user-1',
          language: 'en',
          status: 'completed',
        },
        {
          id: 'pdf-2',
          fileName: 'quick-start.pdf',
          fileSizeBytes: 512,
          uploadedAt: '2024-01-02T00:00:00Z',
          uploadedByUserId: 'user-1',
          language: 'en',
          status: 'completed',
        },
      ];

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: mockPdfs }),
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.pdfs).toEqual(mockPdfs);
      expect(result.current.error).toBeNull();
      expect(mockFetch).toHaveBeenCalledWith('http://localhost:8080/api/v1/games/game-123/pdfs', {
        credentials: 'include',
      });
    });

    it('should handle empty response', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({}),
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.pdfs).toEqual([]);
    });

    it('should handle API error response', async () => {
      mockLoggerError.mockClear();

      mockFetch.mockResolvedValueOnce({
        ok: false,
        statusText: 'Not Found',
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.error).toBe('Unable to load uploaded PDFs. Please try again.');
      expect(result.current.pdfs).toEqual([]);
      expect(mockLoggerError).toHaveBeenCalledWith(expect.stringContaining('Failed to load PDFs'));
    });

    it('should handle network error', async () => {
      mockLoggerError.mockClear();

      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.error).toBe('Unable to load uploaded PDFs. Please try again.');
      expect(mockLoggerError).toHaveBeenCalled();
    });
  });

  describe('refetch', () => {
    it('should allow manual refetch', async () => {
      const mockPdfs1 = [
        {
          id: 'pdf-1',
          fileName: 'old.pdf',
          fileSizeBytes: 100,
          uploadedAt: '2024-01-01T00:00:00Z',
          uploadedByUserId: 'user-1',
        },
      ];
      const mockPdfs2 = [
        {
          id: 'pdf-1',
          fileName: 'old.pdf',
          fileSizeBytes: 100,
          uploadedAt: '2024-01-01T00:00:00Z',
          uploadedByUserId: 'user-1',
        },
        {
          id: 'pdf-2',
          fileName: 'new.pdf',
          fileSizeBytes: 200,
          uploadedAt: '2024-01-02T00:00:00Z',
          uploadedByUserId: 'user-1',
        },
      ];

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: mockPdfs1 }),
      });

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.pdfs).toEqual(mockPdfs1);
      });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: mockPdfs2 }),
      });

      await act(async () => {
        await result.current.refetch();
      });

      expect(result.current.pdfs).toEqual(mockPdfs2);
    });

    it('should clear error on successful refetch', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => usePdfs('game-123'));

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to load uploaded PDFs. Please try again.');
      });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: [] }),
      });

      await act(async () => {
        await result.current.refetch();
      });

      expect(result.current.error).toBeNull();

      consoleSpy.mockRestore();
    });
  });

  describe('gameId changes', () => {
    it('should refetch when gameId changes', async () => {
      const mockPdfs1 = [
        {
          id: 'pdf-1',
          fileName: 'game1.pdf',
          fileSizeBytes: 100,
          uploadedAt: '2024-01-01T00:00:00Z',
          uploadedByUserId: 'user-1',
        },
      ];
      const mockPdfs2 = [
        {
          id: 'pdf-2',
          fileName: 'game2.pdf',
          fileSizeBytes: 200,
          uploadedAt: '2024-01-02T00:00:00Z',
          uploadedByUserId: 'user-1',
        },
      ];

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: mockPdfs1 }),
      });

      const { result, rerender } = renderHook(({ gameId }) => usePdfs(gameId), {
        initialProps: { gameId: 'game-1' },
      });

      await waitFor(() => {
        expect(result.current.pdfs).toEqual(mockPdfs1);
      });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({ pdfs: mockPdfs2 }),
      });

      rerender({ gameId: 'game-2' });

      await waitFor(() => {
        expect(result.current.pdfs).toEqual(mockPdfs2);
      });

      expect(mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should clear pdfs when gameId becomes null', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          pdfs: [
            {
              id: 'pdf-1',
              fileName: 'test.pdf',
              fileSizeBytes: 100,
              uploadedAt: '2024-01-01T00:00:00Z',
              uploadedByUserId: 'user-1',
            },
          ],
        }),
      });

      const { result, rerender } = renderHook(({ gameId }) => usePdfs(gameId), {
        initialProps: { gameId: 'game-1' as string | null },
      });

      await waitFor(() => {
        expect(result.current.pdfs).toHaveLength(1);
      });

      rerender({ gameId: null });

      expect(result.current.pdfs).toEqual([]);
      expect(result.current.error).toBeNull();
    });
  });
});

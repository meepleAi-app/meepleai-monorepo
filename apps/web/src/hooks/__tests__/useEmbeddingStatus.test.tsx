/**
 * useEmbeddingStatus Hook Tests (Issue #4065)
 *
 * Tests for Knowledge Base embedding status polling hook.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useEmbeddingStatus } from '../useEmbeddingStatus';

import type { KnowledgeBaseStatus } from '@/lib/api/schemas/knowledge-base.schemas';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getEmbeddingStatus: vi.fn(),
    },
  },
}));

// Import after mock
import { api } from '@/lib/api';

const mockGetStatus = vi.mocked(api.knowledgeBase.getEmbeddingStatus);

function createStatus(overrides: Partial<KnowledgeBaseStatus> = {}): KnowledgeBaseStatus {
  return {
    status: 'Embedding',
    progress: 50,
    totalChunks: 120,
    processedChunks: 60,
    errorMessage: null,
    gameName: 'Test Game',
    ...overrides,
  };
}

describe('useEmbeddingStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ===== Basic behavior =====

  describe('Basic behavior', () => {
    it('returns initial loading state', () => {
      mockGetStatus.mockResolvedValue(createStatus());

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      expect(result.current.isLoading).toBe(true);
      expect(result.current.data).toBeNull();
    });

    it('fetches status on mount when enabled', async () => {
      mockGetStatus.mockResolvedValue(createStatus());

      renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(mockGetStatus).toHaveBeenCalledWith('game-1');
    });

    it('does not fetch when gameId is null', () => {
      renderHook(() => useEmbeddingStatus(null));

      expect(mockGetStatus).not.toHaveBeenCalled();
    });

    it('does not fetch when disabled', () => {
      renderHook(() => useEmbeddingStatus('game-1', { enabled: false }));

      expect(mockGetStatus).not.toHaveBeenCalled();
    });

    it('returns data after successful fetch', async () => {
      const status = createStatus({ status: 'Embedding', progress: 75, processedChunks: 90 });
      mockGetStatus.mockResolvedValue(status);

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.data).toEqual(status);
      expect(result.current.isLoading).toBe(false);
    });
  });

  // ===== Polling behavior =====

  describe('Polling behavior', () => {
    it('polls at default interval (2000ms)', async () => {
      mockGetStatus.mockResolvedValue(createStatus());

      renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(2);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(3);
    });

    it('uses custom polling interval', async () => {
      mockGetStatus.mockResolvedValue(createStatus());

      renderHook(() => useEmbeddingStatus('game-1', { pollingInterval: 5000 }));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(2);
    });

    it('stops polling on Completed status', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Completed', progress: 100 }));

      renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(mockGetStatus).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });
      // Should NOT poll again after terminal state
      expect(mockGetStatus).toHaveBeenCalledTimes(1);
    });

    it('stops polling on Failed status', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Failed', errorMessage: 'OOM' }));

      renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });

      expect(mockGetStatus).toHaveBeenCalledTimes(1);
    });
  });

  // ===== Status flags =====

  describe('Status flags', () => {
    it('isReady is true when Completed', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Completed', progress: 100 }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.isReady).toBe(true);
      expect(result.current.isFailed).toBe(false);
      expect(result.current.isPolling).toBe(false);
    });

    it('isFailed is true when Failed', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Failed' }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.isFailed).toBe(true);
      expect(result.current.isReady).toBe(false);
    });

    it('isPolling is true during active processing', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Embedding' }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.isPolling).toBe(true);
    });
  });

  // ===== Labels and progress =====

  describe('Labels and progress', () => {
    it('returns chunk progress string', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ processedChunks: 87, totalChunks: 120 }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.chunkProgress).toBe('87/120');
    });

    it('returns embedding stage label with progress', async () => {
      mockGetStatus.mockResolvedValue(
        createStatus({ status: 'Embedding', progress: 75, processedChunks: 90, totalChunks: 120 }),
      );

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.stageLabel).toBe('Generazione embeddings: 90/120 (75%)');
    });

    it('returns chunking stage label with progress', async () => {
      mockGetStatus.mockResolvedValue(
        createStatus({ status: 'Chunking', processedChunks: 40, totalChunks: 120 }),
      );

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.stageLabel).toBe('Creazione chunks: 40/120');
    });

    it('returns completed stage label', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Completed' }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.stageLabel).toBe('Knowledge Base pronta!');
    });

    it('returns default progress for no data', () => {
      const { result } = renderHook(() => useEmbeddingStatus(null));

      expect(result.current.chunkProgress).toBe('0/0');
      expect(result.current.stageLabel).toBe('In attesa...');
    });
  });

  // ===== Callbacks =====

  describe('Callbacks', () => {
    it('calls onReady when status becomes Completed', async () => {
      const onReady = vi.fn();
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Completed', gameName: 'Catan' }));

      renderHook(() => useEmbeddingStatus('game-1', { onReady }));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(onReady).toHaveBeenCalledWith('Catan');
      expect(onReady).toHaveBeenCalledTimes(1);
    });

    it('calls onError when status becomes Failed', async () => {
      const onError = vi.fn();
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Failed', errorMessage: 'OOM error' }));

      renderHook(() => useEmbeddingStatus('game-1', { onError }));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(onError).toHaveBeenCalledWith('OOM error');
    });

    it('does not call callbacks twice', async () => {
      const onReady = vi.fn();
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Completed' }));

      const { result } = renderHook(() => useEmbeddingStatus('game-1', { onReady }));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      // Trigger refetch
      await act(async () => {
        result.current.refetch();
        await vi.advanceTimersByTimeAsync(0);
      });

      // onReady called once: refetch resets flag but fetchStatus skips terminal state
      expect(onReady).toHaveBeenCalledTimes(1);
    });
  });

  // ===== Error handling =====

  describe('Error handling', () => {
    it('sets error after max retries', async () => {
      mockGetStatus.mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      // Advance through initial fetch + retries + polling cycles
      // (waitFor incompatible with fake timers, so advance explicitly)
      for (let i = 0; i < 10; i++) {
        await act(async () => {
          await vi.advanceTimersByTimeAsync(1500);
        });
      }

      expect(result.current.error).not.toBeNull();
      expect(result.current.error?.message).toBe('Network error');
    });

    it('refetch resets error state', async () => {
      mockGetStatus
        .mockRejectedValueOnce(new Error('Network error'))
        .mockRejectedValueOnce(new Error('Network error'))
        .mockRejectedValueOnce(new Error('Network error'))
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce(createStatus());

      const { result } = renderHook(() => useEmbeddingStatus('game-1'));

      // Exhaust retries
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });
      for (let i = 0; i < 3; i++) {
        await act(async () => {
          await vi.advanceTimersByTimeAsync(1500);
        });
      }

      // Now refetch
      await act(async () => {
        result.current.refetch();
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.error).toBeNull();
    });
  });

  // ===== Cleanup =====

  describe('Cleanup', () => {
    it('resets state on gameId change', async () => {
      mockGetStatus.mockResolvedValue(createStatus({ status: 'Embedding' }));

      const { result, rerender } = renderHook(
        ({ gameId }) => useEmbeddingStatus(gameId),
        { initialProps: { gameId: 'game-1' as string | null } },
      );

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(result.current.data).not.toBeNull();

      rerender({ gameId: 'game-2' });

      expect(result.current.data).toBeNull();
    });

    it('stops polling on unmount', async () => {
      mockGetStatus.mockResolvedValue(createStatus());

      const { unmount } = renderHook(() => useEmbeddingStatus('game-1'));

      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      unmount();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(10000);
      });

      // Only the initial fetch should have been called
      expect(mockGetStatus).toHaveBeenCalledTimes(1);
    });
  });
});

const MAX_RETRY_ATTEMPTS = 3;

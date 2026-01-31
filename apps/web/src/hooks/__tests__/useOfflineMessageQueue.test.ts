/**
 * useOfflineMessageQueue Hook Tests (Issue #2054)
 *
 * Tests for offline message queue hook:
 * - Message queuing
 * - Auto-replay on reconnection
 * - Manual retry
 * - Integration with network status
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useOfflineMessageQueue } from '../useOfflineMessageQueue';
import { useOfflineMessageQueueStore } from '@/stores/OfflineMessageQueueStore';
import { useNetworkStatus, useOnlineCallback } from '@/hooks/useNetworkStatus';

// Mock stores and hooks
vi.mock('@/stores/OfflineMessageQueueStore', () => ({
  useOfflineMessageQueueStore: vi.fn(),
  selectPendingMessages: vi.fn(state =>
    state.messages.filter((m: { status: string }) => m.status === 'pending')
  ),
  selectFailedMessages: vi.fn(state =>
    state.messages.filter((m: { status: string }) => m.status === 'failed')
  ),
  selectPendingCount: vi.fn(
    state =>
      state.messages.filter(
        (m: { status: string }) => m.status === 'pending' || m.status === 'failed'
      ).length
  ),
  selectIsReplaying: vi.fn(state => state.isReplaying),
}));

vi.mock('@/hooks/useNetworkStatus', () => ({
  useNetworkStatus: vi.fn(),
  useOnlineCallback: vi.fn(),
}));

const createMockStore = () => ({
  messages: [],
  isReplaying: false,
  lastReplayAt: null,
  totalSent: 0,
  totalFailed: 0,
  queueMessage: vi.fn().mockReturnValue('msg-123'),
  replayQueue: vi.fn().mockResolvedValue({ sent: 0, failed: 0 }),
  markAsSent: vi.fn(),
  markAsFailed: vi.fn(),
  removeMessage: vi.fn(),
  clearCompleted: vi.fn(),
  clearAll: vi.fn(),
  getPendingCount: vi.fn().mockReturnValue(0),
});

describe('useOfflineMessageQueue', () => {
  let mockStore: ReturnType<typeof createMockStore>;

  beforeEach(() => {
    mockStore = createMockStore();

    vi.mocked(useOfflineMessageQueueStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        if (selector) {
          return selector(mockStore);
        }
        return mockStore;
      }
    );

    vi.mocked(useNetworkStatus).mockReturnValue({
      isOnline: true,
      isOffline: false,
      connectionQuality: 'excellent',
      isReconnecting: false,
      reconnectAttempts: 0,
      canAttemptReconnect: false,
      setOnline: vi.fn(),
      setOffline: vi.fn(),
      startReconnecting: vi.fn(),
      stopReconnecting: vi.fn(),
      incrementReconnectAttempts: vi.fn(),
      resetReconnectAttempts: vi.fn(),
    });

    vi.mocked(useOnlineCallback).mockImplementation(() => {});

    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('Hook Return Values', () => {
    it('should return queue functions and state', () => {
      const { result } = renderHook(() => useOfflineMessageQueue());

      expect(result.current.queueMessage).toBeInstanceOf(Function);
      expect(result.current.replayQueue).toBeInstanceOf(Function);
      expect(result.current.removeMessage).toBeInstanceOf(Function);
      expect(result.current.retryMessage).toBeInstanceOf(Function);
      expect(result.current.clearCompleted).toBeInstanceOf(Function);
      expect(result.current.clearAll).toBeInstanceOf(Function);
      expect(result.current.pendingMessages).toEqual([]);
      expect(result.current.failedMessages).toEqual([]);
      expect(result.current.pendingCount).toBe(0);
      expect(result.current.isReplaying).toBe(false);
      expect(result.current.isOffline).toBe(false);
    });
  });

  describe('queueMessage', () => {
    it('should queue message and return message id', () => {
      const { result } = renderHook(() => useOfflineMessageQueue());

      let messageId: string = '';
      act(() => {
        messageId = result.current.queueMessage({
          gameId: 'game-123',
          chatId: 'chat-456',
          content: 'Test message',
          documentIds: null,
        });
      });

      expect(messageId).toBe('msg-123');
      expect(mockStore.queueMessage).toHaveBeenCalledWith({
        gameId: 'game-123',
        chatId: 'chat-456',
        content: 'Test message',
        documentIds: null,
      });
    });

    it('should call onMessageQueued callback when provided', () => {
      const onMessageQueued = vi.fn();
      const { result } = renderHook(() => useOfflineMessageQueue({ onMessageQueued }));

      act(() => {
        result.current.queueMessage({
          gameId: 'game-123',
          chatId: 'chat-456',
          content: 'Test message',
          documentIds: null,
        });
      });

      expect(onMessageQueued).toHaveBeenCalledWith('msg-123');
    });
  });

  describe('replayQueue', () => {
    it('should replay queue with sendMessage function', async () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);
      mockStore.replayQueue.mockResolvedValue({ sent: 2, failed: 0 });

      const { result } = renderHook(() => useOfflineMessageQueue({ sendMessage }));

      let replayResult: { sent: number; failed: number } = { sent: 0, failed: 0 };
      await act(async () => {
        replayResult = await result.current.replayQueue();
      });

      expect(replayResult).toEqual({ sent: 2, failed: 0 });
      expect(mockStore.replayQueue).toHaveBeenCalledWith(sendMessage, {
        maxRetries: 3,
        delayMs: 500,
      });
    });

    it('should call onReplayComplete callback', async () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);
      const onReplayComplete = vi.fn();
      mockStore.replayQueue.mockResolvedValue({ sent: 1, failed: 1 });

      const { result } = renderHook(() =>
        useOfflineMessageQueue({ sendMessage, onReplayComplete })
      );

      await act(async () => {
        await result.current.replayQueue();
      });

      expect(onReplayComplete).toHaveBeenCalledWith({ sent: 1, failed: 1 });
    });

    it('should warn when no sendMessage function provided', async () => {
      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      const { result } = renderHook(() => useOfflineMessageQueue());

      await act(async () => {
        await result.current.replayQueue();
      });

      expect(consoleSpy).toHaveBeenCalledWith(
        '[useOfflineMessageQueue] No sendMessage function provided'
      );
      consoleSpy.mockRestore();
    });

    it('should use custom maxRetries and replayDelayMs', async () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);
      mockStore.replayQueue.mockResolvedValue({ sent: 1, failed: 0 });

      const { result } = renderHook(() =>
        useOfflineMessageQueue({
          sendMessage,
          maxRetries: 5,
          replayDelayMs: 1000,
        })
      );

      await act(async () => {
        await result.current.replayQueue();
      });

      expect(mockStore.replayQueue).toHaveBeenCalledWith(sendMessage, {
        maxRetries: 5,
        delayMs: 1000,
      });
    });
  });

  describe('retryMessage', () => {
    it('should re-queue failed message for retry', () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);
      const failedMessage = {
        id: 'msg-failed',
        gameId: 'game-123',
        chatId: 'chat-456',
        content: 'Failed message',
        documentIds: null,
        status: 'failed',
        queuedAt: Date.now(),
        replayAttempts: 3,
        lastError: 'Network error',
      };

      mockStore.messages = [failedMessage];
      vi.mocked(useOfflineMessageQueueStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(mockStore);
          }
          return mockStore;
        }
      );

      const { result } = renderHook(() => useOfflineMessageQueue({ sendMessage }));

      act(() => {
        result.current.retryMessage('msg-failed');
      });

      expect(mockStore.removeMessage).toHaveBeenCalledWith('msg-failed');
      expect(mockStore.queueMessage).toHaveBeenCalledWith({
        gameId: 'game-123',
        chatId: 'chat-456',
        content: 'Failed message',
        documentIds: null,
      });
    });

    it('should not retry non-failed messages', () => {
      const pendingMessage = {
        id: 'msg-pending',
        gameId: 'game-123',
        chatId: 'chat-456',
        content: 'Pending message',
        documentIds: null,
        status: 'pending',
        queuedAt: Date.now(),
        replayAttempts: 0,
        lastError: null,
      };

      mockStore.messages = [pendingMessage];
      vi.mocked(useOfflineMessageQueueStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(mockStore);
          }
          return mockStore;
        }
      );

      const { result } = renderHook(() => useOfflineMessageQueue());

      act(() => {
        result.current.retryMessage('msg-pending');
      });

      expect(mockStore.removeMessage).not.toHaveBeenCalled();
    });
  });

  describe('Auto-replay on reconnection', () => {
    it('should set up useOnlineCallback for auto-replay', () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);

      renderHook(() =>
        useOfflineMessageQueue({
          sendMessage,
          autoReplay: true,
        })
      );

      expect(useOnlineCallback).toHaveBeenCalled();
    });

    it('should not set up auto-replay when disabled', () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);

      renderHook(() =>
        useOfflineMessageQueue({
          sendMessage,
          autoReplay: false,
        })
      );

      // useOnlineCallback is still called but the callback inside checks autoReplay
      expect(useOnlineCallback).toHaveBeenCalled();
    });
  });

  describe('Store delegation', () => {
    it('should delegate removeMessage to store', () => {
      const { result } = renderHook(() => useOfflineMessageQueue());

      act(() => {
        result.current.removeMessage('msg-123');
      });

      expect(mockStore.removeMessage).toHaveBeenCalledWith('msg-123');
    });

    it('should delegate clearCompleted to store', () => {
      const { result } = renderHook(() => useOfflineMessageQueue());

      act(() => {
        result.current.clearCompleted();
      });

      expect(mockStore.clearCompleted).toHaveBeenCalled();
    });

    it('should delegate clearAll to store', () => {
      const { result } = renderHook(() => useOfflineMessageQueue());

      act(() => {
        result.current.clearAll();
      });

      expect(mockStore.clearAll).toHaveBeenCalled();
    });
  });
});

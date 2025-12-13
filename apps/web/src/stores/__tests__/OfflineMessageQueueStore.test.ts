/**
 * Offline Message Queue Store Tests (Issue #2054)
 *
 * Tests for offline message queue Zustand store:
 * - Message queuing
 * - Queue replay with send function
 * - Message deduplication
 * - Status tracking (pending, replaying, failed, sent)
 * - Retry logic
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import {
  useOfflineMessageQueueStore,
  selectPendingMessages,
  selectFailedMessages,
  selectPendingCount,
  selectIsReplaying,
  type QueuedMessage,
} from '../OfflineMessageQueueStore';

// Helper to create mock message input
const createMessageInput = (
  overrides?: Partial<
    Omit<QueuedMessage, 'id' | 'queuedAt' | 'replayAttempts' | 'lastError' | 'status'>
  >
) => ({
  gameId: 'game-123',
  chatId: 'chat-456',
  content: 'Test message',
  documentIds: null,
  ...overrides,
});

describe('useOfflineMessageQueueStore', () => {
  beforeEach(() => {
    // Reset store state manually via setState to ensure clean state
    useOfflineMessageQueueStore.setState({
      messages: [],
      isReplaying: false,
      lastReplayAt: null,
      totalSent: 0,
      totalFailed: 0,
    });
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should initialize with empty queue', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      expect(result.current.messages).toEqual([]);
      expect(result.current.isReplaying).toBe(false);
      expect(result.current.lastReplayAt).toBeNull();
      expect(result.current.totalSent).toBe(0);
      expect(result.current.totalFailed).toBe(0);
    });
  });

  describe('queueMessage', () => {
    it('should add message to queue with generated id', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const input = createMessageInput();

      let messageId: string = '';
      act(() => {
        messageId = result.current.queueMessage(input);
      });

      expect(messageId).toMatch(/^msg_\d+_[a-z0-9]+$/);
      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0]).toMatchObject({
        ...input,
        id: messageId,
        status: 'pending',
        replayAttempts: 0,
        lastError: null,
      });
    });

    it('should set queuedAt timestamp', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const before = Date.now();

      act(() => {
        result.current.queueMessage(createMessageInput());
      });

      const after = Date.now();
      const queuedAt = result.current.messages[0]?.queuedAt ?? 0;
      expect(queuedAt).toBeGreaterThanOrEqual(before);
      expect(queuedAt).toBeLessThanOrEqual(after);
    });

    it('should prevent duplicate messages within 5 seconds', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const input = createMessageInput();

      act(() => {
        result.current.queueMessage(input);
        result.current.queueMessage(input); // Duplicate
      });

      expect(result.current.messages).toHaveLength(1);
    });

    it('should allow same content for different game/chat', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        result.current.queueMessage(createMessageInput({ gameId: 'game-1', chatId: 'chat-1' }));
        result.current.queueMessage(createMessageInput({ gameId: 'game-2', chatId: 'chat-1' }));
        result.current.queueMessage(createMessageInput({ gameId: 'game-1', chatId: 'chat-2' }));
      });

      expect(result.current.messages).toHaveLength(3);
    });

    it('should handle null chatId', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const input = createMessageInput({ chatId: null });

      act(() => {
        result.current.queueMessage(input);
      });

      expect(result.current.messages[0]?.chatId).toBeNull();
    });

    it('should handle documentIds array', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const input = createMessageInput({ documentIds: ['doc-1', 'doc-2'] });

      act(() => {
        result.current.queueMessage(input);
      });

      expect(result.current.messages[0]?.documentIds).toEqual(['doc-1', 'doc-2']);
    });
  });

  describe('replayQueue', () => {
    it('should replay pending messages in order', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const sendFn = vi.fn().mockResolvedValue(undefined);

      act(() => {
        result.current.queueMessage(createMessageInput({ content: 'First' }));
        result.current.queueMessage(createMessageInput({ content: 'Second' }));
      });

      let replayResult: { sent: number; failed: number } = { sent: 0, failed: 0 };
      await act(async () => {
        replayResult = await result.current.replayQueue(sendFn, { delayMs: 0 });
      });

      expect(sendFn).toHaveBeenCalledTimes(2);
      expect(replayResult.sent).toBe(2);
      expect(replayResult.failed).toBe(0);
      expect(result.current.messages.every(m => m.status === 'sent')).toBe(true);
    });

    it('should not replay if already replaying', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const sendFn = vi
        .fn()
        .mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

      act(() => {
        result.current.queueMessage(createMessageInput());
      });

      // Start first replay (don't await)
      const firstReplay = act(async () => {
        return result.current.replayQueue(sendFn, { delayMs: 0 });
      });

      // Try to start second replay immediately
      let secondResult: { sent: number; failed: number } = { sent: 0, failed: 0 };
      await act(async () => {
        secondResult = await result.current.replayQueue(sendFn, { delayMs: 0 });
      });

      // Second replay should return immediately with 0 results
      expect(secondResult.sent).toBe(0);
      expect(secondResult.failed).toBe(0);

      await firstReplay;
    });

    it('should mark failed messages after max retries', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const error = new Error('Network error');
      const sendFn = vi.fn().mockRejectedValue(error);

      act(() => {
        result.current.queueMessage(createMessageInput());
      });

      // First replay attempt with maxRetries=1
      await act(async () => {
        await result.current.replayQueue(sendFn, { maxRetries: 1, delayMs: 0 });
      });

      // After 1 retry (maxRetries=1), should be marked as failed
      expect(result.current.messages[0]?.status).toBe('failed');
      expect(result.current.messages[0]?.lastError).toBe('Network error');
      expect(result.current.totalFailed).toBeGreaterThanOrEqual(1);
    });

    it('should update lastReplayAt timestamp after replay', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const sendFn = vi.fn().mockResolvedValue(undefined);

      act(() => {
        result.current.queueMessage(createMessageInput());
      });

      // First verify lastReplayAt is null initially
      expect(result.current.lastReplayAt).toBeNull();

      await act(async () => {
        await result.current.replayQueue(sendFn, { delayMs: 0 });
      });

      // After replay, lastReplayAt should be set
      expect(result.current.lastReplayAt).toBeTruthy();
      expect(result.current.lastReplayAt).toBeGreaterThan(0);
    });

    it('should increment totalSent on successful replay', async () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());
      const sendFn = vi.fn().mockResolvedValue(undefined);

      // Get initial totalSent
      const initialTotalSent = result.current.totalSent;

      act(() => {
        result.current.queueMessage(createMessageInput({ content: 'msg1' }));
        result.current.queueMessage(createMessageInput({ content: 'msg2' }));
      });

      await act(async () => {
        await result.current.replayQueue(sendFn, { delayMs: 0 });
      });

      expect(result.current.totalSent).toBe(initialTotalSent + 2);
    });
  });

  describe('markAsSent', () => {
    it('should mark specific message as sent', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      let messageId: string = '';
      act(() => {
        messageId = result.current.queueMessage(createMessageInput());
      });

      act(() => {
        result.current.markAsSent(messageId);
      });

      expect(result.current.messages[0]?.status).toBe('sent');
      expect(result.current.messages[0]?.lastError).toBeNull();
    });
  });

  describe('markAsFailed', () => {
    it('should mark specific message as failed with error', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      let messageId: string = '';
      act(() => {
        messageId = result.current.queueMessage(createMessageInput());
      });

      act(() => {
        result.current.markAsFailed(messageId, 'Custom error message');
      });

      expect(result.current.messages[0]?.status).toBe('failed');
      expect(result.current.messages[0]?.lastError).toBe('Custom error message');
    });
  });

  describe('removeMessage', () => {
    it('should remove message from queue', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      let messageId: string = '';
      act(() => {
        messageId = result.current.queueMessage(createMessageInput());
      });

      expect(result.current.messages).toHaveLength(1);

      act(() => {
        result.current.removeMessage(messageId);
      });

      expect(result.current.messages).toHaveLength(0);
    });

    it('should not error when removing non-existent message', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        result.current.removeMessage('non-existent-id');
      });

      expect(result.current.messages).toHaveLength(0);
    });
  });

  describe('clearCompleted', () => {
    it('should remove sent and failed messages', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        const id1 = result.current.queueMessage(createMessageInput({ content: 'pending' }));
        const id2 = result.current.queueMessage(createMessageInput({ content: 'sent' }));
        const id3 = result.current.queueMessage(createMessageInput({ content: 'failed' }));
        result.current.markAsSent(id2);
        result.current.markAsFailed(id3, 'error');
      });

      expect(result.current.messages).toHaveLength(3);

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0]?.content).toBe('pending');
    });
  });

  describe('clearAll', () => {
    it('should remove all messages', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        result.current.queueMessage(createMessageInput({ content: 'msg1' }));
        result.current.queueMessage(createMessageInput({ content: 'msg2' }));
        result.current.queueMessage(createMessageInput({ content: 'msg3' }));
      });

      expect(result.current.messages).toHaveLength(3);

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.messages).toHaveLength(0);
      expect(result.current.isReplaying).toBe(false);
    });
  });

  describe('getPendingCount', () => {
    it('should count pending and failed messages', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        const id1 = result.current.queueMessage(createMessageInput({ content: 'pending1' }));
        const id2 = result.current.queueMessage(createMessageInput({ content: 'pending2' }));
        const id3 = result.current.queueMessage(createMessageInput({ content: 'sent' }));
        const id4 = result.current.queueMessage(createMessageInput({ content: 'failed' }));
        result.current.markAsSent(id3);
        result.current.markAsFailed(id4, 'error');
      });

      expect(result.current.getPendingCount()).toBe(3); // 2 pending + 1 failed
    });
  });

  describe('Selectors', () => {
    it('selectPendingMessages should return only pending messages', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        const id1 = result.current.queueMessage(createMessageInput({ content: 'pending' }));
        const id2 = result.current.queueMessage(createMessageInput({ content: 'sent' }));
        result.current.markAsSent(id2);
      });

      const pendingMessages = selectPendingMessages(result.current);
      expect(pendingMessages).toHaveLength(1);
      expect(pendingMessages[0]?.content).toBe('pending');
    });

    it('selectFailedMessages should return only failed messages', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        const id1 = result.current.queueMessage(createMessageInput({ content: 'pending' }));
        const id2 = result.current.queueMessage(createMessageInput({ content: 'failed' }));
        result.current.markAsFailed(id2, 'error');
      });

      const failedMessages = selectFailedMessages(result.current);
      expect(failedMessages).toHaveLength(1);
      expect(failedMessages[0]?.content).toBe('failed');
    });

    it('selectPendingCount should return count of pending + failed', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      act(() => {
        const id1 = result.current.queueMessage(createMessageInput({ content: 'pending' }));
        const id2 = result.current.queueMessage(createMessageInput({ content: 'failed' }));
        const id3 = result.current.queueMessage(createMessageInput({ content: 'sent' }));
        result.current.markAsFailed(id2, 'error');
        result.current.markAsSent(id3);
      });

      expect(selectPendingCount(result.current)).toBe(2);
    });

    it('selectIsReplaying should return replay state', () => {
      const { result } = renderHook(() => useOfflineMessageQueueStore());

      expect(selectIsReplaying(result.current)).toBe(false);

      // Note: isReplaying is internal to replayQueue, tested via integration
    });
  });
});

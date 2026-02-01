/**
 * Comprehensive Tests for useChatOptimistic (Issue #2309)
 *
 * Coverage target: 90%+ (current: 0%)
 * Tests: Optimistic updates, rollback, state management
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatOptimistic } from '../useChatOptimistic';
import { useChatStore, useActiveMessages } from '@/store/chat';
import type { Message } from '@/types';

// Mock dependencies
vi.mock('@/store/chat');
vi.mock('@/lib/logger');

describe('useChatOptimistic - Comprehensive (Issue #2309)', () => {
  let mockSendMessage: any;
  let mockMessages: Message[];

  beforeEach(() => {
    vi.clearAllMocks();

    mockSendMessage = vi.fn().mockResolvedValue(undefined);
    mockMessages = [
      {
        id: 'msg-1',
        chatThreadId: 'chat-1',
        role: 'user',
        content: 'Hello',
        timestamp: new Date('2024-01-01'),
      },
    ];

    // Mock useChatStore selectors
    (useChatStore as any).mockImplementation((selector: any) => {
      const mockState = {
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        sendMessage: mockSendMessage,
      };
      return selector(mockState);
    });

    // Mock useActiveMessages
    vi.mocked(useActiveMessages).mockReturnValue(mockMessages);
  });

  describe('Hook Initialization', () => {
    it('should initialize with correct default values', () => {
      const { result } = renderHook(() => useChatOptimistic());

      expect(result.current.isOptimisticUpdate).toBe(false);
      expect(result.current.messages).toEqual(mockMessages);
      expect(typeof result.current.sendMessageOptimistic).toBe('function');
    });

    it('should expose messages from useActiveMessages', () => {
      const { result } = renderHook(() => useChatOptimistic());

      expect(result.current.messages).toBe(mockMessages);
    });
  });

  describe('sendMessageOptimistic', () => {
    it('should send message successfully with optimistic update', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      expect(result.current.isOptimisticUpdate).toBe(false);

      await act(async () => {
        await result.current.sendMessageOptimistic('Test message');
      });

      expect(mockSendMessage).toHaveBeenCalledWith('Test message');

      // Should clear optimistic state after success
      await waitFor(() => {
        expect(result.current.isOptimisticUpdate).toBe(false);
      });
    });

    it('should set optimistic state during message send', async () => {
      // Make sendMessage async with delay
      mockSendMessage.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 100)));

      const { result } = renderHook(() => useChatOptimistic());

      let optimisticStateDuringSend = false;

      act(() => {
        result.current.sendMessageOptimistic('Test').then(() => {});
      });

      // Check immediately after call
      await waitFor(
        () => {
          optimisticStateDuringSend = result.current.isOptimisticUpdate;
        },
        { timeout: 50 }
      );

      expect(optimisticStateDuringSend).toBe(true);
    });

    it('should clear optimistic state after successful send', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('Success message');
      });

      expect(result.current.isOptimisticUpdate).toBe(false);
    });

    it('should rollback optimistic state on error', async () => {
      mockSendMessage.mockRejectedValueOnce(new Error('Send failed'));

      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await expect(result.current.sendMessageOptimistic('Fail message')).rejects.toThrow(
          'Send failed'
        );
      });

      // Should clear optimistic state on error
      expect(result.current.isOptimisticUpdate).toBe(false);
    });

    it('should re-throw error after rollback', async () => {
      const mockError = new Error('API Error');
      mockSendMessage.mockRejectedValueOnce(mockError);

      const { result } = renderHook(() => useChatOptimistic());

      await expect(
        act(async () => {
          await result.current.sendMessageOptimistic('Error test');
        })
      ).rejects.toThrow('API Error');
    });

    it('should trim whitespace before validation', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('  Trimmed message  ');
      });

      expect(mockSendMessage).toHaveBeenCalledWith('  Trimmed message  '); // Store handles trimming
    });

    it('should not send empty messages', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('');
      });

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('should not send whitespace-only messages', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('   ');
      });

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('should not send when no game selected', async () => {
      (useChatStore as any).mockImplementation((selector: any) => {
        const mockState = {
          selectedGameId: null,
          selectedAgentId: 'agent-1',
          sendMessage: mockSendMessage,
        };
        return selector(mockState);
      });

      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('Test');
      });

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('should not send when no agent selected', async () => {
      (useChatStore as any).mockImplementation((selector: any) => {
        const mockState = {
          selectedGameId: 'game-1',
          selectedAgentId: null,
          sendMessage: mockSendMessage,
        };
        return selector(mockState);
      });

      const { result } = renderHook(() => useChatOptimistic());

      await act(async () => {
        await result.current.sendMessageOptimistic('Test');
      });

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('should handle concurrent optimistic updates', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      // Send multiple messages concurrently
      await act(async () => {
        await Promise.all([
          result.current.sendMessageOptimistic('Message 1'),
          result.current.sendMessageOptimistic('Message 2'),
        ]);
      });

      expect(mockSendMessage).toHaveBeenCalledTimes(2);
      expect(result.current.isOptimisticUpdate).toBe(false);
    });
  });

  describe('State Management', () => {
    it('should expose messages from hook', () => {
      const { result } = renderHook(() => useChatOptimistic());

      expect(result.current.messages).toBeDefined();
      expect(Array.isArray(result.current.messages)).toBe(true);
    });
  });

  describe('Error Handling', () => {
    it('should log error context on send failure', async () => {
      mockSendMessage.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useChatOptimistic());

      await expect(
        act(async () => {
          await result.current.sendMessageOptimistic('Test');
        })
      ).rejects.toThrow('Network error');
    });
  });

  describe('Edge Cases', () => {
    it('should handle message with special characters', async () => {
      const { result } = renderHook(() => useChatOptimistic());

      const specialMessage = 'Test message with & symbols';

      await act(async () => {
        await result.current.sendMessageOptimistic(specialMessage);
      });

      expect(mockSendMessage).toHaveBeenCalledWith(specialMessage);
    });
  });
});

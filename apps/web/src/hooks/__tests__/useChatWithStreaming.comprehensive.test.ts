/**
 * Comprehensive Tests for useChatWithStreaming (Issue #2309)
 *
 * Coverage target: 90%+ (current: 0%)
 * Tests: Hook integration, streaming controls, state management
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatWithStreaming } from '../useChatWithStreaming';
import { useChatStore } from '@/store/chat/store';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

// Mock dependencies
vi.mock('@/store/chat/store');
vi.mock('@/lib/hooks/useStreamingChat');

describe('useChatWithStreaming - Comprehensive (Issue #2309)', () => {
  let mockStore: any;
  let mockStreamingState: any;
  let mockStreamingControls: any;

  beforeEach(() => {
    vi.clearAllMocks();

    // Mock store
    mockStore = {
      selectedGameId: 'game-1',
      selectedDocumentIds: ['doc-1', 'doc-2'],
      activeChatIds: { 'game-1': 'chat-1' },
      chatsByGame: { 'game-1': [{ id: 'chat-1', title: 'Chat 1', createdAt: '2024-01-01' }] },
      messagesByChat: {
        'chat-1': [{ id: 'msg-1', role: 'user', content: 'Hello', timestamp: new Date() }],
      },
      sendMessage: vi.fn().mockResolvedValue(undefined),
      addOptimisticMessage: vi.fn(),
      loadMessages: vi.fn().mockResolvedValue(undefined),
      setError: vi.fn(),
    };

    // Mock streaming state
    mockStreamingState = {
      isStreaming: false,
      currentAnswer: '',
      stateMessage: null,
      citations: [],
      confidence: null,
      error: null,
    };

    // Mock streaming controls
    mockStreamingControls = {
      startStreaming: vi.fn().mockResolvedValue(undefined),
      stopStreaming: vi.fn(),
    };

    vi.mocked(useChatStore).mockReturnValue(mockStore);
    vi.mocked(useStreamingChat).mockReturnValue([mockStreamingState, mockStreamingControls]);
  });

  describe('Hook Initialization', () => {
    it('should initialize with correct derived values', () => {
      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.activeChatId).toBe('chat-1');
      expect(result.current.chats).toHaveLength(1);
      expect(result.current.messages).toHaveLength(1);
    });

    it('should handle no selected game', () => {
      mockStore.selectedGameId = null;

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.chats).toEqual([]);
    });

    it('should handle no active chat for selected game', () => {
      mockStore.activeChatIds = {};

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.activeChatId).toBeUndefined();
      expect(result.current.messages).toEqual([]);
    });
  });

  describe('Streaming State Exposure', () => {
    it('should expose streaming state properties', () => {
      mockStreamingState.isStreaming = true;
      mockStreamingState.currentAnswer = 'Streaming answer...';
      mockStreamingState.stateMessage = 'Processing';
      mockStreamingState.citations = [{ text: 'Citation', page: '1' }] as Citation[];

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.isStreaming).toBe(true);
      expect(result.current.streamingAnswer).toBe('Streaming answer...');
      expect(result.current.streamingState).toBe('Processing');
      expect(result.current.streamingCitations).toHaveLength(1);
    });

    it('should expose streaming controls', () => {
      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.stopStreaming).toBe(mockStreamingControls.stopStreaming);
    });
  });

  describe('sendMessage Integration', () => {
    it('should send message and trigger streaming', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('Test question');
      });

      expect(mockStore.sendMessage).toHaveBeenCalledWith('Test question');
      expect(mockStreamingControls.startStreaming).toHaveBeenCalledWith(
        'game-1',
        'Test question',
        'chat-1',
        ['doc-1', 'doc-2']
      );
    });

    it('should trim whitespace before sending', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('  Question with spaces  ');
      });

      expect(mockStreamingControls.startStreaming).toHaveBeenCalledWith(
        'game-1',
        'Question with spaces',
        'chat-1',
        ['doc-1', 'doc-2']
      );
    });

    it('should not send empty or whitespace-only messages', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('');
      });

      expect(mockStore.sendMessage).not.toHaveBeenCalled();

      await act(async () => {
        await result.current.sendMessage('   ');
      });

      expect(mockStore.sendMessage).not.toHaveBeenCalled();
    });

    it('should not send when no game selected', async () => {
      mockStore.selectedGameId = null;

      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      expect(mockStore.sendMessage).not.toHaveBeenCalled();
      expect(mockStreamingControls.startStreaming).not.toHaveBeenCalled();
    });

    it('should handle streaming start when no active chat', async () => {
      mockStore.activeChatIds = {};

      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      // Should send message but not start streaming
      expect(mockStore.sendMessage).toHaveBeenCalled();
      expect(mockStreamingControls.startStreaming).not.toHaveBeenCalled();
    });
  });

  describe('Streaming Callbacks', () => {
    it('should handle onComplete callback', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      // Simulate streaming completion
      const onComplete = vi.mocked(useStreamingChat).mock.calls[0][0].onComplete;

      await act(async () => {
        onComplete!('Final answer', [], null);
      });

      await waitFor(() => {
        expect(mockStore.addOptimisticMessage).toHaveBeenCalledWith(
          expect.objectContaining({
            role: 'assistant',
            content: 'Final answer',
            gameId: 'game-1',
          }),
          'chat-1'
        );
        expect(mockStore.loadMessages).toHaveBeenCalledWith('chat-1');
      });
    });

    it('should not call addOptimisticMessage when no active chat', async () => {
      mockStore.activeChatIds = {};

      const { result } = renderHook(() => useChatWithStreaming());

      const onComplete = vi.mocked(useStreamingChat).mock.calls[0][0].onComplete;

      await act(async () => {
        onComplete!('Answer', [], null);
      });

      expect(mockStore.addOptimisticMessage).not.toHaveBeenCalled();
    });

    it('should handle onError callback', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      const onError = vi.mocked(useStreamingChat).mock.calls[0][0].onError;

      await act(async () => {
        onError!(new Error('Streaming failed'));
      });

      expect(mockStore.setError).toHaveBeenCalledWith('Streaming failed');
    });

    it('should handle onError with message-less error', async () => {
      const { result } = renderHook(() => useChatWithStreaming());

      const onError = vi.mocked(useStreamingChat).mock.calls[0][0].onError;

      await act(async () => {
        onError!(new Error(''));
      });

      expect(mockStore.setError).toHaveBeenCalledWith('Errore durante lo streaming');
    });

    it('should use onToken callback without state updates', () => {
      renderHook(() => useChatWithStreaming());

      const onToken = vi.mocked(useStreamingChat).mock.calls[0][0].onToken;

      // Should not throw
      expect(() => onToken!('token', 'accumulated')).not.toThrow();
    });

    it('should use onStateUpdate callback without state updates', () => {
      renderHook(() => useChatWithStreaming());

      const onStateUpdate = vi.mocked(useStreamingChat).mock.calls[0][0].onStateUpdate;

      // Should not throw
      expect(() => onStateUpdate!('Processing...')).not.toThrow();
    });
  });

  describe('Store State Pass-through', () => {
    it('should expose all store properties', () => {
      mockStore.someProperty = 'test-value';

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.someProperty).toBe('test-value');
    });

    it('should expose selectedGameId', () => {
      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.selectedGameId).toBe('game-1');
    });

    it('should expose selectedDocumentIds', () => {
      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.selectedDocumentIds).toEqual(['doc-1', 'doc-2']);
    });
  });

  describe('Memoization', () => {
    it('should memoize return value', () => {
      const { result, rerender } = renderHook(() => useChatWithStreaming());

      const firstResult = result.current;
      rerender();
      const secondResult = result.current;

      // Should return same object if dependencies haven't changed
      expect(firstResult).toBe(secondResult);
    });

    it('should update when store changes', () => {
      const { result, rerender } = renderHook(() => useChatWithStreaming());

      const firstGameId = result.current.selectedGameId;

      // Change store
      mockStore.selectedGameId = 'game-2';
      mockStore.activeChatIds = { 'game-2': 'chat-2' };

      rerender();

      expect(result.current.selectedGameId).not.toBe(firstGameId);
    });
  });

  describe('Edge Cases', () => {
    it('should handle undefined chatsByGame', () => {
      mockStore.chatsByGame = {};

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.chats).toEqual([]);
    });

    it('should handle undefined messagesByChat', () => {
      mockStore.messagesByChat = {};

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.messages).toEqual([]);
    });

    it('should handle empty selectedDocumentIds', async () => {
      mockStore.selectedDocumentIds = [];

      const { result } = renderHook(() => useChatWithStreaming());

      await act(async () => {
        await result.current.sendMessage('Test');
      });

      expect(mockStreamingControls.startStreaming).toHaveBeenCalledWith(
        'game-1',
        'Test',
        'chat-1',
        []
      );
    });

    it('should handle null selectedDocumentIds', async () => {
      mockStore.selectedDocumentIds = null;

      const { result } = renderHook(() => useChatWithStreaming());

      expect(result.current.selectedDocumentIds).toBeNull();
    });
  });
});

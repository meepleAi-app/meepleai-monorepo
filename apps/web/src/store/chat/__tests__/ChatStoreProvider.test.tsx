/**
 * ChatStoreProvider Tests - Issue #2762
 *
 * Tests for initialization wrapper:
 * - Mount effects (loadGames, loadAgents)
 * - Cascading loads (game → chats → messages)
 * - E2E test hooks exposure
 * - Streaming state integration
 *
 * Coverage target: 90%+
 */

import { render, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';
import { ChatStoreProvider } from '../ChatStoreProvider';
import { useChatStore } from '../store';
import { resetChatStore } from '../slices/__tests__/chatSlice.test-helpers';

// Mock useStreamingChat hook
const mockStartStreaming = vi.fn();
const mockStopStreaming = vi.fn();
const mockResetStreaming = vi.fn();

vi.mock('@/lib/hooks/useStreamingChat', () => ({
  useStreamingChat: vi.fn(() => [
    {
      isStreaming: false,
      currentAnswer: '',
      citations: [],
      confidence: null,
      error: null,
    },
    {
      startStreaming: mockStartStreaming,
      stopStreaming: mockStopStreaming,
      resetStreaming: mockResetStreaming,
    },
  ]),
}));

// Import mocked module for manipulation
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';

describe('ChatStoreProvider - Issue #2762', () => {
  let loadGamesSpy: Mock;
  let loadAgentsSpy: Mock;
  let loadChatsSpy: Mock;
  let loadMessagesSpy: Mock;
  let selectGameSpy: Mock;
  let selectAgentSpy: Mock;
  let selectChatSpy: Mock;
  let sendMessageSpy: Mock;

  beforeEach(() => {
    vi.clearAllMocks();
    resetChatStore(useChatStore);

    // Spy on store actions
    loadGamesSpy = vi.fn().mockResolvedValue(undefined);
    loadAgentsSpy = vi.fn().mockResolvedValue(undefined);
    loadChatsSpy = vi.fn().mockResolvedValue(undefined);
    loadMessagesSpy = vi.fn().mockResolvedValue(undefined);
    selectGameSpy = vi.fn();
    selectAgentSpy = vi.fn();
    selectChatSpy = vi.fn().mockResolvedValue(undefined);
    sendMessageSpy = vi.fn().mockResolvedValue(undefined);

    useChatStore.setState({
      loadGames: loadGamesSpy,
      loadAgents: loadAgentsSpy,
      loadChats: loadChatsSpy,
      loadMessages: loadMessagesSpy,
      selectGame: selectGameSpy,
      selectAgent: selectAgentSpy,
      selectChat: selectChatSpy,
      sendMessage: sendMessageSpy,
    });

    // Clean up window test hooks
    if (typeof window !== 'undefined') {
      delete (window as any).__MEEPLEAI_TEST_HOOKS__;
      delete (window as any).__TEST_STREAMING_STATE__;
    }
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Mount Effects', () => {
    it('should load games on mount', async () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadGamesSpy).toHaveBeenCalledTimes(1);
      });
    });

    it('should load agents on mount', async () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadAgentsSpy).toHaveBeenCalledTimes(1);
      });
    });

    it('should render children', () => {
      const { getByText } = render(
        <ChatStoreProvider>
          <div>Child Content</div>
        </ChatStoreProvider>
      );

      expect(getByText('Child Content')).toBeInTheDocument();
    });
  });

  describe('Cascading Loads - Game Selection', () => {
    it('should load chats when game is selected', async () => {
      const { rerender } = render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      // Simulate game selection
      act(() => {
        useChatStore.setState({ selectedGameId: 'game-1' });
      });

      // Force re-render to trigger effect
      rerender(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadChatsSpy).toHaveBeenCalledWith('game-1');
      });
    });

    it('should not load chats when no game selected', async () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      // Initial state has no selectedGameId
      await waitFor(() => {
        expect(loadChatsSpy).not.toHaveBeenCalled();
      });
    });
  });

  describe('Cascading Loads - Chat Selection', () => {
    it('should load messages when active chat changes', async () => {
      // Set up state with game and chat
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadMessagesSpy).toHaveBeenCalledWith('chat-1');
      });
    });

    it('should not load messages when no active chat', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: {},
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadGamesSpy).toHaveBeenCalled();
      });

      // loadMessages should not be called without active chat
      expect(loadMessagesSpy).not.toHaveBeenCalled();
    });

    it('should not load messages when no game selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
        activeChatIds: { 'game-1': 'chat-1' },
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(loadGamesSpy).toHaveBeenCalled();
      });

      expect(loadMessagesSpy).not.toHaveBeenCalled();
    });
  });

  describe('E2E Test Hooks', () => {
    it('should expose test hooks on window', async () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__MEEPLEAI_TEST_HOOKS__).toBeDefined();
        expect(window.__MEEPLEAI_TEST_HOOKS__?.chat).toBeDefined();
      });
    });

    it('should expose selectGame function', async () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__MEEPLEAI_TEST_HOOKS__?.chat?.selectGame).toBeInstanceOf(Function);
      });

      await window.__MEEPLEAI_TEST_HOOKS__?.chat?.selectGame('game-1');
      expect(selectGameSpy).toHaveBeenCalledWith('game-1');
    });

    it('should expose selectGameAndAgent function', async () => {
      // Set up chats for auto-selection
      useChatStore.setState({
        chatsByGame: {
          'game-1': [{ id: 'chat-1', title: 'Chat 1', gameId: 'game-1', status: 'Active' as const }],
        },
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__MEEPLEAI_TEST_HOOKS__?.chat?.selectGameAndAgent).toBeInstanceOf(Function);
      });

      await window.__MEEPLEAI_TEST_HOOKS__?.chat?.selectGameAndAgent('game-1', 'agent-1');

      expect(selectGameSpy).toHaveBeenCalledWith('game-1');
      expect(selectAgentSpy).toHaveBeenCalledWith('agent-1');
    });

    it('should expose sendMessage function', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__MEEPLEAI_TEST_HOOKS__?.chat?.sendMessage).toBeInstanceOf(Function);
      });

      await window.__MEEPLEAI_TEST_HOOKS__?.chat?.sendMessage('Hello');

      expect(sendMessageSpy).toHaveBeenCalledWith('Hello');
    });

    it('should trigger streaming after sendMessage', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__MEEPLEAI_TEST_HOOKS__?.chat?.sendMessage).toBeInstanceOf(Function);
      });

      await window.__MEEPLEAI_TEST_HOOKS__?.chat?.sendMessage('Hello');

      expect(mockStartStreaming).toHaveBeenCalledWith('game-1', 'Hello', 'chat-1');
    });
  });

  describe('Streaming State Integration', () => {
    it('should call useStreamingChat with callbacks', () => {
      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      expect(useStreamingChat).toHaveBeenCalledWith(
        expect.objectContaining({
          onComplete: expect.any(Function),
          onError: expect.any(Function),
        })
      );
    });

    it('should set streaming state on window during streaming', async () => {
      // Mock streaming state
      vi.mocked(useStreamingChat).mockReturnValue([
        {
          isStreaming: true,
          currentAnswer: 'Streaming answer',
          citations: [{ source: 'test' }],
          confidence: 0.95,
          error: null,
        },
        {
          startStreaming: mockStartStreaming,
          stopStreaming: mockStopStreaming,
          resetStreaming: mockResetStreaming,
        },
      ]);

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      await waitFor(() => {
        expect(window.__TEST_STREAMING_STATE__).toBeDefined();
        expect(window.__TEST_STREAMING_STATE__?.isStreaming).toBe(true);
        expect(window.__TEST_STREAMING_STATE__?.answer).toBe('Streaming answer');
      });
    });

    it('should set completed state on onComplete callback', async () => {
      let capturedOnComplete: Function | undefined;

      vi.mocked(useStreamingChat).mockImplementation(({ onComplete }) => {
        capturedOnComplete = onComplete;
        return [
          {
            isStreaming: false,
            currentAnswer: '',
            citations: [],
            confidence: null,
            error: null,
          },
          {
            startStreaming: mockStartStreaming,
            stopStreaming: mockStopStreaming,
            resetStreaming: mockResetStreaming,
          },
        ];
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      // Trigger onComplete callback
      act(() => {
        capturedOnComplete?.('Final answer', [{ source: 'citation' }], 0.9);
      });

      expect(window.__TEST_STREAMING_STATE__).toEqual({
        answer: 'Final answer',
        citations: [{ source: 'citation' }],
        confidence: 0.9,
        completed: true,
      });
    });

    it('should log error on onError callback', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      let capturedOnError: Function | undefined;

      vi.mocked(useStreamingChat).mockImplementation(({ onError }) => {
        capturedOnError = onError;
        return [
          {
            isStreaming: false,
            currentAnswer: '',
            citations: [],
            confidence: null,
            error: null,
          },
          {
            startStreaming: mockStartStreaming,
            stopStreaming: mockStopStreaming,
            resetStreaming: mockResetStreaming,
          },
        ];
      });

      render(
        <ChatStoreProvider>
          <div>Test</div>
        </ChatStoreProvider>
      );

      // Trigger onError callback
      act(() => {
        capturedOnError?.(new Error('Streaming failed'));
      });

      expect(consoleErrorSpy).toHaveBeenCalledWith('Streaming error:', expect.any(Error));
      consoleErrorSpy.mockRestore();
    });
  });

  describe('Re-exports', () => {
    it('should export useChatStore', async () => {
      const { useChatStore: exportedStore } = await import('../ChatStoreProvider');
      expect(exportedStore).toBeDefined();
    });

    it('should export useChatStoreWithSelectors', async () => {
      const { useChatStoreWithSelectors } = await import('../ChatStoreProvider');
      expect(useChatStoreWithSelectors).toBeDefined();
    });
  });
});

// Type augmentation for test hooks
declare global {
  interface Window {
    __MEEPLEAI_TEST_HOOKS__?: {
      chat?: {
        selectGame: (gameId: string) => Promise<void>;
        selectGameAndAgent: (gameId: string, agentId: string) => Promise<void>;
        sendMessage: (content: string) => Promise<void>;
      };
    };
    __TEST_STREAMING_STATE__?: {
      answer?: string;
      citations?: any[];
      confidence?: number | null;
      isStreaming?: boolean;
      completed?: boolean;
    };
  }
}

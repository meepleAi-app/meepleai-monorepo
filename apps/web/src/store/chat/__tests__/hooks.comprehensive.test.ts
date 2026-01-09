/**
 * Comprehensive Tests for Chat Store Hooks (Issue #2309)
 *
 * Coverage target: 90%+ (current: 57.37%)
 * Tests: Missing hook functions and auto-generated selectors
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useChatStore } from '../store';
import {
  useActiveMessages,
  useSelectedGame,
  useSelectedAgent,
  useIsLoading,
  useIsCreating,
  useIsSending,
  useChatStoreWithSelectors,
} from '../hooks';
import { resetChatStore } from '../slices/__tests__/chatSlice.test-helpers';
import type { Game, AgentDto } from '@/lib/api/schemas';
import type { Message } from '@/types';

describe('Chat Store Hooks - Comprehensive (Issue #2309)', () => {
  beforeEach(() => {
    resetChatStore(useChatStore);
  });

  describe('useActiveMessages', () => {
    it('should return empty array when no game selected', () => {
      const { result } = renderHook(() => useActiveMessages());
      expect(result.current).toEqual([]);
    });

    it('should return empty array when no active chat', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: {},
        messagesByChat: {},
      });

      const { result } = renderHook(() => useActiveMessages());
      expect(result.current).toEqual([]);
    });

    it('should return messages for active chat', () => {
      const mockMessages: Message[] = [
        {
          id: 'msg-1',
          chatThreadId: 'chat-1',
          role: 'user',
          content: 'Hello',
          timestamp: new Date('2024-01-01'),
        },
        {
          id: 'msg-2',
          chatThreadId: 'chat-1',
          role: 'assistant',
          content: 'Hi',
          timestamp: new Date('2024-01-02'),
        },
      ];

      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
        messagesByChat: { 'chat-1': mockMessages },
      });

      const { result } = renderHook(() => useActiveMessages());
      expect(result.current).toEqual(mockMessages);
      expect(result.current).toHaveLength(2);
    });

    it('should return empty array when messages not found for active chat', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
        messagesByChat: {},
      });

      const { result } = renderHook(() => useActiveMessages());
      expect(result.current).toEqual([]);
    });
  });

  describe('useSelectedGame', () => {
    it('should return undefined when no game selected', () => {
      const { result } = renderHook(() => useSelectedGame());
      expect(result.current).toBeUndefined();
    });

    it('should return selected game object', () => {
      const mockGames: Game[] = [
        {
          id: 'game-1',
          title: 'Chess',
          publisher: null,
          yearPublished: null,
          minPlayers: 2,
          maxPlayers: 2,
          minPlayTimeMinutes: null,
          maxPlayTimeMinutes: null,
          bggId: null,
          iconUrl: null,
          imageUrl: null,
          createdAt: '2024-01-01',
        },
        {
          id: 'game-2',
          title: 'Catan',
          publisher: null,
          yearPublished: null,
          minPlayers: 3,
          maxPlayers: 4,
          minPlayTimeMinutes: null,
          maxPlayTimeMinutes: null,
          bggId: null,
          iconUrl: null,
          imageUrl: null,
          createdAt: '2024-01-02',
        },
      ];

      useChatStore.setState({
        selectedGameId: 'game-2',
        games: mockGames,
      });

      const { result } = renderHook(() => useSelectedGame());
      expect(result.current).toEqual(mockGames[1]);
      expect(result.current?.title).toBe('Catan');
    });

    it('should return undefined when game not found in list', () => {
      const mockGames: Game[] = [];

      useChatStore.setState({
        selectedGameId: 'non-existent',
        games: mockGames,
      });

      const { result } = renderHook(() => useSelectedGame());
      expect(result.current).toBeUndefined();
    });
  });

  describe('useSelectedAgent', () => {
    it('should return undefined when no agent selected', () => {
      const { result } = renderHook(() => useSelectedAgent());
      expect(result.current).toBeUndefined();
    });

    it('should return selected agent object', () => {
      const mockAgents: AgentDto[] = [
        {
          id: 'agent-1',
          name: 'Rules Expert',
          description: 'Expert in rules',
          modelProvider: 'openai',
          modelName: 'gpt-4',
          temperature: 0.7,
          maxTokens: 1000,
          systemPrompt: 'Prompt',
          isActive: true,
        },
        {
          id: 'agent-2',
          name: 'Strategy Expert',
          description: 'Expert in strategy',
          modelProvider: 'anthropic',
          modelName: 'claude-3',
          temperature: 0.5,
          maxTokens: 2000,
          systemPrompt: 'Prompt 2',
          isActive: true,
        },
      ];

      useChatStore.setState({
        selectedAgentId: 'agent-2',
        agents: mockAgents,
      });

      const { result } = renderHook(() => useSelectedAgent());
      expect(result.current).toEqual(mockAgents[1]);
      expect(result.current?.name).toBe('Strategy Expert');
    });

    it('should return undefined when agent not found in list', () => {
      useChatStore.setState({
        selectedAgentId: 'non-existent',
        agents: [],
      });

      const { result } = renderHook(() => useSelectedAgent());
      expect(result.current).toBeUndefined();
    });
  });

  describe('useIsLoading', () => {
    it('should return false when no operations loading', () => {
      useChatStore.setState({
        loading: {
          games: false,
          agents: false,
          chats: false,
          messages: false,
          creating: false,
          sending: false,
        },
      });

      const { result } = renderHook(() => useIsLoading());
      expect(result.current).toBe(false);
    });

    it('should return true when any operation is loading', () => {
      useChatStore.setState({
        loading: {
          games: false,
          agents: true, // One operation loading
          chats: false,
          messages: false,
          creating: false,
          sending: false,
        },
      });

      const { result } = renderHook(() => useIsLoading());
      expect(result.current).toBe(true);
    });

    it('should return true when multiple operations loading', () => {
      useChatStore.setState({
        loading: {
          games: true,
          agents: true,
          chats: true,
          messages: false,
          creating: false,
          sending: false,
        },
      });

      const { result } = renderHook(() => useIsLoading());
      expect(result.current).toBe(true);
    });
  });

  describe('useIsCreating', () => {
    it('should return false when not creating', () => {
      useChatStore.setState({
        loading: { creating: false },
      });

      const { result } = renderHook(() => useIsCreating());
      expect(result.current).toBe(false);
    });

    it('should return true when creating chat', () => {
      useChatStore.setState({
        loading: { creating: true },
      });

      const { result } = renderHook(() => useIsCreating());
      expect(result.current).toBe(true);
    });
  });

  describe('useIsSending', () => {
    it('should return false when not sending', () => {
      useChatStore.setState({
        loading: { sending: false },
      });

      const { result } = renderHook(() => useIsSending());
      expect(result.current).toBe(false);
    });

    it('should return true when sending message', () => {
      useChatStore.setState({
        loading: { sending: true },
      });

      const { result } = renderHook(() => useIsSending());
      expect(result.current).toBe(true);
    });
  });

  describe('useChatStoreWithSelectors', () => {
    it('should expose use object with selector hooks', () => {
      expect(useChatStoreWithSelectors.use).toBeDefined();
      expect(typeof useChatStoreWithSelectors.use).toBe('object');
    });

    it('should have selector for games', () => {
      const mockGames: Game[] = [
        {
          id: 'game-1',
          title: 'Test Game',
          publisher: null,
          yearPublished: null,
          minPlayers: 2,
          maxPlayers: 4,
          minPlayTimeMinutes: null,
          maxPlayTimeMinutes: null,
          bggId: null,
          iconUrl: null,
          imageUrl: null,
          createdAt: '2024-01-01',
        },
      ];

      useChatStore.setState({ games: mockGames });

      const { result } = renderHook(() => useChatStoreWithSelectors.use.games());
      expect(result.current).toEqual(mockGames);
    });

    it('should have selector for selectedGameId', () => {
      useChatStore.setState({ selectedGameId: 'game-123' });

      const { result } = renderHook(() => useChatStoreWithSelectors.use.selectedGameId());
      expect(result.current).toBe('game-123');
    });

    it('should have selector for loading state', () => {
      const loadingState = {
        games: true,
        agents: false,
        chats: false,
        messages: false,
        creating: false,
        sending: false,
      };

      useChatStore.setState({ loading: loadingState });

      const { result } = renderHook(() => useChatStoreWithSelectors.use.loading());
      expect(result.current).toEqual(loadingState);
    });

    it('should have selector for error state', () => {
      useChatStore.setState({ error: 'Test error' });

      const { result } = renderHook(() => useChatStoreWithSelectors.use.error());
      expect(result.current).toBe('Test error');
    });
  });
});

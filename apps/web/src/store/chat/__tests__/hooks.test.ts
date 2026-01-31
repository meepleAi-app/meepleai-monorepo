/**
 * Chat Store Hooks Tests - Issue #2340
 * Coverage target: hooks.ts (convenience hooks)
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useChatStore } from '../store';
import { useCurrentChats, useActiveChat } from '../hooks';
import { resetChatStore } from '../slices/__tests__/chatSlice.test-helpers';

describe('Chat Store Hooks - Issue #2340', () => {
  beforeEach(() => {
    // Use resetChatStore to avoid overwriting action functions
    // which causes infinite loop (Maximum update depth exceeded)
    resetChatStore(useChatStore);
  });

  describe('useCurrentChats', () => {
    it('should return empty array when no game selected', () => {
      const { result } = renderHook(() => useCurrentChats());
      expect(result.current).toEqual([]);
    });

    it('should return chats for selected game', () => {
      const mockChats = [
        { id: 'chat-1', title: 'Chat 1', gameId: 'game-1', status: 'Active' as const },
        { id: 'chat-2', title: 'Chat 2', gameId: 'game-1', status: 'Active' as const },
      ];

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': mockChats },
      });

      const { result } = renderHook(() => useCurrentChats());
      expect(result.current).toEqual(mockChats);
    });

    it('should return empty array when game has no chats', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: {},
      });

      const { result } = renderHook(() => useCurrentChats());
      expect(result.current).toEqual([]);
    });
  });

  describe('useActiveChat', () => {
    it('should return null when no game selected', () => {
      const { result } = renderHook(() => useActiveChat());
      expect(result.current).toBeNull();
    });

    it('should return null when no active chat for game', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: {},
        chatsByGame: {},
      });

      const { result } = renderHook(() => useActiveChat());
      expect(result.current).toBeNull();
    });

    it('should return active chat for selected game', () => {
      const mockChat = {
        id: 'chat-1',
        title: 'Active Chat',
        gameId: 'game-1',
        status: 'Active' as const,
      };

      useChatStore.setState({
        selectedGameId: 'game-1',
        activeChatIds: { 'game-1': 'chat-1' },
        chatsByGame: { 'game-1': [mockChat] },
      });

      const { result } = renderHook(() => useActiveChat());
      expect(result.current).toEqual(mockChat);
    });
  });
});

/**
 * Agent Store Tests (Issue #3188)
 *
 * Tests for agent state management store
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useAgentStore } from '../store';
import type { AgentConfig } from '../types';

describe('AgentStore', () => {
  beforeEach(() => {
    // Clear store state before each test
    const { result } = renderHook(() => useAgentStore());
    act(() => {
      result.current.gameConfigs = {};
      result.current.activeSessions = {};
      result.current.conversations = {};
      result.current.conversationCache = [];
    });
  });

  describe('ConfigSlice', () => {
    it('should load config', async () => {
      const { result } = renderHook(() => useAgentStore());

      expect(result.current.loadingConfig).toBe(false);

      let config: AgentConfig | null = null;
      await act(async () => {
        config = await result.current.loadConfig('game-1');
      });

      expect(config).not.toBeNull();
      expect(config?.gameId).toBe('game-1');
      expect(result.current.gameConfigs['game-1']).toBeDefined();
      expect(result.current.loadingConfig).toBe(false);
    });

    it('should save config with optimistic update', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Load initial config
      await act(async () => {
        await result.current.loadConfig('game-1');
      });

      const initialConfig = result.current.gameConfigs['game-1'];

      // Save updated config
      await act(async () => {
        await result.current.saveConfig('game-1', { temperature: 0.9 });
      });

      const updatedConfig = result.current.gameConfigs['game-1'];
      expect(updatedConfig.temperature).toBe(0.9);
      expect(updatedConfig.gameId).toBe(initialConfig.gameId);
    });

    it.skip('should handle config load error', async () => {
      // TODO: Fix - requires store fetch integration and retry timing
      const { result } = renderHook(() => useAgentStore());

      // Mock error for all retry attempts
      vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Network error'));

      await act(async () => {
        const config = await result.current.loadConfig('game-1');
        expect(config).toBeNull();
      });

      expect(result.current.configError).not.toBeNull();
      expect(result.current.configError?.code).toBe('LOAD_CONFIG_ERROR');
      
      vi.restoreAllMocks();
    });

    it('should update config locally', () => {
      const { result } = renderHook(() => useAgentStore());

      act(() => {
        result.current.updateConfigLocal('game-1', {
          gameId: 'game-1',
          mode: 'StrategyAdvisor',
          temperature: 0.8,
          maxTokens: 1500,
          useRAG: false,
          updatedAt: new Date(),
        });
      });

      expect(result.current.gameConfigs['game-1'].mode).toBe('StrategyAdvisor');
      expect(result.current.gameConfigs['game-1'].temperature).toBe(0.8);
    });
  });

  describe('SessionSlice', () => {
    it('should launch agent session', async () => {
      const { result } = renderHook(() => useAgentStore());

      await act(async () => {
        await result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
      });

      const session = result.current.activeSessions['session-1'];
      expect(session).toBeDefined();
      expect(session.gameId).toBe('game-1');
      expect(session.mode).toBe('RulesClarifier');
      expect(session.status).toBe('active');
    });

    it('should end session', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Launch session first
      await act(async () => {
        await result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
      });

      // End session
      await act(async () => {
        await result.current.endSession('session-1');
      });

      const session = result.current.activeSessions['session-1'];
      expect(session?.status).toBe('ended');
    });

    it('should increment message count', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Launch session
      await act(async () => {
        await result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
      });

      expect(result.current.activeSessions['session-1'].messageCount).toBe(0);

      // Increment count
      act(() => {
        result.current.incrementMessageCount('session-1');
      });

      expect(result.current.activeSessions['session-1'].messageCount).toBe(1);
    });

    it('should get session by ID', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Launch session
      await act(async () => {
        await result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
      });

      const session = result.current.getSession('session-1');
      expect(session).not.toBeNull();
      expect(session?.sessionId).toBe('session-1');

      const nonExistent = result.current.getSession('session-999');
      expect(nonExistent).toBeNull();
    });
  });

  describe('ConversationSlice', () => {
    it('should send message with optimistic update', async () => {
      const { result } = renderHook(() => useAgentStore());

      await act(async () => {
        await result.current.sendMessage('session-1', 'Test message', 'game-1');
      });

      const messages = result.current.conversations['session-1'];
      expect(messages).toBeDefined();
      expect(messages.length).toBeGreaterThan(0);

      // User message
      expect(messages[0].type).toBe('user');
      expect(messages[0].content).toBe('Test message');

      // Agent response (mocked)
      expect(messages[1].type).toBe('agent');
    });

    it('should load conversation history', async () => {
      const { result } = renderHook(() => useAgentStore());

      await act(async () => {
        await result.current.loadHistory('session-1', 'game-1');
      });

      const messages = result.current.conversations['session-1'];
      expect(messages).toBeDefined();
      expect(messages.length).toBeGreaterThan(0);
      expect(messages[0].type).toBe('system');
    });

    it('should add message locally', () => {
      const { result } = renderHook(() => useAgentStore());

      act(() => {
        result.current.addMessageLocal('session-1', {
          type: 'user',
          content: 'Local message',
          timestamp: new Date(),
        });
      });

      const messages = result.current.conversations['session-1'];
      expect(messages.length).toBe(1);
      expect(messages[0].content).toBe('Local message');
    });

    it('should update conversation cache', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Add messages
      act(() => {
        result.current.addMessageLocal('session-1', {
          type: 'user',
          content: 'Test',
          timestamp: new Date(),
        });
      });

      // Update cache
      act(() => {
        result.current.updateCache('session-1', 'game-1');
      });

      expect(result.current.conversationCache.length).toBe(1);
      expect(result.current.conversationCache[0].sessionId).toBe('session-1');
      expect(result.current.conversationCache[0].gameId).toBe('game-1');
    });

    it('should limit cache to 5 conversations', async () => {
      const { result } = renderHook(() => useAgentStore());

      // Add 7 conversations
      for (let i = 1; i <= 7; i++) {
        act(() => {
          result.current.addMessageLocal(`session-${i}`, {
            type: 'user',
            content: `Message ${i}`,
            timestamp: new Date(),
          });
          result.current.updateCache(`session-${i}`, 'game-1');
        });
      }

      // Should keep only last 5
      expect(result.current.conversationCache.length).toBe(5);
      expect(result.current.conversationCache[0].sessionId).toBe('session-7');
      expect(result.current.conversationCache[4].sessionId).toBe('session-3');
    });
  });

  describe('Error Handling', () => {
    it.skip('should clear config error', async () => {
      // TODO: Requires proper error state handling in store
      const { result } = renderHook(() => useAgentStore());

      // Mock error for all retry attempts
      vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Network error'));

      await act(async () => {
        await result.current.loadConfig('game-1');
      });

      expect(result.current.configError).not.toBeNull();

      act(() => {
        result.current.clearConfigError();
      });

      expect(result.current.configError).toBeNull();
      vi.restoreAllMocks();
    });

    it.skip('should clear session error', async () => {
      // TODO: Requires proper error state handling in store
      const { result } = renderHook(() => useAgentStore());

      // Mock error for all retry attempts
      vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Network error'));

      await act(async () => {
        try {
          await result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
        } catch {
          // Expected
        }
      });

      expect(result.current.sessionError).not.toBeNull();

      act(() => {
        result.current.clearSessionError();
      });

      expect(result.current.sessionError).toBeNull();
      vi.restoreAllMocks();
    });

    it.skip('should clear conversation error', async () => {
      // TODO: Requires proper error state handling in store
      const { result } = renderHook(() => useAgentStore());

      // Mock error for all retry attempts
      vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Network error'));

      await act(async () => {
        try {
          await result.current.sendMessage('session-1', 'Test', 'game-1');
        } catch {
          // Expected
        }
      });

      expect(result.current.conversationError).not.toBeNull();

      act(() => {
        result.current.clearConversationError();
      });

      expect(result.current.conversationError).toBeNull();
      vi.restoreAllMocks();
    });
  });

  describe('Persistence', () => {
    it('should not persist sessions (ephemeral)', () => {
      const { result } = renderHook(() => useAgentStore());

      act(() => {
        result.current.launchAgent('session-1', 'game-1', 'RulesClarifier');
      });

      // Check localStorage
      const stored = localStorage.getItem('meepleai-agent-store');
      if (stored) {
        const parsed = JSON.parse(stored);
        expect(parsed.state.activeSessions).toBeUndefined();
      }
    });

    it('should persist config and conversations', async () => {
      const { result } = renderHook(() => useAgentStore());

      await act(async () => {
        await result.current.loadConfig('game-1');
        result.current.addMessageLocal('session-1', {
          type: 'user',
          content: 'Test',
          timestamp: new Date(),
        });
        result.current.updateCache('session-1', 'game-1');
      });

      // Check localStorage
      const stored = localStorage.getItem('meepleai-agent-store');
      expect(stored).toBeTruthy();

      if (stored) {
        const parsed = JSON.parse(stored);
        expect(parsed.state.gameConfigs).toBeDefined();
        expect(parsed.state.conversationCache).toBeDefined();
      }
    });
  });
});
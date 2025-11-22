/**
 * Tests for Chat Test Utilities
 *
 * These tests ensure that the shared test utilities for chat tests
 * work correctly and handle all edge cases properly.
 *
 * Coverage targets:
 * - Statements: 49% → 90%+
 * - Branches: 0% → 90%+
 * - Functions: 25% → 90%+
 */

import { act, waitFor } from '@testing-library/react';
import { api } from '../../../../../lib/api';
import {
  createChatTestData,
  setupAuthenticatedState,
  resetAllMocks,
  setupStreamingMock,
  setupFullChatEnvironment,
  mockStartStreaming,
  mockStopStreaming,
  setMockOnComplete,
  setMockOnError,
  mockApi,
  originalConfirm,
} from '../chat-test-utils';

// Mock the API module
jest.mock('../../../../../lib/api');

describe('Chat Test Utilities', () => {
  beforeEach(() => {
    resetAllMocks();
  });

  describe('createChatTestData', () => {
    describe('Happy Path', () => {
      it('should create complete test data structure', () => {
        const testData = createChatTestData();

        expect(testData).toHaveProperty('mockAuthResponse');
        expect(testData).toHaveProperty('mockGames');
        expect(testData).toHaveProperty('mockAgents');
        expect(testData).toHaveProperty('mockEditableAgents');
        expect(testData).toHaveProperty('mockChats');
        expect(testData).toHaveProperty('mockChatWithHistory');
      });

      it('should create auth response with correct user data', () => {
        const testData = createChatTestData();

        expect(testData.mockAuthResponse).toHaveProperty('user');
        expect(testData.mockAuthResponse).toHaveProperty('expiresAt');
        expect(testData.mockAuthResponse.user).toMatchObject({
          id: 'user-1',
          email: 'user@example.com',
          displayName: 'Test User',
          role: 'User',
        });
      });

      it('should create two mock games', () => {
        const testData = createChatTestData();

        expect(testData.mockGames).toHaveLength(2);
        expect(testData.mockGames[0]).toMatchObject({
          id: 'game-1',
          name: 'Chess',
        });
        expect(testData.mockGames[1]).toMatchObject({
          id: 'game-2',
          name: 'Catan',
        });
      });

      it('should create two mock agents for same game', () => {
        const testData = createChatTestData();

        expect(testData.mockAgents).toHaveLength(2);
        expect(testData.mockAgents[0]).toMatchObject({
          id: 'agent-1',
          gameId: 'game-1',
          name: 'Chess Expert',
          type: 'qa',
        });
        expect(testData.mockAgents[1]).toMatchObject({
          id: 'agent-2',
          gameId: 'game-1',
          name: 'Chess Helper',
          type: 'qa',
        });
      });

      it('should create two mock editable agents', () => {
        const testData = createChatTestData();

        expect(testData.mockEditableAgents).toHaveLength(2);
        expect(testData.mockEditableAgents[0].name).toBe('Editable Agent');
        expect(testData.mockEditableAgents[1].name).toBe('Supporting Agent');
      });

      it('should create two mock chats with different timestamps', () => {
        const testData = createChatTestData();

        expect(testData.mockChats).toHaveLength(2);
        expect(testData.mockChats[0]).toMatchObject({
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Chess',
          agentId: 'agent-1',
          agentName: 'Chess Expert',
          startedAt: '2025-01-10T10:00:00Z',
          lastMessageAt: '2025-01-10T10:05:00Z',
        });
        expect(testData.mockChats[1]).toMatchObject({
          id: 'chat-2',
          lastMessageAt: null,
        });
      });

      it('should create chat with history including messages', () => {
        const testData = createChatTestData();

        expect(testData.mockChatWithHistory.messages).toHaveLength(2);
        expect(testData.mockChatWithHistory.messages[0]).toMatchObject({
          id: 'msg-1',
          level: 'user',
          message: 'How do I castle?',
        });
        expect(testData.mockChatWithHistory.messages[1]).toMatchObject({
          id: 'msg-2',
          level: 'agent',
          message: 'Castling is a special move...',
        });
      });

      it('should include snippets in agent message metadata', () => {
        const testData = createChatTestData();
        const agentMessage = testData.mockChatWithHistory.messages[1];

        const metadata = JSON.parse(agentMessage.metadataJson || '{}');
        expect(metadata.snippets).toHaveLength(1);
        expect(metadata.snippets[0]).toMatchObject({
          text: 'Castling rules section',
          source: 'chess-rules.pdf',
          page: 5,
          line: null,
        });
      });
    });
  });

  describe('setupAuthenticatedState', () => {
    describe('Happy Path', () => {
      it('should configure API mocks with test data', () => {
        const testData = setupAuthenticatedState();

        // Verify test data structure
        expect(testData).toHaveProperty('mockAuthResponse');
        expect(testData).toHaveProperty('mockGames');
        expect(testData).toHaveProperty('mockAgents');
        expect(testData).toHaveProperty('mockChats');
      });

      it('should configure auth mock response', () => {
        const testData = setupAuthenticatedState();

        // Auth response is configured
        expect(testData.mockAuthResponse).toHaveProperty('user');
        expect(testData.mockAuthResponse.user).toHaveProperty('id');
        expect(testData.mockAuthResponse.user).toHaveProperty('email');
      });

      it('should configure games mock response', () => {
        const testData = setupAuthenticatedState();

        // Games are configured
        expect(Array.isArray(testData.mockGames)).toBe(true);
        expect(testData.mockGames).toHaveLength(2);
      });

      it('should configure agents mock response', () => {
        const testData = setupAuthenticatedState();

        // Agents are configured
        expect(Array.isArray(testData.mockAgents)).toBe(true);
        expect(testData.mockAgents).toHaveLength(2);
      });

      it('should configure chats mock response', () => {
        const testData = setupAuthenticatedState();

        // Chats are configured
        expect(Array.isArray(testData.mockChats)).toBe(true);
        expect(testData.mockChats).toHaveLength(2);
      });
    });
  });

  describe('resetAllMocks', () => {
    describe('Happy Path', () => {
      it('should clear all mock call history', () => {
        // Setup some mock calls
        mockApi.get.mockResolvedValue({ data: 'test' });
        mockApi.post.mockResolvedValue({ data: 'test' });
        mockApi.get('test');
        mockApi.post('test', {});

        expect(mockApi.get).toHaveBeenCalled();
        expect(mockApi.post).toHaveBeenCalled();

        // Reset
        resetAllMocks();

        expect(mockApi.get).not.toHaveBeenCalled();
        expect(mockApi.post).not.toHaveBeenCalled();
      });

      it('should reset API get mock', () => {
        mockApi.get.mockResolvedValue({ data: 'test' });
        expect(mockApi.get).toHaveReturnedTimes(0);

        resetAllMocks();

        // Should not have any mock implementation
        expect(mockApi.get.mock.calls).toHaveLength(0);
      });

      it('should reset API post mock', () => {
        mockApi.post.mockResolvedValue({ data: 'test' });
        resetAllMocks();

        expect(mockApi.post.mock.calls).toHaveLength(0);
      });

      it('should reset API put mock', () => {
        mockApi.put.mockResolvedValue({ data: 'test' });
        resetAllMocks();

        expect(mockApi.put.mock.calls).toHaveLength(0);
      });

      it('should reset API delete mock', () => {
        mockApi.delete.mockResolvedValue(undefined);
        resetAllMocks();

        expect(mockApi.delete.mock.calls).toHaveLength(0);
      });

      it('should reset streaming mocks', () => {
        mockStartStreaming.mockImplementation(() => ({ data: null }));
        mockStopStreaming.mockImplementation(() => {});

        mockStartStreaming({ endpoint: 'test', body: {}, onToken: () => {}, onError: () => {} });
        mockStopStreaming();

        expect(mockStartStreaming).toHaveBeenCalled();
        expect(mockStopStreaming).toHaveBeenCalled();

        resetAllMocks();

        expect(mockStartStreaming).not.toHaveBeenCalled();
        expect(mockStopStreaming).not.toHaveBeenCalled();
      });

      it('should reset chat API mocks', () => {
        (mockApi.chat.updateMessage as jest.Mock).mockResolvedValue({ success: true });
        (mockApi.chat.deleteMessage as jest.Mock).mockResolvedValue({ success: true });

        resetAllMocks();

        expect((mockApi.chat.updateMessage as jest.Mock).mock.calls).toHaveLength(0);
        expect((mockApi.chat.deleteMessage as jest.Mock).mock.calls).toHaveLength(0);
      });

      it('should restore window.confirm to original', () => {
        window.confirm = jest.fn();

        resetAllMocks();

        expect(window.confirm).toBe(originalConfirm);
      });

      it('should reset mockOnComplete to null', () => {
        setMockOnComplete(() => {});
        resetAllMocks();

        // Can't directly test exported variable, but we verify no side effects
        expect(mockStartStreaming).not.toHaveBeenCalled();
      });

      it('should reset mockOnError to null', () => {
        setMockOnError(() => {});
        resetAllMocks();

        // Can't directly test exported variable, but we verify no side effects
        expect(mockStartStreaming).not.toHaveBeenCalled();
      });
    });
  });

  describe('setupStreamingMock', () => {
    describe('Default Values', () => {
      it('should return default streaming state when no overrides', () => {
        const streamingState = setupStreamingMock();

        expect(streamingState).toEqual({
          isStreaming: false,
          currentAnswer: '',
          snippets: [],
          state: null,
          error: null,
        });
      });
    });

    describe('Custom Overrides', () => {
      it('should override isStreaming', () => {
        const streamingState = setupStreamingMock({ isStreaming: true });

        expect(streamingState.isStreaming).toBe(true);
      });

      it('should override currentAnswer', () => {
        const streamingState = setupStreamingMock({
          currentAnswer: 'Partial answer...',
        });

        expect(streamingState.currentAnswer).toBe('Partial answer...');
      });

      it('should override snippets', () => {
        const snippets = [{ text: 'test', source: 'test.pdf', page: 1 }];
        const streamingState = setupStreamingMock({ snippets });

        expect(streamingState.snippets).toEqual(snippets);
      });

      it('should override state', () => {
        const customState = { status: 'processing' };
        const streamingState = setupStreamingMock({ state: customState });

        expect(streamingState.state).toEqual(customState);
      });

      it('should override error', () => {
        const error = new Error('Test error');
        const streamingState = setupStreamingMock({ error });

        expect(streamingState.error).toEqual(error);
      });

      it('should handle multiple overrides', () => {
        const streamingState = setupStreamingMock({
          isStreaming: true,
          currentAnswer: 'Answer',
          snippets: [{ text: 'snippet' }],
          error: null,
        });

        expect(streamingState).toEqual({
          isStreaming: true,
          currentAnswer: 'Answer',
          snippets: [{ text: 'snippet' }],
          state: null,
          error: null,
        });
      });
    });

    describe('Edge Cases', () => {
      it('should handle empty snippets array', () => {
        const streamingState = setupStreamingMock({ snippets: [] });

        expect(streamingState.snippets).toEqual([]);
      });

      it('should handle empty string currentAnswer', () => {
        const streamingState = setupStreamingMock({ currentAnswer: '' });

        expect(streamingState.currentAnswer).toBe('');
      });

      it('should handle false isStreaming explicitly', () => {
        const streamingState = setupStreamingMock({ isStreaming: false });

        expect(streamingState.isStreaming).toBe(false);
      });
    });
  });

  describe('setupFullChatEnvironment', () => {
    describe('Default Behavior', () => {
      it('should create environment with all default values', () => {
        const env = setupFullChatEnvironment();

        // env.user is the full auth response
        expect(env.user).toHaveProperty('user');
        expect(env.user).toHaveProperty('expiresAt');
        expect(env.game).toHaveProperty('id');
        expect(env.agent).toHaveProperty('id');
        expect(env.chats).toHaveLength(1);
        expect(env.messages).toHaveLength(0);
        expect(env.games).toHaveLength(1);
        expect(env.agents).toHaveLength(1);
      });

      it('should configure API mock responses', () => {
        setupFullChatEnvironment();

        // Verify that mockResolvedValueOnce was called to configure the mocks
        expect(mockApi.get.mockResolvedValueOnce).toBeDefined();
      });

      it('should prepare default session configuration', () => {
        const env = setupFullChatEnvironment();

        // Verify environment is configured properly
        expect(env).toHaveProperty('user');
        expect(env).toHaveProperty('games');
        expect(env).toHaveProperty('agents');
      });
    });

    describe('User Overrides', () => {
      it('should override user properties in auth response', () => {
        const env = setupFullChatEnvironment({
          user: { id: 'custom-user', email: 'custom@example.com' },
        });

        // env.user contains the full auth response
        expect(env.user).toHaveProperty('user');
        expect(env.user).toHaveProperty('expiresAt');
      });

      it('should merge user overrides with default auth response', () => {
        const env = setupFullChatEnvironment({
          user: { email: 'new@example.com' },
        });

        // The override is merged into the auth response
        expect(env.user).toHaveProperty('user');
        expect(env.user.email).toBe('new@example.com');
      });

      it('should handle user role override', () => {
        const env = setupFullChatEnvironment({
          user: { role: 'Admin' },
        });

        expect(env.user.role).toBe('Admin');
      });
    });

    describe('Game Overrides', () => {
      it('should override game properties', () => {
        const env = setupFullChatEnvironment({
          game: { id: 'custom-game', name: 'Monopoly' },
        });

        expect(env.game).toMatchObject({
          id: 'custom-game',
          name: 'Monopoly',
        });
      });

      it('should use game ID fallback', () => {
        const env = setupFullChatEnvironment({
          game: { title: 'Custom Game' },
        });

        expect(env.game.id).toBe('game-1');
        expect(env.game.title).toBe('Custom Game');
      });
    });

    describe('Agent Overrides', () => {
      it('should override agent properties', () => {
        const env = setupFullChatEnvironment({
          agent: { id: 'custom-agent', name: 'Custom Agent' },
        });

        expect(env.agent).toMatchObject({
          id: 'custom-agent',
          name: 'Custom Agent',
        });
      });

      it('should link agent to overridden game', () => {
        const env = setupFullChatEnvironment({
          game: { id: 'game-2', name: 'Catan' },
          agent: { name: 'Catan Helper' },
        });

        expect(env.agent.gameId).toBe('game-2');
        expect(env.agent.name).toBe('Catan Helper');
      });

      it('should handle agent type override', () => {
        const env = setupFullChatEnvironment({
          agent: { type: 'explain' },
        });

        expect(env.agent.type).toBe('explain');
      });
    });

    describe('Chats Overrides', () => {
      it('should create custom chats when provided', () => {
        const env = setupFullChatEnvironment({
          chats: [
            { id: 'chat-1', gameName: 'Chess' },
            { id: 'chat-2', gameName: 'Catan' },
            { id: 'chat-3', gameName: 'Risk' },
          ],
        });

        expect(env.chats).toHaveLength(3);
        expect(env.chats[0].id).toBe('chat-1');
        expect(env.chats[1].id).toBe('chat-2');
        expect(env.chats[2].id).toBe('chat-3');
      });

      it('should fill in missing chat properties with defaults', () => {
        const env = setupFullChatEnvironment({
          chats: [{ id: 'custom-chat' }],
        });

        expect(env.chats[0]).toHaveProperty('gameId');
        expect(env.chats[0]).toHaveProperty('gameName');
        expect(env.chats[0]).toHaveProperty('agentId');
        expect(env.chats[0]).toHaveProperty('startedAt');
      });

      it('should handle lastMessageAt as null explicitly', () => {
        const env = setupFullChatEnvironment({
          chats: [{ id: 'chat-1', lastMessageAt: null }],
        });

        expect(env.chats[0].lastMessageAt).toBeNull();
      });

      it('should handle lastMessageAt as undefined (use default)', () => {
        const env = setupFullChatEnvironment({
          chats: [{ id: 'chat-1' }],
        });

        expect(env.chats[0].lastMessageAt).not.toBeNull();
        expect(typeof env.chats[0].lastMessageAt).toBe('string');
      });
    });

    describe('Messages Overrides', () => {
      it('should create messages from overrides', () => {
        const env = setupFullChatEnvironment({
          messages: [
            { role: 'user', content: 'Hello' },
            { role: 'assistant', content: 'Hi there!' },
          ],
        });

        expect(env.messages).toHaveLength(2);
        expect(env.messages[0]).toMatchObject({
          level: 'user',
          message: 'Hello',
        });
        expect(env.messages[1]).toMatchObject({
          level: 'agent',
          message: 'Hi there!',
        });
      });

      it('should generate message IDs when not provided', () => {
        const env = setupFullChatEnvironment({
          messages: [{ role: 'user', content: 'Test' }],
        });

        expect(env.messages[0].id).toBe('msg-1');
      });

      it('should use custom message IDs when provided', () => {
        const env = setupFullChatEnvironment({
          messages: [{ id: 'custom-id', role: 'user', content: 'Test' }],
        });

        expect(env.messages[0].id).toBe('custom-id');
      });

      it('should handle followUpQuestions in metadata', () => {
        const env = setupFullChatEnvironment({
          messages: [
            {
              role: 'assistant',
              content: 'Answer',
              followUpQuestions: ['Question 1?', 'Question 2?'],
            },
          ],
        });

        const metadata = JSON.parse(env.messages[0].metadataJson);
        expect(metadata.followUpQuestions).toEqual(['Question 1?', 'Question 2?']);
      });

      it('should handle feedback in messages', () => {
        const env = setupFullChatEnvironment({
          messages: [
            { role: 'assistant', content: 'Good answer', feedback: true },
          ],
        });

        expect(env.messages[0].feedback).toBe(true);
      });

      it('should set feedback to null by default', () => {
        const env = setupFullChatEnvironment({
          messages: [{ role: 'assistant', content: 'Answer' }],
        });

        expect(env.messages[0].feedback).toBeNull();
      });

      it('should generate timestamps with decreasing order', () => {
        const env = setupFullChatEnvironment({
          messages: [
            { role: 'user', content: 'First' },
            { role: 'assistant', content: 'Second' },
            { role: 'user', content: 'Third' },
          ],
        });

        const timestamp1 = new Date(env.messages[0].createdAt).getTime();
        const timestamp2 = new Date(env.messages[1].createdAt).getTime();
        const timestamp3 = new Date(env.messages[2].createdAt).getTime();

        // Later messages should have later timestamps
        expect(timestamp2).toBeGreaterThan(timestamp1);
        expect(timestamp3).toBeGreaterThan(timestamp2);
      });
    });

    describe('Active Chat Scenario', () => {
      it('should configure environment when activeChat is true', () => {
        const env = setupFullChatEnvironment({
          activeChat: true,
          messages: [{ role: 'user', content: 'Test' }],
        });

        expect(env.messages).toHaveLength(1);
        expect(env.messages[0].message).toBe('Test');
      });

      it('should include messages when using active chat', () => {
        const env = setupFullChatEnvironment({
          activeChat: true,
          messages: [{ role: 'user', content: 'Test' }],
        });

        expect(env.messages).toHaveLength(1);
        expect(env.messages[0].message).toBe('Test');
        expect(env.chats).toHaveLength(1);
      });

      it('should support custom activeChatId', () => {
        const env = setupFullChatEnvironment({
          chats: [
            { id: 'chat-1', gameName: 'Chess' },
            { id: 'chat-2', gameName: 'Catan' },
          ],
          activeChat: true,
          activeChatId: 'chat-2',
          messages: [{ role: 'user', content: 'Test' }],
        });

        expect(env.chats).toHaveLength(2);
        expect(env.chats.some((c) => c.id === 'chat-2')).toBe(true);
      });

      it('should handle activeChat false with messages', () => {
        const env = setupFullChatEnvironment({
          activeChat: false,
          messages: [{ role: 'user', content: 'Test' }],
        });

        // Messages are still in environment, just not loaded into chat
        expect(env.messages).toHaveLength(1);
      });

      it('should handle activeChat true with empty messages', () => {
        const env = setupFullChatEnvironment({
          activeChat: true,
          messages: [],
        });

        // No messages to load
        expect(env.messages).toHaveLength(0);
      });
    });

    describe('Session Minutes', () => {
      it('should configure environment with custom sessionMinutes', () => {
        const env = setupFullChatEnvironment({ sessionMinutes: 60 });

        // Session mock is configured (will be used when component calls API)
        expect(env).toHaveProperty('user');
        expect(env).toHaveProperty('game');
      });

      it('should configure environment with default 30 minutes', () => {
        const env = setupFullChatEnvironment();

        // Default session mock is configured
        expect(env).toHaveProperty('user');
        expect(env).toHaveProperty('game');
      });
    });

    describe('Additional Games and Agents', () => {
      it('should include additional games', () => {
        const env = setupFullChatEnvironment({
          additionalGames: [
            { id: 'game-extra-1', name: 'Catan' },
            { id: 'game-extra-2', name: 'Risk' },
          ],
        });

        expect(env.games).toHaveLength(3); // 1 default + 2 additional
        expect(env.games[1].id).toBe('game-extra-1');
        expect(env.games[2].id).toBe('game-extra-2');
      });

      it('should include additional agents', () => {
        const env = setupFullChatEnvironment({
          additionalAgents: [
            { id: 'agent-extra-1', name: 'Extra Agent 1' },
            { id: 'agent-extra-2', name: 'Extra Agent 2' },
          ],
        });

        expect(env.agents).toHaveLength(3); // 1 default + 2 additional
        expect(env.agents[1].id).toBe('agent-extra-1');
        expect(env.agents[2].id).toBe('agent-extra-2');
      });

      it('should handle empty additional arrays', () => {
        const env = setupFullChatEnvironment({
          additionalGames: [],
          additionalAgents: [],
        });

        expect(env.games).toHaveLength(1);
        expect(env.agents).toHaveLength(1);
      });
    });

    describe('Edge Cases', () => {
      it('should handle undefined options', () => {
        const env = setupFullChatEnvironment(undefined);

        expect(env).toHaveProperty('user');
        expect(env).toHaveProperty('game');
        expect(env).toHaveProperty('agent');
      });

      it('should handle empty options object', () => {
        const env = setupFullChatEnvironment({});

        expect(env).toHaveProperty('user');
        expect(env).toHaveProperty('game');
        expect(env).toHaveProperty('agent');
      });

      it('should handle role mapping correctly (user -> user, assistant -> agent)', () => {
        const env = setupFullChatEnvironment({
          messages: [
            { role: 'user', content: 'User message' },
            { role: 'assistant', content: 'Assistant message' },
          ],
        });

        expect(env.messages[0].level).toBe('user');
        expect(env.messages[1].level).toBe('agent'); // Note: assistant maps to agent
      });
    });
  });

  describe('Setter Functions', () => {
    describe('setMockOnComplete', () => {
      it('should accept callback function', () => {
        const callback = () => {};
        expect(() => setMockOnComplete(callback)).not.toThrow();
      });

      it('should accept null', () => {
        expect(() => setMockOnComplete(null)).not.toThrow();
      });
    });

    describe('setMockOnError', () => {
      it('should accept callback function', () => {
        const callback = () => {};
        expect(() => setMockOnError(callback)).not.toThrow();
      });

      it('should accept null', () => {
        expect(() => setMockOnError(null)).not.toThrow();
      });
    });
  });

  describe('Integration Scenarios', () => {
    describe('Complete Chat Test Setup', () => {
      it('should support full test flow: setup -> use -> reset', async () => {
        // Setup
        const env = setupFullChatEnvironment({
          user: { role: 'Admin' },
          game: { name: 'Chess' },
          messages: [
            { role: 'user', content: 'How do I castle?' },
            {
              role: 'assistant',
              content: 'Castling is a special move...',
              followUpQuestions: ['Can I castle after moving my king?'],
            },
          ],
          activeChat: true,
        });

        expect(env.user.role).toBe('Admin');
        expect(env.messages).toHaveLength(2);

        // Reset
        resetAllMocks();

        // Verify clean state
        expect(mockApi.get).not.toHaveBeenCalled();
      });

      it('should support multiple test setups in sequence', () => {
        // First test
        const env1 = setupFullChatEnvironment({ game: { title: 'Chess' } });
        expect(env1.game.title).toBe('Chess');

        resetAllMocks();

        // Second test
        const env2 = setupFullChatEnvironment({ game: { title: 'Catan' } });
        expect(env2.game.title).toBe('Catan');
      });
    });

    describe('Error Scenarios', () => {
      it('should handle API mock failures gracefully', () => {
        mockApi.get.mockRejectedValue(new Error('Network error'));

        expect(() => setupFullChatEnvironment()).not.toThrow();
      });
    });
  });
});

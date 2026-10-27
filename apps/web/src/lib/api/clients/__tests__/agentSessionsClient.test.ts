/**
 * Agent Sessions Client Tests
 * Issue #3375 - Agent Session Launch API Integration
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createAgentSessionsClient } from '../agentSessionsClient';

// Mock HTTP client
const mockGet = vi.fn();
const mockPost = vi.fn();
const mockPatch = vi.fn();
const mockDelete = vi.fn();

const mockHttpClient = {
  get: mockGet,
  post: mockPost,
  patch: mockPatch,
  delete: mockDelete,
};

describe('agentSessionsClient', () => {
  let client: ReturnType<typeof createAgentSessionsClient>;

  beforeEach(() => {
    vi.clearAllMocks();
    client = createAgentSessionsClient({ httpClient: mockHttpClient as never });
  });

  describe('launch', () => {
    it('launches agent session with correct parameters', async () => {
      const mockResponse = { agentSessionId: 'session-123' };
      mockPost.mockResolvedValue(mockResponse);

      const request = {
        agentDefinitionId: 'typology-abc',
        agentId: 'agent-def',
        gameId: 'game-ghi',
        initialGameStateJson: '{"turn": 1}',
      };

      const result = await client.launch('game-session-xyz', request);

      expect(mockPost).toHaveBeenCalledWith(
        '/api/v1/game-sessions/game-session-xyz/agent/launch',
        request,
        expect.any(Object) // Schema
      );
      expect(result).toEqual(mockResponse);
    });

    it('throws error when no response', async () => {
      mockPost.mockResolvedValue(null);

      const request = {
        agentDefinitionId: 'typology-abc',
        agentId: 'agent-def',
        gameId: 'game-ghi',
      };

      await expect(client.launch('game-session-xyz', request)).rejects.toThrow(
        'Failed to launch agent session: no response from server'
      );
    });

    it('encodes game session ID in URL', async () => {
      mockPost.mockResolvedValue({ agentSessionId: 'session-123' });

      const request = {
        agentDefinitionId: 'typology-abc',
        agentId: 'agent-def',
        gameId: 'game-ghi',
      };

      await client.launch('game-session/with/slashes', request);

      expect(mockPost).toHaveBeenCalledWith(
        '/api/v1/game-sessions/game-session%2Fwith%2Fslashes/agent/launch',
        expect.any(Object),
        expect.any(Object)
      );
    });
  });

  /**
   * #4618: `PATCH /game-sessions/{id}/agent/config` rispondeva 204 e non salvava niente (`AgentSession.Config`
   * non ha una colonna). La configurazione sta nel profilo dell'agente (ADR-095), quindi la rotta è tolta e
   * il client non ha più un modo di chiamarla.
   */
  describe('the retired config route (#4618)', () => {
    it('has no updateConfig: nothing can PATCH the agent config of a session', async () => {
      expect(typeof (client as unknown as Record<string, unknown>).updateConfig).toBe('undefined');
      expect(mockPatch).not.toHaveBeenCalled();
    });
  });

  describe('endSession', () => {
    it('ends session with correct parameters', async () => {
      mockDelete.mockResolvedValue(undefined);

      await client.endSession('game-session-xyz', 'agent-session-abc');

      expect(mockDelete).toHaveBeenCalledWith(
        '/api/v1/game-sessions/game-session-xyz/agent?agentSessionId=agent-session-abc'
      );
    });
  });
});

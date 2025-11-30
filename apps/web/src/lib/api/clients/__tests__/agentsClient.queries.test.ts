/**
 * Comprehensive Tests for Agents Client (Issue #1661 - Fase 1.2)
 *
 * Coverage target: 95%+
 * Tests: Agent queries, commands, error handling, edge cases
 */

import { createAgentsClient } from '../agentsClient';
import { HttpClient } from '../../core/httpClient';

describe('createAgentsClient', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let agentsClient: ReturnType<typeof createAgentsClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
    } as any;

    agentsClient = createAgentsClient({ httpClient: mockHttpClient });
  });

  describe('getAll', () => {
    it('should fetch all agents without filters', async () => {
      const mockResponse = {
        success: true,
        agents: [
          { id: 'agent-1', name: 'GPT-4', type: 'chat', isActive: true },
          { id: 'agent-2', name: 'Claude', type: 'chat', isActive: true },
        ],
        count: 2,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.getAll();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/agents', expect.anything());
      expect(result).toEqual(mockResponse.agents);
    });

    it('should filter by activeOnly=true', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [{ id: 'agent-1', isActive: true }],
        count: 1,
      });

      await agentsClient.getAll(true);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('activeOnly=true'),
        expect.anything()
      );
    });

    it('should filter by activeOnly=false', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await agentsClient.getAll(false);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('activeOnly=false'),
        expect.anything()
      );
    });

    it('should filter by agent type', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await agentsClient.getAll(undefined, 'chat');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('type=chat'),
        expect.anything()
      );
    });

    it('should filter by both activeOnly and type', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await agentsClient.getAll(true, 'rag');

      const callUrl = mockHttpClient.get.mock.calls[0][0] as string;
      expect(callUrl).toContain('activeOnly=true');
      expect(callUrl).toContain('type=rag');
    });

    it('should return empty array when response is null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await agentsClient.getAll();

      expect(result).toEqual([]);
    });

    it('should return empty array when agents field is missing', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        count: 0,
      });

      const result = await agentsClient.getAll();

      expect(result).toEqual([]);
    });

    it('should handle special characters in type filter', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await agentsClient.getAll(undefined, 'chat/advanced');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });
  });

  describe('getAvailable', () => {
    it('should fetch only active agents', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [{ id: 'agent-1', isActive: true }],
        count: 1,
      });

      const result = await agentsClient.getAvailable();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('activeOnly=true'),
        expect.anything()
      );
      expect(result).toHaveLength(1);
    });

    it('should filter by type when provided', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await agentsClient.getAvailable('chat');

      const callUrl = mockHttpClient.get.mock.calls[0][0] as string;
      expect(callUrl).toContain('activeOnly=true');
      expect(callUrl).toContain('type=chat');
    });

    it('should return empty array when no active agents', async () => {
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      const result = await agentsClient.getAvailable();

      expect(result).toEqual([]);
    });
  });

  describe('getById', () => {
    it('should fetch agent by ID', async () => {
      const mockAgent = {
        id: 'agent-123',
        name: 'GPT-4',
        type: 'chat',
        isActive: true,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockAgent);

      const result = await agentsClient.getById('agent-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123',
        expect.anything()
      );
      expect(result).toEqual(mockAgent);
    });

    it('should return null when agent not found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await agentsClient.getById('non-existent');

      expect(result).toBeNull();
    });

    it('should encode agentId in URL', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await agentsClient.getById('agent with/spaces');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('agent%20with%2Fspaces'),
        expect.anything()
      );
    });

    it('should handle UUID format agent IDs', async () => {
      const uuid = '550e8400-e29b-41d4-a716-446655440000';
      mockHttpClient.get.mockResolvedValueOnce({ id: uuid });

      await agentsClient.getById(uuid);

      expect(mockHttpClient.get).toHaveBeenCalledWith(`/api/v1/agents/${uuid}`, expect.anything());
    });

    it('should handle special characters in agent ID', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await agentsClient.getById('agent&id=123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('agent%26id%3D123'),
        expect.anything()
      );
    });
  });

  describe('invoke', () => {
    it('should invoke agent with query', async () => {
      const request = {
        query: 'What are the rules for Catan?',
        context: { gameId: 'catan' },
      };

      const mockResponse = {
        agentId: 'agent-123',
        response: 'The objective is to reach 10 victory points...',
        confidence: 0.95,
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.invoke('agent-123', request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123/invoke',
        request,
        expect.anything()
      );
      expect(result).toEqual(mockResponse);
    });

    it('should invoke agent without context', async () => {
      const request = {
        query: 'General question',
      };

      mockHttpClient.post.mockResolvedValueOnce({
        agentId: 'agent-1',
        response: 'Answer',
      });

      await agentsClient.invoke('agent-1', request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ query: 'General question' }),
        expect.anything()
      );
    });

    it('should throw error when response is null', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      await expect(agentsClient.invoke('agent-1', { query: 'test' })).rejects.toThrow(
        'Failed to invoke agent: no response from server'
      );
    });

    it('should encode agentId in URL', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ agentId: 'agent', response: '' });

      await agentsClient.invoke('agent with/spaces', { query: 'test' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.stringContaining('agent%20with%2Fspaces'),
        expect.anything(),
        expect.anything()
      );
    });

    it('should handle empty query', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ agentId: 'agent-1', response: '' });

      await agentsClient.invoke('agent-1', { query: '' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ query: '' }),
        expect.anything()
      );
    });

    it('should handle very long queries', async () => {
      const longQuery = 'A'.repeat(10000);
      mockHttpClient.post.mockResolvedValueOnce({ agentId: 'agent-1', response: '' });

      await agentsClient.invoke('agent-1', { query: longQuery });

      expect(mockHttpClient.post).toHaveBeenCalled();
    });

    it('should handle Unicode in query', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ agentId: 'agent-1', response: '' });

      await agentsClient.invoke('agent-1', { query: '什么是卡坦岛的规则？' });

      expect(mockHttpClient.post).toHaveBeenCalled();
    });
  });

  describe('create', () => {

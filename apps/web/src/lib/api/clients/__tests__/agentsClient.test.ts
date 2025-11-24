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
    it('should create new agent', async () => {
      const request = {
        name: 'New Agent',
        type: 'chat',
        strategyName: 'default',
        strategyParameters: { model: 'gpt-4' },
        isActive: true,
      };

      const mockResponse = {
        id: 'agent-new',
        ...request,
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.create(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents',
        request,
        expect.anything()
      );
      expect(result).toEqual(mockResponse);
    });

    it('should create agent with minimal fields', async () => {
      const request = {
        name: 'Minimal Agent',
        type: 'rag',
        strategyName: 'default',
      };

      mockHttpClient.post.mockResolvedValueOnce({ id: 'agent-min' });

      await agentsClient.create(request);

      expect(mockHttpClient.post).toHaveBeenCalled();
    });

    it('should throw error when creation fails', async () => {
      mockHttpClient.post.mockResolvedValueOnce(null);

      await expect(
        agentsClient.create({ name: 'Test', type: 'chat', strategyName: 'default' })
      ).rejects.toThrow('Failed to create agent: no response from server');
    });

    it('should handle complex configuration objects', async () => {
      const request = {
        name: 'Complex Agent',
        type: 'advanced',
        strategyName: 'advanced',
        strategyParameters: {
          model: 'gpt-4',
          temperature: 0.7,
          maxTokens: 2000,
          systemPrompt: 'You are a helpful assistant',
          tools: ['search', 'calculator'],
        },
      };

      mockHttpClient.post.mockResolvedValueOnce({ id: 'agent-complex' });

      await agentsClient.create(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ strategyParameters: request.strategyParameters }),
        expect.anything()
      );
    });
  });

  describe('configure', () => {
    it('should configure agent strategy', async () => {
      const request = {
        strategyName: 'advanced',
        strategyParameters: {
          temperature: 0.8,
          maxTokens: 3000,
        },
      };

      const mockResponse = {
        success: true,
        agentId: 'agent-123',
        message: 'Agent configured successfully',
      };

      mockHttpClient.put.mockResolvedValueOnce(mockResponse);

      const result = await agentsClient.configure('agent-123', request);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123/configure',
        request,
        expect.anything()
      );
      expect(result).toEqual(mockResponse);
    });

    it('should configure with empty parameters', async () => {
      const request = {
        strategyName: 'default',
        strategyParameters: {},
      };

      mockHttpClient.put.mockResolvedValueOnce({ success: true });

      await agentsClient.configure('agent-1', request);

      expect(mockHttpClient.put).toHaveBeenCalled();
    });

    it('should throw error when configuration fails', async () => {
      mockHttpClient.put.mockResolvedValueOnce(null);

      await expect(agentsClient.configure('agent-1', { strategyName: 'test' })).rejects.toThrow(
        'Failed to configure agent: no response from server'
      );
    });

    it('should encode agentId in URL', async () => {
      mockHttpClient.put.mockResolvedValueOnce({ success: true });

      await agentsClient.configure('agent with/spaces', { strategyName: 'test' });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        expect.stringContaining('agent%20with%2Fspaces'),
        expect.anything(),
        expect.anything()
      );
    });

    it('should handle complex configuration parameters', async () => {
      const request = {
        strategyName: 'multi-model',
        strategyParameters: {
          primary: { model: 'gpt-4', weight: 0.7 },
          secondary: { model: 'claude-3', weight: 0.3 },
          consensus: {
            enabled: true,
            threshold: 0.8,
          },
        },
      };

      mockHttpClient.put.mockResolvedValueOnce({ success: true });

      await agentsClient.configure('agent-1', request);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({ strategyParameters: request.strategyParameters }),
        expect.anything()
      );
    });
  });

  describe('Error Handling', () => {
    it('should propagate errors from getAll', async () => {
      const error = new Error('Network error');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(agentsClient.getAll()).rejects.toThrow('Network error');
    });

    it('should propagate errors from getById', async () => {
      const error = new Error('Not found');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(agentsClient.getById('agent-1')).rejects.toThrow('Not found');
    });

    it('should propagate errors from invoke', async () => {
      const error = new Error('Agent execution failed');
      mockHttpClient.post.mockRejectedValueOnce(error);

      await expect(agentsClient.invoke('agent-1', { query: 'test' })).rejects.toThrow(
        'Agent execution failed'
      );
    });

    it('should propagate errors from create', async () => {
      const error = new Error('Validation error');
      mockHttpClient.post.mockRejectedValueOnce(error);

      await expect(
        agentsClient.create({ name: 'Test', type: 'chat', strategyName: 'default' })
      ).rejects.toThrow('Validation error');
    });

    it('should propagate errors from configure', async () => {
      const error = new Error('Invalid strategy');
      mockHttpClient.put.mockRejectedValueOnce(error);

      await expect(agentsClient.configure('agent-1', { strategyName: 'invalid' })).rejects.toThrow(
        'Invalid strategy'
      );
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete agent lifecycle', async () => {
      // 1. Get all agents
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      const initialAgents = await agentsClient.getAll();
      expect(initialAgents).toHaveLength(0);

      // 2. Create new agent
      mockHttpClient.post.mockResolvedValueOnce({
        id: 'agent-new',
        name: 'Test Agent',
        type: 'chat',
      });

      const newAgent = await agentsClient.create({
        name: 'Test Agent',
        type: 'chat',
        strategyName: 'default',
      });

      expect(newAgent.id).toBe('agent-new');

      // 3. Configure agent
      mockHttpClient.put.mockResolvedValueOnce({
        success: true,
        agentId: 'agent-new',
      });

      await agentsClient.configure('agent-new', { strategyName: 'advanced' });

      // 4. Invoke agent
      mockHttpClient.post.mockResolvedValueOnce({
        agentId: 'agent-new',
        response: 'Agent response',
      });

      const response = await agentsClient.invoke('agent-new', { query: 'test query' });

      expect(response.response).toBe('Agent response');
    });

    it('should handle search by type workflow', async () => {
      // Get chat agents
      mockHttpClient.get.mockResolvedValueOnce({
        success: true,
        agents: [
          { id: 'agent-1', type: 'chat' },
          { id: 'agent-2', type: 'chat' },
        ],
        count: 2,
      });

      const chatAgents = await agentsClient.getAvailable('chat');
      expect(chatAgents).toHaveLength(2);

      // Get specific agent details
      mockHttpClient.get.mockResolvedValueOnce({
        id: 'agent-1',
        type: 'chat',
        name: 'GPT-4',
      });

      const details = await agentsClient.getById(chatAgents[0].id);
      expect(details?.name).toBe('GPT-4');
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty agent name', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ id: 'agent' });

      await agentsClient.create({ name: '', type: 'chat', strategyName: 'default' });

      expect(mockHttpClient.post).toHaveBeenCalled();
    });

    it('should handle very long agent names', async () => {
      const longName = 'A'.repeat(1000);
      mockHttpClient.post.mockResolvedValueOnce({ id: 'agent' });

      await agentsClient.create({ name: longName, type: 'chat', strategyName: 'default' });

      expect(mockHttpClient.post).toHaveBeenCalled();
    });

    it('should handle concurrent agent invocations', async () => {
      mockHttpClient.post.mockResolvedValue({
        agentId: 'agent-1',
        response: 'Response',
      });

      const promises = [
        agentsClient.invoke('agent-1', { query: 'query1' }),
        agentsClient.invoke('agent-1', { query: 'query2' }),
        agentsClient.invoke('agent-1', { query: 'query3' }),
      ];

      await Promise.all(promises);

      expect(mockHttpClient.post).toHaveBeenCalledTimes(3);
    });

    it('should handle optional gameId and chatThreadId', async () => {
      mockHttpClient.post.mockResolvedValueOnce({
        agentId: 'agent-1',
        response: 'Response',
      });

      await agentsClient.invoke('agent-1', {
        query: 'test',
        gameId: undefined,
        chatThreadId: undefined,
      });

      expect(mockHttpClient.post).toHaveBeenCalled();
    });

    it('should handle numeric agent IDs', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ id: '123' });

      await agentsClient.getById('123');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/agents/123', expect.anything());
    });
  });
});

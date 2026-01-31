/**
 * AgentsClient Command Tests
 *
 * Tests for write operations: invoke, create, configure
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createAgentsClient } from '../agentsClient';
import type { HttpClient } from '../../core/httpClient';

describe('agentsClient commands', () => {
  const mockHttpClient: HttpClient = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  };

  const client = createAgentsClient({ httpClient: mockHttpClient });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('invoke', () => {
    it('should invoke agent with query', async () => {
      const mockResponse = {
        answer: 'This is the answer',
        confidence: 0.85,
        sources: [],
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResponse);

      const result = await client.invoke('agent-123', {
        query: 'What are the rules?',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123/invoke',
        { query: 'What are the rules?' },
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should invoke agent with context', async () => {
      const mockResponse = {
        answer: 'Contextual answer',
        confidence: 0.9,
        sources: [],
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResponse);

      const result = await client.invoke('agent-123', {
        query: 'Follow up question',
        context: 'Previous conversation context',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123/invoke',
        {
          query: 'Follow up question',
          context: 'Previous conversation context',
        },
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should throw error on null response', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(client.invoke('agent-123', { query: 'Test' })).rejects.toThrow(
        'Failed to invoke agent: no response from server'
      );
    });

    it('should encode special characters in agent ID', async () => {
      const mockResponse = { answer: 'Response', confidence: 0.8, sources: [] };
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResponse);

      await client.invoke('agent/special', { query: 'Test' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/agent%2Fspecial/invoke',
        expect.any(Object),
        expect.any(Object)
      );
    });
  });

  describe('create', () => {
    it('should create a new agent', async () => {
      const mockAgent = {
        id: 'new-agent-id',
        name: 'New Agent',
        type: 'qa',
        isActive: true,
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockAgent);

      const result = await client.create({
        name: 'New Agent',
        type: 'qa',
        description: 'A new QA agent',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents',
        {
          name: 'New Agent',
          type: 'qa',
          description: 'A new QA agent',
        },
        expect.any(Object)
      );
      expect(result).toEqual(mockAgent);
    });

    it('should throw error on null response', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(client.create({ name: 'Test', type: 'qa' })).rejects.toThrow(
        'Failed to create agent: no response from server'
      );
    });
  });

  describe('configure', () => {
    it('should configure agent strategy', async () => {
      const mockResponse = {
        success: true,
        agent: {
          id: 'agent-123',
          name: 'Configured Agent',
          strategy: 'enhanced',
        },
      };

      vi.mocked(mockHttpClient.put).mockResolvedValueOnce(mockResponse);

      const result = await client.configure('agent-123', {
        strategy: 'enhanced',
        parameters: { temperature: 0.7 },
      });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123/configure',
        {
          strategy: 'enhanced',
          parameters: { temperature: 0.7 },
        },
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should throw error on null response', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValueOnce(null);

      await expect(client.configure('agent-123', { strategy: 'basic' })).rejects.toThrow(
        'Failed to configure agent: no response from server'
      );
    });

    it('should encode special characters in agent ID', async () => {
      const mockResponse = { success: true, agent: {} };
      vi.mocked(mockHttpClient.put).mockResolvedValueOnce(mockResponse);

      await client.configure('agent/config', { strategy: 'basic' });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/agents/agent%2Fconfig/configure',
        expect.any(Object),
        expect.any(Object)
      );
    });
  });
});

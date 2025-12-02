/**
 * AgentsClient Query Tests
 *
 * Tests for read operations: getAll, getAvailable, getById
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createAgentsClient } from '../agentsClient';
import type { HttpClient } from '../../core/httpClient';

describe('agentsClient queries', () => {
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

  describe('getAll', () => {
    it('should fetch all agents without filters', async () => {
      const mockAgents = [
        { id: 'agent-1', name: 'Agent One', type: 'qa', isActive: true },
        { id: 'agent-2', name: 'Agent Two', type: 'rules', isActive: false },
      ];

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: mockAgents,
        count: 2,
      });

      const result = await client.getAll();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/agents', expect.any(Object));
      expect(result).toEqual(mockAgents);
    });

    it('should fetch agents with activeOnly filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await client.getAll(true);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents?activeOnly=true',
        expect.any(Object)
      );
    });

    it('should fetch agents with type filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await client.getAll(undefined, 'qa');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/agents?type=qa', expect.any(Object));
    });

    it('should fetch agents with both filters', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await client.getAll(true, 'rules');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents?activeOnly=true&type=rules',
        expect.any(Object)
      );
    });

    it('should return empty array on null response', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getAll();

      expect(result).toEqual([]);
    });
  });

  describe('getAvailable', () => {
    it('should fetch active agents only', async () => {
      const mockAgents = [{ id: 'agent-1', name: 'Active Agent', type: 'qa', isActive: true }];

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: mockAgents,
        count: 1,
      });

      const result = await client.getAvailable();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents?activeOnly=true',
        expect.any(Object)
      );
      expect(result).toEqual(mockAgents);
    });

    it('should fetch active agents with type filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce({
        success: true,
        agents: [],
        count: 0,
      });

      await client.getAvailable('qa');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents?activeOnly=true&type=qa',
        expect.any(Object)
      );
    });
  });

  describe('getById', () => {
    it('should fetch agent by ID', async () => {
      const mockAgent = {
        id: 'agent-123',
        name: 'Test Agent',
        type: 'qa',
        isActive: true,
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockAgent);

      const result = await client.getById('agent-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents/agent-123',
        expect.any(Object)
      );
      expect(result).toEqual(mockAgent);
    });

    it('should return null for non-existent agent', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getById('non-existent');

      expect(result).toBeNull();
    });

    it('should encode special characters in ID', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      await client.getById('agent/with/slashes');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/agents/agent%2Fwith%2Fslashes',
        expect.any(Object)
      );
    });
  });
});

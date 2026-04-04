/**
 * Ownership & RAG Access API Client Tests
 *
 * Tests for: declareOwnership (library), quickCreateTutor (agents), setRagPublicAccess (admin)
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createLibraryClient } from '../clients/libraryClient';
import { createAgentsClient } from '../clients/agentsClient';
import type { HttpClient } from '../core/httpClient';

describe('ownership API methods', () => {
  const mockHttpClient = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  } as unknown as HttpClient;

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('library.declareOwnership', () => {
    const client = createLibraryClient({ httpClient: mockHttpClient });

    it('should POST to declare-ownership endpoint', async () => {
      const mockResult = {
        gameState: 'Owned',
        ownershipDeclaredAt: '2026-03-14T10:00:00Z',
        hasRagAccess: true,
        kbCardCount: 3,
        isRagPublic: false,
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResult);

      const result = await client.declareOwnership('game-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/library/game-123/declare-ownership',
        {},
        expect.any(Object)
      );
      expect(result).toEqual(mockResult);
    });

    it('should throw when server returns null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(client.declareOwnership('game-123')).rejects.toThrow(
        'Failed to declare ownership'
      );
    });
  });

  describe('agents.quickCreateTutor', () => {
    const client = createAgentsClient({ httpClient: mockHttpClient });

    it('should POST to quick-create endpoint with gameId', async () => {
      const mockResult = {
        agentId: 'agent-1',
        chatThreadId: 'thread-1',
        agentName: 'Catan Tutor',
        kbCardCount: 2,
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResult);

      const result = await client.quickCreateTutor('game-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/quick-create',
        { gameId: 'game-123' },
        expect.any(Object)
      );
      expect(result).toEqual(mockResult);
    });

    it('should include sharedGameId when provided', async () => {
      const mockResult = {
        agentId: 'agent-1',
        chatThreadId: 'thread-1',
        agentName: 'Catan Tutor',
        kbCardCount: 2,
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockResult);

      await client.quickCreateTutor('game-123', 'shared-456');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/agents/quick-create',
        { gameId: 'game-123', sharedGameId: 'shared-456' },
        expect.any(Object)
      );
    });

    it('should throw when server returns null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(null);

      await expect(client.quickCreateTutor('game-123')).rejects.toThrow(
        'Failed to quick-create tutor'
      );
    });
  });
});

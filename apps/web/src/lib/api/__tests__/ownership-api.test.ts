/**
 * Ownership & RAG Access API Client Tests
 *
 * Tests for: declareOwnership (library), quickCreateTutor (agents), setRagPublicAccess (admin)
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createLibraryClient } from '../clients/libraryClient';
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

  // Issue #4138: a describe block for `agents.quickCreateTutor` stood here, with 3
  // tests asserting `post` was called with '/api/v1/agents/quick-create'. That route
  // is retired, and the tests were of the shape that let the #4139 defect survive a
  // green suite: a mocked client asserted against itself, never against the server.
  //
  // No replacement here. On the frontend the guard is the type system - the method does
  // not exist, so no test can call it. The route's absence is asserted server-side, by
  // `RetiredRoutes` in Api.Tests/Routing/EndpointContractTests.cs, against the real host.
});

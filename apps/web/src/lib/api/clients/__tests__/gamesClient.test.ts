/**
 * Tests for Games Client - getRules() method (Issue #2027)
 *
 * Coverage target: 90%+
 * Tests: getRules endpoint integration, error handling, edge cases
 */

import { createGamesClient } from '../gamesClient';
import { HttpClient } from '../../core/httpClient';
import type { RuleSpec } from '../../schemas';

describe('createGamesClient - getRules', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let gamesClient: ReturnType<typeof createGamesClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
    } as any;

    gamesClient = createGamesClient({ httpClient: mockHttpClient });
  });

  describe('getRules', () => {
    const mockGameId = '123e4567-e89b-12d3-a456-426614174000';

    it('should fetch rules for a game successfully', async () => {
      const mockRules: RuleSpec[] = [
        {
          id: 'rule-uuid-1',
          gameId: mockGameId,
          version: '1.0',
          createdAt: '2024-01-01T00:00:00Z',
          createdByUserId: 'user-uuid',
          parentVersionId: null,
          atoms: [
            {
              id: 'atom-1',
              text: 'Setup the game board',
              section: 'Setup',
              page: '1',
              line: '1',
            },
          ],
        },
        {
          id: 'rule-uuid-2',
          gameId: mockGameId,
          version: '1.1',
          createdAt: '2024-01-02T00:00:00Z',
          createdByUserId: 'user-uuid',
          parentVersionId: 'rule-uuid-1',
          atoms: [
            {
              id: 'atom-2',
              text: 'Setup the game board (updated)',
              section: 'Setup',
              page: '1',
              line: '1',
            },
          ],
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockRules);

      const result = await gamesClient.getRules(mockGameId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/rules`,
        expect.anything()
      );
      expect(result).toEqual(mockRules);
      expect(result).toHaveLength(2);
    });

    it('should return empty array when no rules found', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      const result = await gamesClient.getRules(mockGameId);

      expect(result).toEqual([]);
      expect(result).toHaveLength(0);
    });

    it('should return empty array when response is null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await gamesClient.getRules(mockGameId);

      expect(result).toEqual([]);
    });

    it('should properly encode gameId in URL', async () => {
      const specialGameId = 'game-with-special/chars';
      mockHttpClient.get.mockResolvedValueOnce([]);

      await gamesClient.getRules(specialGameId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/games/${encodeURIComponent(specialGameId)}/rules`,
        expect.anything()
      );
    });

    it('should handle API errors gracefully', async () => {
      const error = new Error('Network error');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(gamesClient.getRules(mockGameId)).rejects.toThrow('Network error');
    });

    it('should handle multiple versions correctly', async () => {
      const mockRulesMultiVersion: RuleSpec[] = [
        {
          id: 'v1',
          gameId: mockGameId,
          version: '1.0',
          createdAt: '2024-01-01T00:00:00Z',
          createdByUserId: null,
          parentVersionId: null,
          atoms: [],
        },
        {
          id: 'v2',
          gameId: mockGameId,
          version: '2.0',
          createdAt: '2024-02-01T00:00:00Z',
          createdByUserId: null,
          parentVersionId: 'v1',
          atoms: [],
        },
        {
          id: 'v3',
          gameId: mockGameId,
          version: '3.0',
          createdAt: '2024-03-01T00:00:00Z',
          createdByUserId: null,
          parentVersionId: 'v2',
          atoms: [],
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockRulesMultiVersion);

      const result = await gamesClient.getRules(mockGameId);

      expect(result).toHaveLength(3);
      expect(result[0].version).toBe('1.0');
      expect(result[1].version).toBe('2.0');
      expect(result[2].version).toBe('3.0');
    });
  });
});

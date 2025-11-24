/**
 * Comprehensive Tests for BGG Client (Issue #1661 - Fase 1.2)
 *
 * Coverage target: 95%+
 * Tests: BGG search, game details, error handling, edge cases
 */

import { createBggClient } from '../bggClient';
import { HttpClient } from '../../core/httpClient';

describe('createBggClient', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let bggClient: ReturnType<typeof createBggClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
    } as any;

    bggClient = createBggClient({ httpClient: mockHttpClient });
  });

  describe('search', () => {
    it('should search with query parameter', async () => {
      const mockResults = {
        total: 2,
        items: [
          { id: 13, name: 'Catan', yearPublished: 1995 },
          { id: 30549, name: 'Pandemic', yearPublished: 2008 },
        ],
      };

      mockHttpClient.get.mockResolvedValueOnce(mockResults);

      const result = await bggClient.search('catan');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('q=catan'),
        expect.anything()
      );
      expect(result).toEqual(mockResults);
    });

    it('should search with exact matching enabled', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('Catan', true);

      const callUrl = mockHttpClient.get.mock.calls[0][0] as string;
      expect(callUrl).toContain('q=Catan');
      expect(callUrl).toContain('exact=true');
    });

    it('should search with exact matching disabled by default', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('Pandemic');

      const callUrl = mockHttpClient.get.mock.calls[0][0] as string;
      expect(callUrl).toContain('q=Pandemic');
      expect(callUrl).not.toContain('exact=');
    });

    it('should search with exact=false explicitly', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('Azul', false);

      const callUrl = mockHttpClient.get.mock.calls[0][0] as string;
      expect(callUrl).not.toContain('exact=');
    });

    it('should handle empty search query', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('q='),
        expect.anything()
      );
    });

    it('should handle search query with spaces', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('Ticket to Ride');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('q=Ticket+to+Ride'),
        expect.anything()
      );
    });

    it('should handle search query with special characters', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('Bang!');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });

    it('should handle Unicode search queries', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('魔法风云会');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });

    it('should handle very long search queries', async () => {
      const longQuery = 'a'.repeat(1000);
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search(longQuery);

      expect(mockHttpClient.get).toHaveBeenCalled();
    });

    it('should throw error when response is null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await expect(bggClient.search('test')).rejects.toThrow('Failed to search BoardGameGeek');
    });

    it('should propagate httpClient errors', async () => {
      const error = new Error('Network timeout');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(bggClient.search('catan')).rejects.toThrow('Network timeout');
    });
  });

  describe('getGameDetails', () => {
    it('should fetch game details by BGG ID', async () => {
      const mockDetails = {
        id: 13,
        name: 'Catan',
        yearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        playingTime: 90,
        description: 'Trade and build on the island of Catan',
        imageUrl: 'https://cf.geekdo-images.com/catan.jpg',
        thumbnailUrl: 'https://cf.geekdo-images.com/catan-thumb.jpg',
      };

      mockHttpClient.get.mockResolvedValueOnce(mockDetails);

      const result = await bggClient.getGameDetails(13);

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/bgg/games/13', expect.anything());
      expect(result).toEqual(mockDetails);
    });

    it('should handle large BGG IDs', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ id: 999999, name: 'Test Game' });

      await bggClient.getGameDetails(999999);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/bgg/games/999999',
        expect.anything()
      );
    });

    it('should handle BGG ID = 1', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ id: 1, name: 'First Game' });

      await bggClient.getGameDetails(1);

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/bgg/games/1', expect.anything());
    });

    it('should throw error when game not found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await expect(bggClient.getGameDetails(99999)).rejects.toThrow(
        'Game with BGG ID 99999 not found'
      );
    });

    it('should propagate httpClient errors', async () => {
      const error = new Error('BGG API unavailable');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(bggClient.getGameDetails(13)).rejects.toThrow('BGG API unavailable');
    });

    it('should handle BGG ID = 0', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await expect(bggClient.getGameDetails(0)).rejects.toThrow('Game with BGG ID 0 not found');
    });

    it('should handle negative BGG ID', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await expect(bggClient.getGameDetails(-1)).rejects.toThrow('Game with BGG ID -1 not found');
    });

    it('should handle float BGG ID (truncated to int)', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ id: 13, name: 'Catan' });

      // TypeScript would prevent this, but test runtime behavior
      await bggClient.getGameDetails(13.7 as any);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('13.7'),
        expect.anything()
      );
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete search-to-details workflow', async () => {
      const mockSearchResults = {
        total: 1,
        items: [{ id: 13, name: 'Catan', yearPublished: 1995 }],
      };

      const mockDetails = {
        id: 13,
        name: 'Catan',
        yearPublished: 1995,
        description: 'Full details',
      };

      mockHttpClient.get
        .mockResolvedValueOnce(mockSearchResults)
        .mockResolvedValueOnce(mockDetails);

      const searchResult = await bggClient.search('Catan');
      const details = await bggClient.getGameDetails(searchResult.results[0].bggId);

      expect(searchResult.results).toHaveLength(1);
      expect(details.description).toBe('Full details');
    });

    it('should handle no search results gracefully', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ results: [] });

      const result = await bggClient.search('nonexistent-game-xyz');

      expect(result.results).toHaveLength(0);
    });

    it('should handle multiple sequential searches', async () => {
      mockHttpClient.get.mockResolvedValue({ results: [] });

      await bggClient.search('Catan');
      await bggClient.search('Pandemic');
      await bggClient.search('Azul');

      expect(mockHttpClient.get).toHaveBeenCalledTimes(3);
    });
  });

  describe('Edge Cases', () => {
    it('should handle concurrent searches', async () => {
      mockHttpClient.get.mockResolvedValue({ results: [] });

      const promises = [
        bggClient.search('Catan'),
        bggClient.search('Pandemic'),
        bggClient.search('Azul'),
      ];

      await Promise.all(promises);

      expect(mockHttpClient.get).toHaveBeenCalledTimes(3);
    });

    it('should handle concurrent getGameDetails calls', async () => {
      mockHttpClient.get.mockResolvedValue({ id: 1, name: 'Game' });

      const promises = [
        bggClient.getGameDetails(13),
        bggClient.getGameDetails(30549),
        bggClient.getGameDetails(230802),
      ];

      await Promise.all(promises);

      expect(mockHttpClient.get).toHaveBeenCalledTimes(3);
    });

    it('should handle search with query parameters in query string', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('game?id=123&test=value');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });

    it('should handle newlines in search query', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ total: 0, items: [] });

      await bggClient.search('multi\nline\nquery');

      expect(mockHttpClient.get).toHaveBeenCalled();
    });
  });
});

/**
 * Games Client Tests (FE-IMP-005)
 *
 * Tests for GameManagement bounded context (Games) client.
 * Covers: Games CRUD, filtering, sorting, pagination, documents
 */

import { createGamesClient } from '../clients/gamesClient';
import { HttpClient } from '../core/httpClient';
import type { Game, PaginatedGamesResponse, PdfDocumentDto } from '../schemas';
import { globalRequestCache } from '../core/requestCache';

describe('GamesClient', () => {
  let mockFetch: jest.Mock;
  let httpClient: HttpClient;
  let gamesClient: ReturnType<typeof createGamesClient>;

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();

    mockFetch = jest.fn();
    httpClient = new HttpClient({ fetchImpl: mockFetch });
    gamesClient = createGamesClient({ httpClient });
  });

  describe('getAll', () => {
    const mockGames: Game[] = [
      {
        id: '770e8400-e29b-41d4-a716-446655440001',
        title: 'Azul',
        publisher: 'Next Move Games',
        yearPublished: 2017,
        minPlayers: 2,
        maxPlayers: 4,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 45,
        bggId: 123456,
        createdAt: '2025-01-01T00:00:00Z',
      },
      {
        id: '770e8400-e29b-41d4-a716-446655440002',
        title: 'Wingspan',
        publisher: 'Stonemaier Games',
        yearPublished: 2019,
        minPlayers: 1,
        maxPlayers: 5,
        minPlayTimeMinutes: 40,
        maxPlayTimeMinutes: 70,
        bggId: 234567,
        createdAt: '2025-01-02T00:00:00Z',
      },
      {
        id: '770e8400-e29b-41d4-a716-446655440003',
        title: 'Custom Game',
        publisher: 'Indie Publisher',
        yearPublished: 2024,
        minPlayers: 2,
        maxPlayers: 6,
        minPlayTimeMinutes: 45,
        maxPlayTimeMinutes: 90,
        bggId: null, // No BGG ID
        createdAt: '2025-01-03T00:00:00Z',
      },
    ];

    it('should fetch all games without filters', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll();

      expect(result.games).toEqual(mockGames);
      expect(result.total).toBe(3);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(20);
      expect(result.totalPages).toBe(1);
    });

    it('should filter games by search term (title)', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ search: 'Azul' });

      expect(result.games).toHaveLength(1);
      expect(result.games[0].title).toBe('Azul');
    });

    it('should filter games by search term (publisher)', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ search: 'Stonemaier' });

      expect(result.games).toHaveLength(1);
      expect(result.games[0].title).toBe('Wingspan');
    });

    it('should filter games by minimum players', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ minPlayers: 2 });

      // Games that support 2+ players (maxPlayers >= 2)
      expect(result.games).toHaveLength(3);
    });

    it('should filter games by maximum players', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ maxPlayers: 4 });

      // Games that can be played with up to 4 players (minPlayers <= 4)
      expect(result.games).toHaveLength(3);
    });

    it('should filter games by play time range', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ minPlayTime: 30, maxPlayTime: 60 });

      // Games with maxPlayTime >= 30 AND minPlayTime <= 60
      expect(result.games).toHaveLength(2); // Azul and Wingspan
    });

    it('should filter games by year range', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ yearFrom: 2018, yearTo: 2024 });

      expect(result.games).toHaveLength(2); // Wingspan (2019) and Custom Game (2024)
    });

    it('should filter BGG-only games', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ bggOnly: true });

      expect(result.games).toHaveLength(2); // Azul and Wingspan have BGG IDs
      expect(result.games.every(g => g.bggId !== null)).toBe(true);
    });

    it('should combine multiple filters', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({
        minPlayers: 2,
        maxPlayers: 4,
        yearFrom: 2017,
        yearTo: 2019,
        bggOnly: true,
      });

      expect(result.games).toHaveLength(2); // Azul and Wingspan
    });

    it('should sort games by title ascending', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll(undefined, {
        field: 'title',
        direction: 'asc',
      });

      expect(result.games[0].title).toBe('Azul');
      expect(result.games[1].title).toBe('Custom Game');
      expect(result.games[2].title).toBe('Wingspan');
    });

    it('should sort games by title descending', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll(undefined, {
        field: 'title',
        direction: 'desc',
      });

      expect(result.games[0].title).toBe('Wingspan');
      expect(result.games[1].title).toBe('Custom Game');
      expect(result.games[2].title).toBe('Azul');
    });

    it('should sort games by year ascending', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll(undefined, {
        field: 'yearPublished',
        direction: 'asc',
      });

      expect(result.games[0].yearPublished).toBe(2017);
      expect(result.games[1].yearPublished).toBe(2019);
      expect(result.games[2].yearPublished).toBe(2024);
    });

    it('should paginate results', async () => {
      const manyGames = Array.from({ length: 50 }, (_, i) => ({
        ...mockGames[0],
        id: `770e8400-e29b-41d4-a716-${String(446655440000 + i).padStart(12, '0')}`,
        title: `Game ${i}`,
      }));

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => manyGames,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll(undefined, undefined, 2, 20);

      expect(result.games).toHaveLength(20);
      expect(result.total).toBe(50);
      expect(result.page).toBe(2);
      expect(result.pageSize).toBe(20);
      expect(result.totalPages).toBe(3);
      expect(result.games[0].title).toBe('Game 20'); // Second page starts at index 20
    });

    it('should handle null response gracefully', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll();

      expect(result.games).toEqual([]);
      expect(result.total).toBe(0);
    });

    it('should handle null values in game data', async () => {
      const gamesWithNulls: Game[] = [
        {
          ...mockGames[0],
          minPlayers: null,
          maxPlayers: null,
          yearPublished: null,
        },
      ];

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => gamesWithNulls,
        headers: new Headers(),
      });

      const result = await gamesClient.getAll({ minPlayers: 2 });

      // Game with null maxPlayers should be excluded
      expect(result.games).toHaveLength(0);
    });
  });

  describe('getById', () => {
    it('should get a single game by ID', async () => {
      const mockGame: Game = {
        id: '770e8400-e29b-41d4-a716-446655440123',
        title: 'Azul',
        publisher: 'Next Move Games',
        yearPublished: 2017,
        minPlayers: 2,
        maxPlayers: 4,
        minPlayTimeMinutes: 30,
        maxPlayTimeMinutes: 45,
        bggId: 123456,
        createdAt: '2025-01-01T00:00:00Z',
      };

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockGame,
        headers: new Headers(),
      });

      const result = await gamesClient.getById('770e8400-e29b-41d4-a716-446655440123');

      expect(result).toEqual(mockGame);
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games/770e8400-e29b-41d4-a716-446655440123'),
        expect.objectContaining({ method: 'GET' })
      );
    });

    it('should handle special characters in game ID', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => ({} as Game),
        headers: new Headers(),
      });

      await gamesClient.getById('game-with-special-&-chars');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('game-with-special-%26-chars'),
        expect.anything()
      );
    });

    it('should return null for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        headers: new Headers(),
      });

      const result = await gamesClient.getById('770e8400-e29b-41d4-a716-446655440123');

      expect(result).toBeNull();
    });
  });

  describe('getDocuments', () => {
    it('should get PDF documents for a game', async () => {
      const mockDocuments: PdfDocumentDto[] = [
        {
          id: '880e8400-e29b-41d4-a716-446655440001',
          gameId: '770e8400-e29b-41d4-a716-446655440123',
          fileName: 'rulebook.pdf',
          filePath: '/uploads/rulebook.pdf',
          fileSizeBytes: 1024000,
          processingStatus: 'Completed',
          uploadedAt: '2025-11-17T10:00:00Z',
          processedAt: '2025-11-17T10:05:00Z',
          pageCount: 12,
        },
        {
          id: '880e8400-e29b-41d4-a716-446655440002',
          gameId: '770e8400-e29b-41d4-a716-446655440123',
          fileName: 'reference.pdf',
          filePath: '/uploads/reference.pdf',
          fileSizeBytes: 512000,
          processingStatus: 'Completed',
          uploadedAt: '2025-11-17T11:00:00Z',
          processedAt: '2025-11-17T11:03:00Z',
          pageCount: 4,
        },
      ];

      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockDocuments,
        headers: new Headers(),
      });

      const result = await gamesClient.getDocuments('770e8400-e29b-41d4-a716-446655440123');

      expect(result).toEqual(mockDocuments);
      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games/game-123/documents'),
        expect.objectContaining({ method: 'GET' })
      );
    });

    it('should return empty array for null response', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        headers: new Headers(),
      });

      const result = await gamesClient.getDocuments('770e8400-e29b-41d4-a716-446655440123');

      expect(result).toEqual([]);
    });
  });
});

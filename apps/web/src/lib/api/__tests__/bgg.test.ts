/**
 * BGG API Client Tests - Issue #4141
 *
 * Test coverage:
 * - Search by title
 * - Fetch by ID
 * - Error handling
 * - Mock data validation
 *
 * Target: >90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { searchBggGames, fetchBggGameById } from '../bgg';

describe('BGG API Client', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  describe('searchBggGames', () => {
    it('should return matching games for valid query', async () => {
      const promise = searchBggGames('Catan');

      // Advance timer for mock delay
      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results).toHaveLength(1);
      expect(results[0].name).toBe('Catan');
      expect(results[0].id).toBe(13);
      expect(results[0].yearPublished).toBe(1995);
    });

    it('should be case-insensitive', async () => {
      const promise = searchBggGames('catan');

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results).toHaveLength(1);
      expect(results[0].name).toBe('Catan');
    });

    it('should return multiple results for partial match', async () => {
      const promise = searchBggGames('ca'); // Matches "Catan" and "Carcassonne"

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results.length).toBeGreaterThanOrEqual(2);
      expect(results.some((r) => r.name === 'Catan')).toBe(true);
      expect(results.some((r) => r.name === 'Carcassonne')).toBe(true);
    });

    it('should return empty array for no matches', async () => {
      const promise = searchBggGames('NonExistentGame123');

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results).toEqual([]);
    });

    it('should limit results to 10 games', async () => {
      const promise = searchBggGames(''); // Matches all games

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results.length).toBeLessThanOrEqual(10);
    });

    it('should return search result with correct structure', async () => {
      const promise = searchBggGames('Pandemic');

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results[0]).toHaveProperty('id');
      expect(results[0]).toHaveProperty('name');
      expect(results[0]).toHaveProperty('yearPublished');
      expect(results[0]).toHaveProperty('thumbnail');
      expect(typeof results[0].id).toBe('number');
      expect(typeof results[0].name).toBe('string');
      expect(typeof results[0].yearPublished).toBe('number');
    });

    it('should handle special characters in query', async () => {
      const promise = searchBggGames('7 Wonders');

      await vi.advanceTimersByTimeAsync(500);

      const results = await promise;

      expect(results).toHaveLength(1);
      expect(results[0].name).toBe('7 Wonders');
    });
  });

  describe('fetchBggGameById', () => {
    it('should return game details for valid ID', async () => {
      const promise = fetchBggGameById(13); // Catan

      await vi.advanceTimersByTimeAsync(300);

      const game = await promise;

      expect(game.id).toBe(13);
      expect(game.name).toBe('Catan');
      expect(game.minPlayers).toBe(3);
      expect(game.maxPlayers).toBe(4);
      expect(game.playingTime).toBe(90);
      expect(game.minAge).toBe(10);
      expect(game.rating).toBeGreaterThan(0);
    });

    it('should return complete game details structure', async () => {
      const promise = fetchBggGameById(822); // Carcassonne

      await vi.advanceTimersByTimeAsync(300);

      const game = await promise;

      expect(game).toHaveProperty('id');
      expect(game).toHaveProperty('name');
      expect(game).toHaveProperty('yearPublished');
      expect(game).toHaveProperty('minPlayers');
      expect(game).toHaveProperty('maxPlayers');
      expect(game).toHaveProperty('playingTime');
      expect(game).toHaveProperty('minAge');
      expect(game).toHaveProperty('rating');
      expect(game).toHaveProperty('thumbnail');
    });

    it('should include optional fields when available', async () => {
      const promise = fetchBggGameById(9209); // Ticket to Ride

      await vi.advanceTimersByTimeAsync(300);

      const game = await promise;

      expect(game.description).toBeDefined();
      expect(game.categories).toBeDefined();
      expect(game.mechanics).toBeDefined();
      expect(Array.isArray(game.categories)).toBe(true);
      expect(Array.isArray(game.mechanics)).toBe(true);
    });

    it('should throw error for invalid ID', async () => {
      const promise = fetchBggGameById(99999);

      await vi.advanceTimersByTimeAsync(300);

      await expect(promise).rejects.toThrow('BGG game not found: ID 99999');
    });

    it('should return different games for different IDs', async () => {
      const promise1 = fetchBggGameById(13);
      await vi.advanceTimersByTimeAsync(300);
      const game1 = await promise1;

      const promise2 = fetchBggGameById(822);
      await vi.advanceTimersByTimeAsync(300);
      const game2 = await promise2;

      expect(game1.id).not.toBe(game2.id);
      expect(game1.name).not.toBe(game2.name);
    });

    it('should return game with valid player counts', async () => {
      const promise = fetchBggGameById(30549); // Pandemic

      await vi.advanceTimersByTimeAsync(300);

      const game = await promise;

      expect(game.minPlayers).toBeGreaterThan(0);
      expect(game.maxPlayers).toBeGreaterThanOrEqual(game.minPlayers);
      expect(game.playingTime).toBeGreaterThan(0);
      expect(game.minAge).toBeGreaterThan(0);
    });

    it('should return game with thumbnail URL', async () => {
      const promise = fetchBggGameById(68448); // 7 Wonders

      await vi.advanceTimersByTimeAsync(300);

      const game = await promise;

      expect(game.thumbnail).toBeTruthy();
      if (game.thumbnail) {
        expect(game.thumbnail).toMatch(/^https?:\/\//);
      }
    });
  });

  describe('Integration', () => {
    it('should allow search followed by fetch', async () => {
      // Search for game
      const searchPromise = searchBggGames('Ticket');
      await vi.advanceTimersByTimeAsync(500);
      const searchResults = await searchPromise;

      expect(searchResults.length).toBeGreaterThan(0);

      // Fetch details for first result
      const fetchPromise = fetchBggGameById(searchResults[0].id);
      await vi.advanceTimersByTimeAsync(300);
      const gameDetails = await fetchPromise;

      expect(gameDetails.id).toBe(searchResults[0].id);
      expect(gameDetails.name).toBe(searchResults[0].name);
    });

    it('should handle multiple concurrent searches', async () => {
      const promise1 = searchBggGames('Catan');
      const promise2 = searchBggGames('Pandemic');
      const promise3 = searchBggGames('Ticket');

      await vi.advanceTimersByTimeAsync(500);

      const [results1, results2, results3] = await Promise.all([
        promise1,
        promise2,
        promise3,
      ]);

      expect(results1[0].name).toBe('Catan');
      expect(results2[0].name).toBe('Pandemic');
      expect(results3[0].name).toBe('Ticket to Ride');
    });

    it('should handle multiple concurrent fetches', async () => {
      const promise1 = fetchBggGameById(13);
      const promise2 = fetchBggGameById(822);

      await vi.advanceTimersByTimeAsync(300);

      const [game1, game2] = await Promise.all([promise1, promise2]);

      expect(game1.name).toBe('Catan');
      expect(game2.name).toBe('Carcassonne');
    });
  });
});

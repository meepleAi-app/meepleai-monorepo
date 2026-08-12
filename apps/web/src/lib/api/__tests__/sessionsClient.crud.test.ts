/**
 * SessionsClient CRUD and Lifecycle Tests
 *
 * Tests for: getById, getHistory, pause, resume, end, complete, abandon
 *
 * NB: `start` (POST /sessions) was removed in #2587 (dead funnel). The orphaned
 * start tests were deleted in #2715; do not re-add them.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createSessionsClient } from '../clients/sessionsClient';
import type { HttpClient } from '../core/httpClient';

describe('sessionsClient CRUD and lifecycle', () => {
  const mockHttpClient: HttpClient = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  };

  const client = createSessionsClient({ httpClient: mockHttpClient });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getById', () => {
    it('should fetch session by ID', async () => {
      const mockSession = {
        id: 'session-123',
        gameId: 'game-1',
        status: 'active',
        players: [],
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockSession);

      const result = await client.getById('session-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/session-123',
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should return null for non-existent session', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getById('non-existent');

      expect(result).toBeNull();
    });
  });

  describe('getHistory', () => {
    // Issue #2848 (#Z): /sessions/history returns a bare List<GameSessionDto>;
    // getHistory validates the array and wraps it into a PaginatedSessionsResponse.
    it('should wrap the empty array into a paginated response', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce([]);

      const result = await client.getHistory();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/history?',
        expect.any(Object)
      );
      expect(result).toEqual({ sessions: [], total: 0, page: 1, pageSize: 20 });
    });

    it('should wrap the returned array and apply filters', async () => {
      const sessions = [{ id: 'session-1', gameId: 'game-1' }];
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(sessions);

      const result = await client.getHistory({
        gameId: 'game-1',
        startDate: '2024-01-01',
        endDate: '2024-12-31',
        limit: 10,
        offset: 0,
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('gameId=game-1'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('startDate=2024-01-01'),
        expect.any(Object)
      );
      expect(result).toEqual({ sessions, total: 1, page: 1, pageSize: 10 });
    });

    it('should return empty response on null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getHistory({ limit: 10 });

      expect(result.sessions).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('lifecycle operations', () => {
    it('should pause a session', async () => {
      const mockSession = { id: 'session-1', status: 'paused' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.pause('session-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/pause',
        {},
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should resume a session', async () => {
      const mockSession = { id: 'session-1', status: 'active' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.resume('session-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/resume',
        {},
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should end a session without winner', async () => {
      const mockSession = { id: 'session-1', status: 'ended' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.end('session-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/end',
        { winnerName: undefined },
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should end a session with winner', async () => {
      const mockSession = { id: 'session-1', status: 'ended', winner: 'Player 1' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.end('session-1', 'Player 1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/end',
        { winnerName: 'Player 1' },
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    // #3662: rimossi i test di `complete` e `abandon`. I metodi del client non esistono
    // piu' perche' gli endpoint che chiamavano sono stati eliminati (nessun consumatore).
    // La conclusione di una sessione resta coperta dai test di `end`, qui sopra.
  });
});

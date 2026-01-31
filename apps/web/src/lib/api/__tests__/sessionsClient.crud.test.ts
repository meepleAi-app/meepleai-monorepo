/**
 * SessionsClient CRUD and Lifecycle Tests
 *
 * Tests for: getById, getHistory, start, pause, resume, end, complete, abandon
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
    it('should fetch session history without filters', async () => {
      const mockResponse = {
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20,
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockResponse);

      const result = await client.getHistory();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/history?',
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should fetch session history with filters', async () => {
      const mockResponse = {
        sessions: [{ id: 'session-1', gameId: 'game-1' }],
        total: 1,
        page: 1,
        pageSize: 10,
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockResponse);

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
      expect(result).toEqual(mockResponse);
    });

    it('should return empty response on null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getHistory({ limit: 10 });

      expect(result.sessions).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('start', () => {
    it('should start a new session', async () => {
      const mockSession = {
        id: 'new-session',
        gameId: 'game-1',
        status: 'active',
        players: [{ playerName: 'Player 1', playerOrder: 1 }],
      };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.start({
        gameId: 'game-1',
        players: [{ playerName: 'Player 1', playerOrder: 1 }],
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions',
        {
          gameId: 'game-1',
          players: [{ playerName: 'Player 1', playerOrder: 1 }],
        },
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should start session with notes', async () => {
      const mockSession = { id: 'session-with-notes', notes: 'Test notes' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      await client.start({
        gameId: 'game-1',
        players: [{ playerName: 'Player 1', playerOrder: 1 }],
        notes: 'Test notes',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions',
        expect.objectContaining({ notes: 'Test notes' }),
        expect.any(Object)
      );
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

    it('should complete a session', async () => {
      const mockSession = { id: 'session-1', status: 'completed' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.complete('session-1', { winnerName: 'Winner' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/complete',
        { winnerName: 'Winner' },
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should complete a session without winner', async () => {
      const mockSession = { id: 'session-1', status: 'completed' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.complete('session-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/complete',
        {},
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });

    it('should abandon a session', async () => {
      const mockSession = { id: 'session-1', status: 'abandoned' };

      vi.mocked(mockHttpClient.post).mockResolvedValueOnce(mockSession);

      const result = await client.abandon('session-1');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/sessions/session-1/abandon',
        {},
        expect.any(Object)
      );
      expect(result).toEqual(mockSession);
    });
  });
});

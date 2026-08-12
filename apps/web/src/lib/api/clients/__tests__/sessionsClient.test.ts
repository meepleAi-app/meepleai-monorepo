/**
 * Sessions Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Game sessions API client tests
 * - Active sessions: getActive
 * - Session history: getHistory
 * - Session CRUD: getById, start
 * - Session lifecycle: pause, resume, end
 * - Game state: initializeState, getState, updateState, createSnapshot, getSnapshots, restoreSnapshot
 * - Quota: getQuota
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createSessionsClient } from '../sessionsClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockSession = {
  id: 'session-123',
  gameId: 'game-456',
  gameName: 'Catan',
  status: 'Active',
  startedAt: '2024-01-15T10:00:00Z',
  endedAt: null,
  players: [
    { playerName: 'Alice', playerOrder: 1, color: 'red' },
    { playerName: 'Bob', playerOrder: 2, color: 'blue' },
  ],
  winnerName: null,
  notes: 'First game of the day',
};

describe('SessionsClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getActive', () => {
    it('should fetch active sessions with default pagination', async () => {
      const mockResponse = {
        sessions: [mockSession],
        total: 1,
        page: 1,
        pageSize: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getActive();

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/active?limit=20&offset=0',
        expect.any(Object)
      );
    });

    it('should apply custom pagination', async () => {
      const mockResponse = { sessions: [], total: 0, page: 3, pageSize: 10 };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getActive(10, 20);

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/active?limit=10&offset=20',
        expect.any(Object)
      );
    });

    it('should return empty response when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getActive();

      expect(result.sessions).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('getHistory', () => {
    // Issue #2848 (#Z): GET /api/v1/sessions/history returns a BARE array
    // (`.Produces<List<GameSessionDto>>`), unlike /sessions/active which returns a
    // paginated envelope. getHistory validates the array and wraps it into the
    // PaginatedSessionsResponse its callers expect. A schema-valid fixture is used
    // so the array-schema assertion below is meaningful.
    const validHistorySession = {
      id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
      gameId: '6ba7b810-9dad-41d1-80b4-00c04fd430c8',
      status: 'Completed',
      startedAt: '2024-01-15T10:00:00Z',
      completedAt: '2024-01-15T12:00:00Z',
      playerCount: 2,
      players: [],
      winnerName: null,
      notes: null,
      durationMinutes: 120,
    };

    it('wraps the bare array the BE returns into a paginated response', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([validHistorySession]);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getHistory();

      expect(result).toEqual({
        sessions: [validHistorySession],
        total: 1,
        page: 1,
        pageSize: 20,
      });
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/history?',
        expect.any(Object)
      );
    });

    it('validates against a schema that accepts the bare array (root cause of #2848)', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([validHistorySession]);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      await client.getHistory();

      const schemaArg = vi.mocked(mockHttpClient.get).mock.calls[0]?.[1] as
        { safeParse: (x: unknown) => { success: boolean } } | undefined;
      expect(schemaArg?.safeParse([validHistorySession]).success).toBe(true);
    });

    it('should apply all filters', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      await client.getHistory({
        gameId: 'game-123',
        startDate: '2024-01-01',
        endDate: '2024-12-31',
        limit: 10,
        offset: 5,
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('gameId=game-123'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('startDate=2024-01-01'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('endDate=2024-12-31'),
        expect.any(Object)
      );
    });

    it('should return empty response when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getHistory({ limit: 10, offset: 5 });

      expect(result.sessions).toEqual([]);
      expect(result.page).toBe(1);
    });
  });

  describe('getById', () => {
    it('should fetch single session by ID', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockSession);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getById('session-123');

      expect(result).toEqual(mockSession);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/session-123',
        expect.any(Object)
      );
    });

    it('should return null for non-existent session', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getById('nonexistent');

      expect(result).toBeNull();
    });

    it('should encode special characters in session ID', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockSession);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      await client.getById('session/special');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/session%2Fspecial',
        expect.any(Object)
      );
    });
  });

  describe('Session Lifecycle', () => {
    describe('pause', () => {
      it('should pause an active session', async () => {
        const pausedSession = { ...mockSession, status: 'Paused' };
        vi.mocked(mockHttpClient.post).mockResolvedValue(pausedSession);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.pause('session-123');

        expect(result.status).toBe('Paused');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/pause',
          {},
          expect.any(Object)
        );
      });
    });

    describe('resume', () => {
      it('should resume a paused session', async () => {
        const resumedSession = { ...mockSession, status: 'Active' };
        vi.mocked(mockHttpClient.post).mockResolvedValue(resumedSession);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.resume('session-123');

        expect(result.status).toBe('Active');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/resume',
          {},
          expect.any(Object)
        );
      });
    });

    describe('end', () => {
      it('should end a session without winner', async () => {
        const endedSession = { ...mockSession, status: 'Ended' };
        vi.mocked(mockHttpClient.post).mockResolvedValue(endedSession);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.end('session-123');

        expect(result.status).toBe('Ended');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/end',
          { winnerName: undefined },
          expect.any(Object)
        );
      });

      it('should end a session with winner', async () => {
        const endedSession = { ...mockSession, status: 'Ended', winnerName: 'Alice' };
        vi.mocked(mockHttpClient.post).mockResolvedValue(endedSession);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.end('session-123', 'Alice');

        expect(result.winnerName).toBe('Alice');
        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/end',
          { winnerName: 'Alice' },
          expect.any(Object)
        );
      });
    });

    // #3662: rimossi i blocchi `complete` e `abandon` — i metodi del client non esistono
    // piu', perche' gli endpoint che chiamavano sono stati eliminati (nessun consumatore).
    // La conclusione di una sessione resta coperta dai test di `end`, qui sopra.
  });

  describe('Game State Management', () => {
    describe('initializeState', () => {
      it('should initialize game state with template', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ success: true });

        const client = createSessionsClient({ httpClient: mockHttpClient });
        await client.initializeState('session-123', 'template-456');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/state/initialize',
          { templateId: 'template-456' }
        );
      });
    });

    describe('getState', () => {
      it('should get current game state', async () => {
        const mockState = { turnNumber: 5, currentPlayer: 'Alice' };
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockState);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.getState('session-123');

        expect(result).toEqual(mockState);
        expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/sessions/session-123/state');
      });
    });

    describe('updateState', () => {
      it('should update game state', async () => {
        vi.mocked(mockHttpClient.patch).mockResolvedValue({ success: true });

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const stateJson = JSON.stringify({ turnNumber: 6 });
        await client.updateState('session-123', stateJson);

        expect(mockHttpClient.patch).toHaveBeenCalledWith('/api/v1/sessions/session-123/state', {
          stateJson,
        });
      });
    });

    describe('createSnapshot', () => {
      it('should create state snapshot with turn number', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'snapshot-1' });

        const client = createSessionsClient({ httpClient: mockHttpClient });
        await client.createSnapshot('session-123', 'End of round 5', 5);

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/state/snapshots',
          { description: 'End of round 5', turnNumber: 5 }
        );
      });

      it('should create snapshot without turn number', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ id: 'snapshot-2' });

        const client = createSessionsClient({ httpClient: mockHttpClient });
        await client.createSnapshot('session-123', 'Quick save');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/state/snapshots',
          { description: 'Quick save', turnNumber: undefined }
        );
      });
    });

    describe('getSnapshots', () => {
      it('should get all snapshots', async () => {
        const mockSnapshots = [
          { id: 'snap-1', description: 'First', turnNumber: 1 },
          { id: 'snap-2', description: 'Second', turnNumber: 2 },
        ];
        vi.mocked(mockHttpClient.get).mockResolvedValue(mockSnapshots);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.getSnapshots('session-123');

        expect(result).toEqual(mockSnapshots);
      });

      it('should return empty array when no snapshots', async () => {
        vi.mocked(mockHttpClient.get).mockResolvedValue(null);

        const client = createSessionsClient({ httpClient: mockHttpClient });
        const result = await client.getSnapshots('session-123');

        expect(result).toEqual([]);
      });
    });

    describe('restoreSnapshot', () => {
      it('should restore state from snapshot', async () => {
        vi.mocked(mockHttpClient.post).mockResolvedValue({ success: true });

        const client = createSessionsClient({ httpClient: mockHttpClient });
        await client.restoreSnapshot('session-123', 'snapshot-456');

        expect(mockHttpClient.post).toHaveBeenCalledWith(
          '/api/v1/sessions/session-123/state/restore/snapshot-456'
        );
      });
    });
  });

  describe('getQuota', () => {
    it('should get session quota for user', async () => {
      const mockQuota = {
        currentSessions: 2,
        maxSessions: 5,
        remainingSlots: 3,
        canCreateNew: true,
        isUnlimited: false,
        userTier: 'premium',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockQuota);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getQuota('user-123');

      expect(result).toEqual(mockQuota);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/users/user-123/session-quota',
        expect.any(Object)
      );
    });

    it('should return default quota when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createSessionsClient({ httpClient: mockHttpClient });
      const result = await client.getQuota('user-123');

      expect(result.currentSessions).toBe(0);
      expect(result.maxSessions).toBe(1);
      expect(result.canCreateNew).toBe(true);
      expect(result.userTier).toBe('free');
    });
  });
});

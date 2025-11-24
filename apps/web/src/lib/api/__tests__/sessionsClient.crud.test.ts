/**
 * Sessions Client Tests (FE-IMP-005)
 *
 * Tests for GameManagement bounded context (Sessions) client.
 * Covers: Session lifecycle, active sessions, history, CRUD operations
 */

import { createSessionsClient } from '../clients/sessionsClient';
import { HttpClient } from '../core/httpClient';
import type { GameSessionDto, PaginatedSessionsResponse } from '../schemas';
import { globalRequestCache } from '../core/requestCache';

          startedAt: '2025-11-17T12:00:00Z',
          completedAt: null,
          status: 'active',
          playerCount: 3,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: 'First game',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.start({
          gameId: '660e8400-e29b-41d4-a716-446655440003',
          players: [
            { playerName: 'Alice', playerOrder: 1, color: 'red' },
            { playerName: 'Bob', playerOrder: 2, color: 'blue' },
            { playerName: 'Charlie', playerOrder: 3, color: null },
          ],
          notes: 'First game',
        });

        expect(result).toEqual(mockSession);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({
              gameId: '660e8400-e29b-41d4-a716-446655440003',
              players: [
                { playerName: 'Alice', playerOrder: 1, color: 'red' },
                { playerName: 'Bob', playerOrder: 2, color: 'blue' },
                { playerName: 'Charlie', playerOrder: 3, color: null },
              ],
              notes: 'First game',
            }),
          })
        );
      });

      it('should start session without notes', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440004',
          gameId: '660e8400-e29b-41d4-a716-446655440003',
          startedAt: '2025-11-17T12:00:00Z',
          completedAt: null,
          status: 'active',
          playerCount: 2,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.start({
          gameId: '660e8400-e29b-41d4-a716-446655440003',
          players: [
            { playerName: 'Alice', playerOrder: 1 },
            { playerName: 'Bob', playerOrder: 2 },
          ],
        });

        expect(result).toEqual(mockSession);
      });
    });
  });

  describe('Session Lifecycle', () => {
    describe('pause', () => {
      it('should pause an active session', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: null,
          status: 'paused',
          playerCount: 4,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.pause('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(result.status).toBe('paused');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/pause'),
          expect.objectContaining({ method: 'POST' })
        );
      });
    });

    describe('resume', () => {
      it('should resume a paused session', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: null,
          status: 'active',
          playerCount: 4,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.resume('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(result.status).toBe('active');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/resume'),
          expect.objectContaining({ method: 'POST' })
        );
      });
    });

    describe('end', () => {
      it('should end a session without winner', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: null,
          status: 'ended',
          playerCount: 4,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.end('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(result.status).toBe('ended');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/end'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ winnerName: undefined }),
          })
        );
      });

      it('should end a session with winner', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: null,
          status: 'ended',
          playerCount: 4,
          players: [],
          winnerName: 'Alice',
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.end('550e8400-e29b-41d4-a716-446655440003', 'Alice');

        expect(result).toEqual(mockSession);
        expect(result.winnerName).toBe('Alice');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/end'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ winnerName: 'Alice' }),
          })
        );
      });
    });

    describe('complete', () => {
      it('should complete a session without winner', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: '2025-11-17T11:00:00Z',
          status: 'completed',
          playerCount: 4,
          players: [],
          winnerName: null,
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.complete('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(result.status).toBe('completed');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/complete'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({}),
          })
        );
      });

      it('should complete a session with winner', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: '2025-11-17T11:00:00Z',
          status: 'completed',
          playerCount: 4,
          players: [],
          winnerName: 'Bob',
          durationMinutes: 0,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.complete('550e8400-e29b-41d4-a716-446655440003', { winnerName: 'Bob' });

        expect(result).toEqual(mockSession);
        expect(result.winnerName).toBe('Bob');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/complete'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ winnerName: 'Bob' }),
          })
        );
      });
    });

    describe('abandon', () => {
      it('should abandon a session', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440003',
          gameId: '660e8400-e29b-41d4-a716-446655440002',
          startedAt: '2025-11-17T10:00:00Z',
          completedAt: null,
          status: 'abandoned',
          playerCount: 4,
          players: [],
          durationMinutes: 0,
          winnerName: null,
          notes: null,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.abandon('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(result.status).toBe('abandoned');
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003/abandon'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({}),
          })
        );
      });
    });
  });
});

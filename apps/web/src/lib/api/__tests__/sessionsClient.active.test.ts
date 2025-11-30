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

describe('SessionsClient', () => {
  let mockFetch: Mock;
  let httpClient: HttpClient;
  let sessionsClient: ReturnType<typeof createSessionsClient>;

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();

    mockFetch = vi.fn();
    httpClient = new HttpClient({ fetchImpl: mockFetch });
    sessionsClient = createSessionsClient({ httpClient });
  });

  describe('Active Sessions', () => {
    describe('getActive', () => {
      it('should get active sessions with default pagination', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [
            {
              id: '550e8400-e29b-41d4-a716-446655440001',
              gameId: '660e8400-e29b-41d4-a716-446655440001',
              startedAt: '2025-11-17T10:00:00Z',
              completedAt: null,
              status: 'active',
              playerCount: 4,
          players: [],
              winnerName: null,
          durationMinutes: 0,
              notes: 'Teaching game',
            },
          ],
          total: 1,
          page: 1,
          pageSize: 20,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getActive();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/active?limit=20&offset=0'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should get active sessions with custom pagination', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [],
          total: 0,
          page: 2,
          pageSize: 10,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getActive(10, 10);

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('limit=10&offset=10'),
          expect.anything()
        );
      });

      it('should return empty response for null', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await sessionsClient.getActive();

        expect(result).toEqual({
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 20,
        });
      });
    });
  });

  describe('Session History', () => {
    describe('getHistory', () => {
      it('should get session history without filters', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [
            {
              id: '550e8400-e29b-41d4-a716-446655440002',
              gameId: '660e8400-e29b-41d4-a716-446655440002',
              startedAt: '2025-11-16T10:00:00Z',
              completedAt: '2025-11-16T11:30:00Z',
              status: 'completed',
              playerCount: 3,
          players: [],
              winnerName: 'Alice',
          durationMinutes: 0,
              notes: null,
            },
          ],
          total: 1,
          page: 1,
          pageSize: 20,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/history?'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should filter history by game ID', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 20,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory({ gameId: '660e8400-e29b-41d4-a716-446655440001' });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('gameId=660e8400-e29b-41d4-a716-446655440001'),
          expect.anything()
        );
      });

      it('should filter history by date range', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 20,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory({
          startDate: '2025-11-01T00:00:00Z',
          endDate: '2025-11-17T23:59:59Z',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('startDate=2025-11-01T00%3A00%3A00Z&endDate=2025-11-17T23%3A59%3A59Z'),
          expect.anything()
        );
      });

      it('should apply custom pagination', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [],
          total: 0,
          page: 3,
          pageSize: 5,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory({
          limit: 5,
          offset: 10,
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('limit=5&offset=10'),
          expect.anything()
        );
      });

      it('should combine multiple filters', async () => {
        const mockResponse: PaginatedSessionsResponse = {
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 10,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory({
          gameId: '660e8400-e29b-41d4-a716-446655440001',
          startDate: '2025-11-01T00:00:00Z',
          endDate: '2025-11-17T23:59:59Z',
          limit: 10,
          offset: 0,
        });

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringMatching(/gameId=660e8400-e29b-41d4-a716-446655440001.*startDate.*endDate.*limit=10/),
          expect.anything()
        );
      });

      it('should return empty response for null', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await sessionsClient.getHistory({ limit: 10, offset: 5 });

        expect(result).toEqual({
          sessions: [],
          total: 0,
          page: 1,
          pageSize: 10,
        });
      });
    });
  });

  describe('Session CRUD', () => {
    describe('getById', () => {
      it('should get a single session by ID', async () => {
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
          notes: 'Fun game',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockSession,
          headers: new Headers(),
        });

        const result = await sessionsClient.getById('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toEqual(mockSession);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/sessions/550e8400-e29b-41d4-a716-446655440003'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should return null for 401 responses', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await sessionsClient.getById('550e8400-e29b-41d4-a716-446655440003');

        expect(result).toBeNull();
      });
    });

    describe('start', () => {
      it('should start a new session', async () => {
        const mockSession: GameSessionDto = {
          id: '550e8400-e29b-41d4-a716-446655440004',
          gameId: '660e8400-e29b-41d4-a716-446655440003',

/**
 * SessionsClient Active Sessions Tests
 *
 * Tests for active session operations: getActive
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createSessionsClient } from '../clients/sessionsClient';
import type { HttpClient } from '../core/httpClient';

describe('sessionsClient active sessions', () => {
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

  describe('getActive', () => {
    it('should fetch active sessions with default pagination', async () => {
      const mockResponse = {
        sessions: [
          { id: 'session-1', gameId: 'game-1', status: 'active' },
          { id: 'session-2', gameId: 'game-2', status: 'active' },
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockResponse);

      const result = await client.getActive();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/active?limit=20&offset=0',
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should fetch active sessions with custom pagination', async () => {
      const mockResponse = {
        sessions: [],
        total: 50,
        page: 3,
        pageSize: 10,
      };

      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(mockResponse);

      const result = await client.getActive(10, 20);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/sessions/active?limit=10&offset=20',
        expect.any(Object)
      );
      expect(result).toEqual(mockResponse);
    });

    it('should return empty response on null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getActive();

      expect(result).toEqual({
        sessions: [],
        total: 0,
        page: 1,
        pageSize: 20,
      });
    });

    it('should calculate page correctly on null response', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValueOnce(null);

      const result = await client.getActive(10, 30);

      expect(result).toEqual({
        sessions: [],
        total: 0,
        page: 4, // offset 30 / limit 10 + 1 = 4
        pageSize: 10,
      });
    });
  });
});

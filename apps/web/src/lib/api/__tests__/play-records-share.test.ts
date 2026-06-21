import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi } from '../play-records.api';

describe('playRecordsApi — share token methods (#2437-2)', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
  afterEach(() => vi.unstubAllGlobals());

  // ---- generateShareToken ----

  describe('generateShareToken', () => {
    it('POSTs to /{id}/share with credentials:include and returns the parsed response', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      const payload = { shareToken: 'tok123', shareUrl: 'https://app.meeple.ai/shared/tok123' };
      fetchMock.mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => payload,
      });

      const result = await playRecordsApi.generateShareToken('rec-id-1');

      expect(fetchMock).toHaveBeenCalledOnce();
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toMatch(/\/rec-id-1\/share$/);
      expect(init.method).toBe('POST');
      expect(init.credentials).toBe('include');
      expect(result).toEqual(payload);
    });

    it('throws when the response is not ok', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ message: 'Forbidden' }),
      });

      await expect(playRecordsApi.generateShareToken('rec-id-1')).rejects.toThrow('Forbidden');
    });

    it('falls back to generic message when error body has no message', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({}),
      });

      await expect(playRecordsApi.generateShareToken('rec-id-1')).rejects.toThrow(
        'Failed to generate share link'
      );
    });
  });

  // ---- revokeShareToken ----

  describe('revokeShareToken', () => {
    it('DELETEs /{id}/share with credentials:include and resolves void on 204', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: true,
        status: 204,
        json: async () => null,
      });

      await expect(playRecordsApi.revokeShareToken('rec-id-2')).resolves.toBeUndefined();

      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toMatch(/\/rec-id-2\/share$/);
      expect(init.method).toBe('DELETE');
      expect(init.credentials).toBe('include');
    });

    it('throws when the response is not ok', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ error: 'Not allowed' }),
      });

      await expect(playRecordsApi.revokeShareToken('rec-id-2')).rejects.toThrow('Not allowed');
    });
  });

  // ---- getSharedRecord ----

  describe('getSharedRecord', () => {
    it('GETs /shared/{token} with credentials:include and returns the parsed DTO', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      const dto = {
        id: '550e8400-e29b-41d4-a716-446655440000',
        gameName: 'Terraforming Mars',
        sessionDate: '2026-06-21',
        status: 'Completed',
        players: [],
        visibility: 'Private',
        createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
        gameId: null,
        duration: null,
        scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
        startTime: null,
        endTime: null,
        notes: null,
        location: null,
        createdAt: '2026-06-21T10:00:00Z',
        updatedAt: '2026-06-21T10:00:00Z',
      };
      fetchMock.mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => dto,
      });

      const result = await playRecordsApi.getSharedRecord('mytoken');

      expect(fetchMock).toHaveBeenCalledOnce();
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toMatch(/\/shared\/mytoken$/);
      expect(init.credentials).toBe('include');
      expect(result).toEqual(dto);
    });

    it('throws "Shared play record not found" on 404', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 404,
        json: async () => ({ message: 'Not found' }),
      });

      await expect(playRecordsApi.getSharedRecord('badtoken')).rejects.toThrow(
        'Shared play record not found'
      );
    });

    it('throws error message from body on non-404 failure', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ message: 'Server exploded' }),
      });

      await expect(playRecordsApi.getSharedRecord('tok')).rejects.toThrow('Server exploded');
    });
  });
});

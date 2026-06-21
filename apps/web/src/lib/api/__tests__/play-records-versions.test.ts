import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi } from '../play-records.api';

const sampleVersions = [
  {
    versionNumber: 2,
    sessionDate: '2026-06-20',
    notes: 'Updated notes',
    location: null,
    createdAt: '2026-06-20T18:30:00Z',
    createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  },
  {
    versionNumber: 1,
    sessionDate: '2026-06-19',
    notes: null,
    location: 'Home',
    createdAt: '2026-06-19T10:00:00Z',
    createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  },
];

describe('playRecordsApi — version history methods (#2437-3)', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
  afterEach(() => vi.unstubAllGlobals());

  // ---- getVersions ----

  describe('getVersions', () => {
    it('GETs /{id}/versions with credentials:include and returns the array', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => sampleVersions,
      });

      const result = await playRecordsApi.getVersions('rec-abc');

      expect(fetchMock).toHaveBeenCalledOnce();
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toMatch(/\/rec-abc\/versions$/);
      expect(init.credentials).toBe('include');
      expect(result).toEqual(sampleVersions);
    });

    it('throws the server error message when the response is not ok', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ message: 'Forbidden' }),
      });

      await expect(playRecordsApi.getVersions('rec-abc')).rejects.toThrow('Forbidden');
    });

    it('falls back to generic message when error body has no message', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({}),
      });

      await expect(playRecordsApi.getVersions('rec-abc')).rejects.toThrow(
        'Failed to load versions'
      );
    });

    it('falls back to generic message when error body is unparseable', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('bad json');
        },
      });

      await expect(playRecordsApi.getVersions('rec-abc')).rejects.toThrow(
        'Failed to load versions'
      );
    });
  });

  // ---- restoreVersion ----

  describe('restoreVersion', () => {
    it('POSTs to /{id}/restore/{versionNumber} with credentials:include and resolves void', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: true,
        status: 204,
        json: async () => null,
      });

      await expect(playRecordsApi.restoreVersion('rec-xyz', 2)).resolves.toBeUndefined();

      expect(fetchMock).toHaveBeenCalledOnce();
      const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
      expect(url).toMatch(/\/rec-xyz\/restore\/2$/);
      expect(init.method).toBe('POST');
      expect(init.credentials).toBe('include');
    });

    it('throws the server error message when the response is not ok', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 403,
        json: async () => ({ error: 'Not allowed' }),
      });

      await expect(playRecordsApi.restoreVersion('rec-xyz', 1)).rejects.toThrow('Not allowed');
    });

    it('falls back to generic message when error body is unparseable', async () => {
      const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
      fetchMock.mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('bad json');
        },
      });

      await expect(playRecordsApi.restoreVersion('rec-xyz', 1)).rejects.toThrow(
        'Failed to restore version'
      );
    });
  });
});

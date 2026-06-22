import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CatalogSeedApiError,
  approveSeed,
  bulkEnqueueSeeds,
  enqueueSeed,
  getSeedById,
  listSeeds,
  rejectSeed,
  wikidataSearch,
} from '../catalog-seed-api';

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.restoreAllMocks();
  fetchMock.mockReset();
});

describe('enqueueSeed', () => {
  it('POSTs JSON body to /api/v1/admin/catalog/seeds', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ id: 'd1', bggId: 13, status: 'Pending', isDuplicate: false }),
    });
    const result = await enqueueSeed({ bggId: 13 });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/catalog/seeds',
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: JSON.stringify({ bggId: 13 }),
      })
    );
    expect(result.id).toBe('d1');
    expect(result.isDuplicate).toBe(false);
  });

  it('throws CatalogSeedApiError on non-ok', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
      json: async () => ({ title: 'Catalog seed pipeline disabled' }),
    });
    await expect(enqueueSeed({ bggId: 13 })).rejects.toMatchObject({
      status: 503,
      message: expect.stringContaining('disabled'),
    });
  });
});

describe('bulkEnqueueSeeds', () => {
  it('POSTs the BGG id list to /bulk', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ total: 3, enqueued: 3, duplicates: 0, newDraftIds: ['a', 'b', 'c'] }),
    });
    const result = await bulkEnqueueSeeds({ bggIds: [13, 30549, 167791] });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/catalog/seeds/bulk',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ bggIds: [13, 30549, 167791] }),
      })
    );
    expect(result.enqueued).toBe(3);
  });

  it('throws on failure', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      json: async () => ({ error: 'Too many IDs' }),
    });
    await expect(bulkEnqueueSeeds({ bggIds: [] })).rejects.toBeInstanceOf(CatalogSeedApiError);
  });
});

describe('listSeeds', () => {
  it('builds query string from params', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ total: 0, skip: 25, take: 50, items: [] }),
    });
    await listSeeds({ status: 'Fetched', skip: 25, take: 50 });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/seeds\?.*status=Fetched.*skip=25.*take=50/),
      expect.objectContaining({ method: 'GET' })
    );
  });

  it('omits empty query string when no params provided', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ total: 0, skip: 0, take: 50, items: [] }),
    });
    await listSeeds();
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/admin/catalog/seeds', expect.any(Object));
  });
});

describe('getSeedById', () => {
  it('returns null on 404', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 404 });
    const result = await getSeedById('missing');
    expect(result).toBeNull();
  });

  it('returns the DTO on 200', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ id: 'd1', bggId: 13, status: 'Fetched', errorMessage: null }),
    });
    const result = await getSeedById('d1');
    expect(result?.id).toBe('d1');
  });

  it('throws on 5xx', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Server Error',
      json: async () => ({}),
    });
    await expect(getSeedById('d1')).rejects.toBeInstanceOf(CatalogSeedApiError);
  });
});

describe('approveSeed', () => {
  it('POSTs /{id}/approve and returns the resulting shared game id', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ draftId: 'd1', resultingSharedGameId: 'sg-1' }),
    });
    const result = await approveSeed('d1');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/catalog/seeds/d1/approve',
      expect.objectContaining({ method: 'POST' })
    );
    expect(result.resultingSharedGameId).toBe('sg-1');
  });
});

describe('rejectSeed', () => {
  it('POSTs reason to /{id}/reject', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({}) });
    await rejectSeed('d1', 'duplicate of #42');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/catalog/seeds/d1/reject',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ reason: 'duplicate of #42' }),
      })
    );
  });

  it('throws on non-ok', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      json: async () => ({}),
    });
    await expect(rejectSeed('d1', 'x')).rejects.toBeInstanceOf(CatalogSeedApiError);
  });
});

describe('wikidataSearch', () => {
  it('POSTs searchTerm to /wikidata-search', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ fields: {}, errorMessage: null }),
    });
    await wikidataSearch('catan');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/admin/catalog/seeds/wikidata-search',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ searchTerm: 'catan' }),
      })
    );
  });
});

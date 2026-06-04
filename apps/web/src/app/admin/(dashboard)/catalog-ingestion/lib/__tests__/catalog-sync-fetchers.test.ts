import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  fetchCatalogSyncStatus,
  fetchCatalogSyncRuns,
  fetchCatalogSyncRunLogs,
  triggerCatalogSync,
} from '../catalog-ingestion-api';

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('fetchCatalogSyncStatus', () => {
  it('GETs /status and returns parsed body', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        status: 'idle',
        lastRun: null,
        currentRun: null,
        cumulative: { gamesTotal: 4812 },
        nextScheduled: null,
      }),
    });
    const result = await fetchCatalogSyncStatus();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/admin/catalog-ingestion/status'),
      expect.objectContaining({ method: 'GET', credentials: 'include' })
    );
    expect(result.status).toBe('idle');
    expect(result.cumulative.gamesTotal).toBe(4812);
  });

  it('throws on non-ok response', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
    await expect(fetchCatalogSyncStatus()).rejects.toThrow();
  });
});

describe('fetchCatalogSyncRuns', () => {
  it('GETs /runs with page+pageSize query', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ items: [], total: 0, page: 2, pageSize: 24, hasMore: false }),
    });
    await fetchCatalogSyncRuns({ page: 2, pageSize: 24 });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/runs\?page=2&pageSize=24/),
      expect.any(Object)
    );
  });

  it('uses defaults page=1 pageSize=12 when called with no args', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ items: [], total: 0, page: 1, pageSize: 12, hasMore: false }),
    });
    await fetchCatalogSyncRuns();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/runs\?page=1&pageSize=12/),
      expect.any(Object)
    );
  });

  it('throws on non-ok response', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
    await expect(fetchCatalogSyncRuns()).rejects.toThrow();
  });
});

describe('fetchCatalogSyncRunLogs', () => {
  it('GETs /runs/{id}/logs?tail=N', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        runId: 'r1',
        status: 'Success',
        errorCode: null,
        errorDetail: null,
        logsAvailable: true,
        logs: ['line1'],
        logsUnavailableReason: null,
      }),
    });
    await fetchCatalogSyncRunLogs('r1', 100);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/runs\/r1\/logs\?tail=100/),
      expect.any(Object)
    );
  });

  it('returns null on 404', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 404 });
    const result = await fetchCatalogSyncRunLogs('missing', 100);
    expect(result).toBeNull();
  });

  it('throws on non-ok non-404 response', async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 500, statusText: 'Server Error' });
    await expect(fetchCatalogSyncRunLogs('r1', 100)).rejects.toThrow();
  });
});

describe('triggerCatalogSync', () => {
  it('POSTs /trigger with provider in body', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 202,
      json: async () => ({ runId: 'r2' }),
    });
    await triggerCatalogSync('BggApi');
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/admin/catalog-ingestion/trigger'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ provider: 'BggApi' }),
      })
    );
  });

  it('throws CatalogSyncApiError on 409', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ error: 'Sync already running' }),
    });
    await expect(triggerCatalogSync('BggApi')).rejects.toMatchObject({
      status: 409,
      message: expect.stringContaining('Sync already running'),
    });
  });

  it('throws CatalogSyncApiError on generic failure', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({}),
    });
    await expect(triggerCatalogSync('BggApi')).rejects.toThrow();
  });
});

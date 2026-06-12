/**
 * Phase E F2 + F3 client coverage for admin-wikidata-dead-letters.
 * Issue #1823.
 */

import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  BULK_RETRY_MAX_BATCH,
  bulkRetryDeadLetters,
  getAttemptTimeline,
} from '../admin-wikidata-dead-letters';

const ENDPOINT_BASE = '/api/v1/admin/wikidata/enrichment';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('bulkRetryDeadLetters (F2 client)', () => {
  it('POSTs JSON body { attemptIds } to /bulk-retry with credentials', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          triggeredCount: 1,
          notFoundCount: 0,
          failedCount: 0,
          rows: [{ attemptId: 'a1', gameId: 'g1', outcome: 'triggered', reason: null }],
        }),
        { status: 200, headers: { 'content-type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await bulkRetryDeadLetters(['a1']);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${ENDPOINT_BASE}/bulk-retry`);
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(JSON.parse(init.body as string)).toEqual({ attemptIds: ['a1'] });
    expect(result.triggeredCount).toBe(1);
  });

  it('rejects with status + body when the server returns non-OK', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('forbidden', { status: 403, statusText: 'Forbidden' }))
    );

    await expect(bulkRetryDeadLetters(['a1'])).rejects.toThrow(/403.*forbidden/i);
  });

  it('exposes the MaxBatchSize cap matching the BE validator', () => {
    expect(BULK_RETRY_MAX_BATCH).toBe(50);
  });
});

describe('getAttemptTimeline (F3 client)', () => {
  it('GETs /games/{gameId}/attempts?limit=N with credentials', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          gameId: 'g1',
          items: [
            {
              id: 'a1',
              attemptedAt: '2026-06-12T10:00:00Z',
              outcome: 'DeadLetter',
              reason: 'r2-upload-error',
              details: '503',
              retryCount: 3,
              nextRetryAt: null,
              deadLetteredAt: '2026-06-12T10:00:00Z',
            },
          ],
        }),
        { status: 200, headers: { 'content-type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await getAttemptTimeline('g1', 7);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${ENDPOINT_BASE}/games/g1/attempts?limit=7`);
    expect(init.credentials).toBe('include');
    expect(result.items[0]?.outcome).toBe('DeadLetter');
  });

  it('defaults the limit to 50 when omitted', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ gameId: 'g1', items: [] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await getAttemptTimeline('g1');

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${ENDPOINT_BASE}/games/g1/attempts?limit=50`);
  });

  it('rejects with status + body when the server returns non-OK', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('not found', { status: 404, statusText: 'Not Found' }))
    );

    await expect(getAttemptTimeline('g1')).rejects.toThrow(/404.*not found/i);
  });
});

/**
 * Phase F F5 + F6 client coverage for admin-wikidata-dead-letters.
 * Issue #2254 (bulk-acknowledge) + #2255 (attempt-source attribution).
 */

import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  AdminBulkAcknowledgeResult,
  BULK_ACKNOWLEDGE_MAX_BATCH,
  BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH,
  bulkAcknowledgeDeadLetters,
  listDeadLetters,
} from '../admin-wikidata-dead-letters';

const ENDPOINT_BASE = '/api/v1/admin/wikidata/enrichment';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('bulkAcknowledgeDeadLetters (F5 client)', () => {
  it('POSTs attemptIds + note to /bulk-acknowledge with credentials', async () => {
    const payload: AdminBulkAcknowledgeResult = {
      ackedCount: 1,
      idempotentNoOpCount: 0,
      notFoundCount: 0,
      rows: [{ attemptId: 'a-1', gameId: 'g-1', outcome: 'acked', reason: null }],
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await bulkAcknowledgeDeadLetters(['a-1'], 'op note');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${ENDPOINT_BASE}/bulk-acknowledge`);
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(JSON.parse(init.body as string)).toEqual({
      attemptIds: ['a-1'],
      note: 'op note',
    });
    expect(result.ackedCount).toBe(1);
    expect(result.rows[0]?.outcome).toBe('acked');
  });

  it('threads a null note unchanged in the JSON body', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ ackedCount: 0, idempotentNoOpCount: 0, notFoundCount: 0, rows: [] }),
          { status: 200, headers: { 'content-type': 'application/json' } }
        )
      );
    vi.stubGlobal('fetch', fetchMock);

    await bulkAcknowledgeDeadLetters(['a-1'], null);

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(JSON.parse(init.body as string)).toEqual({
      attemptIds: ['a-1'],
      note: null,
    });
  });

  it('throws Error on 422 with body included in the message', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          new Response('Too many', { status: 422, statusText: 'Unprocessable Entity' })
        )
    );

    await expect(bulkAcknowledgeDeadLetters(['a-1'], null)).rejects.toThrow(
      /Failed to bulk-acknowledge.*422.*Too many/i
    );
  });

  it('exposes BE-mirrored constants for MaxBatchSize + MaxNoteLength', () => {
    expect(BULK_ACKNOWLEDGE_MAX_BATCH).toBe(50);
    expect(BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH).toBe(500);
  });
});

describe('listDeadLetters (includeAcknowledged threading)', () => {
  it('threads includeAcknowledged=true into the query string', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], totalCount: 0, skip: 0, take: 50 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await listDeadLetters({ includeAcknowledged: true });

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain('includeAcknowledged=true');
  });

  it('omits includeAcknowledged when false / default', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], totalCount: 0, skip: 0, take: 50 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await listDeadLetters({});

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).not.toContain('includeAcknowledged');
  });

  it('omits includeAcknowledged when explicitly false', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], totalCount: 0, skip: 0, take: 50 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await listDeadLetters({ includeAcknowledged: false });

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).not.toContain('includeAcknowledged');
  });
});

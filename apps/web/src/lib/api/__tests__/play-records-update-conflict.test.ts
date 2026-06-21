import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi, UpdatePlayRecordError } from '../play-records.api';

describe('playRecordsApi.updateRecord conflict handling (#2437-1)', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
  afterEach(() => vi.unstubAllGlobals());

  it('sends xmin in the body when provided', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: true,
      status: 204,
      headers: new Headers(),
      json: async () => ({}),
    });
    await playRecordsApi.updateRecord('r1', { notes: 'hi', xmin: 99 });
    const body = JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string);
    expect(body.xmin).toBe(99);
  });

  it('throws UpdatePlayRecordError kind=conflict on 409 with X-Warning-Code', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      headers: new Headers({ 'X-Warning-Code': 'concurrent-edit' }),
      json: async () => ({ error: 'concurrent_edit' }),
    });
    await expect(playRecordsApi.updateRecord('r1', { notes: 'hi', xmin: 1 })).rejects.toMatchObject(
      {
        kind: 'conflict',
        status: 409,
        warningCode: 'concurrent-edit',
      }
    );
  });
});

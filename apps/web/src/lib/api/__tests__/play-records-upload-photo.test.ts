import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi } from '../play-records.api';

describe('playRecordsApi.uploadPhoto', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('POSTs multipart with file + flags and returns the result', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        photoId: 'p1',
        photoUrl: 'https://cdn/p.webp',
        thumbnailUrl: null,
        ocrText: '42',
        wasDeduplicated: false,
      }),
    });

    const blob = new Blob(['x'], { type: 'image/jpeg' });
    const res = await playRecordsApi.uploadPhoto('rec-1', blob, {
      caption: 'board',
      extractScoreFromPhoto: true,
    });

    expect(res.photoId).toBe('p1');
    expect(res.ocrText).toBe('42');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/api/v1/play-records/rec-1/photos');
    expect(init.method).toBe('POST');
    expect(init.body).toBeInstanceOf(FormData);
    expect((init.body as FormData).get('extractScoreFromPhoto')).toBe('true');
    expect((init.body as FormData).get('caption')).toBe('board');
  });

  it('throws on non-ok response', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: false,
      status: 413,
      json: async () => ({ error: 'too big' }),
    });
    const blob = new Blob(['x'], { type: 'image/jpeg' });
    await expect(playRecordsApi.uploadPhoto('rec-1', blob, {})).rejects.toThrow('too big');
  });
});

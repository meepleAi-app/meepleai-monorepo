import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import {
  GamebookCampaignSchema,
  createCampaign,
  getCampaign,
  listMyCampaigns,
  updateProgress,
} from '../gamebook-campaigns';

// Valid v4 UUIDs: position 14 = [1-8], position 19 = [89ab]
const validRow = {
  id: '11111111-1111-4111-a111-111111111111',
  gameId: '22222222-2222-4222-a222-222222222222',
  ownerUserId: '33333333-3333-4333-a333-333333333333',
  title: 'Campagna #1',
  currentParagraph: 47,
  history: [42, 45, 47],
  lastReadAt: '2026-05-07T12:00:00Z',
  createdAt: '2026-05-01T10:00:00Z',
  updatedAt: '2026-05-07T12:00:00Z',
};

describe('GamebookCampaignSchema', () => {
  it('parses valid payload', () => {
    expect(() => GamebookCampaignSchema.parse(validRow)).not.toThrow();
  });

  it('rejects negative currentParagraph', () => {
    expect(() => GamebookCampaignSchema.parse({ ...validRow, currentParagraph: -1 })).toThrow();
  });

  it('handles null history → empty array', () => {
    const parsed = GamebookCampaignSchema.parse({ ...validRow, history: null });
    expect(parsed.history).toEqual([]);
  });
});

describe('gamebook-campaigns client', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('createCampaign POSTs body and parses response', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(validRow), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const result = await createCampaign({ gameId: validRow.gameId, title: validRow.title });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/api/v1/gamebook/campaigns');
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(JSON.parse(init.body as string)).toEqual({
      gameId: validRow.gameId,
      title: validRow.title,
    });
    expect(result.id).toBe(validRow.id);
  });

  it('getCampaign GETs by id', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify(validRow), { status: 200 }));
    const result = await getCampaign(validRow.id);
    expect(fetchMock.mock.calls[0][0]).toContain(`/api/v1/gamebook/campaigns/${validRow.id}`);
    expect(result.title).toBe('Campagna #1');
  });

  it('listMyCampaigns appends gameId query when provided', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify([validRow]), { status: 200 }));
    await listMyCampaigns(validRow.gameId);
    expect(fetchMock.mock.calls[0][0]).toContain(`gameId=${validRow.gameId}`);
  });

  it('updateProgress PUTs new paragraph with gameBookId (C2 multi-book)', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify(validRow), { status: 200 }));
    const gameBookId = '44444444-4444-4444-a444-444444444444';
    await updateProgress(validRow.id, gameBookId, 50);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain(`/progress`);
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body as string)).toEqual({ gameBookId, currentParagraph: 50 });
  });

  it('throws helpful error on non-2xx', async () => {
    fetchMock.mockResolvedValueOnce(new Response('forbidden', { status: 403 }));
    await expect(getCampaign(validRow.id)).rejects.toThrow(/403/);
  });
});

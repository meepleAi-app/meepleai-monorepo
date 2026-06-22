import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import {
  GamebookGlossaryEntrySchema,
  listGlossary,
  bootstrapGlossary,
  upsertGlossary,
} from '../gamebook-glossary';

// Valid v4 UUIDs
const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const ENTRY_ID = '44444444-4444-4444-a444-444444444444';

const validEntry = {
  id: ENTRY_ID,
  termEn: 'Dragon',
  termIt: 'Drago',
  source: 'AutoBootstrap',
  updatedAt: '2026-05-07T10:00:00Z',
};

// ---------------------------------------------------------------------------
// Schema tests
// ---------------------------------------------------------------------------

describe('GamebookGlossaryEntrySchema', () => {
  it('parses valid entry', () => {
    const result = GamebookGlossaryEntrySchema.parse(validEntry);
    expect(result.termEn).toBe('Dragon');
  });

  it('rejects empty termEn', () => {
    expect(() => GamebookGlossaryEntrySchema.parse({ ...validEntry, termEn: '' })).toThrow();
  });

  it('rejects unknown source', () => {
    expect(() => GamebookGlossaryEntrySchema.parse({ ...validEntry, source: 'Unknown' })).toThrow();
  });
});

// ---------------------------------------------------------------------------
// Fetch behaviour tests
// ---------------------------------------------------------------------------

describe('gamebook-glossary client', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('listGlossary GETs glossary list', async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify([validEntry]), { status: 200 }));

    const result = await listGlossary(CAMPAIGN_ID);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/campaigns/${CAMPAIGN_ID}/glossary`);
    expect(url).not.toContain('bootstrap');
    expect((init as RequestInit & { credentials: string }).credentials).toBe('include');
    expect(result).toHaveLength(1);
    expect(result[0].termIt).toBe('Drago');
  });

  it('bootstrapGlossary POSTs to bootstrap endpoint', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify([
          validEntry,
          {
            ...validEntry,
            id: '55555555-5555-4555-a555-555555555555',
            termEn: 'Sword',
            termIt: 'Spada',
          },
        ]),
        {
          status: 200,
        }
      )
    );

    const result = await bootstrapGlossary(CAMPAIGN_ID);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain('/glossary/bootstrap');
    expect(init.method).toBe('POST');
    expect(result).toHaveLength(2);
  });

  it('upsertGlossary PUTs to the entry endpoint', async () => {
    const updatedEntry = { ...validEntry, termIt: 'Drago Antico' };
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify(updatedEntry), { status: 200 }));

    const result = await upsertGlossary(CAMPAIGN_ID, ENTRY_ID, {
      termEn: 'Dragon',
      termIt: 'Drago Antico',
    });

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/glossary/${ENTRY_ID}`);
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body as string)).toEqual({ termEn: 'Dragon', termIt: 'Drago Antico' });
    expect(result.termIt).toBe('Drago Antico');
  });

  it('throws on non-2xx', async () => {
    fetchMock.mockResolvedValueOnce(new Response('forbidden', { status: 403 }));
    await expect(listGlossary(CAMPAIGN_ID)).rejects.toThrow(/403/);
  });
});

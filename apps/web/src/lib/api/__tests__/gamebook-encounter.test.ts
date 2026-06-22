import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import {
  EncounterCheatsheetSchema,
  EncounterParseError,
  parseEncounter,
} from '../gamebook-encounter';

// Valid v4 UUIDs: position 14 = [1-8], position 19 = [89ab]
const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const PHOTO_ID = '22222222-2222-4222-a222-222222222222';
const BOOK_ID = '44444444-4444-4444-a444-444444444444';

const validCheatsheet = {
  enemies: [
    {
      name: 'Goblin Scout (×3)',
      icon: '⚔️',
      paragraphMarker: '§218',
      hp: '8',
      atk: '+3',
      def: '12',
      mov: '5',
    },
  ],
  options: [
    {
      label: '⚔️ Attacca subito',
      diceRoll: { sides: 6, count: 1, modifier: 0, threshold: 4 },
      outcome: 'Successo → §223 · Fallimento → §241',
    },
    {
      label: '💬 Tenta di parlare',
      diceRoll: null,
      outcome: null,
    },
  ],
  conditions: {
    win: 'Riduci tutti i Goblin a 0 HP — drop: Voidstone × 2',
    loss: 'Party HP ≤ 0 → §247 (cattura)',
  },
  confidence: { enemies: 0.94, options: 0.9, conditions: 0.88 },
};

// ---------------------------------------------------------------------------
// Schema tests
// ---------------------------------------------------------------------------

describe('EncounterCheatsheetSchema', () => {
  it('parses a valid cheatsheet', () => {
    const result = EncounterCheatsheetSchema.parse(validCheatsheet);
    expect(result.enemies).toHaveLength(1);
    expect(result.enemies[0].hp).toBe('8');
    expect(result.options).toHaveLength(2);
    expect(result.confidence.enemies).toBeCloseTo(0.94);
  });

  it('accepts an optional null diceRoll on an option', () => {
    const result = EncounterCheatsheetSchema.parse(validCheatsheet);
    expect(result.options[1].diceRoll).toBeNull();
  });

  it('accepts null win/loss conditions', () => {
    const result = EncounterCheatsheetSchema.parse({
      ...validCheatsheet,
      conditions: { win: null, loss: null },
    });
    expect(result.conditions.win).toBeNull();
    expect(result.conditions.loss).toBeNull();
  });

  it('normalises a missing enemies array to empty', () => {
    const result = EncounterCheatsheetSchema.parse({ ...validCheatsheet, enemies: null });
    expect(result.enemies).toEqual([]);
  });

  it('rejects a non-numeric confidence', () => {
    expect(() =>
      EncounterCheatsheetSchema.parse({
        ...validCheatsheet,
        confidence: { enemies: 'high', options: 0.9, conditions: 0.9 },
      })
    ).toThrow();
  });
});

// ---------------------------------------------------------------------------
// parseEncounter fetch behaviour
// ---------------------------------------------------------------------------

describe('parseEncounter', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('POSTs JSON body to the encounter-parse sub-resource with credentials', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(validCheatsheet), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const result = await parseEncounter(CAMPAIGN_ID, PHOTO_ID, {
      paragraphNumber: 218,
      gameBookId: BOOK_ID,
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(
      `/api/v1/gamebook/campaigns/${CAMPAIGN_ID}/photos/${PHOTO_ID}/encounter-parse`
    );
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(init.headers).toMatchObject({ 'Content-Type': 'application/json' });
    expect(JSON.parse(init.body as string)).toEqual({
      paragraphNumber: 218,
      gameBookId: BOOK_ID,
    });
    expect(result.enemies[0].name).toBe('Goblin Scout (×3)');
  });

  it('throws EncounterParseError with status 409 when the LLM cannot extract a payload', async () => {
    fetchMock.mockResolvedValueOnce(new Response('parse failed', { status: 409 }));

    await expect(
      parseEncounter(CAMPAIGN_ID, PHOTO_ID, { paragraphNumber: 218, gameBookId: BOOK_ID })
    ).rejects.toMatchObject({ name: 'EncounterParseError', status: 409 });
  });

  it('throws EncounterParseError with status 404 when the photo/segment is missing', async () => {
    fetchMock.mockResolvedValueOnce(new Response('not found', { status: 404 }));

    await expect(
      parseEncounter(CAMPAIGN_ID, PHOTO_ID, { paragraphNumber: 218, gameBookId: BOOK_ID })
    ).rejects.toMatchObject({ name: 'EncounterParseError', status: 404 });
  });

  it('exposes EncounterParseError as an Error subclass', () => {
    const err = new EncounterParseError(409, 'boom');
    expect(err).toBeInstanceOf(Error);
    expect(err.status).toBe(409);
  });
});

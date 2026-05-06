/**
 * Unit tests for gamebook-index schemas (SP6 Phase B Task 1).
 */

import { describe, expect, it } from 'vitest';

import {
  gamebookCardDataSchema,
  gamebookIndexFixtureKindSchema,
  gamebookStatusSchema,
  quotaInfoSchema,
} from '../schemas';

const VALID_GAMEBOOK = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000c0001',
  title: 'Test Gamebook',
  publisher: 'Acme',
  year: 2024,
  pages: 50,
  totalPages: 50,
  chunks: 142,
  status: 'ready' as const,
  cover: null,
  emoji: '📕',
  qaCount: 12,
  sessionsCount: 3,
  errorMsg: null,
};

const VALID_QUOTA = {
  used: 12,
  total: 50,
  resetDate: '2026-06-01T00:00:00.000Z',
  tier: 'free' as const,
};

// ---------------------------------------------------------------------------
// gamebookStatusSchema
// ---------------------------------------------------------------------------

describe('gamebookStatusSchema', () => {
  it.each(['ready', 'indexing', 'error'])('accepts %s', status => {
    expect(gamebookStatusSchema.parse(status)).toBe(status);
  });

  it('rejects unknown status', () => {
    expect(() => gamebookStatusSchema.parse('processing')).toThrow();
  });

  it('rejects empty string', () => {
    expect(() => gamebookStatusSchema.parse('')).toThrow();
  });
});

// ---------------------------------------------------------------------------
// gamebookCardDataSchema
// ---------------------------------------------------------------------------

describe('gamebookCardDataSchema', () => {
  it('parses a fully valid ready gamebook', () => {
    const parsed = gamebookCardDataSchema.parse(VALID_GAMEBOOK);
    expect(parsed.title).toBe('Test Gamebook');
    expect(parsed.status).toBe('ready');
    expect(parsed.chunks).toBe(142);
  });

  it('parses an indexing gamebook with partial pages', () => {
    const parsed = gamebookCardDataSchema.parse({
      ...VALID_GAMEBOOK,
      status: 'indexing',
      pages: 18,
      totalPages: 45,
      chunks: 0,
    });
    expect(parsed.status).toBe('indexing');
    expect(parsed.pages).toBeLessThan(parsed.totalPages);
  });

  it('parses an error gamebook with errorMsg present', () => {
    const parsed = gamebookCardDataSchema.parse({
      ...VALID_GAMEBOOK,
      status: 'error',
      errorMsg: 'OCR failed at page 23',
    });
    expect(parsed.status).toBe('error');
    expect(parsed.errorMsg).toBe('OCR failed at page 23');
  });

  it('rejects invalid status enum', () => {
    expect(() => gamebookCardDataSchema.parse({ ...VALID_GAMEBOOK, status: 'unknown' })).toThrow();
  });

  it('rejects negative pages count', () => {
    expect(() => gamebookCardDataSchema.parse({ ...VALID_GAMEBOOK, pages: -1 })).toThrow();
  });

  it('rejects negative chunks count', () => {
    expect(() => gamebookCardDataSchema.parse({ ...VALID_GAMEBOOK, chunks: -5 })).toThrow();
  });

  it('rejects non-uuid id', () => {
    expect(() => gamebookCardDataSchema.parse({ ...VALID_GAMEBOOK, id: 'not-a-uuid' })).toThrow();
  });

  it('accepts nullable publisher / year / cover', () => {
    const parsed = gamebookCardDataSchema.parse({
      ...VALID_GAMEBOOK,
      publisher: null,
      year: null,
      cover: null,
    });
    expect(parsed.publisher).toBeNull();
    expect(parsed.year).toBeNull();
    expect(parsed.cover).toBeNull();
  });

  it('accepts a valid cover URL', () => {
    const parsed = gamebookCardDataSchema.parse({
      ...VALID_GAMEBOOK,
      cover: 'https://example.com/cover.png',
    });
    expect(parsed.cover).toBe('https://example.com/cover.png');
  });
});

// ---------------------------------------------------------------------------
// quotaInfoSchema
// ---------------------------------------------------------------------------

describe('quotaInfoSchema', () => {
  it('parses a fully valid free-tier quota', () => {
    const parsed = quotaInfoSchema.parse(VALID_QUOTA);
    expect(parsed.used).toBe(12);
    expect(parsed.total).toBe(50);
    expect(parsed.tier).toBe('free');
  });

  it('parses premium tier', () => {
    const parsed = quotaInfoSchema.parse({ ...VALID_QUOTA, tier: 'premium' });
    expect(parsed.tier).toBe('premium');
  });

  it('defaults tier to free when omitted', () => {
    const { tier: _tier, ...withoutTier } = VALID_QUOTA;
    const parsed = quotaInfoSchema.parse(withoutTier);
    expect(parsed.tier).toBe('free');
  });

  it('rejects negative used count', () => {
    expect(() => quotaInfoSchema.parse({ ...VALID_QUOTA, used: -1 })).toThrow();
  });

  it('rejects zero or negative total', () => {
    expect(() => quotaInfoSchema.parse({ ...VALID_QUOTA, total: 0 })).toThrow();
    expect(() => quotaInfoSchema.parse({ ...VALID_QUOTA, total: -10 })).toThrow();
  });

  it('validates resetDate as ISO 8601', () => {
    expect(() => quotaInfoSchema.parse({ ...VALID_QUOTA, resetDate: 'not-a-date' })).toThrow();
  });

  it('rejects unknown tier value', () => {
    expect(() => quotaInfoSchema.parse({ ...VALID_QUOTA, tier: 'enterprise' })).toThrow();
  });
});

// ---------------------------------------------------------------------------
// gamebookIndexFixtureKindSchema
// ---------------------------------------------------------------------------

describe('gamebookIndexFixtureKindSchema', () => {
  it.each(['default', 'empty', 'quota-soft', 'quota-hard', 'loading', 'error'])(
    'accepts %s',
    kind => {
      expect(gamebookIndexFixtureKindSchema.parse(kind)).toBe(kind);
    }
  );

  it('rejects unknown fixture kind', () => {
    expect(() => gamebookIndexFixtureKindSchema.parse('partial')).toThrow();
  });
});

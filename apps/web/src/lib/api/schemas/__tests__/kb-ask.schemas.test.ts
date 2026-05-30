import { describe, it, expect } from 'vitest';
import { KbCitationSchema, KbAskEventSchema, type KbAskEvent } from '../kb-ask.schemas';

describe('KbCitationSchema', () => {
  it('parses a valid page-level citation (D-E: no chunkId)', () => {
    const valid = {
      docId: '550e8400-e29b-41d4-a716-446655440000',
      source: '550e8400-e29b-41d4-a716-446655440000',
      page: 14,
      snippet: 'The Scout class begins with three signature abilities.',
      score: 0.87,
    };
    expect(KbCitationSchema.parse(valid)).toEqual(valid);
  });

  it('rejects negative page numbers', () => {
    expect(() =>
      KbCitationSchema.parse({
        docId: 'd',
        source: 's',
        page: -1,
        snippet: 't',
        score: 0.5,
      })
    ).toThrow();
  });

  it('rejects missing required fields', () => {
    expect(() => KbCitationSchema.parse({ docId: 'd' })).toThrow();
  });
});

describe('KbAskEventSchema (discriminated union on numeric type)', () => {
  it('parses StateUpdate (type=0)', () => {
    const evt = { type: 0 as const, data: { message: 'Ricerca…' } };
    const parsed = KbAskEventSchema.parse(evt);
    expect(parsed.type).toBe(0);
  });

  it('parses Citations (type=1)', () => {
    const evt = {
      type: 1 as const,
      data: {
        citations: [
          {
            docId: 'd',
            source: 's',
            page: 14,
            snippet: 't',
            score: 0.9,
          },
        ],
      },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(1);
  });

  it('parses Token (type=7)', () => {
    expect(KbAskEventSchema.parse({ type: 7 as const, data: { token: 'hello' } }).type).toBe(7);
  });

  it('parses Complete (type=4) with null confidence', () => {
    const evt = {
      type: 4 as const,
      data: {
        totalTokens: 412,
        promptTokens: 150,
        completionTokens: 262,
        estimatedReadingTimeMinutes: 0,
        confidence: null,
      },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(4);
  });

  it('parses Error (type=5) with message + code', () => {
    const evt = {
      type: 5 as const,
      data: { message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(5);
  });

  it('rejects unknown type value (e.g. 99)', () => {
    expect(() => KbAskEventSchema.parse({ type: 99, data: {} })).toThrow();
  });
});

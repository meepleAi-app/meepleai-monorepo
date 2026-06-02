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

describe('KbCitationSchema — chunk-level fields (#1702)', () => {
  const basePayload = {
    docId: 'doc-123',
    source: 'PDF:doc-123',
    page: 14,
    snippet: 'rules text',
    score: 0.85,
  };

  it('parses cleanly when chunk fields are absent (page-level legacy payload)', () => {
    const result = KbCitationSchema.safeParse(basePayload);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBeUndefined();
      expect(result.data.chunkPosition).toBeUndefined();
    }
  });

  it('parses cleanly with both chunk fields set (cross-game chunk-level payload)', () => {
    const result = KbCitationSchema.safeParse({
      ...basePayload,
      chunkId: 'doc-123_3',
      chunkPosition: 3,
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBe('doc-123_3');
      expect(result.data.chunkPosition).toBe(3);
    }
  });

  it('parses cleanly with only chunkId (partial set)', () => {
    const result = KbCitationSchema.safeParse({
      ...basePayload,
      chunkId: 'doc-123_0',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBe('doc-123_0');
      expect(result.data.chunkPosition).toBeUndefined();
    }
  });

  it('rejects chunkPosition that is negative', () => {
    const result = KbCitationSchema.safeParse({ ...basePayload, chunkPosition: -1 });
    expect(result.success).toBe(false);
  });

  it('rejects chunkPosition that is non-integer', () => {
    const result = KbCitationSchema.safeParse({ ...basePayload, chunkPosition: 1.5 });
    expect(result.success).toBe(false);
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

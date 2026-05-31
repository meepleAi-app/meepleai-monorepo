import { describe, expect, it } from 'vitest';
import {
  KbDocsListResponseSchema,
  ProcessingStateSchema,
  UserKbDocDtoSchema,
  type UserKbDocDto,
} from '../kb-docs.schemas';

describe('kb-docs schemas', () => {
  const validDoc: UserKbDocDto = {
    id: '550e8400-e29b-41d4-a716-446655440000',
    gameId: '6ba7b810-9dad-11d1-80b4-00c04fd430c8',
    gameName: 'Catan',
    fileName: 'catan-rules.pdf',
    processingState: 'Ready',
    pageCount: 24,
    processedAt: '2026-05-28T11:00:00+00:00',
    uploadedAt: '2026-05-28T09:00:00+00:00',
    updatedAt: '2026-05-28T11:00:00+00:00',
  };

  it('accepts a full Ready doc', () => {
    expect(() => UserKbDocDtoSchema.parse(validDoc)).not.toThrow();
  });

  it('accepts gameId/gameName null (orphan doc)', () => {
    const parsed = UserKbDocDtoSchema.parse({ ...validDoc, gameId: null, gameName: null });
    expect(parsed.gameId).toBeNull();
    expect(parsed.gameName).toBeNull();
  });

  it('accepts processedAt null + pageCount null (Pending doc)', () => {
    const parsed = UserKbDocDtoSchema.parse({
      ...validDoc,
      processingState: 'Pending',
      processedAt: null,
      pageCount: null,
    });
    expect(parsed.processedAt).toBeNull();
    expect(parsed.pageCount).toBeNull();
  });

  it.each([
    'Pending',
    'Uploading',
    'Extracting',
    'Chunking',
    'Embedding',
    'Indexing',
    'Ready',
    'Failed',
  ])('accepts processingState "%s"', state => {
    expect(() => ProcessingStateSchema.parse(state)).not.toThrow();
  });

  it('rejects unknown processingState', () => {
    expect(() => UserKbDocDtoSchema.parse({ ...validDoc, processingState: 'Unknown' })).toThrow();
  });

  it('rejects malformed uploadedAt (not ISO-8601)', () => {
    expect(() => UserKbDocDtoSchema.parse({ ...validDoc, uploadedAt: 'yesterday' })).toThrow();
  });

  it('parses the list envelope { items, total, page, pageSize }', () => {
    const envelope = { items: [validDoc], total: 1, page: 1, pageSize: 20 };
    const parsed = KbDocsListResponseSchema.parse(envelope);
    expect(parsed.total).toBe(1);
    expect(parsed.items).toHaveLength(1);
  });

  it('rejects envelope that uses totalCount instead of total (BE-1 contract)', () => {
    const envelope = { items: [], totalCount: 0, page: 1, pageSize: 20 };
    expect(() => KbDocsListResponseSchema.parse(envelope)).toThrow();
  });
});

describe('UserKbDocDtoSchema — Phase 3 #1687 metadata fields', () => {
  const baseFixture = {
    id: '550e8400-e29b-41d4-a716-446655440000',
    gameId: '550e8400-e29b-41d4-a716-446655440002',
    gameName: 'Azul',
    fileName: 'azul-rules.pdf',
    processingState: 'Ready' as const,
    pageCount: 24,
    processedAt: '2026-05-31T00:00:00Z',
    uploadedAt: '2026-05-31T00:00:00Z',
    updatedAt: '2026-05-31T00:00:00Z',
  };

  it('accepts title as nullable string', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, title: 'Azul Master Edition' });
    expect(result.success).toBe(true);
  });

  it('accepts title as null', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, title: null });
    expect(result.success).toBe(true);
  });

  it('accepts tags as string array', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, tags: ['strategy', 'family'] });
    expect(result.success).toBe(true);
  });

  it('accepts tags as empty array', () => {
    const result = UserKbDocDtoSchema.safeParse({ ...baseFixture, tags: [] });
    expect(result.success).toBe(true);
  });

  it('accepts updatedBy as nullable uuid', () => {
    const result = UserKbDocDtoSchema.safeParse({
      ...baseFixture,
      updatedBy: '550e8400-e29b-41d4-a716-446655440003',
    });
    expect(result.success).toBe(true);
  });

  it('back-compat: validates without title/tags/updatedBy (existing payloads)', () => {
    const result = UserKbDocDtoSchema.safeParse(baseFixture);
    expect(result.success).toBe(true);
  });
});

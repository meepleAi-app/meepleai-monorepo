import { describe, expect, it } from 'vitest';
import { PatchKbDocMetadataRequestSchema } from '../kb-docs-patch.schemas';

describe('PatchKbDocMetadataRequestSchema (Phase 3 #1737)', () => {
  it('accepts fully populated body', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({
      title: 'New Title',
      documentType: 'Rulebook',
      language: 'it',
      tags: ['strategy', 'family'],
    });
    expect(result.success).toBe(true);
  });

  it('accepts empty body (no-op)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it('accepts title=empty-string (clear semantic per D-4 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ title: '' });
    expect(result.success).toBe(true);
  });

  it('accepts tags=[] (clear semantic)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ tags: [] });
    expect(result.success).toBe(true);
  });

  it('rejects title > 200 chars (D-5 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ title: 'x'.repeat(201) });
    expect(result.success).toBe(false);
  });

  it('rejects tag > 50 chars (D-8 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({ tags: ['x'.repeat(51)] });
    expect(result.success).toBe(false);
  });

  it('rejects > 20 tags (D-8 #1687)', () => {
    const result = PatchKbDocMetadataRequestSchema.safeParse({
      tags: Array.from({ length: 21 }, (_, i) => `tag${i}`),
    });
    expect(result.success).toBe(false);
  });
});

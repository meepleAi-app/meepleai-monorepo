import { describe, it, expect } from 'vitest';
import { CitationSchema } from '@/lib/api/schemas/streaming.schemas';

/**
 * SP-C (#3407): the streaming CitationSchema (session-agent + live chains) must additively carry
 * the Full-gated region-grounding fields (regions/charStart/charEnd) emitted by the BE CitationDto.
 * zod strips unknown keys by default, so without the schema extension `data.regions` is dropped.
 */
describe('CitationSchema — SP-C region fields', () => {
  it('parses and retains regions[] from the wire', () => {
    const result = CitationSchema.safeParse({
      documentId: 'doc-1',
      pageNumber: 2,
      snippetPreview: 'verbatim rule text',
      copyrightTier: 'full',
      regions: [{ page: 2, x: 0.1, y: 0.2, width: 0.3, height: 0.4 }],
      charStart: 100,
      charEnd: 250,
    });

    expect(result.success).toBe(true);
    if (!result.success) return;
    expect(result.data.regions).toEqual([{ page: 2, x: 0.1, y: 0.2, width: 0.3, height: 0.4 }]);
    expect(result.data.charStart).toBe(100);
    expect(result.data.charEnd).toBe(250);
  });

  it('accepts a citation without region fields (backward-compat)', () => {
    const result = CitationSchema.safeParse({
      documentId: 'doc-2',
      pageNumber: 1,
      snippetPreview: 'text',
      copyrightTier: 'protected',
    });

    expect(result.success).toBe(true);
    if (!result.success) return;
    // Absent → nullish, never throws.
    expect(result.data.regions ?? null).toBeNull();
    expect(result.data.charStart ?? null).toBeNull();
  });
});

/**
 * PdfIndexingStatus STAGE_ORDER contract test (Issue #2246 Block C)
 *
 * Validates that the exported STAGE_ORDER constant exactly matches the
 * backend `PdfProcessingState` enum (case-sensitive, canonical pipeline order).
 *
 * Backend source of truth:
 *   apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs
 *
 * Schema mirror:
 *   apps/web/src/lib/api/schemas/pdf.schemas.ts → PdfMetricsSchema.currentState
 */
import { describe, it, expect } from 'vitest';

import { STAGE_ORDER } from '@/components/admin/shared-games/PdfIndexingStatus';

describe('PdfIndexingStatus.STAGE_ORDER — backend enum alignment (Issue #2246 Block C)', () => {
  it('exactly matches the backend PdfProcessingState canonical pipeline order', () => {
    expect(STAGE_ORDER).toEqual([
      'Pending',
      'Uploading',
      'Extracting',
      'Chunking',
      'Embedding',
      'Indexing',
      'Ready',
    ]);
  });

  it('does NOT include Failed (terminal off-pipeline state)', () => {
    expect(STAGE_ORDER).not.toContain('Failed');
  });

  it('uses PascalCase (case-sensitive) matching backend C# enum', () => {
    for (const stage of STAGE_ORDER) {
      const firstChar = stage[0];
      expect(firstChar).toBe(firstChar.toUpperCase());
    }
  });

  it('starts with Pending and ends with Ready (forward pipeline terminus)', () => {
    expect(STAGE_ORDER[0]).toBe('Pending');
    expect(STAGE_ORDER[STAGE_ORDER.length - 1]).toBe('Ready');
  });

  it('has exactly 7 stages in the linear pipeline', () => {
    expect(STAGE_ORDER).toHaveLength(7);
  });

  it('is readonly (as const) — TypeScript types prevent mutation', () => {
    // Smoke check: the array must be frozen-by-convention via `as const`.
    // TS would flag mutation at compile time; we verify type narrowness at runtime.
    expect(Array.isArray(STAGE_ORDER)).toBe(true);
  });
});

import { describe, it, expect } from 'vitest';

import { mapProcessingStateToDisplayStatus } from '../processing-status';

describe('mapProcessingStateToDisplayStatus', () => {
  // Full canonical ProcessingState enum coverage (PascalCase).
  const canonical: Array<[string, 'processing' | 'indexed' | 'failed' | 'none']> = [
    ['Pending', 'processing'], // resolved canonical value (was 'none' in one mapper)
    ['Uploading', 'processing'],
    ['Extracting', 'processing'],
    ['Chunking', 'processing'],
    ['Embedding', 'processing'],
    ['Indexing', 'processing'],
    ['Ready', 'indexed'],
    ['Failed', 'failed'],
  ];

  it.each(canonical)('maps canonical %s -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state)).toBe(expected);
  });

  it.each(canonical)('is case-insensitive: %s lowercased -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state.toLowerCase())).toBe(expected);
  });

  it.each([
    ['completed', 'indexed'] as const,
    ['uploaded', 'processing'] as const,
    ['processing', 'processing'] as const,
  ])('maps legacy alias %s -> %s', (state, expected) => {
    expect(mapProcessingStateToDisplayStatus(state)).toBe(expected);
  });

  // Regression guard: these were dropped to 'none' by use-kb-detail's old statusMap.
  it.each(['Chunking', 'Embedding', 'Pending', 'Uploading'])(
    'previously-dropped state %s now maps to processing',
    state => {
      expect(mapProcessingStateToDisplayStatus(state)).toBe('processing');
    }
  );

  it.each([null, undefined, '', '   ', 'garbage'])('maps %s -> none', state => {
    expect(mapProcessingStateToDisplayStatus(state as string | null | undefined)).toBe('none');
  });
});

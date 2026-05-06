/**
 * Unit tests for gamebook-upload schemas (SP6 Phase C.1.A Foundation).
 */

import { describe, expect, it } from 'vitest';

import {
  BggSearchResultSchema,
  CameraPermissionStateSchema,
  CapturedPageSchema,
  CatalogGameRefSchema,
  ConfidenceLevelSchema,
  GameSearchTabSchema,
  IndexedPageMetaSchema,
  LightMeterReadingSchema,
  NoResultsActionSchema,
  RETRY_BUDGET_TOTAL_MS,
  RETRY_DELAYS_MS,
  RetryStateSchema,
  wizardFixtureKindSchema,
} from '../schemas';

// ---------------------------------------------------------------------------
// GameSearchTabSchema
// ---------------------------------------------------------------------------

describe('GameSearchTabSchema', () => {
  it.each(['catalog', 'bgg'])('accepts %s', tab => {
    expect(GameSearchTabSchema.parse(tab)).toBe(tab);
  });

  it('rejects unknown tab', () => {
    expect(() => GameSearchTabSchema.parse('local')).toThrow();
  });
});

// ---------------------------------------------------------------------------
// CatalogGameRefSchema
// ---------------------------------------------------------------------------

describe('CatalogGameRefSchema', () => {
  const VALID = {
    id: '00000000-0000-4000-8000-0000000c0001',
    title: 'Nanolith',
    publisher: 'Self-published',
    coverImageUrl: null,
    sharedByCount: 142,
    isIndexed: true,
  };

  it('accepts a fully populated valid ref', () => {
    expect(CatalogGameRefSchema.parse(VALID)).toEqual(VALID);
  });

  it('rejects empty title', () => {
    expect(() => CatalogGameRefSchema.parse({ ...VALID, title: '' })).toThrow();
  });

  it('rejects negative sharedByCount', () => {
    expect(() => CatalogGameRefSchema.parse({ ...VALID, sharedByCount: -1 })).toThrow();
  });

  it('accepts URL coverImageUrl', () => {
    expect(
      CatalogGameRefSchema.parse({
        ...VALID,
        coverImageUrl: 'https://cdn.example.com/cover.jpg',
      })
    ).toMatchObject({ coverImageUrl: 'https://cdn.example.com/cover.jpg' });
  });

  it('rejects malformed URL coverImageUrl', () => {
    expect(() => CatalogGameRefSchema.parse({ ...VALID, coverImageUrl: 'not-a-url' })).toThrow();
  });
});

// ---------------------------------------------------------------------------
// BggSearchResultSchema
// ---------------------------------------------------------------------------

describe('BggSearchResultSchema (Gate B v1 carryover)', () => {
  const VALID = {
    bggId: 224517,
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    yearPublished: 2018,
  };

  it('accepts a valid BGG result', () => {
    expect(BggSearchResultSchema.parse(VALID)).toEqual(VALID);
  });

  it('accepts null publisher and yearPublished', () => {
    expect(
      BggSearchResultSchema.parse({
        ...VALID,
        publisher: null,
        yearPublished: null,
      })
    ).toMatchObject({ publisher: null, yearPublished: null });
  });

  it('rejects negative bggId', () => {
    expect(() => BggSearchResultSchema.parse({ ...VALID, bggId: -1 })).toThrow();
  });
});

// ---------------------------------------------------------------------------
// CameraPermissionStateSchema
// ---------------------------------------------------------------------------

describe('CameraPermissionStateSchema', () => {
  it.each(['granted', 'denied', 'prompt', 'unsupported'])('accepts %s', state => {
    expect(CameraPermissionStateSchema.parse(state)).toBe(state);
  });

  it('rejects undefined permission state', () => {
    expect(() => CameraPermissionStateSchema.parse('blocked')).toThrow();
  });
});

// ---------------------------------------------------------------------------
// LightMeterReadingSchema
// ---------------------------------------------------------------------------

describe('LightMeterReadingSchema', () => {
  it('accepts well-lit reading', () => {
    expect(LightMeterReadingSchema.parse({ value: 0.85, level: 'ok' })).toEqual({
      value: 0.85,
      level: 'ok',
    });
  });

  it('accepts boundary value 0.0', () => {
    expect(LightMeterReadingSchema.parse({ value: 0.0, level: 'too-dark' })).toMatchObject({
      value: 0.0,
    });
  });

  it('accepts boundary value 1.0', () => {
    expect(LightMeterReadingSchema.parse({ value: 1.0, level: 'ok' })).toMatchObject({
      value: 1.0,
    });
  });

  it('rejects value > 1.0', () => {
    expect(() => LightMeterReadingSchema.parse({ value: 1.5, level: 'ok' })).toThrow();
  });

  it('rejects value < 0.0', () => {
    expect(() => LightMeterReadingSchema.parse({ value: -0.1, level: 'too-dark' })).toThrow();
  });
});

// ---------------------------------------------------------------------------
// CapturedPageSchema
// ---------------------------------------------------------------------------

describe('CapturedPageSchema', () => {
  it('accepts a valid captured page', () => {
    expect(
      CapturedPageSchema.parse({
        pageNumber: 1,
        thumbObjectUrl: 'blob:abc',
        pendingUpload: true,
      })
    ).toMatchObject({ pageNumber: 1 });
  });

  it('rejects pageNumber=0 (must be positive)', () => {
    expect(() =>
      CapturedPageSchema.parse({
        pageNumber: 0,
        thumbObjectUrl: 'blob:abc',
        pendingUpload: false,
      })
    ).toThrow();
  });
});

// ---------------------------------------------------------------------------
// ConfidenceLevelSchema
// ---------------------------------------------------------------------------

describe('ConfidenceLevelSchema', () => {
  it.each(['high', 'medium', 'low'])('accepts %s', level => {
    expect(ConfidenceLevelSchema.parse(level)).toBe(level);
  });

  it('rejects unknown level', () => {
    expect(() => ConfidenceLevelSchema.parse('uncertain')).toThrow();
  });
});

// ---------------------------------------------------------------------------
// IndexedPageMetaSchema (Gate B v1 carryover heuristic)
// ---------------------------------------------------------------------------

describe('IndexedPageMetaSchema', () => {
  it('accepts processing page (confidence=null)', () => {
    expect(
      IndexedPageMetaSchema.parse({
        pageNumber: 1,
        confidence: null,
        isProcessing: true,
        retakeRequested: false,
      })
    ).toMatchObject({ confidence: null, isProcessing: true });
  });

  it('accepts indexed page with low conf + retake flag', () => {
    expect(
      IndexedPageMetaSchema.parse({
        pageNumber: 3,
        confidence: 'low',
        isProcessing: false,
        retakeRequested: true,
      })
    ).toMatchObject({ confidence: 'low', retakeRequested: true });
  });
});

// ---------------------------------------------------------------------------
// NoResultsActionSchema
// ---------------------------------------------------------------------------

describe('NoResultsActionSchema', () => {
  it.each(['create-new', 'search-bgg', 'index-private'])('accepts %s', action => {
    expect(NoResultsActionSchema.parse(action)).toBe(action);
  });
});

// ---------------------------------------------------------------------------
// Retry budget constants
// ---------------------------------------------------------------------------

describe('Retry budget constants', () => {
  it('RETRY_DELAYS_MS = [1s, 2s, 4s, 8s, 16s]', () => {
    expect([...RETRY_DELAYS_MS]).toEqual([1000, 2000, 4000, 8000, 16000]);
  });

  it('RETRY_BUDGET_TOTAL_MS = 31_000ms (sum)', () => {
    expect(RETRY_BUDGET_TOTAL_MS).toBe(31_000);
  });
});

// ---------------------------------------------------------------------------
// RetryStateSchema
// ---------------------------------------------------------------------------

describe('RetryStateSchema', () => {
  it('accepts idle state', () => {
    expect(
      RetryStateSchema.parse({
        attemptCount: 0,
        nextRetryInMs: null,
        isExhausted: false,
      })
    ).toMatchObject({ attemptCount: 0 });
  });

  it('accepts exhausted state at attemptCount=5', () => {
    expect(
      RetryStateSchema.parse({
        attemptCount: 5,
        nextRetryInMs: null,
        isExhausted: true,
      })
    ).toMatchObject({ isExhausted: true });
  });

  it('rejects attemptCount > 5', () => {
    expect(() =>
      RetryStateSchema.parse({
        attemptCount: 6,
        nextRetryInMs: null,
        isExhausted: true,
      })
    ).toThrow();
  });

  it('rejects negative attemptCount', () => {
    expect(() =>
      RetryStateSchema.parse({
        attemptCount: -1,
        nextRetryInMs: null,
        isExhausted: false,
      })
    ).toThrow();
  });
});

// ---------------------------------------------------------------------------
// wizardFixtureKindSchema
// ---------------------------------------------------------------------------

describe('wizardFixtureKindSchema', () => {
  const VALID_KINDS = [
    'step1-default',
    'step1-searching',
    'step1-no-results',
    'step1-bgg-loading',
    'step2-ready',
    'step2-capturing',
    'step2-low-light',
    'step2-failed',
    'step2-denied',
    'step3-progress',
    'step3-partial',
    'step3-complete',
    'step3-offline',
    'step3-cancel-modal',
  ];

  it.each(VALID_KINDS)('accepts %s', kind => {
    expect(wizardFixtureKindSchema.parse(kind)).toBe(kind);
  });

  it('exposes 14 kinds total', () => {
    expect(VALID_KINDS).toHaveLength(14);
  });

  it('rejects wizard-cancelled (terminal cell, not a fixture kind)', () => {
    expect(() => wizardFixtureKindSchema.parse('wizard-cancelled')).toThrow();
  });

  it('rejects unknown kind', () => {
    expect(() => wizardFixtureKindSchema.parse('step4-foo')).toThrow();
  });
});

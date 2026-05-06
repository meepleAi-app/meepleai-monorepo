/**
 * Unit tests for Idempotency-Key composer + parser
 * (SP6 Phase C.1.A Foundation).
 */

import { describe, expect, it } from 'vitest';

import { composeIdempotencyKey, parseIdempotencyKey } from '../idempotency-key';

const SAMPLE_BATCH_ID = '00000000-0000-4000-8000-00000000b001';

// ---------------------------------------------------------------------------
// composeIdempotencyKey
// ---------------------------------------------------------------------------

describe('composeIdempotencyKey', () => {
  it('produces ${batchId}:${pageNumber}:${attemptCount}', () => {
    expect(composeIdempotencyKey(SAMPLE_BATCH_ID, 1, 0)).toBe(`${SAMPLE_BATCH_ID}:1:0`);
  });

  it('handles attemptCount > 0', () => {
    expect(composeIdempotencyKey(SAMPLE_BATCH_ID, 5, 3)).toBe(`${SAMPLE_BATCH_ID}:5:3`);
  });

  it('throws RangeError on empty batchId', () => {
    expect(() => composeIdempotencyKey('', 1, 0)).toThrow(RangeError);
  });

  it('throws RangeError on pageNumber=0 (must be positive)', () => {
    expect(() => composeIdempotencyKey(SAMPLE_BATCH_ID, 0, 0)).toThrow(RangeError);
  });

  it('throws RangeError on negative pageNumber', () => {
    expect(() => composeIdempotencyKey(SAMPLE_BATCH_ID, -1, 0)).toThrow(RangeError);
  });

  it('throws RangeError on non-integer pageNumber', () => {
    expect(() => composeIdempotencyKey(SAMPLE_BATCH_ID, 1.5, 0)).toThrow(RangeError);
  });

  it('throws RangeError on negative attemptCount', () => {
    expect(() => composeIdempotencyKey(SAMPLE_BATCH_ID, 1, -1)).toThrow(RangeError);
  });
});

// ---------------------------------------------------------------------------
// parseIdempotencyKey
// ---------------------------------------------------------------------------

describe('parseIdempotencyKey', () => {
  it('round-trips a valid composed key', () => {
    const key = composeIdempotencyKey(SAMPLE_BATCH_ID, 5, 3);
    expect(parseIdempotencyKey(key)).toEqual({
      batchId: SAMPLE_BATCH_ID,
      pageNumber: 5,
      attemptCount: 3,
    });
  });

  it('returns null for empty string', () => {
    expect(parseIdempotencyKey('')).toBe(null);
  });

  it('returns null for null/undefined', () => {
    expect(parseIdempotencyKey(null)).toBe(null);
    expect(parseIdempotencyKey(undefined)).toBe(null);
  });

  it('returns null when missing colons', () => {
    expect(parseIdempotencyKey('only-one-segment')).toBe(null);
  });

  it('returns null when too many colons', () => {
    expect(parseIdempotencyKey('a:1:2:3')).toBe(null);
  });

  it('returns null on non-numeric pageNumber', () => {
    expect(parseIdempotencyKey('a:b:0')).toBe(null);
  });

  it('returns null on pageNumber=0 (positive only)', () => {
    expect(parseIdempotencyKey('a:0:0')).toBe(null);
  });

  it('returns null on negative attempt', () => {
    expect(parseIdempotencyKey('a:1:-1')).toBe(null);
  });

  it('returns null on leading-zero pageNumber', () => {
    expect(parseIdempotencyKey('a:01:0')).toBe(null);
  });

  it('returns null on leading-zero attemptCount', () => {
    expect(parseIdempotencyKey('a:1:00')).toBe(null);
  });

  it('accepts attemptCount=0 literal', () => {
    expect(parseIdempotencyKey('a:1:0')).toEqual({
      batchId: 'a',
      pageNumber: 1,
      attemptCount: 0,
    });
  });
});

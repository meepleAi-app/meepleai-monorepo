/**
 * Unit tests for confidence-classifier (SP6 Phase C.2.A Interactions).
 *
 * Coverage matrix (per contract §12 thresholds):
 *   - high (≥0.8) / medium (0.5..0.8) / low (<0.5) classification
 *   - boundary values: 0.5, 0.79, 0.8 inclusive on the upper boundary
 *   - null input → null (still processing)
 *   - NaN / Infinity defensive behavior
 *   - shouldRequestRetake binary helper consistency
 *   - deriveHeuristicPageConfidence: uniform vs bimodal distribution
 */

import { describe, expect, it } from 'vitest';

import {
  CONFIDENCE_HIGH_THRESHOLD,
  CONFIDENCE_MEDIUM_THRESHOLD,
  classifyConfidence,
  deriveHeuristicPageConfidence,
  shouldRequestRetake,
} from '../confidence-classifier';

// ---------------------------------------------------------------------------
// Threshold constants
// ---------------------------------------------------------------------------

describe('threshold constants', () => {
  it('CONFIDENCE_HIGH_THRESHOLD = 0.8', () => {
    expect(CONFIDENCE_HIGH_THRESHOLD).toBe(0.8);
  });

  it('CONFIDENCE_MEDIUM_THRESHOLD = 0.5', () => {
    expect(CONFIDENCE_MEDIUM_THRESHOLD).toBe(0.5);
  });
});

// ---------------------------------------------------------------------------
// classifyConfidence
// ---------------------------------------------------------------------------

describe('classifyConfidence', () => {
  it('returns null for null input (still processing)', () => {
    expect(classifyConfidence(null)).toBeNull();
  });

  // High band [0.8 .. 1+]
  it.each([0.8, 0.85, 0.92, 1.0])('classifies %s as high', score => {
    expect(classifyConfidence(score)).toBe('high');
  });

  // Medium band [0.5 .. 0.8)
  it.each([0.5, 0.65, 0.79])('classifies %s as medium', score => {
    expect(classifyConfidence(score)).toBe('medium');
  });

  // Low band [< 0.5]
  it.each([0.0, 0.1, 0.49, 0.499])('classifies %s as low', score => {
    expect(classifyConfidence(score)).toBe('low');
  });

  it('treats 0.8 as high (boundary inclusive on upper)', () => {
    expect(classifyConfidence(0.8)).toBe('high');
  });

  it('treats 0.5 as medium (boundary inclusive on upper)', () => {
    expect(classifyConfidence(0.5)).toBe('medium');
  });

  it('classifies NaN as low (defensive — surfaces retake on garbage)', () => {
    expect(classifyConfidence(Number.NaN)).toBe('low');
  });

  it('classifies -Infinity as low', () => {
    expect(classifyConfidence(Number.NEGATIVE_INFINITY)).toBe('low');
  });

  it('classifies +Infinity as high (passes ≥0.8)', () => {
    expect(classifyConfidence(Number.POSITIVE_INFINITY)).toBe('high');
  });

  it('classifies negative scores as low', () => {
    expect(classifyConfidence(-0.5)).toBe('low');
  });

  it('classifies values >1 as high (no upper clamp)', () => {
    expect(classifyConfidence(1.5)).toBe('high');
  });
});

// ---------------------------------------------------------------------------
// shouldRequestRetake
// ---------------------------------------------------------------------------

describe('shouldRequestRetake', () => {
  it('returns true for low scores', () => {
    expect(shouldRequestRetake(0.3)).toBe(true);
    expect(shouldRequestRetake(0.0)).toBe(true);
    expect(shouldRequestRetake(0.49)).toBe(true);
  });

  it('returns false for medium scores', () => {
    expect(shouldRequestRetake(0.5)).toBe(false);
    expect(shouldRequestRetake(0.65)).toBe(false);
    expect(shouldRequestRetake(0.79)).toBe(false);
  });

  it('returns false for high scores', () => {
    expect(shouldRequestRetake(0.8)).toBe(false);
    expect(shouldRequestRetake(0.95)).toBe(false);
  });

  it('returns false for null (still processing — no retake yet)', () => {
    expect(shouldRequestRetake(null)).toBe(false);
  });

  it('returns true for NaN (defensive)', () => {
    expect(shouldRequestRetake(Number.NaN)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// deriveHeuristicPageConfidence (Gate B v1 carryover heuristic)
// ---------------------------------------------------------------------------

describe('deriveHeuristicPageConfidence', () => {
  describe('failCount === 0 (uniform distribution)', () => {
    it('reports batchAvg unchanged for any page', () => {
      expect(deriveHeuristicPageConfidence(0, 12, 0.85, 0)).toBe(0.85);
      expect(deriveHeuristicPageConfidence(5, 12, 0.85, 0)).toBe(0.85);
      expect(deriveHeuristicPageConfidence(11, 12, 0.85, 0)).toBe(0.85);
    });
  });

  describe('failCount > 0 (bimodal distribution)', () => {
    it('returns 0.4 (low) for first failCount pages', () => {
      expect(deriveHeuristicPageConfidence(0, 12, 0.7, 3)).toBe(0.4);
      expect(deriveHeuristicPageConfidence(1, 12, 0.7, 3)).toBe(0.4);
      expect(deriveHeuristicPageConfidence(2, 12, 0.7, 3)).toBe(0.4);
    });

    it('boosts pages beyond failCount by +0.1', () => {
      // batchAvg + 0.1 = 0.80
      expect(deriveHeuristicPageConfidence(3, 12, 0.7, 3)).toBeCloseTo(0.8, 5);
      expect(deriveHeuristicPageConfidence(11, 12, 0.7, 3)).toBeCloseTo(0.8, 5);
    });

    it('clamps boosted value to 1', () => {
      expect(deriveHeuristicPageConfidence(5, 12, 0.95, 1)).toBe(1);
    });
  });

  it('low-conf pages (0.4) classify as low', () => {
    const conf = deriveHeuristicPageConfidence(0, 12, 0.7, 3);
    expect(classifyConfidence(conf)).toBe('low');
  });

  it('boosted pages classify above low (≥medium) — heuristic lifts above retake threshold', () => {
    // batchAvg 0.70 + 0.10 = 0.7999... (floating-point) → classifies as medium
    // Caveat documented in §12: heuristic is approximate; the +0.1 boost is
    // intended to lift fail-adjacent pages out of the low band, NOT to
    // guarantee high. Real per-page conf will be exposed by backend later.
    const conf = deriveHeuristicPageConfidence(3, 12, 0.7, 3);
    expect(shouldRequestRetake(conf)).toBe(false); // not flagged for retake
    expect(classifyConfidence(conf)).not.toBe('low');
  });

  it('boosted pages cross high threshold when batchAvg ≥0.7 + epsilon', () => {
    // batchAvg 0.71 + 0.10 = 0.81 → classifies as high
    const conf = deriveHeuristicPageConfidence(3, 12, 0.71, 3);
    expect(classifyConfidence(conf)).toBe('high');
  });
});

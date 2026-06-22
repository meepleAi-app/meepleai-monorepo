import { describe, it, expect } from 'vitest';
import { estimateCost } from '../calcCost';

describe('estimateCost', () => {
  it('returns 0 for self-hosted bge-base-en-v1.5', () => {
    const r = estimateCost(100, 'bge-base-en-v1.5');
    expect(r.value).toBe(0);
    expect(r.model).toBe('bge-base-en-v1.5');
    expect(r.formula).toContain('self-hosted');
  });

  it('computes cost for OpenAI text-embedding-3-small', () => {
    const r = estimateCost(100, 'text-embedding-3-small');
    // 100 chunks × 512 tokens × $0.00000002 = $0.001024
    expect(r.value).toBeCloseTo(0.001024, 6);
    expect(r.model).toBe('text-embedding-3-small');
    expect(r.formula).toContain('100');
    expect(r.formula).toContain('512');
  });

  it('returns null result for unknown models', () => {
    const r = estimateCost(100, 'mystery-model');
    expect(r).toBeNull();
  });

  it('returns 0 for zero chunks (self-hosted)', () => {
    const r = estimateCost(0, 'bge-base-en-v1.5');
    expect(r!.value).toBe(0);
  });
});

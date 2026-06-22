import { describe, expect, it } from 'vitest';

import { getLangTier, type LangTier } from '../lang-tier';

describe('getLangTier', () => {
  it('returns "none" for null', () => {
    expect(getLangTier(null)).toBe('none' satisfies LangTier);
  });

  it('returns "none" for undefined', () => {
    expect(getLangTier(undefined)).toBe('none' satisfies LangTier);
  });

  it('returns "high" for confidence > 0.8', () => {
    expect(getLangTier(0.81)).toBe('high' satisfies LangTier);
    expect(getLangTier(0.95)).toBe('high' satisfies LangTier);
    expect(getLangTier(1.0)).toBe('high' satisfies LangTier);
  });

  it('returns "medium" for confidence in [0.5, 0.8] inclusive', () => {
    expect(getLangTier(0.5)).toBe('medium' satisfies LangTier);
    expect(getLangTier(0.65)).toBe('medium' satisfies LangTier);
    expect(getLangTier(0.8)).toBe('medium' satisfies LangTier);
  });

  it('returns "low" for confidence < 0.5', () => {
    expect(getLangTier(0.49)).toBe('low' satisfies LangTier);
    expect(getLangTier(0.31)).toBe('low' satisfies LangTier);
    expect(getLangTier(0)).toBe('low' satisfies LangTier);
  });

  it('returns "low" for negative confidence (defensive)', () => {
    expect(getLangTier(-0.1)).toBe('low' satisfies LangTier);
  });

  it('returns "high" for confidence above 1 (defensive)', () => {
    expect(getLangTier(1.5)).toBe('high' satisfies LangTier);
  });
});

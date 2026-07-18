import { describe, it, expect } from 'vitest';
import { hasSummaryFlavor } from '../SummaryFlavorRenderer';

describe('hasSummaryFlavor', () => {
  it('is true for catan', () => {
    expect(hasSummaryFlavor('catan')).toBe(true);
  });

  it('is false for unknown slug / null / undefined', () => {
    expect(hasSummaryFlavor('wingspan')).toBe(false);
    expect(hasSummaryFlavor(null)).toBe(false);
    expect(hasSummaryFlavor(undefined)).toBe(false);
  });
});

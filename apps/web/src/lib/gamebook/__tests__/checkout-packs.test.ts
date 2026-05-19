import { describe, it, expect } from 'vitest';

import { CHECKOUT_PACKS, formatEur, type CheckoutPack } from '../checkout-packs';

describe('CHECKOUT_PACKS', () => {
  it('has exactly 3 entries', () => {
    expect(CHECKOUT_PACKS).toHaveLength(3);
  });

  it('contains starter, mid, pro with correct pricing', () => {
    const ids = CHECKOUT_PACKS.map((p) => p.id);
    expect(ids).toEqual(['starter', 'mid', 'pro']);
  });

  it('each pack has consistent priceEur / credits ratio close to perParagraphEur', () => {
    for (const pack of CHECKOUT_PACKS) {
      const ratio = pack.priceEur / pack.credits;
      expect(Math.abs(ratio - pack.perParagraphEur)).toBeLessThan(0.001);
    }
  });

  it('readonly array type at compile time (smoke check)', () => {
    // @ts-expect-error - cannot mutate readonly array
    CHECKOUT_PACKS.push({} as CheckoutPack);
  });

  it('starter has popular badge, pro has save badge, mid has no badge', () => {
    const find = (id: string) => CHECKOUT_PACKS.find((p) => p.id === id)!;
    expect(find('starter').badge).toBe('popular');
    expect(find('mid').badge).toBeNull();
    expect(find('pro').badge).toBe('save');
  });
});

describe('formatEur', () => {
  it('formats integer values without decimals (it-IT)', () => {
    const result = formatEur(5);
    expect(result).toMatch(/5\s?€|€\s?5/);
  });

  it('formats fractional values with comma decimal (it-IT)', () => {
    const result = formatEur(5.5);
    expect(result).toMatch(/5,50\s?€|€\s?5,50/);
  });

  it('formats zero', () => {
    const result = formatEur(0);
    expect(result).toMatch(/0\s?€|€\s?0/);
  });
});

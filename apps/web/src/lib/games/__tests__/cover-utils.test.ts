import { describe, it, expect } from 'vitest';

import { extractInitials, hashToHue, shouldUsePlaceholder } from '../cover-utils';

describe('shouldUsePlaceholder', () => {
  it.each([
    [null, true],
    [undefined, true],
    ['', true],
    ['   ', true],
    ['not-a-url', true],
  ])('returns true for invalid input %p', (input, expected) => {
    expect(shouldUsePlaceholder(input)).toBe(expected);
  });

  it('returns true for BGG CDN hosts (#1822 — Geekdo ToS / rate-limit)', () => {
    expect(
      shouldUsePlaceholder(
        'https://cf.geekdo-images.com/x3zxjr-Vw5iU4yDPg70Jgw__original/img/FpyxH41Y6_ROoePAilPNEhXnzO8=/0x0/filters:format(jpeg)/pic3490053.jpg'
      )
    ).toBe(true);
    expect(shouldUsePlaceholder('https://images.geekdo.com/foo.png')).toBe(true);
    expect(shouldUsePlaceholder('https://geekdo-images.com/foo.png')).toBe(true);
  });

  it('returns true for subdomains of blocked hosts', () => {
    expect(shouldUsePlaceholder('https://eu.cf.geekdo-images.com/foo.png')).toBe(true);
  });

  it('returns false for our R2 / Cloudflare-fronted bucket', () => {
    expect(
      shouldUsePlaceholder(
        'https://covers.meepleai.app/cc1678e8-f460-4b53-81f6-6d6539f82b65/cover.webp'
      )
    ).toBe(false);
  });

  it('returns false for data URLs', () => {
    expect(shouldUsePlaceholder('data:image/png;base64,iVBORw0KGgo=')).toBe(false);
  });

  it('returns false for same-origin relative paths', () => {
    expect(shouldUsePlaceholder('/uploads/covers/abc.webp')).toBe(false);
  });

  it('returns false for arbitrary first-party HTTPS hosts', () => {
    expect(shouldUsePlaceholder('https://meepleai.app/static/foo.png')).toBe(false);
    expect(shouldUsePlaceholder('https://cdn.wikimedia.org/foo.jpg')).toBe(false);
  });
});

describe('hashToHue', () => {
  it('is deterministic across runs', () => {
    expect(hashToHue('cc1678e8-f460-4b53-81f6-6d6539f82b65')).toBe(
      hashToHue('cc1678e8-f460-4b53-81f6-6d6539f82b65')
    );
  });

  it('returns a hue in [0, 359]', () => {
    for (const input of ['a', 'catan', 'wingspan', 'a-very-long-game-id-1234567890', '']) {
      const hue = hashToHue(input);
      expect(hue).toBeGreaterThanOrEqual(0);
      expect(hue).toBeLessThanOrEqual(359);
    }
  });

  it('produces distinct hues for visibly distinct inputs', () => {
    // Sanity: a small sample should map to several different hues. Not a
    // collision-free promise — just a smoke check against an accidental
    // constant return.
    const ids = ['catan', 'wingspan', 'agricola', 'gloomhaven', '7-wonders', 'azul'];
    const hues = new Set(ids.map(hashToHue));
    expect(hues.size).toBeGreaterThan(3);
  });
});

describe('extractInitials', () => {
  it.each([
    ['Catan', 'C'],
    ['7 Wonders', '7W'],
    ["Aeon's End", 'AE'],
    ['Gloomhaven', 'G'],
    ['Azul', 'A'],
  ])('extracts initials from %p → %p', (title, expected) => {
    expect(extractInitials(title)).toBe(expected);
  });

  it('skips English stop words', () => {
    expect(extractInitials('The Lord of the Rings')).toBe('LR');
    expect(extractInitials('A Feast for Odin')).toBe('FO');
  });

  it('skips Italian stop words', () => {
    expect(extractInitials('Il Signore degli Anelli')).toBe('SA');
    expect(extractInitials('La Granja')).toBe('G');
  });

  it('handles diacritics (NFD-normalized matching)', () => {
    expect(extractInitials('Pòllen')).toBe('P');
    expect(extractInitials('Cabo Verdê')).toBe('CV');
  });

  it('caps result at 3 characters', () => {
    expect(extractInitials('Brass Birmingham Lancashire Extension')).toBe('BBL');
  });

  it.each([
    [null, '?'],
    [undefined, '?'],
    ['', '?'],
    ['   ', '?'],
  ])('falls back to "?" for empty input %p', (input, expected) => {
    expect(extractInitials(input)).toBe(expected);
  });

  it('falls back to first char when every word is a stop word', () => {
    expect(extractInitials('the')).toBe('T');
    expect(extractInitials('a an the')).toBe('A');
  });
});

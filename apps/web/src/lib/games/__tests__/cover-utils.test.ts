import { describe, it, expect } from 'vitest';

import {
  extractInitials,
  hashToHue,
  isBlockedImageHost,
  shouldUsePlaceholder,
} from '../cover-utils';

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

  // #5066: a leading "/" is not always this origin. "//host/…" is protocol-relative, and browsers read
  // "/\host/…" the same way (backslash as slash), so both are requests to another host.
  it.each(['//cf.geekdo-images.com/pic123.jpg', '/\\cf.geekdo-images.com/pic123.jpg'])(
    'returns true for a BGG host behind a leading slash: %s',
    src => {
      expect(shouldUsePlaceholder(src)).toBe(true);
    }
  );

  it('returns false for a protocol-relative first-party host', () => {
    expect(shouldUsePlaceholder('//covers.meepleai.app/abc/cover.webp')).toBe(false);
  });

  it('returns false for arbitrary first-party HTTPS hosts', () => {
    expect(shouldUsePlaceholder('https://meepleai.app/static/foo.png')).toBe(false);
    expect(shouldUsePlaceholder('https://cdn.wikimedia.org/foo.jpg')).toBe(false);
  });
});

describe('isBlockedImageHost (#2655 — hostname-anchored BGG detection)', () => {
  it('returns true only for a parsed hostname that is a blocked BGG image CDN', () => {
    expect(isBlockedImageHost('https://cf.geekdo-images.com/foo.jpg')).toBe(true);
    expect(isBlockedImageHost('https://images.geekdo.com/foo.png')).toBe(true);
    expect(isBlockedImageHost('https://geekdo-images.com/foo.png')).toBe(true);
  });

  it('returns true for subdomains of blocked hosts', () => {
    expect(isBlockedImageHost('https://eu.cf.geekdo-images.com/foo.png')).toBe(true);
  });

  // #5069: the same hosts as the backend BggHostDenyList — BGG itself and any geekdo host, not only the image CDNs.
  it.each([
    'https://boardgamegeek.com/image/123/catan',
    'https://www.boardgamegeek.com/image/123/catan',
    'https://static.geekdo.com/images/x.png',
  ])('returns true for a BGG host the backend also bans: %s', url => {
    expect(isBlockedImageHost(url)).toBe(true);
    expect(shouldUsePlaceholder(url)).toBe(true);
  });

  // #5069: "host." is the same host as "host" (fully qualified form), and the URL parser keeps the dot.
  it.each(['https://cf.geekdo-images.com./pic.jpg', 'https://www.boardgamegeek.com../pic.jpg'])(
    'reads a trailing dot as the same host: %s',
    url => {
      expect(isBlockedImageHost(url)).toBe(true);
      expect(shouldUsePlaceholder(url)).toBe(true);
    }
  );

  it('anchors the match on a label boundary', () => {
    expect(isBlockedImageHost('https://notboardgamegeek.com/pic.jpg')).toBe(false);
    expect(isBlockedImageHost('https://geekdo.com.evil.example/pic.jpg')).toBe(false);
  });

  it.each([null, undefined, '', '   '])('returns false for missing input %p', input => {
    expect(isBlockedImageHost(input)).toBe(false);
  });

  it('returns false for unparseable src even when it contains a BGG host substring', () => {
    // Regression for the js/regex/missing-regexp-anchor finding: the old
    // unanchored `/…geekdo-images\.com…/.test(src)` matched a raw substring, so
    // a garbage/unparseable src that merely CONTAINS a BGG host token was
    // mis-detected. A parsed hostname check rejects it (no real BGG request).
    expect(isBlockedImageHost('not-a-real-url-boardgamegeek.com-marketing')).toBe(false);
    expect(isBlockedImageHost('cf.geekdo-images.com.evil.example/pic.jpg')).toBe(false);
  });

  it('returns false when a BGG token appears only in the path/query of a non-BGG host', () => {
    // Attacker-style URL whose real hostname is benign but whose path embeds a
    // BGG token. Substring matching would flag it; hostname matching must not.
    expect(
      isBlockedImageHost('https://evil.example/redirect?to=cf.geekdo-images.com%2Fx.jpg')
    ).toBe(false);
  });

  it('returns false for first-party and data/relative sources', () => {
    expect(isBlockedImageHost('https://covers.meepleai.app/abc/cover.webp')).toBe(false);
    expect(isBlockedImageHost('data:image/png;base64,iVBORw0KGgo=')).toBe(false);
    expect(isBlockedImageHost('/uploads/covers/abc.webp')).toBe(false);
  });

  it('returns true for a protocol-relative BGG host (#5066)', () => {
    expect(isBlockedImageHost('//cf.geekdo-images.com/pic123.jpg')).toBe(true);
    expect(isBlockedImageHost('/\\cf.geekdo-images.com/pic123.jpg')).toBe(true);
  });

  // #5072: resolved like an <img src> on the page, https or http. SafeImg blocks on this alone.
  it.each([
    '\\\\cf.geekdo-images.com/pic.jpg', // \\host → //host
    '  //cf.geekdo-images.com/pic.jpg  ',
    'http:cf.geekdo-images.com/pic.jpg', // on an https page: another scheme, so absolute
    'https:cf.geekdo-images.com/pic.jpg', // on an http page (dev, E2E): another scheme, so absolute
    '//cf.geekdo-images.com\t/pic.jpg', // the parser drops tabs
  ])('returns true for %p, which a browser sends to BGG (#5072)', url => {
    expect(isBlockedImageHost(url)).toBe(true);
  });

  it.each([
    'blob:https://meepleai.app/3f1c2a9e-0000-4000-8000-000000000000',
    `  data:image/png;base64,${'A'.repeat(64)}`,
    'images/cf.geekdo-images.com.jpg',
  ])('returns false for %p, which stays on this origin or is local (#5072)', url => {
    expect(isBlockedImageHost(url)).toBe(false);
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

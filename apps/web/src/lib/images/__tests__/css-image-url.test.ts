import { describe, expect, it } from 'vitest';

import { cssImageUrl } from '../css-image-url';

describe('cssImageUrl (#5072)', () => {
  it('quotes an allowed URL', () => {
    expect(cssImageUrl('https://covers.meepleai.app/abc/cover.webp')).toBe(
      'url("https://covers.meepleai.app/abc/cover.webp")'
    );
  });

  it.each([
    'https://cf.geekdo-images.com/abc/pic1.jpg',
    'https://www.boardgamegeek.com/image/1',
    '//images.geekdo.com/pic.png',
  ])('gives undefined for the BGG URL %p', url => {
    expect(cssImageUrl(url)).toBeUndefined();
  });

  it.each([null, undefined, '', '  '])('gives undefined for a missing URL %p', url => {
    expect(cssImageUrl(url)).toBeUndefined();
  });

  it('lets local previews through', () => {
    expect(cssImageUrl('blob:https://meepleai.app/3f1c2a9e-0000-4000-8000-000000000000')).toBe(
      'url("blob:https://meepleai.app/3f1c2a9e-0000-4000-8000-000000000000")'
    );
  });

  it('escapes what would end the CSS string', () => {
    expect(cssImageUrl('https://x.example/a"b\\c\nd.jpg')).toBe(
      'url("https://x.example/a\\"b\\\\c\\a d.jpg")'
    );
  });
});

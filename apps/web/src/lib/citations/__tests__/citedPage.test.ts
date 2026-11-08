import { describe, expect, it } from 'vitest';

import { citedPage, pdfDownloadHref } from '../citedPage';

describe('citedPage (#4819)', () => {
  it.each([1, 2, 17, 320])('keeps page %i', page => {
    expect(citedPage(page)).toBe(page);
  });

  // «No page» is not «page 0»: whatever is not a positive whole number names no page.
  it.each([
    ['null', null],
    ['undefined', undefined],
    ['zero', 0],
    ['a negative number', -1],
    ['a fraction', 2.5],
    ['NaN', Number.NaN],
    ['infinity', Number.POSITIVE_INFINITY],
  ])('names no page for %s', (_label, page) => {
    expect(citedPage(page)).toBeNull();
  });
});

describe('pdfDownloadHref (#4819)', () => {
  it('opens the PDF at the cited page', () => {
    expect(pdfDownloadHref('abc', 4)).toBe('/api/v1/pdfs/abc/download#page=4');
  });

  it.each([null, undefined, 0])('opens the PDF at its start when the page is %s', page => {
    expect(pdfDownloadHref('abc', page)).toBe('/api/v1/pdfs/abc/download');
  });
});

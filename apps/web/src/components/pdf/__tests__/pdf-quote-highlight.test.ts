import { describe, expect, it } from 'vitest';
import { normalizeQuoteText, makeQuoteTextRenderer } from '../pdf-quote-highlight';

describe('pdf-quote-highlight', () => {
  it('normalizes whitespace, soft hyphens, case', () => {
    expect(normalizeQuoteText('Score  1­point\nper  Road')).toBe('score 1point per road');
  });

  it('wraps text items contained in the quote and reports a match', () => {
    const r = makeQuoteTextRenderer('players score one point per road');
    expect(r.render({ str: 'score one point' })).toContain('<mark');
    expect(r.render({ str: 'per road' })).toContain('<mark');
    expect(r.matched()).toBe(true);
  });

  it('escapes HTML and does not wrap non-quote items', () => {
    const r = makeQuoteTextRenderer('players score one point');
    expect(r.render({ str: '<b>bonus</b>' })).toBe('&lt;b&gt;bonus&lt;/b&gt;');
    expect(r.matched()).toBe(false);
  });

  it('ignores trivially short items to reduce false positives', () => {
    const r = makeQuoteTextRenderer('a player scores one point');
    expect(r.render({ str: 'a' })).toBe('a'); // len<=2 not wrapped
  });
});

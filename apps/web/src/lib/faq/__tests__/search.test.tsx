import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { FAQ_CATEGORIES, FAQS, type FAQ } from '../data';
import {
  countByCategory,
  filterFAQs,
  highlight,
  renderInline,
  renderLong,
  type FaqResolved,
} from '../search';

// Stub resolve: builds a deterministic FaqResolved using id-based content so
// filtering tests don't depend on the real i18n bundle.
const FIXTURE: Record<string, FaqResolved> = {
  q1: {
    q: 'How do I create an account?',
    short: 'Use the invite email.',
    long: 'Sign up via /join with password reset.',
  },
  q2: {
    q: 'I forgot my password',
    short: 'Reset via /login link.',
    long: 'Click password forgot.',
  },
  q3: { q: 'Change email', short: 'From settings.', long: 'Settings > Account > Email.' },
  q4: {
    q: 'Install a toolkit',
    short: 'Click install on the toolkit.',
    long: 'Catalog > Toolkit tab.',
  },
  q5: { q: 'Add a new game', short: 'Suggest via contact.', long: 'Email admins.' },
  q6: { q: 'Library overview', short: 'Track owned games.', long: 'Library tracks games.' },
  q7: { q: 'How do agents work?', short: 'Specialized per game.', long: 'KB indexed agents.' },
  q8: { q: 'Are chats private?', short: 'Yes by default.', long: 'Private not used for training.' },
  q9: { q: 'Choose AI model?', short: 'Yes.', long: 'Switch model from menu.' },
  q10: { q: 'What data?', short: 'Email and gameplay.', long: 'Account email plus games.' },
  q11: { q: 'Export my data', short: 'GDPR export.', long: 'Zip in 24h.' },
  q12: { q: 'Delete account', short: 'Grace period 30d.', long: 'Permanent after grace.' },
  q13: { q: 'Is it free?', short: 'Free during alpha.', long: 'Free tier post-alpha.' },
  q14: { q: 'Alpha duration', short: 'Until Q2 2026.', long: 'Started Jan 2026.' },
};

const resolve = (f: FAQ): FaqResolved => FIXTURE[f.id];

describe('highlight()', () => {
  it('returns text unchanged when query is empty', () => {
    expect(highlight('hello world', '')).toBe('hello world');
    expect(highlight('hello world', '   ')).toBe('hello world');
  });

  it('wraps case-insensitive matches in mark elements', () => {
    const node = highlight('Hello World hello', 'hello');
    const { container } = render(<>{node}</>);
    const marks = container.querySelectorAll('mark');
    expect(marks).toHaveLength(2);
    expect(marks[0].textContent).toBe('Hello');
    expect(marks[1].textContent).toBe('hello');
  });

  it('escapes regex metacharacters in query', () => {
    const node = highlight('Cost is $5.00', '$5.00');
    const { container } = render(<>{node}</>);
    const marks = container.querySelectorAll('mark');
    expect(marks).toHaveLength(1);
    expect(marks[0].textContent).toBe('$5.00');
  });

  it('uses the warning color via CSS variable', () => {
    const node = highlight('test', 'test');
    const { container } = render(<>{node}</>);
    const mark = container.querySelector('mark');
    expect(mark?.className).toContain('--c-warning');
  });
});

describe('renderInline()', () => {
  it('renders bold marker as strong element', () => {
    const { container } = render(<>{renderInline('alpha **beta** gamma', '')}</>);
    expect(container.querySelector('strong')?.textContent).toBe('beta');
  });

  it('renders code marker as code element', () => {
    const { container } = render(<>{renderInline('open `/login`', '')}</>);
    const code = container.querySelector('code');
    expect(code?.textContent).toBe('/login');
  });

  it('highlights inside bold marker', () => {
    const { container } = render(<>{renderInline('hello **password reset**', 'password')}</>);
    const strong = container.querySelector('strong');
    expect(strong?.querySelector('mark')?.textContent).toBe('password');
  });

  it('preserves plain text without markers', () => {
    const { container } = render(<>{renderInline('plain prose only', '')}</>);
    expect(container.textContent).toBe('plain prose only');
  });
});

describe('renderLong()', () => {
  it('renders numbered list lines as flex rows with monospace numbering', () => {
    const text = '1. First item\n2. Second item';
    const { container } = render(<>{renderLong(text, '')}</>);
    const rows = container.querySelectorAll('div.flex');
    expect(rows.length).toBeGreaterThanOrEqual(2);
    const monoSpans = container.querySelectorAll('span.font-mono');
    expect(monoSpans[0].textContent).toBe('1.');
    expect(monoSpans[1].textContent).toBe('2.');
  });

  it('renders bullet lines with bullet glyph', () => {
    const text = '- Option A\n- Option B';
    const { container } = render(<>{renderLong(text, '')}</>);
    const bullets = Array.from(container.querySelectorAll('span')).filter(
      el => el.textContent === '\u2022'
    );
    expect(bullets).toHaveLength(2);
  });

  it('renders blank lines as spacer divs', () => {
    const text = 'paragraph one\n\nparagraph two';
    const { container } = render(<>{renderLong(text, '')}</>);
    const spacers = container.querySelectorAll('div.h-\\[6px\\]');
    expect(spacers).toHaveLength(1);
  });

  it('passes query through to inline highlighter', () => {
    const { container } = render(<>{renderLong('match the keyword here', 'keyword')}</>);
    expect(container.querySelector('mark')?.textContent).toBe('keyword');
  });
});

describe('filterFAQs()', () => {
  it('returns full list with cat=all and empty query', () => {
    const result = filterFAQs(FAQS, { activeCat: 'all', query: '', resolve });
    expect(result).toHaveLength(FAQS.length);
  });

  it('filters by category', () => {
    const result = filterFAQs(FAQS, { activeCat: 'ai', query: '', resolve });
    expect(result).toHaveLength(3);
    expect(result.every(f => f.cat === 'ai')).toBe(true);
  });

  it('filters by free-text query across question/short/long', () => {
    const result = filterFAQs(FAQS, { activeCat: 'all', query: 'password', resolve });
    expect(result.length).toBeGreaterThanOrEqual(1);
    expect(result.some(f => f.id === 'q2')).toBe(true);
  });

  it('combines cat + query AND-style', () => {
    const result = filterFAQs(FAQS, { activeCat: 'account', query: 'invite', resolve });
    expect(result.every(f => f.cat === 'account')).toBe(true);
    expect(result.some(f => f.id === 'q1')).toBe(true);
  });

  it('returns empty list when query has no match', () => {
    const result = filterFAQs(FAQS, { activeCat: 'all', query: 'xyz123notfound', resolve });
    expect(result).toHaveLength(0);
  });

  it('case-insensitive query matching', () => {
    const a = filterFAQs(FAQS, { activeCat: 'all', query: 'PASSWORD', resolve });
    const b = filterFAQs(FAQS, { activeCat: 'all', query: 'password', resolve });
    expect(a.map(f => f.id)).toEqual(b.map(f => f.id));
  });
});

describe('countByCategory()', () => {
  it('returns total counts when query empty', () => {
    const counts = countByCategory(FAQS, '', FAQ_CATEGORIES, resolve);
    expect(counts.all).toBe(14);
    expect(counts.account).toBe(3);
    expect(counts.games).toBe(3);
    expect(counts.ai).toBe(3);
    expect(counts.privacy).toBe(3);
    expect(counts.billing).toBe(2);
  });

  it('respects query when computing per-cat counts', () => {
    const counts = countByCategory(FAQS, 'password', FAQ_CATEGORIES, resolve);
    expect(counts.all).toBeGreaterThanOrEqual(1);
    expect(counts.account).toBeGreaterThanOrEqual(1);
  });

  it('returns zero for non-matching query', () => {
    const counts = countByCategory(FAQS, 'xyznomatch', FAQ_CATEGORIES, resolve);
    expect(counts.all).toBe(0);
    expect(counts.account).toBe(0);
    expect(counts.games).toBe(0);
  });
});

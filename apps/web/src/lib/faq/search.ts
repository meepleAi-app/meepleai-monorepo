/**
 * FAQ search helpers — pure functions (no React state).
 *
 * Wave A.1 (Issue #583). Mirror admin-mockups/design_files/sp3-faq-enhanced.jsx
 * lines 102-166 with TypeScript + ReactNode return types and inline-style
 * → Tailwind class translation.
 */

import type { ReactNode } from 'react';
import { Fragment, createElement } from 'react';

import type { CategoryId, FAQ, FAQCategory } from './data';

const HIGHLIGHT_CLASS =
  'rounded-[3px] px-[3px] py-[1px] font-bold text-[color:var(--text)] bg-[hsl(var(--c-warning)/0.28)]';

const CODE_CLASS =
  'rounded px-[5px] py-[1px] font-mono text-[11.5px] bg-[hsl(var(--bg-muted))] text-[hsl(var(--c-info))]';

function escapeRegex(input: string): string {
  return input.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Wrap `query` matches inside `text` with `<mark>` elements.
 * No-op if `query` is empty / whitespace.
 */
export function highlight(text: string, query: string): ReactNode {
  if (!query || !query.trim()) return text;
  const q = query.trim();
  const re = new RegExp(`(${escapeRegex(q)})`, 'gi');
  const parts = text.split(re);
  return createElement(
    Fragment,
    null,
    ...parts.map((part, i) => {
      if (part.toLowerCase() === q.toLowerCase()) {
        return createElement('mark', { key: i, className: HIGHLIGHT_CLASS }, part);
      }
      return createElement(Fragment, { key: i }, part);
    })
  );
}

/**
 * Render an inline string supporting bold (double-asterisk) and code (backtick)
 * markers. Each non-marker slice is run through highlight() so search hits
 * inside bold or plain prose still render mark elements.
 *
 * Mirrors mockup lines 150-166.
 */
export function renderInline(text: string, query: string): ReactNode {
  const parts = text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g);
  return createElement(
    Fragment,
    null,
    ...parts.map((part, i) => {
      if (part.startsWith('**') && part.endsWith('**')) {
        return createElement(
          'strong',
          { key: i, className: 'text-[color:var(--text)]' },
          highlight(part.slice(2, -2), query)
        );
      }
      if (part.startsWith('`') && part.endsWith('`')) {
        return createElement('code', { key: i, className: CODE_CLASS }, part.slice(1, -1));
      }
      return createElement(Fragment, { key: i }, highlight(part, query));
    })
  );
}

/**
 * Render the long answer body — splits on newlines and emits paragraphs,
 * numbered list items, or bullet items. Inline markers are delegated to
 * renderInline().
 *
 * Mirrors mockup lines 122-149.
 */
export function renderLong(text: string, query: string): ReactNode {
  const lines = text.split('\n');
  return createElement(
    Fragment,
    null,
    ...lines.map((line, i) => {
      if (line.trim() === '') {
        return createElement('div', { key: i, className: 'h-[6px]' });
      }
      const numMatch = line.match(/^(\d+)\.\s/);
      if (numMatch) {
        const num = numMatch[1];
        const content = line.replace(/^\d+\.\s/, '');
        return createElement(
          'div',
          { key: i, className: 'flex gap-2 mb-1 items-start' },
          createElement(
            'span',
            {
              className: 'font-mono font-bold text-xs flex-shrink-0 text-[hsl(var(--text-muted))]',
            },
            `${num}.`
          ),
          createElement('span', null, renderInline(content, query))
        );
      }
      if (line.startsWith('- ')) {
        return createElement(
          'div',
          { key: i, className: 'flex gap-2 mb-[3px] items-start' },
          createElement(
            'span',
            { className: 'flex-shrink-0 text-[hsl(var(--text-muted))]' },
            '\u2022'
          ),
          createElement('span', null, renderInline(line.slice(2), query))
        );
      }
      return createElement(
        'p',
        { key: i, className: 'leading-relaxed mb-[6px] mt-0' },
        renderInline(line, query)
      );
    })
  );
}

/**
 * Filter FAQs by active category and free-text query.
 *
 * The function takes the resolved display strings via a resolve() callback
 * because the FAQ dataset only stores i18n keys (locale-agnostic). Callers
 * pass a function returning the localized question / short / long for an id.
 */
export interface FaqResolved {
  q: string;
  short: string;
  long: string;
}

export function filterFAQs(
  faqs: ReadonlyArray<FAQ>,
  args: { activeCat: CategoryId; query: string; resolve: (faq: FAQ) => FaqResolved }
): ReadonlyArray<FAQ> {
  const { activeCat, query, resolve } = args;
  let list: ReadonlyArray<FAQ> = faqs;
  if (activeCat !== 'all') {
    list = list.filter(f => f.cat === activeCat);
  }
  const q = query.trim().toLowerCase();
  if (q.length > 0) {
    list = list.filter(f => {
      const r = resolve(f);
      return (
        r.q.toLowerCase().includes(q) ||
        r.short.toLowerCase().includes(q) ||
        r.long.toLowerCase().includes(q)
      );
    });
  }
  return list;
}

/**
 * Per-category visible counts respecting current query (but ignoring active
 * category — counts are global).
 */
export function countByCategory(
  faqs: ReadonlyArray<FAQ>,
  query: string,
  categories: ReadonlyArray<FAQCategory>,
  resolve: (faq: FAQ) => FaqResolved
): Record<CategoryId, number> {
  const counts: Partial<Record<CategoryId, number>> = {};
  const q = query.trim().toLowerCase();
  const matchesQuery = (f: FAQ): boolean => {
    if (q.length === 0) return true;
    const r = resolve(f);
    return (
      r.q.toLowerCase().includes(q) ||
      r.short.toLowerCase().includes(q) ||
      r.long.toLowerCase().includes(q)
    );
  };

  for (const cat of categories) {
    if (cat.id === 'all') {
      counts.all = faqs.filter(matchesQuery).length;
    } else {
      counts[cat.id] = faqs.filter(f => f.cat === cat.id && matchesQuery(f)).length;
    }
  }
  return counts as Record<CategoryId, number>;
}

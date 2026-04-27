import { describe, expect, it } from 'vitest';

import { CATEGORY_ENTITY, FAQ_CATEGORIES, FAQS, POPULAR_FAQS, type CategoryId } from '../data';

describe('FAQ data', () => {
  it('exposes 6 categories starting with all', () => {
    expect(FAQ_CATEGORIES).toHaveLength(6);
    expect(FAQ_CATEGORIES[0].id).toBe('all');
    const ids = FAQ_CATEGORIES.map(c => c.id);
    expect(ids).toEqual(['all', 'account', 'games', 'ai', 'privacy', 'billing']);
  });

  it('exposes 14 FAQs with stable ids q1..q14', () => {
    expect(FAQS).toHaveLength(14);
    const ids = FAQS.map(f => f.id);
    expect(ids).toEqual([
      'q1',
      'q2',
      'q3',
      'q4',
      'q5',
      'q6',
      'q7',
      'q8',
      'q9',
      'q10',
      'q11',
      'q12',
      'q13',
      'q14',
    ]);
  });

  it('every FAQ has matching i18n keys', () => {
    for (const f of FAQS) {
      expect(f.questionKey).toBe(`pages.faq.items.${f.id}.question`);
      expect(f.shortKey).toBe(`pages.faq.items.${f.id}.short`);
      expect(f.longKey).toBe(`pages.faq.items.${f.id}.long`);
    }
  });

  it('5 FAQs are popular with monotonic ranks 1..5', () => {
    const popular = FAQS.filter(f => f.popular);
    expect(popular).toHaveLength(5);
    const ranks = popular.map(f => f.popularRank).sort((a, b) => (a ?? 0) - (b ?? 0));
    expect(ranks).toEqual([1, 2, 3, 4, 5]);
  });

  it('POPULAR_FAQS is sliced to 4 sorted by rank', () => {
    expect(POPULAR_FAQS).toHaveLength(4);
    const ranks = POPULAR_FAQS.map(f => f.popularRank);
    expect(ranks).toEqual([1, 2, 3, 4]);
  });

  it('category distribution matches mockup mapping', () => {
    const byCat = FAQS.reduce<Record<string, number>>((acc, f) => {
      acc[f.cat] = (acc[f.cat] ?? 0) + 1;
      return acc;
    }, {});
    expect(byCat).toEqual({ account: 3, games: 3, ai: 3, privacy: 3, billing: 2 });
  });

  it('CATEGORY_ENTITY covers every CategoryId', () => {
    const ids: CategoryId[] = ['all', 'account', 'games', 'ai', 'privacy', 'billing'];
    for (const id of ids) {
      expect(CATEGORY_ENTITY[id]).toBeDefined();
    }
  });
});

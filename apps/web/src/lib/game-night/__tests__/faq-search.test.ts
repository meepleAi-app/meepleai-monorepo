import { describe, expect, it } from 'vitest';

import { searchFaqs, searchCommonQuestions, suggestSection } from '../faq-search';

import type { GeneratedFaqDto } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Test Data
// ============================================================================

const SAMPLE_FAQS: GeneratedFaqDto[] = [
  {
    question: 'How many resources do I get when I build a settlement?',
    answer:
      'A settlement produces one resource card for each adjacent terrain hex when that hex number is rolled.',
    sourceSection: 'Setup Phase',
    confidence: 0.88,
    tags: ['settlement', 'resources'],
  },
  {
    question: 'Can I trade with the bank?',
    answer:
      'Yes, you can trade 4 of the same resource to the bank for 1 resource of your choice. With a port, the ratio improves.',
    sourceSection: 'Trading',
    confidence: 0.92,
    tags: ['trading', 'bank'],
  },
  {
    question: 'What happens when a 7 is rolled?',
    answer:
      'When a 7 is rolled, all players with more than 7 resource cards must discard half. Then the active player moves the robber.',
    sourceSection: 'Dice Rolling',
    confidence: 0.95,
    tags: ['dice', 'robber', 'seven'],
  },
  {
    question: 'How do I win the game?',
    answer: 'The first player to reach 10 victory points on their turn wins the game.',
    sourceSection: 'Victory Conditions',
    confidence: 0.98,
    tags: ['victory', 'winning'],
  },
  {
    question: 'Can I build roads on sea hexes?',
    answer: 'No, roads can only be built along the edges of land hexes.',
    sourceSection: 'Building',
    confidence: 0.85,
    tags: ['roads', 'building'],
  },
];

const SAMPLE_PHASES = [
  { name: 'Setup Phase', description: 'Place initial settlements and roads', order: 1 },
  {
    name: 'Trading Phase',
    description: 'Trade resources with other players or the bank',
    order: 2,
  },
  {
    name: 'Building Phase',
    description: 'Spend resources to build roads, settlements, cities',
    order: 3,
  },
  { name: 'Dice Rolling', description: 'Roll dice to determine resource production', order: 4 },
];

// ============================================================================
// Tests: searchFaqs
// ============================================================================

describe('searchFaqs', () => {
  it('returns empty array for empty query', () => {
    expect(searchFaqs('', SAMPLE_FAQS)).toEqual([]);
    expect(searchFaqs('  ', SAMPLE_FAQS)).toEqual([]);
  });

  it('returns empty array for empty faqs', () => {
    expect(searchFaqs('settlement', [])).toEqual([]);
  });

  it('finds exact substring match in question', () => {
    const results = searchFaqs('build a settlement', SAMPLE_FAQS);
    expect(results.length).toBeGreaterThan(0);
    expect(results[0].faq.question).toContain('settlement');
    expect(results[0].score).toBeGreaterThan(0.3);
  });

  it('finds matches by word overlap', () => {
    const results = searchFaqs('resources settlement', SAMPLE_FAQS);
    expect(results.length).toBeGreaterThan(0);
    // Should match the settlement FAQ
    expect(results[0].faq.tags).toContain('settlement');
  });

  it('finds matches by tag', () => {
    const results = searchFaqs('robber', SAMPLE_FAQS, { minScore: 0.1 });
    expect(results.length).toBeGreaterThan(0);
    expect(results[0].faq.tags).toContain('robber');
  });

  it('respects maxResults option', () => {
    const results = searchFaqs('game', SAMPLE_FAQS, { maxResults: 2 });
    expect(results.length).toBeLessThanOrEqual(2);
  });

  it('respects minScore option', () => {
    const results = searchFaqs('zzz nonsense query', SAMPLE_FAQS, { minScore: 0.5 });
    expect(results.length).toBe(0);
  });

  it('ranks higher confidence FAQs higher', () => {
    const results = searchFaqs('win the game victory', SAMPLE_FAQS, { maxResults: 5 });
    if (results.length >= 2) {
      // Results should be sorted by score (descending)
      for (let i = 1; i < results.length; i++) {
        expect(results[i - 1].score).toBeGreaterThanOrEqual(results[i].score);
      }
    }
  });

  it('returns matchType for exact matches', () => {
    const results = searchFaqs('trade with the bank', SAMPLE_FAQS);
    expect(results.length).toBeGreaterThan(0);
    expect(results[0].matchType).toBe('exact');
  });

  it('scores are between 0 and 1', () => {
    const results = searchFaqs('settlement resources trading', SAMPLE_FAQS, {
      maxResults: 10,
      minScore: 0,
    });
    for (const r of results) {
      expect(r.score).toBeGreaterThanOrEqual(0);
      expect(r.score).toBeLessThanOrEqual(1);
    }
  });
});

// ============================================================================
// Tests: searchCommonQuestions
// ============================================================================

describe('searchCommonQuestions', () => {
  const COMMON_QUESTIONS = [
    'How many roads can I build per turn?',
    'Can I trade with the bank?',
    'What are the victory point conditions?',
    'How does the robber work?',
  ];

  it('returns empty array for empty query', () => {
    expect(searchCommonQuestions('', COMMON_QUESTIONS)).toEqual([]);
  });

  it('returns empty array for empty questions', () => {
    expect(searchCommonQuestions('roads', [])).toEqual([]);
  });

  it('finds matching questions', () => {
    const results = searchCommonQuestions('roads build per turn', COMMON_QUESTIONS);
    expect(results.length).toBeGreaterThan(0);
    expect(results[0].question).toContain('roads');
  });

  it('respects maxResults', () => {
    const results = searchCommonQuestions('the', COMMON_QUESTIONS, 1);
    expect(results.length).toBeLessThanOrEqual(1);
  });
});

// ============================================================================
// Tests: suggestSection
// ============================================================================

describe('suggestSection', () => {
  it('returns null for empty query', () => {
    expect(suggestSection('', SAMPLE_PHASES)).toBeNull();
  });

  it('returns null for empty phases', () => {
    expect(suggestSection('trading', [])).toBeNull();
  });

  it('suggests relevant phase for trading query', () => {
    const result = suggestSection('trading resources bank', SAMPLE_PHASES);
    expect(result).toBe('Trading Phase');
  });

  it('suggests relevant phase for building query', () => {
    const result = suggestSection('build settlement roads', SAMPLE_PHASES);
    expect(result).toBe('Building Phase');
  });

  it('suggests relevant phase for dice query', () => {
    const result = suggestSection('rolling dice production', SAMPLE_PHASES);
    expect(result).toBe('Dice Rolling');
  });

  it('returns null when no phase matches', () => {
    const result = suggestSection('zzzzz completely unrelated', SAMPLE_PHASES);
    expect(result).toBeNull();
  });
});

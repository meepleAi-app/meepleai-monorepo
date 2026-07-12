import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';

import type { MeepleCardProps, MeepleCardVariant } from '../types';

/**
 * Behavioral acceptance matrix — prop × variant (issue #2859, follow-up of audit
 * docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md).
 *
 * The pre-existing prop "contract" tests (coverEmoji-prop.contract.test.ts,
 * headingLevel-prop.contract.test.ts) only asserted TYPES via `expectTypeOf` —
 * they never rendered anything, so a variant could silently drop a prop and stay
 * green. This matrix renders each of the 5 variants against the full prop surface
 * and asserts real DOM output, giving MeepleCard a behavioral contract that a
 * type-only test cannot provide (audit Wiegers CRIT: "no verifiable prop→render
 * requirement"; Adzic: "no executable example ties a prop to what it renders").
 *
 * Assertions are deliberately variant-agnostic (title text + crash-free mount of
 * the full prop surface) so the matrix stays robust as individual variants evolve
 * their internal layout. Variant-specific rendering (footer/status/cover slots on
 * grid) is covered by MeepleCard.sp4-contract.test.tsx and the parts/ unit tests.
 */

const VARIANTS: readonly MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

const FULL_PROPS: MeepleCardProps = {
  entity: 'game',
  title: 'Catan',
  subtitle: 'Kosmos',
  rating: 4,
  ratingMax: 5,
  status: 'owned',
  tags: ['strategy', 'family'],
  coverEmoji: '🎲',
  headingLevel: 3,
  badge: 'NEW',
  metadata: [{ label: 'Players', value: '2-4' }],
  connections: [{ entityType: 'session', count: 3 }],
};

describe('MeepleCard acceptance matrix — prop × variant (#2859)', () => {
  describe.each(VARIANTS)('variant=%s', variant => {
    it('renders the title', () => {
      render(<MeepleCard entity="game" title="Catan" variant={variant} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('mounts against the full prop surface without crashing and shows the title', () => {
      render(<MeepleCard {...FULL_PROPS} variant={variant} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });
  });

  it('renders a title for every one of the 9 entity types (default variant)', () => {
    const entities = [
      'game',
      'player',
      'session',
      'agent',
      'kb',
      'chat',
      'event',
      'toolkit',
      'tool',
    ] as const;
    for (const entity of entities) {
      const { unmount } = render(<MeepleCard entity={entity} title={`${entity}-t`} />);
      expect(screen.getByText(`${entity}-t`)).toBeInTheDocument();
      unmount();
    }
  });
});

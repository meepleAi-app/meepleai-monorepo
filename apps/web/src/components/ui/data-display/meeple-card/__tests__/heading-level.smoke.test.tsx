/**
 * #1842 headingLevel cross-variant smoke test.
 *
 * Verifies the MeepleCard API contract: passing `headingLevel` produces
 * the correct semantic tag for each of the 5 variants that own a title
 * heading (GridCard, ListCard, FeaturedCard, HeroCard, FocusCard).
 *
 * CompactCard is intentionally skipped — it renders <span> for the title
 * (compact-ticker semantic), not a heading.
 *
 * Per DEC-5: no visual regression test. Assertions cover semantic tag
 * + className stability only.
 */
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { MeepleCard } from '../MeepleCard';

import type { MeepleCardVariant } from '../types';

interface VariantSpec {
  variant: MeepleCardVariant;
  defaultTag: 'H2' | 'H3';
}

const variants: VariantSpec[] = [
  { variant: 'grid', defaultTag: 'H3' },
  { variant: 'list', defaultTag: 'H3' },
  { variant: 'featured', defaultTag: 'H3' },
  { variant: 'hero', defaultTag: 'H3' },
  { variant: 'focus', defaultTag: 'H2' },
];

describe('MeepleCard headingLevel smoke (#1842)', () => {
  for (const { variant, defaultTag } of variants) {
    it(`${variant} renders ${defaultTag} by default`, () => {
      render(<MeepleCard entity="game" title={`${variant}-default`} variant={variant} />);
      expect(screen.getByText(`${variant}-default`).tagName).toBe(defaultTag);
    });

    it(`${variant} renders H2 when headingLevel={2}`, () => {
      render(
        <MeepleCard entity="game" title={`${variant}-h2`} variant={variant} headingLevel={2} />
      );
      expect(screen.getByText(`${variant}-h2`).tagName).toBe('H2');
    });

    it(`${variant} preserves className when headingLevel changes (visual stability)`, () => {
      const { rerender } = render(
        <MeepleCard entity="game" title={`${variant}-stable`} variant={variant} />
      );
      const defaultClass = screen.getByText(`${variant}-stable`).className;
      rerender(
        <MeepleCard entity="game" title={`${variant}-stable`} variant={variant} headingLevel={4} />
      );
      expect(screen.getByText(`${variant}-stable`).className).toBe(defaultClass);
    });
  }
});

/**
 * MeepleCard Decomposition Verification Tests
 *
 * Smoke tests ensuring each variant renders correctly with each entity type
 * after the monolith was decomposed into modular variant components.
 *
 * 5 variants x 9 entity types = 45 smoke tests
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { MeepleCard } from '../meeple-card';

import type { MeepleEntityType, MeepleCardVariant } from '../meeple-card';

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock window.matchMedia for mobile detection
beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

const ENTITY_TYPES: MeepleEntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chatSession',
  'event',
  'toolkit',
  'custom',
];

const VARIANTS: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

describe('MeepleCard Decomposition: variant x entity smoke tests', () => {
  for (const variant of VARIANTS) {
    describe(`variant="${variant}"`, () => {
      for (const entity of ENTITY_TYPES) {
        it(`renders entity="${entity}" with title visible`, () => {
          const title = `Test ${entity} ${variant}`;
          render(
            <MeepleCard entity={entity} variant={variant} title={title} subtitle="Subtitle" />
          );

          expect(screen.getByText(title)).toBeInTheDocument();
        });
      }
    });
  }
});

describe('MeepleCard Decomposition: public API exports', () => {
  it('re-exports MeepleCard component', () => {
    expect(MeepleCard).toBeDefined();
    expect(typeof MeepleCard).toBe('object'); // React.memo wraps it
  });

  it('re-exports MeepleCardSkeleton', async () => {
    const { MeepleCardSkeleton } = await import('../meeple-card');
    expect(MeepleCardSkeleton).toBeDefined();
  });

  it('re-exports entityColors', async () => {
    const { entityColors } = await import('../meeple-card');
    expect(entityColors).toBeDefined();
    expect(entityColors.game).toHaveProperty('hsl');
    expect(entityColors.game).toHaveProperty('name');
  });
});

/**
 * MeepleCardExpanded - Expanded variant tests
 *
 * Tests for the expanded card variant used in the card browser overlay.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeAll } from 'vitest';
import { Users, Clock, Star, Gamepad2, Layers } from 'lucide-react';

import { MeepleCard } from '../meeple-card';

import type { MeepleEntityType } from '../meeple-card-styles';

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

describe('MeepleCardExpanded', () => {
  it('renders with expanded variant showing title and subtitle', () => {
    render(<MeepleCard entity="game" variant="expanded" title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('clamps title to 2 lines via line-clamp-2 class', () => {
    render(
      <MeepleCard
        entity="game"
        variant="expanded"
        title="A Very Long Game Title That Should Be Clamped"
      />
    );

    const titleElement = screen.getByTestId('expanded-title');
    expect(titleElement.className).toContain('line-clamp-2');
  });

  it('renders max 4 metadata chips when 5 are provided', () => {
    const metadata = [
      { icon: Users, label: '2-4 Players' },
      { icon: Clock, label: '60 min' },
      { icon: Star, label: '4.5' },
      { icon: Gamepad2, label: 'Strategy' },
      { icon: Layers, label: 'Hidden 5th' },
    ];

    render(<MeepleCard entity="game" variant="expanded" title="Test Game" metadata={metadata} />);

    expect(screen.getByText('2-4 Players')).toBeInTheDocument();
    expect(screen.getByText('60 min')).toBeInTheDocument();
    expect(screen.getByText('4.5')).toBeInTheDocument();
    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.queryByText('Hidden 5th')).not.toBeInTheDocument();
  });

  it('renders "See all" button when onClick is provided', () => {
    const handleClick = vi.fn();

    render(<MeepleCard entity="game" variant="expanded" title="Test Game" onClick={handleClick} />);

    const seeAllButton = screen.getByTestId('expanded-see-all');
    expect(seeAllButton).toBeInTheDocument();
    expect(seeAllButton).toHaveTextContent('See all');
  });

  describe('renders all 9 entity types without errors', () => {
    for (const entity of ENTITY_TYPES) {
      it(`renders entity="${entity}"`, () => {
        const title = `Test ${entity} expanded`;
        render(<MeepleCard entity={entity} variant="expanded" title={title} subtitle="Subtitle" />);

        expect(screen.getByText(title)).toBeInTheDocument();
      });
    }
  });
});

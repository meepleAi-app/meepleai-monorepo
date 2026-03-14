/**
 * CardLinkBar Tests
 *
 * Coverage:
 * - Renders tabs for game entity
 * - Returns null when no tabs have content
 * - Renders compact cards in active tab
 * - Calls onCardTap when a card is clicked
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CardLinkBar } from '../meeple-card-browser/CardLinkBar';
import type { CardRef } from '../meeple-card-browser/CardBrowserContext';

// ---------- Mocks ----------

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

// Mock matchMedia
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ---------- Helpers ----------

function makeCard(id: string): CardRef {
  return {
    id,
    entity: 'game',
    title: `Card ${id}`,
    subtitle: `Sub ${id}`,
    color: '25 95% 45%',
  };
}

// ---------- Tests ----------

describe('CardLinkBar', () => {
  it('renders tabs for game entity with populated tab data', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="game-1"
        tabData={{
          related: [makeCard('r1'), makeCard('r2')],
          similar: [makeCard('s1')],
        }}
        onCardTap={vi.fn()}
      />
    );

    const tabs = screen.getAllByTestId('card-link-tab');
    expect(tabs.length).toBe(2);
    expect(tabs[0]).toHaveTextContent('Related');
    expect(tabs[1]).toHaveTextContent('Similar');
  });

  it('returns null when no tabs have content', () => {
    const { container } = render(
      <CardLinkBar entityType="game" entityId="game-1" tabData={{}} onCardTap={vi.fn()} />
    );

    expect(container.innerHTML).toBe('');
  });

  it('renders compact cards in the active tab', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="game-1"
        tabData={{
          related: [makeCard('r1'), makeCard('r2'), makeCard('r3')],
        }}
        onCardTap={vi.fn()}
      />
    );

    const cards = screen.getAllByTestId('card-link-item');
    expect(cards).toHaveLength(3);
  });

  it('calls onCardTap when a card is clicked', () => {
    const onCardTap = vi.fn();
    const card = makeCard('r1');

    render(
      <CardLinkBar
        entityType="game"
        entityId="game-1"
        tabData={{ related: [card] }}
        onCardTap={onCardTap}
      />
    );

    fireEvent.click(screen.getByTestId('card-link-item'));
    expect(onCardTap).toHaveBeenCalledWith(card);
  });

  it('switches tab content when clicking a different tab', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="game-1"
        tabData={{
          related: [makeCard('r1')],
          similar: [makeCard('s1'), makeCard('s2')],
        }}
        onCardTap={vi.fn()}
      />
    );

    // Initially showing Related (first tab with content)
    expect(screen.getAllByTestId('card-link-item')).toHaveLength(1);

    // Click Similar tab
    const tabs = screen.getAllByTestId('card-link-tab');
    fireEvent.click(tabs[1]);

    expect(screen.getAllByTestId('card-link-item')).toHaveLength(2);
  });

  it('only shows tabs that have content', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="game-1"
        tabData={{
          collections: [makeCard('c1')],
          // 'related' and 'similar' are empty
        }}
        onCardTap={vi.fn()}
      />
    );

    const tabs = screen.getAllByTestId('card-link-tab');
    expect(tabs).toHaveLength(1);
    expect(tabs[0]).toHaveTextContent('Collections');
  });
});

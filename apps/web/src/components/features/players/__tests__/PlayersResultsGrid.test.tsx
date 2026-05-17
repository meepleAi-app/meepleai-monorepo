/**
 * PlayersResultsGrid unit tests — Wave 4 D1 (Issue #682).
 *
 * TDD red phase: written before the component implementation.
 * Mirror pattern from AgentsResultsGrid.test.tsx (Wave B.2 reference).
 *
 * 5 tests:
 * 1. Renders data-slot="players-results-grid"
 * 2. Renders a MeepleCard for each item (via title text)
 * 3. Each card uses entity="game" (verifies data-entity attribute on root)
 * 4. Fires onItemClick with correct item when card is clicked
 * 5. Renders empty grid with 0 items (no crash)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { PlayerListItem } from '@/lib/players/players-filters';

import { PlayersResultsGrid } from '../PlayersResultsGrid';
import type { PlayersResultsGridProps } from '../PlayersResultsGrid';

const LABELS: PlayersResultsGridProps['labels'] = {
  resultsAriaLabel: 'Lista giochi giocati',
  resultsCount: '{count} risultati',
  cardSubtitle: '{count} partite',
  cardOpenAriaLabel: 'Apri {gameName}',
};

const ITEMS: ReadonlyArray<PlayerListItem> = [
  { id: 'wingspan', displayName: 'Wingspan', gameName: 'Wingspan', playCount: 12 },
  { id: 'azul', displayName: 'Azul', gameName: 'Azul', playCount: 8 },
  { id: 'catan', displayName: 'Catan', gameName: 'Catan', playCount: 5 },
];

const DEFAULT_PROPS: PlayersResultsGridProps = {
  items: ITEMS,
  onItemClick: vi.fn(),
  labels: LABELS,
};

describe('PlayersResultsGrid', () => {
  it('renders data-slot="players-results-grid"', () => {
    render(<PlayersResultsGrid {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="players-results-grid"]')).not.toBeNull();
  });

  it('renders a card for each item (title visible)', () => {
    render(<PlayersResultsGrid {...DEFAULT_PROPS} />);
    expect(screen.getByText('Wingspan')).toBeTruthy();
    expect(screen.getByText('Azul')).toBeTruthy();
    expect(screen.getByText('Catan')).toBeTruthy();
  });

  it('renders MeepleCard with entity="game" (data-entity attribute on root)', () => {
    render(<PlayersResultsGrid {...DEFAULT_PROPS} />);
    const roots = document.querySelectorAll('[data-entity="game"]');
    expect(roots.length).toBe(ITEMS.length);
  });

  it('fires onItemClick with the correct item when a card wrapper is clicked', () => {
    const onItemClick = vi.fn();
    render(<PlayersResultsGrid {...DEFAULT_PROPS} onItemClick={onItemClick} />);
    // Click the first card wrapper (data-slot="players-results-grid-item")
    const wrappers = document.querySelectorAll('[data-slot="players-results-grid-item"]');
    expect(wrappers.length).toBe(ITEMS.length);
    fireEvent.click(wrappers[0]);
    expect(onItemClick).toHaveBeenCalledOnce();
    expect(onItemClick).toHaveBeenCalledWith(ITEMS[0]);
  });

  it('renders empty grid with 0 items without crashing', () => {
    render(<PlayersResultsGrid {...DEFAULT_PROPS} items={[]} />);
    const grid = document.querySelector('[data-slot="players-results-grid"]');
    expect(grid).not.toBeNull();
    // No grid items rendered (sr-only count announcement is not a grid item)
    expect(document.querySelectorAll('[data-slot="players-results-grid-item"]').length).toBe(0);
  });

  it('renders sr-only resultsCount announcement using labels.resultsCount template', () => {
    render(<PlayersResultsGrid {...DEFAULT_PROPS} />);
    const announce = document.querySelector('[data-slot="players-results-count"]');
    expect(announce).not.toBeNull();
    expect(announce!.textContent).toContain(`${ITEMS.length} risultati`);
    expect(announce!.getAttribute('aria-live')).toBe('polite');
  });
});

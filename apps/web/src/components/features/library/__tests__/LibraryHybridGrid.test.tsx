/**
 * Wave B.3 (Issue #574) — LibraryHybridGrid v2 component tests.
 *
 * Spec §3.2:
 *   - Maps `entries: ReadonlyArray<UserLibraryEntry>` → MeepleCard rendering.
 *   - View modes: `grid` / `list` / `compact` → distinct layout classes +
 *     MeepleCard variant prop pass-through.
 *   - Selection mode FSM:
 *       browse  → no aria-pressed, native click; data-selection-mode="browse".
 *       select  → aria-pressed reflects selected Set membership;
 *                 data-selection-mode="select"; check overlay slot rendered
 *                 only when selected.has(entry.id).
 *   - Click dispatch single-handler contract: orchestrator decides toggle vs
 *     drill-into; component just calls onCardClick(entry.id).
 *
 * MeepleCard is mocked to keep this a focused wrapper-logic unit test
 * (MeepleCard has its own 100+ tests). The mock surfaces `entity`,
 * `variant`, `title`, `subtitle`, `imageUrl`, `rating` as data-attributes
 * on a `<div data-slot="meeple-card-mock">` for assertion.
 */

import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { LibraryHybridGrid, type LibraryHybridGridProps } from '../LibraryHybridGrid';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ---------- MeepleCard mock ---------------------------------------------------
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: {
    entity: string;
    title: string;
    variant?: string;
    subtitle?: string;
    imageUrl?: string;
    rating?: number;
    ratingMax?: number;
  }) => (
    <div
      data-slot="meeple-card-mock"
      data-entity={props.entity}
      data-variant={props.variant ?? 'grid'}
      data-title={props.title}
      data-subtitle={props.subtitle ?? ''}
      data-image-url={props.imageUrl ?? ''}
      data-rating={props.rating ?? ''}
      data-rating-max={props.ratingMax ?? ''}
    >
      {props.title}
    </div>
  ),
}));

// ---------- Fixture helpers ---------------------------------------------------
const NOW = '2026-04-30T10:00:00.000Z';
const USER_ID = '00000000-0000-4000-8000-000000000aaa';

function makeEntry(
  overrides: Partial<UserLibraryEntry> & Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle'>
): UserLibraryEntry {
  return {
    userId: USER_ID,
    gamePublisher: null,
    gameYearPublished: null,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: NOW,
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: true,
    minPlayers: null,
    maxPlayers: null,
    playingTimeMinutes: null,
    complexityRating: null,
    averageRating: null,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  };
}

const e1 = makeEntry({
  id: 'entry-1',
  gameId: 'game-1',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  averageRating: 7.2,
  gameImageUrl: 'https://example.test/catan.png',
});
const e2 = makeEntry({
  id: 'entry-2',
  gameId: 'game-2',
  gameTitle: 'Wingspan',
  gamePublisher: 'Stonemaier Games',
  averageRating: 8.1,
});
const e3 = makeEntry({
  id: 'entry-3',
  gameId: 'game-3',
  gameTitle: 'Brass: Birmingham',
  // no publisher → subtitle should be empty/undefined
  averageRating: 8.6,
  gameIconUrl: 'https://example.test/brass-icon.png',
});
const baseEntries: ReadonlyArray<UserLibraryEntry> = [e1, e2, e3];

function renderGrid(overrides: Partial<LibraryHybridGridProps> = {}) {
  const onCardClick = vi.fn();
  const utils = render(
    <LibraryHybridGrid
      entries={baseEntries}
      view="grid"
      selectionMode="browse"
      selected={new Set()}
      onCardClick={onCardClick}
      {...overrides}
    />
  );
  return { ...utils, onCardClick };
}

// ---------- Tests -------------------------------------------------------------
describe('LibraryHybridGrid (Wave B.3)', () => {
  describe('rendering basics', () => {
    it('renders one card per entry', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      expect(cards).toHaveLength(3);
    });

    it('exposes data-slot="library-hybrid-grid" on container', () => {
      const { container } = renderGrid();
      expect(container.querySelector('[data-slot="library-hybrid-grid"]')).not.toBeNull();
    });

    it('passes title + subtitle + imageUrl + rating to MeepleCard', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      expect(cards[0]).toHaveAttribute('data-title', 'Catan');
      expect(cards[0]).toHaveAttribute('data-subtitle', 'Kosmos');
      expect(cards[0]).toHaveAttribute('data-image-url', 'https://example.test/catan.png');
      expect(cards[0]).toHaveAttribute('data-rating', '7.2');
      expect(cards[0]).toHaveAttribute('data-rating-max', '10');
      expect(cards[0]).toHaveAttribute('data-entity', 'game');
    });

    it('falls back to gameIconUrl when gameImageUrl is null', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      // entry-3 has gameIconUrl but null gameImageUrl
      expect(cards[2]).toHaveAttribute('data-image-url', 'https://example.test/brass-icon.png');
    });

    it('renders empty container when entries=[]', () => {
      const { container } = renderGrid({ entries: [] });
      const grid = container.querySelector('[data-slot="library-hybrid-grid"]');
      expect(grid).not.toBeNull();
      expect(grid?.querySelectorAll('[data-slot="library-grid-card"]')).toHaveLength(0);
    });
  });

  describe('view mode → MeepleCard variant pass-through', () => {
    it('view="grid" → MeepleCard variant="grid"', () => {
      const { container } = renderGrid({ view: 'grid' });
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      cards.forEach(c => expect(c).toHaveAttribute('data-variant', 'grid'));
    });

    it('view="list" → MeepleCard variant="list"', () => {
      const { container } = renderGrid({ view: 'list' });
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      cards.forEach(c => expect(c).toHaveAttribute('data-variant', 'list'));
    });

    it('view="compact" → MeepleCard variant="compact"', () => {
      const { container } = renderGrid({ view: 'compact' });
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      cards.forEach(c => expect(c).toHaveAttribute('data-variant', 'compact'));
    });

    it('exposes data-view on container reflecting current view mode', () => {
      const { container } = renderGrid({ view: 'list' });
      const grid = container.querySelector('[data-slot="library-hybrid-grid"]');
      expect(grid).toHaveAttribute('data-view', 'list');
    });
  });

  describe('selectionMode="browse"', () => {
    it('does NOT set aria-pressed on cards', () => {
      const { container } = renderGrid({ selectionMode: 'browse' });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      cards.forEach(c => expect(c).not.toHaveAttribute('aria-pressed'));
    });

    it('sets data-selection-mode="browse" on each card', () => {
      const { container } = renderGrid({ selectionMode: 'browse' });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      cards.forEach(c => expect(c).toHaveAttribute('data-selection-mode', 'browse'));
    });

    it('click → onCardClick(entry.id)', () => {
      const { container, onCardClick } = renderGrid({ selectionMode: 'browse' });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      fireEvent.click(cards[1]);
      expect(onCardClick).toHaveBeenCalledTimes(1);
      expect(onCardClick).toHaveBeenCalledWith('entry-2');
    });

    it('does NOT render check overlay in browse mode (even if id in selected Set)', () => {
      const { container } = renderGrid({
        selectionMode: 'browse',
        selected: new Set(['entry-1']),
      });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      const overlay = within(cards[0] as HTMLElement).queryByTestId('library-grid-card-check');
      expect(overlay).toBeNull();
    });
  });

  describe('selectionMode="select"', () => {
    it('sets aria-pressed on EVERY card (toggle button pattern)', () => {
      const { container } = renderGrid({
        selectionMode: 'select',
        selected: new Set(['entry-2']),
      });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      expect(cards[0]).toHaveAttribute('aria-pressed', 'false');
      expect(cards[1]).toHaveAttribute('aria-pressed', 'true');
      expect(cards[2]).toHaveAttribute('aria-pressed', 'false');
    });

    it('sets data-selection-mode="select" on each card', () => {
      const { container } = renderGrid({ selectionMode: 'select' });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      cards.forEach(c => expect(c).toHaveAttribute('data-selection-mode', 'select'));
    });

    it('renders check overlay only on selected cards', () => {
      const { container } = renderGrid({
        selectionMode: 'select',
        selected: new Set(['entry-1', 'entry-3']),
      });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      expect(
        within(cards[0] as HTMLElement).queryByTestId('library-grid-card-check')
      ).not.toBeNull();
      expect(within(cards[1] as HTMLElement).queryByTestId('library-grid-card-check')).toBeNull();
      expect(
        within(cards[2] as HTMLElement).queryByTestId('library-grid-card-check')
      ).not.toBeNull();
    });

    it('click → onCardClick(entry.id) (orchestrator decides toggle)', () => {
      const { container, onCardClick } = renderGrid({
        selectionMode: 'select',
        selected: new Set(['entry-1']),
      });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      fireEvent.click(cards[2]);
      expect(onCardClick).toHaveBeenCalledTimes(1);
      expect(onCardClick).toHaveBeenCalledWith('entry-3');
    });
  });
});

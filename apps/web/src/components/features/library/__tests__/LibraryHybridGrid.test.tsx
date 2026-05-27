/**
 * Phase 2a (Issue #1605) — LibraryHybridGrid hybrid-item component tests.
 *
 * The grid is a pure component over a heterogeneous `items: HybridHubItem[]`
 * (game/agent/kb/session/chat). Migrated from the Wave B.3 games-only
 * `UserLibraryEntry[]` shape (#574).
 *
 * Contract under test:
 *   - One MeepleCard per item; `entity`/`title`/`subtitle` come from the base,
 *     `rating`/`imageUrl` are game-only extras (non-game items render without
 *     them, no crash).
 *   - View modes: `grid` / `list` / `compact` → distinct layout classes +
 *     MeepleCard variant prop pass-through.
 *   - Selection mode FSM:
 *       browse  → no aria-pressed, native click; data-selection-mode="browse".
 *       select  → aria-pressed reflects selected Set membership;
 *                 data-selection-mode="select"; check overlay slot rendered
 *                 only when selected.has(item.id).
 *   - Click dispatch single-handler contract: orchestrator decides toggle vs
 *     drill-into; component just calls onCardClick(item.id).
 *
 * MeepleCard is mocked to keep this a focused wrapper-logic unit test
 * (MeepleCard has its own 100+ tests). The mock surfaces `entity`,
 * `variant`, `title`, `subtitle`, `imageUrl`, `rating` as data-attributes
 * on a `<div data-slot="meeple-card-mock">` for assertion.
 */

import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { LibraryHybridGrid, type LibraryHybridGridProps } from '../LibraryHybridGrid';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

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
// Heterogeneous hybrid items: one game (with rating + image), one session, one
// chat (both without rating/image — the game-only visual extras must be absent).
const items: HybridHubItem[] = [
  {
    id: 'g1',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Kosmos',
    updatedAt: '2026-01-01T00:00:00Z',
    href: '/library/game-1',
    gameId: 'game-1',
    rating: 7,
    state: 'Owned',
    imageUrl: 'https://example.test/catan.jpg',
    hasKb: false,
  },
  {
    id: 's1',
    entity: 'session',
    title: 'Session s1',
    subtitle: 'Alice',
    updatedAt: '2026-02-01T00:00:00Z',
    href: '/sessions/s1',
    status: 'Completed',
    playerCount: 4,
  },
  {
    id: 'c1',
    entity: 'chat',
    title: 'How to play?',
    subtitle: 'Catan',
    updatedAt: '2026-03-01T00:00:00Z',
    href: '/chats/c1',
    messageCount: 3,
  },
];

function renderGrid(overrides: Partial<LibraryHybridGridProps> = {}) {
  const onCardClick = vi.fn();
  const utils = render(
    <LibraryHybridGrid
      items={items}
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
describe('LibraryHybridGrid (Phase 2a hybrid items)', () => {
  describe('rendering basics', () => {
    it('renders one card per item (game/session/chat)', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      expect(cards).toHaveLength(3);
    });

    it('exposes data-slot="library-hybrid-grid" on container', () => {
      const { container } = renderGrid();
      expect(container.querySelector('[data-slot="library-hybrid-grid"]')).not.toBeNull();
    });

    it('passes each item entity through to MeepleCard', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      expect(cards[0]).toHaveAttribute('data-entity', 'game');
      expect(cards[1]).toHaveAttribute('data-entity', 'session');
      expect(cards[2]).toHaveAttribute('data-entity', 'chat');
    });

    it('passes title + subtitle + imageUrl + rating for a game item', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      expect(cards[0]).toHaveAttribute('data-title', 'Catan');
      expect(cards[0]).toHaveAttribute('data-subtitle', 'Kosmos');
      expect(cards[0]).toHaveAttribute('data-image-url', 'https://example.test/catan.jpg');
      expect(cards[0]).toHaveAttribute('data-rating', '7');
      expect(cards[0]).toHaveAttribute('data-rating-max', '10');
    });

    it('renders a non-game item without rating/image (no crash)', () => {
      const { container } = renderGrid();
      const cards = container.querySelectorAll('[data-slot="meeple-card-mock"]');
      // session card carries title + subtitle but no game-only visual extras
      expect(cards[1]).toHaveAttribute('data-title', 'Session s1');
      expect(cards[1]).toHaveAttribute('data-subtitle', 'Alice');
      expect(cards[1]).toHaveAttribute('data-image-url', '');
      expect(cards[1]).toHaveAttribute('data-rating', '');
      // chat card likewise
      expect(cards[2]).toHaveAttribute('data-title', 'How to play?');
      expect(cards[2]).toHaveAttribute('data-image-url', '');
      expect(cards[2]).toHaveAttribute('data-rating', '');
    });

    it('renders empty container when items=[]', () => {
      const { container } = renderGrid({ items: [] });
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

    it('click → onCardClick(item.id)', () => {
      const { container, onCardClick } = renderGrid({ selectionMode: 'browse' });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      fireEvent.click(cards[1]);
      expect(onCardClick).toHaveBeenCalledTimes(1);
      expect(onCardClick).toHaveBeenCalledWith('s1');
    });

    it('does NOT render check overlay in browse mode (even if id in selected Set)', () => {
      const { container } = renderGrid({
        selectionMode: 'browse',
        selected: new Set(['g1']),
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
        selected: new Set(['s1']),
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
        selected: new Set(['g1', 'c1']),
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

    it('click → onCardClick(item.id) (orchestrator decides toggle)', () => {
      const { container, onCardClick } = renderGrid({
        selectionMode: 'select',
        selected: new Set(['g1']),
      });
      const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
      fireEvent.click(cards[2]);
      expect(onCardClick).toHaveBeenCalledTimes(1);
      expect(onCardClick).toHaveBeenCalledWith('c1');
    });
  });
});

/**
 * Issue #1483 — HorizontalRow unit tests.
 *
 * Most complex discover component — 4 card variants, loading/error/empty/disabled-shell
 * states, IntersectionObserver telemetry, card click callbacks, visible=false hiding.
 *
 * Contract:
 *   - data-slot="horizontal-row", data-row-id, data-state on root <section>
 *   - visible=false → aria-hidden="true" + hidden CSS class applied
 *   - isLoading → renders 3 skeleton cards (data-slot="row-card-skeleton"), no real cards
 *   - isError (not disabled) → renders role="alert" with "Errore di caricamento" + retry button
 *   - retryLabel / onRetry: retry button text and callback
 *   - disabled (state="disabled") → renders skeleton, title badge, skips error/empty/cards
 *   - empty (no items, !loading, !error, !disabled) → data-slot="row-empty" with emptyLabel
 *   - populated → renders data-slot="row-card" for each item
 *   - onCardClick fired with (rowId, item) when a card is clicked
 *   - viewAllLabel shown only when not disabled and has items
 *   - 4 variants produce correct data-variant on cards: featured, compact, grid, list-row
 *   - subtitle is optional
 *   - disabledTooltip shown in badge when state="disabled"
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { HorizontalRow, type RowItemBase } from '../HorizontalRow';

const makeItem = (id: string, name: string): RowItemBase => ({ id, name });

const ITEMS: ReadonlyArray<RowItemBase> = [
  makeItem('1', 'Catan'),
  makeItem('2', 'Wingspan'),
  makeItem('3', 'Azul'),
];

describe('HorizontalRow', () => {
  // ---- root attributes ----

  it('renders data-slot="horizontal-row" with data-row-id and data-state', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={ITEMS} />
    );
    const root = container.querySelector('[data-slot="horizontal-row"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-row-id', 'games');
    expect(root).toHaveAttribute('data-state', 'enabled');
  });

  it('data-state="disabled" when state prop is "disabled"', () => {
    const { container } = render(
      <HorizontalRow rowId="agents" title="Agenti" variant="compact" items={[]} state="disabled" />
    );
    const root = container.querySelector('[data-slot="horizontal-row"]');
    expect(root).toHaveAttribute('data-state', 'disabled');
  });

  // ---- visible prop ----

  it('visible=false sets aria-hidden="true" and applies hidden class', () => {
    const { container } = render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={ITEMS}
        visible={false}
      />
    );
    const root = container.querySelector('[data-slot="horizontal-row"]');
    expect(root).toHaveAttribute('aria-hidden', 'true');
    expect(root?.className).toContain('hidden');
  });

  it('visible=true (default) does not hide the row', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={ITEMS} />
    );
    const root = container.querySelector('[data-slot="horizontal-row"]');
    expect(root).toHaveAttribute('aria-hidden', 'false');
    expect(root?.className).not.toContain('hidden');
  });

  // ---- title & subtitle ----

  it('renders the row title in an h2', () => {
    render(<HorizontalRow rowId="games" title="Top Giochi" variant="featured" items={[]} />);
    expect(screen.getByRole('heading', { level: 2, name: /Top Giochi/ })).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        subtitle="I più giocati"
        variant="featured"
        items={[]}
      />
    );
    expect(screen.getByText('I più giocati')).toBeInTheDocument();
  });

  // ---- loading state ----

  it('renders 3 skeleton cards when isLoading=true', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={[]} isLoading />
    );
    const skeletons = container.querySelectorAll('[data-slot="row-card-skeleton"]');
    expect(skeletons).toHaveLength(3);
  });

  it('does not render real cards when isLoading=true', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={ITEMS} isLoading />
    );
    expect(container.querySelectorAll('[data-slot="row-card"]')).toHaveLength(0);
  });

  // ---- error state ----

  it('renders error alert with "Errore di caricamento" when isError=true', () => {
    render(<HorizontalRow rowId="games" title="Giochi" variant="featured" items={[]} isError />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Errore di caricamento')).toBeInTheDocument();
  });

  it('renders retry button with custom retryLabel', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[]}
        isError
        retryLabel="Riprova ora"
        onRetry={vi.fn()}
      />
    );
    expect(screen.getByRole('button', { name: 'Riprova ora' })).toBeInTheDocument();
  });

  it('calls onRetry when retry button is clicked', () => {
    const onRetry = vi.fn();
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[]}
        isError
        onRetry={onRetry}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('does not render retry button when onRetry is not provided', () => {
    render(<HorizontalRow rowId="games" title="Giochi" variant="featured" items={[]} isError />);
    expect(screen.queryByRole('button', { name: /Riprova/i })).toBeNull();
  });

  it('does not render error alert when state=disabled even if isError=true', () => {
    render(
      <HorizontalRow
        rowId="agents"
        title="Agenti"
        variant="compact"
        items={[]}
        isError
        state="disabled"
      />
    );
    expect(screen.queryByRole('alert')).toBeNull();
  });

  // ---- disabled shell ----

  it('renders 3 skeleton cards when state="disabled"', () => {
    const { container } = render(
      <HorizontalRow rowId="agents" title="Agenti" variant="compact" items={[]} state="disabled" />
    );
    const skeletons = container.querySelectorAll('[data-slot="row-card-skeleton"]');
    expect(skeletons).toHaveLength(3);
  });

  it('shows disabledTooltip in badge title when state="disabled"', () => {
    render(
      <HorizontalRow
        rowId="agents"
        title="Agenti"
        variant="compact"
        items={[]}
        state="disabled"
        disabledTooltip="Disponibile presto"
      />
    );
    // The badge span has title=disabledTooltip
    const badge = screen.getByTitle('Disponibile presto');
    expect(badge).toBeInTheDocument();
  });

  it('shows default badge text "Disponibile in Phase 0.5" when disabledTooltip is omitted', () => {
    render(
      <HorizontalRow rowId="agents" title="Agenti" variant="compact" items={[]} state="disabled" />
    );
    expect(screen.getByText('Disponibile in Phase 0.5')).toBeInTheDocument();
  });

  // ---- empty state ----

  it('renders data-slot="row-empty" with custom emptyLabel when items=[] and not loading/error/disabled', () => {
    const { container } = render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[]}
        emptyLabel="Nessun gioco trovato."
      />
    );
    const emptySlot = container.querySelector('[data-slot="row-empty"]');
    expect(emptySlot).not.toBeNull();
    expect(screen.getByText('Nessun gioco trovato.')).toBeInTheDocument();
  });

  it('renders default emptyLabel when emptyLabel prop is omitted', () => {
    render(<HorizontalRow rowId="games" title="Giochi" variant="featured" items={[]} />);
    expect(screen.getByText('Nessun elemento disponibile.')).toBeInTheDocument();
  });

  it('does not render empty slot when items are present', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={ITEMS} />
    );
    expect(container.querySelector('[data-slot="row-empty"]')).toBeNull();
  });

  // ---- populated cards ----

  it('renders one data-slot="row-card" per item when populated', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={ITEMS} />
    );
    expect(container.querySelectorAll('[data-slot="row-card"]')).toHaveLength(3);
  });

  it('calls onCardClick with (rowId, item) when a card is clicked', () => {
    const onCardClick = vi.fn();
    const { container } = render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={ITEMS}
        onCardClick={onCardClick}
      />
    );
    const cards = container.querySelectorAll('[data-slot="row-card"]');
    fireEvent.click(cards[0]);
    expect(onCardClick).toHaveBeenCalledTimes(1);
    expect(onCardClick).toHaveBeenCalledWith('games', ITEMS[0]);
  });

  // ---- variant data-variant attributes ----

  it('featured variant cards have data-variant="featured"', () => {
    const { container } = render(
      <HorizontalRow rowId="games" title="Giochi" variant="featured" items={[makeItem('x', 'X')]} />
    );
    const card = container.querySelector('[data-slot="row-card"]');
    expect(card).toHaveAttribute('data-variant', 'featured');
  });

  it('compact variant cards have data-variant="compact"', () => {
    const { container } = render(
      <HorizontalRow rowId="agents" title="Agenti" variant="compact" items={[makeItem('x', 'X')]} />
    );
    const card = container.querySelector('[data-slot="row-card"]');
    expect(card).toHaveAttribute('data-variant', 'compact');
  });

  it('grid variant cards have data-variant="grid"', () => {
    const { container } = render(
      <HorizontalRow rowId="toolkits" title="Toolkit" variant="grid" items={[makeItem('x', 'X')]} />
    );
    const card = container.querySelector('[data-slot="row-card"]');
    expect(card).toHaveAttribute('data-variant', 'grid');
  });

  it('list-row variant cards have data-variant="list-row"', () => {
    const { container } = render(
      <HorizontalRow
        rowId="people"
        title="Persone"
        variant="list-row"
        items={[makeItem('x', 'X')]}
      />
    );
    const card = container.querySelector('[data-slot="row-card"]');
    expect(card).toHaveAttribute('data-variant', 'list-row');
  });

  // ---- viewAllLabel ----

  it('shows viewAllLabel button when items are present and state=enabled', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={ITEMS}
        viewAllLabel="Vedi tutti"
      />
    );
    expect(screen.getByRole('button', { name: /Vedi tutti/ })).toBeInTheDocument();
  });

  it('hides viewAllLabel button when state=disabled even with items', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={ITEMS}
        state="disabled"
        viewAllLabel="Vedi tutti"
      />
    );
    expect(screen.queryByRole('button', { name: /Vedi tutti/ })).toBeNull();
  });

  it('hides viewAllLabel button when items=[]', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[]}
        viewAllLabel="Vedi tutti"
      />
    );
    expect(screen.queryByRole('button', { name: /Vedi tutti/ })).toBeNull();
  });

  // ---- card text rendering per variant ----

  it('featured card shows item name', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[{ id: '1', name: 'Pandemic' }]}
      />
    );
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('featured card falls back to title then "Senza titolo" when name is absent', () => {
    render(
      <HorizontalRow
        rowId="games"
        title="Giochi"
        variant="featured"
        items={[{ id: '1', title: 'KB Game' }]}
      />
    );
    expect(screen.getByText('KB Game')).toBeInTheDocument();
  });

  it('list-row variant uses displayName when present', () => {
    render(
      <HorizontalRow
        rowId="people"
        title="Persone"
        variant="list-row"
        items={[{ id: '1', displayName: 'Mario Rossi' }]}
      />
    );
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
  });
});

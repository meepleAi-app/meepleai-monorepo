/**
 * Issue #1483 — EntityFilterPillBar unit tests.
 *
 * 7-pill segmented control for /discover entity filtering.
 * Each pill is a button with aria-pressed. Supports ArrowLeft/ArrowRight keyboard
 * navigation (wraps around). The active pill has aria-pressed=true.
 *
 * Contract:
 *   - data-slot="entity-filter-pill-bar" on the root div (role="toolbar")
 *   - Renders 7 pills (one per EntityFilter value)
 *   - Active pill has aria-pressed="true", others have aria-pressed="false"
 *   - Each pill has data-filter-id matching the filter key
 *   - Clicking a pill calls onChange with the correct filter
 *   - ArrowRight moves to the next filter and calls onChange
 *   - ArrowLeft moves to the previous filter and calls onChange
 *   - ariaLabel is applied to the toolbar landmark
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EntityFilterPillBar, ENTITY_FILTERS, type EntityFilter } from '../EntityFilterPillBar';

const labels: Readonly<Record<EntityFilter, string>> = {
  all: 'Tutti',
  games: 'Giochi',
  agents: 'Agenti',
  toolkits: 'Toolkit',
  kbs: 'Basi di conoscenza',
  people: 'Persone',
  events: 'Eventi',
};

describe('EntityFilterPillBar', () => {
  it('renders data-slot="entity-filter-pill-bar" on the root toolbar', () => {
    const { container } = render(
      <EntityFilterPillBar value="all" onChange={vi.fn()} labels={labels} />
    );
    const root = container.querySelector('[data-slot="entity-filter-pill-bar"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('role', 'toolbar');
  });

  it('renders 7 pills — one per EntityFilter', () => {
    render(<EntityFilterPillBar value="all" onChange={vi.fn()} labels={labels} />);
    // ENTITY_FILTERS has 7 entries
    expect(ENTITY_FILTERS).toHaveLength(7);
    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(7);
  });

  it('renders labels from the labels prop', () => {
    render(<EntityFilterPillBar value="all" onChange={vi.fn()} labels={labels} />);
    expect(screen.getByRole('button', { name: 'Tutti' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Giochi' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Agenti' })).toBeInTheDocument();
  });

  it('sets aria-pressed=true only on the active pill', () => {
    render(<EntityFilterPillBar value="games" onChange={vi.fn()} labels={labels} />);
    const gamesBtn = screen.getByRole('button', { name: 'Giochi' });
    expect(gamesBtn).toHaveAttribute('aria-pressed', 'true');

    // All others should be false
    const allBtn = screen.getByRole('button', { name: 'Tutti' });
    expect(allBtn).toHaveAttribute('aria-pressed', 'false');
  });

  it('each pill has data-filter-id matching its filter key', () => {
    const { container } = render(
      <EntityFilterPillBar value="all" onChange={vi.fn()} labels={labels} />
    );
    for (const filter of ENTITY_FILTERS) {
      expect(container.querySelector(`[data-filter-id="${filter}"]`)).not.toBeNull();
    }
  });

  it('calls onChange with the clicked filter value', () => {
    const onChange = vi.fn();
    render(<EntityFilterPillBar value="all" onChange={onChange} labels={labels} />);
    fireEvent.click(screen.getByRole('button', { name: 'Giochi' }));
    expect(onChange).toHaveBeenCalledWith('games');
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it('calls onChange with "all" when the Tutti pill is clicked', () => {
    const onChange = vi.fn();
    render(<EntityFilterPillBar value="games" onChange={onChange} labels={labels} />);
    fireEvent.click(screen.getByRole('button', { name: 'Tutti' }));
    expect(onChange).toHaveBeenCalledWith('all');
  });

  it('accepts ariaLabel on the toolbar landmark', () => {
    render(
      <EntityFilterPillBar
        value="all"
        onChange={vi.fn()}
        labels={labels}
        ariaLabel="Filtra entità"
      />
    );
    expect(screen.getByRole('toolbar', { name: 'Filtra entità' })).toBeInTheDocument();
  });

  it('ArrowRight calls onChange with the next filter in sequence', () => {
    const onChange = vi.fn();
    const { container } = render(
      <EntityFilterPillBar value="all" onChange={onChange} labels={labels} />
    );
    const toolbar = container.querySelector('[data-slot="entity-filter-pill-bar"]')!;
    fireEvent.keyDown(toolbar, { key: 'ArrowRight' });
    // 'all' is index 0, next is 'games' (index 1)
    expect(onChange).toHaveBeenCalledWith('games');
  });

  it('ArrowLeft calls onChange with the previous filter in sequence', () => {
    const onChange = vi.fn();
    const { container } = render(
      <EntityFilterPillBar value="games" onChange={onChange} labels={labels} />
    );
    const toolbar = container.querySelector('[data-slot="entity-filter-pill-bar"]')!;
    fireEvent.keyDown(toolbar, { key: 'ArrowLeft' });
    // 'games' is index 1, previous is 'all' (index 0)
    expect(onChange).toHaveBeenCalledWith('all');
  });

  it('ArrowLeft wraps from the first filter to the last', () => {
    const onChange = vi.fn();
    const { container } = render(
      <EntityFilterPillBar value="all" onChange={onChange} labels={labels} />
    );
    const toolbar = container.querySelector('[data-slot="entity-filter-pill-bar"]')!;
    fireEvent.keyDown(toolbar, { key: 'ArrowLeft' });
    // 'all' is index 0, wrapping back gives the last: 'events'
    expect(onChange).toHaveBeenCalledWith('events');
  });

  it('ArrowRight wraps from the last filter to the first', () => {
    const onChange = vi.fn();
    const { container } = render(
      <EntityFilterPillBar value="events" onChange={onChange} labels={labels} />
    );
    const toolbar = container.querySelector('[data-slot="entity-filter-pill-bar"]')!;
    fireEvent.keyDown(toolbar, { key: 'ArrowRight' });
    // 'events' is the last, wrapping forward gives 'all'
    expect(onChange).toHaveBeenCalledWith('all');
  });

  it('non-arrow keys do not call onChange', () => {
    const onChange = vi.fn();
    const { container } = render(
      <EntityFilterPillBar value="all" onChange={onChange} labels={labels} />
    );
    const toolbar = container.querySelector('[data-slot="entity-filter-pill-bar"]')!;
    fireEvent.keyDown(toolbar, { key: 'Enter' });
    fireEvent.keyDown(toolbar, { key: 'Tab' });
    expect(onChange).not.toHaveBeenCalled();
  });
});

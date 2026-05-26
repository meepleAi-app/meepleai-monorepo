/**
 * HubFilters - Unit tests (Issue #1480).
 *
 * Pure presentational filter bar (search + status tabs + sort dropdown + count).
 * Maps from sp4-hub-toolkits.jsx:298-384 (function HubFilters).
 * Sticky at top via backdrop-blur. Labels + state injected (no internal state).
 *
 * Test matrix (Crispin):
 *   T1. data-slot on root.
 *   T2. Search input renders with placeholder and value; fires onQueryChange.
 *   T3. Clear button hidden when query is empty; visible + fires onQueryChange('') when query non-empty.
 *   T4. 4 status tabs render with labels; aria-selected on active; click fires onStatusChange.
 *   T5. Sort dropdown renders 4 options; current value selected; fires onSortChange.
 *   T6. Result count rendered via labels.countTemplate.
 *   T7. className composition.
 *   T8. Passes axe a11y scan.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { HubFilters, type HubFiltersStatus, type HubFiltersSort } from '../HubFilters';

const labels = {
  searchPlaceholder: 'Cerca per nome, gioco, categoria…',
  searchClearAriaLabel: 'Pulisci ricerca',
  statusTablistAriaLabel: 'Filtra toolkit per status',
  statusOptions: { all: 'Tutti', featured: 'Featured', new: 'Nuovi', top: 'Top 100' },
  sortLabel: 'Ordina',
  sortOptions: { popular: 'Popolarità', rating: 'Rating', title: 'Titolo A-Z', uses: 'Uses' },
  countTemplate: '{count} toolkit',
};

function renderFilters(overrides?: {
  query?: string;
  status?: HubFiltersStatus;
  sort?: HubFiltersSort;
  count?: number;
  onQueryChange?: (q: string) => void;
  onStatusChange?: (s: HubFiltersStatus) => void;
  onSortChange?: (s: HubFiltersSort) => void;
}) {
  return render(
    <HubFilters
      query={overrides?.query ?? ''}
      onQueryChange={overrides?.onQueryChange ?? (() => {})}
      status={overrides?.status ?? 'all'}
      onStatusChange={overrides?.onStatusChange ?? (() => {})}
      sort={overrides?.sort ?? 'popular'}
      onSortChange={overrides?.onSortChange ?? (() => {})}
      count={overrides?.count ?? 24}
      labels={labels}
    />
  );
}

describe('HubFilters (Issue #1480)', () => {
  // T1
  it('exposes a data-slot on the root', () => {
    const { container } = renderFilters();
    expect(container.querySelector('[data-slot="toolkits-index-filters"]')).toBeInTheDocument();
  });

  // T2
  it('renders search input with placeholder and value; fires onQueryChange', () => {
    const onQueryChange = vi.fn();
    renderFilters({ query: 'azul', onQueryChange });
    const input = screen.getByPlaceholderText(labels.searchPlaceholder);
    expect(input).toHaveValue('azul');
    fireEvent.change(input, { target: { value: 'wingspan' } });
    expect(onQueryChange).toHaveBeenCalledWith('wingspan');
  });

  // T3
  it('hides clear button when empty; shows + fires onQueryChange("") when non-empty', () => {
    const onQueryChange = vi.fn();
    const { rerender } = renderFilters({ query: '', onQueryChange });
    expect(
      screen.queryByRole('button', { name: labels.searchClearAriaLabel })
    ).not.toBeInTheDocument();
    rerender(
      <HubFilters
        query="azul"
        onQueryChange={onQueryChange}
        status="all"
        onStatusChange={() => {}}
        sort="popular"
        onSortChange={() => {}}
        count={24}
        labels={labels}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: labels.searchClearAriaLabel }));
    expect(onQueryChange).toHaveBeenCalledWith('');
  });

  // T4
  it('renders 4 status tabs; aria-selected on active; click fires onStatusChange', () => {
    const onStatusChange = vi.fn();
    const { container } = renderFilters({ status: 'featured', onStatusChange });
    const tabs = container.querySelectorAll('[role="tab"]');
    expect(tabs).toHaveLength(4);
    expect(screen.getByRole('tab', { name: 'Featured' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Tutti' })).toHaveAttribute('aria-selected', 'false');
    fireEvent.click(screen.getByRole('tab', { name: 'Top 100' }));
    expect(onStatusChange).toHaveBeenCalledWith('top');
  });

  // T5
  it('renders sort dropdown with 4 options; current value selected; fires onSortChange', () => {
    const onSortChange = vi.fn();
    renderFilters({ sort: 'rating', onSortChange });
    const select = screen.getByRole('combobox', { name: labels.sortLabel });
    expect(select).toHaveValue('rating');
    expect(select.querySelectorAll('option')).toHaveLength(4);
    fireEvent.change(select, { target: { value: 'title' } });
    expect(onSortChange).toHaveBeenCalledWith('title');
  });

  // T6 — count is compact (mobile) only, matching the mockup (desktop hides it)
  it('renders the count label via countTemplate when compact', () => {
    render(
      <HubFilters
        query=""
        onQueryChange={() => {}}
        status="all"
        onStatusChange={() => {}}
        sort="popular"
        onSortChange={() => {}}
        count={42}
        labels={labels}
        compact
      />
    );
    expect(screen.getByText('42 toolkit')).toBeInTheDocument();
  });

  it('hides the count label on desktop (non-compact)', () => {
    renderFilters({ count: 42 });
    expect(screen.queryByText('42 toolkit')).not.toBeInTheDocument();
  });

  // T7
  it('composes custom className with base classes', () => {
    const { container } = render(
      <HubFilters
        query=""
        onQueryChange={() => {}}
        status="all"
        onStatusChange={() => {}}
        sort="popular"
        onSortChange={() => {}}
        count={0}
        labels={labels}
        className="extra"
      />
    );
    const root = container.querySelector('[data-slot="toolkits-index-filters"]');
    expect(root).toHaveClass('extra');
  });

  // T8
  it('passes axe a11y scan', async () => {
    const { container } = renderFilters({ query: 'azul', count: 3 });
    expect(await axe(container)).toHaveNoViolations();
  });
});

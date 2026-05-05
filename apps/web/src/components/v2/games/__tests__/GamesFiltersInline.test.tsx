/**
 * Wave B.1 (Issue #633) — GamesFiltersInline v2 component tests.
 *
 * Pure component (mirror Wave A.4): labels via prop, no `useTranslation`.
 *
 * Contract under test (spec §3.2 + plan §4.2):
 *   - Search: type=search, sr-only label, 300ms debounce trailing-edge
 *   - Status segmented: WAI-ARIA tablist (role="tablist" + role="tab" +
 *     aria-selected). Roving tabindex via `useTablistKeyboardNav`. Arrow
 *     Left/Right wrap, Home/End jump.
 *   - Sort: native <select> with <label>
 *   - View toggle: role="group" with 2 buttons + aria-pressed
 *   - Result count rendered
 *   - compact=true: view toggle hidden (mobile collapse)
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  GamesFiltersInline,
  type GamesFiltersInlineLabels,
  type GamesFiltersInlineProps,
} from '../GamesFiltersInline';

const labels: GamesFiltersInlineLabels = {
  search: {
    placeholder: 'Cerca per titolo, editore o anno…',
    ariaLabel: 'Cerca nella libreria',
    clearAriaLabel: 'Cancella la ricerca',
  },
  status: {
    label: 'Stato',
    options: {
      all: 'Tutti',
      owned: 'Posseduti',
      wishlist: 'Wishlist',
      played: 'Giocati',
    },
  },
  sort: {
    label: 'Ordina per',
    options: {
      'last-played': 'Aggiunti di recente',
      rating: 'Valutazione',
      title: 'Titolo',
      year: 'Anno',
    },
  },
  view: {
    label: 'Visualizzazione',
    options: { grid: 'Griglia', list: 'Lista' },
  },
  resultCount: (count: number) => `${count} risultati`,
};

function renderFilters(overrides: Partial<GamesFiltersInlineProps> = {}) {
  const props: GamesFiltersInlineProps = {
    labels,
    query: '',
    onQueryChange: vi.fn(),
    status: 'all',
    onStatusChange: vi.fn(),
    sort: 'last-played',
    onSortChange: vi.fn(),
    view: 'grid',
    onViewChange: vi.fn(),
    resultCount: 12,
    compact: false,
    ...overrides,
  };
  return { ...render(<GamesFiltersInline {...props} />), props };
}

describe('GamesFiltersInline (Wave B.1)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders search input as type=search with placeholder and sr-only label', () => {
    renderFilters();
    const input = screen.getByPlaceholderText(
      'Cerca per titolo, editore o anno…'
    ) as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.type).toBe('search');
    // The accessible name comes from the sr-only <label> wrapping the input.
    expect(screen.getByLabelText('Cerca nella libreria')).toBeInTheDocument();
  });

  it('debounces onQueryChange by 300ms (single trailing-edge call)', () => {
    const onQueryChange = vi.fn();
    renderFilters({ onQueryChange });
    const input = screen.getByLabelText('Cerca nella libreria') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'c' } });
    fireEvent.change(input, { target: { value: 'ca' } });
    fireEvent.change(input, { target: { value: 'cat' } });

    // No fire before 300ms.
    act(() => vi.advanceTimersByTime(299));
    expect(onQueryChange).not.toHaveBeenCalled();

    // After 300ms idle: single trailing call with last value.
    act(() => vi.advanceTimersByTime(1));
    expect(onQueryChange).toHaveBeenCalledTimes(1);
    expect(onQueryChange).toHaveBeenCalledWith('cat');
  });

  it('renders the status tablist with 4 tabs and aria-selected on the current key', () => {
    renderFilters({ status: 'owned' });
    const tablist = screen.getByRole('tablist', { name: 'Stato' });
    expect(tablist).toBeInTheDocument();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(4);
    const owned = screen.getByRole('tab', { name: 'Posseduti' });
    expect(owned).toHaveAttribute('aria-selected', 'true');
    const wishlist = screen.getByRole('tab', { name: 'Wishlist' });
    expect(wishlist).toHaveAttribute('aria-selected', 'false');
  });

  it('fires onStatusChange when a status tab is clicked', () => {
    const onStatusChange = vi.fn();
    renderFilters({ onStatusChange });
    fireEvent.click(screen.getByRole('tab', { name: 'Wishlist' }));
    expect(onStatusChange).toHaveBeenCalledTimes(1);
    expect(onStatusChange).toHaveBeenCalledWith('wishlist');
  });

  it('navigates status tabs with ArrowRight (wraps last → first)', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'played', onStatusChange });
    const playedTab = screen.getByRole('tab', { name: 'Giocati' });
    playedTab.focus();
    fireEvent.keyDown(playedTab, { key: 'ArrowRight' });
    expect(onStatusChange).toHaveBeenCalledWith('all');
  });

  it('navigates status tabs with ArrowLeft (wraps first → last)', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'all', onStatusChange });
    const allTab = screen.getByRole('tab', { name: 'Tutti' });
    allTab.focus();
    fireEvent.keyDown(allTab, { key: 'ArrowLeft' });
    expect(onStatusChange).toHaveBeenCalledWith('played');
  });

  it('jumps to first/last with Home/End on the status tablist', () => {
    const onStatusChange = vi.fn();
    renderFilters({ status: 'wishlist', onStatusChange });
    const wishlistTab = screen.getByRole('tab', { name: 'Wishlist' });
    wishlistTab.focus();
    fireEvent.keyDown(wishlistTab, { key: 'Home' });
    expect(onStatusChange).toHaveBeenLastCalledWith('all');
    fireEvent.keyDown(wishlistTab, { key: 'End' });
    expect(onStatusChange).toHaveBeenLastCalledWith('played');
  });

  it('renders the sort <select> with 4 options and fires onSortChange', () => {
    const onSortChange = vi.fn();
    renderFilters({ onSortChange });
    const select = screen.getByLabelText('Ordina per') as HTMLSelectElement;
    expect(select.tagName).toBe('SELECT');
    expect(select.options).toHaveLength(4);
    fireEvent.change(select, { target: { value: 'rating' } });
    expect(onSortChange).toHaveBeenCalledWith('rating');
  });

  it('renders the view toggle group with 2 buttons and aria-pressed reflecting current view', () => {
    renderFilters({ view: 'list' });
    const group = screen.getByRole('group', { name: 'Visualizzazione' });
    expect(group).toBeInTheDocument();
    const grid = screen.getByRole('button', { name: 'Griglia' });
    const list = screen.getByRole('button', { name: 'Lista' });
    expect(grid).toHaveAttribute('aria-pressed', 'false');
    expect(list).toHaveAttribute('aria-pressed', 'true');
  });

  it('fires onViewChange when a view button is clicked', () => {
    const onViewChange = vi.fn();
    renderFilters({ view: 'grid', onViewChange });
    fireEvent.click(screen.getByRole('button', { name: 'Lista' }));
    expect(onViewChange).toHaveBeenCalledWith('list');
  });

  it('hides the view toggle when compact=true (mobile collapse)', () => {
    renderFilters({ compact: true });
    expect(screen.queryByRole('group', { name: 'Visualizzazione' })).toBeNull();
  });

  it('renders the result count via labels.resultCount(n)', () => {
    renderFilters({ resultCount: 7 });
    expect(screen.getByText('7 risultati')).toBeInTheDocument();
  });

  it('exposes a clear-search button when query is non-empty (calls onQueryChange("") immediately)', () => {
    const onQueryChange = vi.fn();
    renderFilters({ query: 'catan', onQueryChange });
    const clearBtn = screen.getByRole('button', { name: 'Cancella la ricerca' });
    fireEvent.click(clearBtn);
    // Clear is immediate (no debounce).
    expect(onQueryChange).toHaveBeenCalledWith('');
  });

  it('does not render the clear-search button when query is empty', () => {
    renderFilters({ query: '' });
    expect(screen.queryByRole('button', { name: 'Cancella la ricerca' })).toBeNull();
  });
});

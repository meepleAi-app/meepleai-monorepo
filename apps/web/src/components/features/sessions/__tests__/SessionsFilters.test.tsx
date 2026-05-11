/**
 * SessionsFilters unit tests — Wave D.1 (Issue #735).
 *
 * 10 tests:
 * 1. Renders data-slot="sessions-filters"
 * 2. Renders all 4 status filter chips
 * 3. Active chip has aria-selected=true
 * 4. Clicking a chip fires onStatusChange with correct value
 * 5. Search input renders with aria-label
 * 6. Search input fires onSearchChange on change
 * 7. View list button renders with aria-pressed=true when view=list
 * 8. View grid button renders with aria-pressed=true when view=grid
 * 9. Clicking grid button fires onViewChange('grid')
 * 10. Status tablist has role="tablist" and aria-orientation="horizontal"
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SessionsFilters } from '../SessionsFilters';
import type { SessionsFiltersProps } from '../SessionsFilters';

const LABELS: SessionsFiltersProps['labels'] = {
  statusAll: 'Tutti',
  statusActive: 'In corso',
  statusCompleted: 'Completate',
  statusAbandoned: 'Abbandonate',
  searchPlaceholder: 'Cerca partita...',
  viewList: 'Vista lista',
  viewGrid: 'Vista griglia',
  statusGroupLabel: 'Filtra per stato',
  viewToggleLabel: 'Modalità visualizzazione',
  searchAriaLabel: 'Cerca partita o gioco',
};

const DEFAULT_PROPS: SessionsFiltersProps = {
  statusFilter: 'all',
  onStatusChange: vi.fn(),
  view: 'list',
  onViewChange: vi.fn(),
  search: '',
  onSearchChange: vi.fn(),
  labels: LABELS,
};

describe('SessionsFilters', () => {
  it('renders data-slot="sessions-filters"', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="sessions-filters"]')).not.toBeNull();
  });

  it('renders all 4 status filter chips', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} />);
    expect(screen.getByText('Tutti')).toBeTruthy();
    expect(screen.getByText('In corso')).toBeTruthy();
    expect(screen.getByText('Completate')).toBeTruthy();
    expect(screen.getByText('Abbandonate')).toBeTruthy();
  });

  it('active chip has aria-selected="true"', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} statusFilter="completed" />);
    const chips = document.querySelectorAll('[data-slot="sessions-filter-chip"]');
    const activeChip = Array.from(chips).find(c => c.getAttribute('data-status') === 'completed');
    expect(activeChip!.getAttribute('aria-selected')).toBe('true');
  });

  it('clicking a chip fires onStatusChange with the correct value', () => {
    const onStatusChange = vi.fn();
    render(<SessionsFilters {...DEFAULT_PROPS} onStatusChange={onStatusChange} />);
    fireEvent.click(screen.getByText('In corso'));
    expect(onStatusChange).toHaveBeenCalledWith('active');
  });

  it('search input renders with correct aria-label', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} />);
    const input = document.querySelector('[data-slot="sessions-filters-search"]');
    expect(input).not.toBeNull();
    expect(input!.getAttribute('aria-label')).toBe('Cerca partita o gioco');
  });

  it('search input fires onSearchChange on change', () => {
    const onSearchChange = vi.fn();
    render(<SessionsFilters {...DEFAULT_PROPS} onSearchChange={onSearchChange} />);
    const input = document.querySelector(
      '[data-slot="sessions-filters-search"]'
    ) as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'wingspan' } });
    expect(onSearchChange).toHaveBeenCalledWith('wingspan');
  });

  it('list view button has aria-pressed="true" when view=list', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} view="list" />);
    const listBtn = document.querySelector('[data-slot="sessions-view-list"]');
    expect(listBtn!.getAttribute('aria-pressed')).toBe('true');
  });

  it('grid view button has aria-pressed="true" when view=grid', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} view="grid" />);
    const gridBtn = document.querySelector('[data-slot="sessions-view-grid"]');
    expect(gridBtn!.getAttribute('aria-pressed')).toBe('true');
  });

  it('clicking grid button fires onViewChange("grid")', () => {
    const onViewChange = vi.fn();
    render(<SessionsFilters {...DEFAULT_PROPS} onViewChange={onViewChange} />);
    const gridBtn = document.querySelector('[data-slot="sessions-view-grid"]') as HTMLButtonElement;
    fireEvent.click(gridBtn);
    expect(onViewChange).toHaveBeenCalledWith('grid');
  });

  it('status tablist has role="tablist" and aria-orientation="horizontal"', () => {
    render(<SessionsFilters {...DEFAULT_PROPS} />);
    const tablist = document.querySelector('[role="tablist"]');
    expect(tablist).not.toBeNull();
    expect(tablist!.getAttribute('aria-orientation')).toBe('horizontal');
    expect(tablist!.getAttribute('aria-label')).toBe('Filtra per stato');
  });
});

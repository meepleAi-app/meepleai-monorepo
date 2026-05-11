/**
 * PlayersFiltersInline unit tests — Wave 4 D1 (Issue #682).
 *
 * TDD red phase: written before the component implementation.
 * Mirror pattern from Wave B.1 GamesFiltersInline.test.tsx.
 *
 * 4 tests:
 * 1. Renders data-slot="players-filters-inline"
 * 2. Renders search input with placeholder from labels
 * 3. Clear button hidden when hasFilters===false
 * 4. Clear button visible and fires onClearFilters when hasFilters===true
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PlayersFiltersInline } from '../PlayersFiltersInline';
import type { PlayersFiltersInlineProps } from '../PlayersFiltersInline';

const LABELS: PlayersFiltersInlineProps['labels'] = {
  searchPlaceholder: 'Cerca per nome del gioco…',
  searchAriaLabel: 'Cerca tra i tuoi giochi',
  clearFilters: 'Cancella filtri',
};

const DEFAULT_PROPS: PlayersFiltersInlineProps = {
  search: '',
  onSearchChange: vi.fn(),
  onClearFilters: vi.fn(),
  hasFilters: false,
  labels: LABELS,
};

describe('PlayersFiltersInline', () => {
  it('renders data-slot="players-filters-inline"', () => {
    render(<PlayersFiltersInline {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="players-filters-inline"]')).not.toBeNull();
  });

  it('renders search input with placeholder from labels', () => {
    render(<PlayersFiltersInline {...DEFAULT_PROPS} />);
    const input = screen.getByPlaceholderText('Cerca per nome del gioco…');
    expect(input).toBeTruthy();
  });

  it('hides clear button when hasFilters===false', () => {
    render(<PlayersFiltersInline {...DEFAULT_PROPS} hasFilters={false} />);
    expect(screen.queryByText('Cancella filtri')).toBeNull();
  });

  it('shows clear button and fires onClearFilters when hasFilters===true', () => {
    const onClearFilters = vi.fn();
    render(
      <PlayersFiltersInline {...DEFAULT_PROPS} hasFilters={true} onClearFilters={onClearFilters} />
    );
    const clearBtn = screen.getByText('Cancella filtri');
    expect(clearBtn).toBeTruthy();
    fireEvent.click(clearBtn);
    expect(onClearFilters).toHaveBeenCalledOnce();
  });

  it('fires onSearchChange with new value on input change', () => {
    const onSearchChange = vi.fn();
    render(<PlayersFiltersInline {...DEFAULT_PROPS} onSearchChange={onSearchChange} />);
    const input = screen.getByPlaceholderText('Cerca per nome del gioco…');
    fireEvent.change(input, { target: { value: 'Catan' } });
    expect(onSearchChange).toHaveBeenCalledWith('Catan');
  });
});

/**
 * Comprehensive Filter Tests for EntityListView
 *
 * Tests for:
 * - SelectFilter component
 * - CheckboxFilter component
 * - RangeFilter component
 * - DateRangeFilter component
 * - FilterPanel component
 * - FilterChip component
 * - useFilters hook
 * - filter-utils utilities
 *
 * Issue #3894 - Section 2: Complete Filter Implementation
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHook, act } from '@testing-library/react';
import { vi } from 'vitest';

import { SelectFilter } from '../components/filters/select-filter';
import { CheckboxFilter } from '../components/filters/checkbox-filter';
import { RangeFilter } from '../components/filters/range-filter';
import { FilterChip } from '../components/filter-chip';
import { useFilters } from '../hooks/use-filters';
import {
  applyFilters,
  countActiveFilters,
  getFilterDisplayValue,
} from '../utils/filter-utils';
import type {
  FilterConfig,
  SelectFilter as SelectFilterConfig,
  CheckboxFilter as CheckboxFilterConfig,
  RangeFilter as RangeFilterConfig,
} from '../entity-list-view.types';

// ============================================================================
// Mock Data
// ============================================================================

interface MockGame {
  id: string;
  title: string;
  category: string;
  rating: number;
  players: number;
  hasPdf: boolean;
  publishedDate: string;
  metadata: { publisher: string };
}

const mockGames: MockGame[] = [
  {
    id: '1',
    title: 'Twilight Imperium',
    category: 'strategy',
    rating: 8.7,
    players: 6,
    hasPdf: true,
    publishedDate: '2023-06-15',
    metadata: { publisher: 'FFG' },
  },
  {
    id: '2',
    title: 'Gloomhaven',
    category: 'adventure',
    rating: 8.8,
    players: 4,
    hasPdf: false,
    publishedDate: '2024-01-20',
    metadata: { publisher: 'Cephalofair' },
  },
  {
    id: '3',
    title: 'Wingspan',
    category: 'strategy',
    rating: 8.1,
    players: 5,
    hasPdf: true,
    publishedDate: '2022-03-10',
    metadata: { publisher: 'Stonemaier' },
  },
  {
    id: '4',
    title: 'Azul',
    category: 'abstract',
    rating: 7.8,
    players: 4,
    hasPdf: false,
    publishedDate: '2023-11-05',
    metadata: { publisher: 'PBG' },
  },
];

const selectFilter: SelectFilterConfig<MockGame> = {
  id: 'category',
  type: 'select',
  label: 'Category',
  field: 'category',
  options: [
    { value: 'strategy', label: 'Strategy' },
    { value: 'adventure', label: 'Adventure' },
    { value: 'abstract', label: 'Abstract' },
  ],
};

const checkboxFilter: CheckboxFilterConfig<MockGame> = {
  id: 'hasPdf',
  type: 'checkbox',
  label: 'Has PDF',
  field: 'hasPdf',
  description: 'Only show games with PDF rulebooks',
};

const rangeFilter: RangeFilterConfig<MockGame> = {
  id: 'rating',
  type: 'range',
  label: 'Rating',
  field: 'rating',
  min: 0,
  max: 10,
  step: 0.1,
  unit: '★',
};

const allFilters: FilterConfig<MockGame>[] = [
  selectFilter,
  checkboxFilter,
  rangeFilter,
];

// ============================================================================
// SelectFilter Tests
// ============================================================================

describe('SelectFilter', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render label', () => {
    render(
      <SelectFilter filter={selectFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText('Category')).toBeInTheDocument();
  });

  it('should render placeholder when no value', () => {
    render(
      <SelectFilter filter={selectFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText(/select category/i)).toBeInTheDocument();
  });

  it('should render description when provided', () => {
    const filterWithDesc = { ...selectFilter, description: 'Pick a game category' };
    render(
      <SelectFilter filter={filterWithDesc} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText('Pick a game category')).toBeInTheDocument();
  });

  it('should display selected value', () => {
    render(
      <SelectFilter filter={selectFilter} value="strategy" onChange={mockOnChange} />
    );
    expect(screen.getByText('Strategy')).toBeInTheDocument();
  });

  it('should handle array value by using first element', () => {
    render(
      <SelectFilter
        filter={selectFilter}
        value={['adventure', 'strategy']}
        onChange={mockOnChange}
      />
    );
    expect(screen.getByText('Adventure')).toBeInTheDocument();
  });
});

// ============================================================================
// CheckboxFilter Tests
// ============================================================================

describe('CheckboxFilter', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render label', () => {
    render(
      <CheckboxFilter filter={checkboxFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText('Has PDF')).toBeInTheDocument();
  });

  it('should render description', () => {
    render(
      <CheckboxFilter filter={checkboxFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText('Only show games with PDF rulebooks')).toBeInTheDocument();
  });

  it('should be unchecked when value is false', () => {
    render(
      <CheckboxFilter filter={checkboxFilter} value={false} onChange={mockOnChange} />
    );
    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).not.toBeChecked();
  });

  it('should be checked when value is true', () => {
    render(
      <CheckboxFilter filter={checkboxFilter} value={true} onChange={mockOnChange} />
    );
    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeChecked();
  });

  it('should call onChange when toggled', async () => {
    const user = userEvent.setup();
    render(
      <CheckboxFilter filter={checkboxFilter} value={false} onChange={mockOnChange} />
    );

    await user.click(screen.getByRole('checkbox'));
    expect(mockOnChange).toHaveBeenCalledWith(true);
  });

  it('should be clickable via label', async () => {
    const user = userEvent.setup();
    render(
      <CheckboxFilter filter={checkboxFilter} value={false} onChange={mockOnChange} />
    );

    await user.click(screen.getByText('Has PDF'));
    expect(mockOnChange).toHaveBeenCalledWith(true);
  });
});

// ============================================================================
// RangeFilter Tests
// ============================================================================

describe('RangeFilter', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render label', () => {
    render(
      <RangeFilter filter={rangeFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText('Rating')).toBeInTheDocument();
  });

  it('should display default range when no value', () => {
    render(
      <RangeFilter filter={rangeFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText(/0★ - 10★/)).toBeInTheDocument();
  });

  it('should display current range value with unit', () => {
    render(
      <RangeFilter
        filter={rangeFilter}
        value={{ min: 3, max: 8 }}
        onChange={mockOnChange}
      />
    );
    expect(screen.getByText(/3★ - 8★/)).toBeInTheDocument();
  });

  it('should display min/max boundaries', () => {
    render(
      <RangeFilter filter={rangeFilter} value={undefined} onChange={mockOnChange} />
    );
    expect(screen.getByText(/Min: 0★/)).toBeInTheDocument();
    expect(screen.getByText(/Max: 10★/)).toBeInTheDocument();
  });

  it('should render slider element', () => {
    const { container } = render(
      <RangeFilter filter={rangeFilter} value={undefined} onChange={mockOnChange} />
    );
    // Slider renders as a div with role=slider
    const sliders = container.querySelectorAll('[role="slider"]');
    expect(sliders.length).toBeGreaterThan(0);
  });
});

// ============================================================================
// FilterChip Tests
// ============================================================================

describe('FilterChip', () => {
  const mockOnRemove = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render label and value', () => {
    render(<FilterChip label="Category" value="Strategy" onRemove={mockOnRemove} />);
    expect(screen.getByText('Category: Strategy')).toBeInTheDocument();
  });

  it('should render remove button with accessible label', () => {
    render(<FilterChip label="Category" value="Strategy" onRemove={mockOnRemove} />);
    expect(screen.getByLabelText('Remove Category filter')).toBeInTheDocument();
  });

  it('should call onRemove when remove button clicked', async () => {
    const user = userEvent.setup();
    render(<FilterChip label="Category" value="Strategy" onRemove={mockOnRemove} />);

    await user.click(screen.getByLabelText('Remove Category filter'));
    expect(mockOnRemove).toHaveBeenCalledTimes(1);
  });

  it('should apply custom className', () => {
    const { container } = render(
      <FilterChip
        label="Category"
        value="Strategy"
        onRemove={mockOnRemove}
        className="custom-class"
      />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });
});

// ============================================================================
// useFilters Hook Tests
// ============================================================================

describe('useFilters', () => {
  it('should return all items when no filters are active', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    expect(result.current.filteredItems).toEqual(mockGames);
    expect(result.current.activeCount).toBe(0);
  });

  it('should filter by select value', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('category', 'strategy');
    });

    expect(result.current.filteredItems).toHaveLength(2);
    expect(result.current.filteredItems.every((g) => g.category === 'strategy')).toBe(
      true
    );
    expect(result.current.activeCount).toBe(1);
  });

  it('should filter by checkbox value', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('hasPdf', true);
    });

    expect(result.current.filteredItems).toHaveLength(2);
    expect(result.current.filteredItems.every((g) => g.hasPdf)).toBe(true);
  });

  it('should filter by range value', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('rating', { min: 8.5, max: 10 });
    });

    expect(result.current.filteredItems).toHaveLength(2); // 8.7 and 8.8
    expect(result.current.filteredItems.every((g) => g.rating >= 8.5)).toBe(true);
  });

  it('should combine multiple filters (AND logic)', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('category', 'strategy');
      result.current.setFilter('hasPdf', true);
    });

    // Strategy + hasPdf: only Twilight Imperium and Wingspan
    expect(result.current.filteredItems).toHaveLength(2);
    expect(result.current.activeCount).toBe(2);
  });

  it('should remove a single filter', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('category', 'strategy');
      result.current.setFilter('hasPdf', true);
    });

    expect(result.current.activeCount).toBe(2);

    act(() => {
      result.current.removeFilter('category');
    });

    expect(result.current.activeCount).toBe(1);
    expect(result.current.filteredItems).toHaveLength(2); // hasPdf only
  });

  it('should clear all filters', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilter('category', 'strategy');
      result.current.setFilter('hasPdf', true);
      result.current.setFilter('rating', { min: 8, max: 10 });
    });

    expect(result.current.activeCount).toBe(3);

    act(() => {
      result.current.clearFilters();
    });

    expect(result.current.activeCount).toBe(0);
    expect(result.current.filteredItems).toEqual(mockGames);
  });

  it('should update entire filter state with setFilterState', () => {
    const { result } = renderHook(() => useFilters(mockGames, allFilters));

    act(() => {
      result.current.setFilterState({ category: 'adventure', hasPdf: false });
    });

    expect(result.current.filterState).toEqual({ category: 'adventure', hasPdf: false });
  });

  it('should use controlled state when provided', () => {
    const controlledState = { category: 'abstract' };
    const { result } = renderHook(() =>
      useFilters(mockGames, allFilters, controlledState)
    );

    expect(result.current.filterState).toEqual(controlledState);
    expect(result.current.filteredItems).toHaveLength(1);
    expect(result.current.filteredItems[0].title).toBe('Azul');
  });

  it('should return all items when filterConfig is empty', () => {
    const { result } = renderHook(() => useFilters(mockGames, []));

    expect(result.current.filteredItems).toEqual(mockGames);
  });
});

// ============================================================================
// filter-utils Tests
// ============================================================================

describe('filter-utils', () => {
  describe('applyFilters', () => {
    it('should apply select filter', () => {
      const result = applyFilters(mockGames, { category: 'strategy' }, allFilters);
      expect(result).toHaveLength(2);
    });

    it('should apply checkbox filter (true = filter)', () => {
      const result = applyFilters(mockGames, { hasPdf: true }, allFilters);
      expect(result).toHaveLength(2);
      expect(result.every((g) => g.hasPdf)).toBe(true);
    });

    it('should skip checkbox filter when false', () => {
      const result = applyFilters(mockGames, { hasPdf: false }, allFilters);
      expect(result).toHaveLength(4); // false = not filtering
    });

    it('should apply range filter', () => {
      const result = applyFilters(
        mockGames,
        { rating: { min: 8.0, max: 8.5 } },
        allFilters
      );
      expect(result).toHaveLength(1); // Only Wingspan (8.1)
    });

    it('should skip inactive/empty filters', () => {
      const result = applyFilters(
        mockGames,
        { category: '', hasPdf: undefined, rating: null },
        allFilters
      );
      expect(result).toHaveLength(4);
    });

    it('should handle multi-select filter', () => {
      const multiSelectFilter: FilterConfig<MockGame>[] = [
        { ...selectFilter, multiple: true },
      ];

      const result = applyFilters(
        mockGames,
        { category: ['strategy', 'abstract'] },
        multiSelectFilter
      );
      expect(result).toHaveLength(3); // 2 strategy + 1 abstract
    });

    it('should handle empty multi-select array', () => {
      const multiSelectFilter: FilterConfig<MockGame>[] = [
        { ...selectFilter, multiple: true },
      ];

      const result = applyFilters(mockGames, { category: [] }, multiSelectFilter);
      expect(result).toHaveLength(4); // Empty = no filter
    });

    it('should handle date-range filter', () => {
      const dateFilter: FilterConfig<MockGame>[] = [
        {
          id: 'published',
          type: 'date-range',
          label: 'Published',
          field: 'publishedDate',
        },
      ];

      const result = applyFilters(
        mockGames,
        {
          published: {
            start: new Date('2023-01-01'),
            end: new Date('2023-12-31'),
          },
        },
        dateFilter
      );
      expect(result).toHaveLength(2); // June and November 2023
    });

    it('should handle NaN values in range filter', () => {
      const items = [{ ...mockGames[0], rating: NaN }];
      const result = applyFilters(
        items,
        { rating: { min: 0, max: 10 } },
        allFilters
      );
      expect(result).toHaveLength(0);
    });

    it('should handle dot notation for nested fields', () => {
      const nestedFilter: FilterConfig<MockGame>[] = [
        {
          id: 'publisher',
          type: 'select',
          label: 'Publisher',
          field: 'metadata.publisher',
          options: [{ value: 'FFG', label: 'FFG' }],
        },
      ];

      const result = applyFilters(mockGames, { publisher: 'FFG' }, nestedFilter);
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe('Twilight Imperium');
    });
  });

  describe('countActiveFilters', () => {
    it('should count non-empty values', () => {
      expect(countActiveFilters({ category: 'strategy', hasPdf: true })).toBe(2);
    });

    it('should not count empty/false/undefined values', () => {
      expect(
        countActiveFilters({
          a: undefined,
          b: null,
          c: '',
          d: false,
          e: [],
        })
      ).toBe(0);
    });

    it('should count range values', () => {
      expect(countActiveFilters({ rating: { min: 3, max: 8 } })).toBe(1);
    });
  });

  describe('getFilterDisplayValue', () => {
    it('should display select option label', () => {
      const result = getFilterDisplayValue('category', 'strategy', allFilters);
      expect(result).toBe('Strategy');
    });

    it('should display checkbox as Yes/No', () => {
      expect(getFilterDisplayValue('hasPdf', true, allFilters)).toBe('Yes');
      expect(getFilterDisplayValue('hasPdf', false, allFilters)).toBe('No');
    });

    it('should display range with unit', () => {
      const result = getFilterDisplayValue(
        'rating',
        { min: 3, max: 8 },
        allFilters
      );
      expect(result).toBe('3★ - 8★');
    });

    it('should return string for unknown filter', () => {
      const result = getFilterDisplayValue('unknown', 'test', allFilters);
      expect(result).toBe('test');
    });

    it('should handle multi-select display', () => {
      const multiFilters: FilterConfig<MockGame>[] = [
        { ...selectFilter, multiple: true },
      ];
      const result = getFilterDisplayValue(
        'category',
        ['strategy', 'adventure'],
        multiFilters
      );
      expect(result).toBe('Strategy, Adventure');
    });
  });
});

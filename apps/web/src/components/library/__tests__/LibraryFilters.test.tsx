/**
 * LibraryFilters Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Search input with debounce
 * - Filter chips (All, Favorites, State filters)
 * - Sort dropdown
 * - Clear filters button
 * - State counts display
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { LibraryFilters, type LibraryFiltersProps } from '../LibraryFilters';

describe('LibraryFilters', () => {
  const mockOnSearchChange = vi.fn();
  const mockOnFavoritesChange = vi.fn();
  const mockOnStateFilterChange = vi.fn();
  const mockOnSortChange = vi.fn();
  const mockOnClearFilters = vi.fn();

  const defaultProps: LibraryFiltersProps = {
    onSearchChange: mockOnSearchChange,
    onFavoritesChange: mockOnFavoritesChange,
    onStateFilterChange: mockOnStateFilterChange,
    onSortChange: mockOnSortChange,
    onClearFilters: mockOnClearFilters,
  };

  const defaultStateCounts = {
    total: 25,
    favorites: 5,
    nuovo: 3,
    inPrestito: 2,
    wishlist: 8,
    owned: 12,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders search input', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.getByLabelText('Search library')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Cerca per titolo...')).toBeInTheDocument();
    });

    it('renders sort dropdown', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.getByLabelText('Sort library')).toBeInTheDocument();
    });

    it('renders all filter chips', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.getByTestId('filter-chip-all')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-favorites')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-nuovo')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-inprestito')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-wishlist')).toBeInTheDocument();
      expect(screen.getByTestId('filter-chip-owned')).toBeInTheDocument();
    });

    it('renders state counts when provided', () => {
      render(<LibraryFilters {...defaultProps} stateCounts={defaultStateCounts} />);

      expect(screen.getByText('25')).toBeInTheDocument(); // total
      expect(screen.getByText('5')).toBeInTheDocument(); // favorites
      expect(screen.getByText('3')).toBeInTheDocument(); // nuovo
    });

    it('does not render clear button without active filters', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.queryByText('Pulisci Filtri')).not.toBeInTheDocument();
    });

    it('renders clear button with active search', () => {
      render(<LibraryFilters {...defaultProps} searchQuery="test" />);

      expect(screen.getByText('Pulisci Filtri')).toBeInTheDocument();
    });

    it('renders clear button with active favorites filter', () => {
      render(<LibraryFilters {...defaultProps} favoritesOnly={true} />);

      expect(screen.getByText('Pulisci Filtri')).toBeInTheDocument();
    });
  });

  describe('Search', () => {
    it('shows current search query', () => {
      render(<LibraryFilters {...defaultProps} searchQuery="Chess" />);

      expect(screen.getByDisplayValue('Chess')).toBeInTheDocument();
    });

    it('shows clear button when search has value', () => {
      render(<LibraryFilters {...defaultProps} searchQuery="Chess" />);

      expect(screen.getByLabelText('Clear search')).toBeInTheDocument();
    });

    it('clears search when clear button clicked', () => {
      render(<LibraryFilters {...defaultProps} searchQuery="Chess" />);

      fireEvent.click(screen.getByLabelText('Clear search'));

      expect(mockOnSearchChange).toHaveBeenCalledWith('');
    });

    it('debounces search input', async () => {
      vi.useFakeTimers();
      render(<LibraryFilters {...defaultProps} />);

      const searchInput = screen.getByLabelText('Search library');
      fireEvent.change(searchInput, { target: { value: 'test' } });

      // Should not have called immediately
      expect(mockOnSearchChange).not.toHaveBeenCalled();

      // Advance past debounce
      vi.advanceTimersByTime(350);

      expect(mockOnSearchChange).toHaveBeenCalledWith('test');

      vi.useRealTimers();
    });
  });

  describe('Filter Chips', () => {
    it('shows "All" as active by default', () => {
      render(<LibraryFilters {...defaultProps} />);

      const allChip = screen.getByTestId('filter-chip-all');
      expect(allChip).toHaveAttribute('aria-pressed', 'true');
    });

    it('toggles favorites filter when clicked', () => {
      render(<LibraryFilters {...defaultProps} />);

      fireEvent.click(screen.getByTestId('filter-chip-favorites'));

      expect(mockOnFavoritesChange).toHaveBeenCalledWith(true);
    });

    it('clears favorites when clicking again', () => {
      render(<LibraryFilters {...defaultProps} favoritesOnly={true} />);

      fireEvent.click(screen.getByTestId('filter-chip-favorites'));

      expect(mockOnFavoritesChange).toHaveBeenCalledWith(false);
    });

    it('adds state filter when chip clicked', () => {
      render(<LibraryFilters {...defaultProps} />);

      fireEvent.click(screen.getByTestId('filter-chip-nuovo'));

      expect(mockOnStateFilterChange).toHaveBeenCalledWith(['Nuovo']);
      expect(mockOnFavoritesChange).toHaveBeenCalledWith(false);
    });

    it('removes state filter when chip clicked again', () => {
      render(<LibraryFilters {...defaultProps} stateFilter={['Nuovo']} />);

      fireEvent.click(screen.getByTestId('filter-chip-nuovo'));

      expect(mockOnStateFilterChange).toHaveBeenCalledWith([]);
    });

    it('clears all filters when "All" clicked', () => {
      render(<LibraryFilters {...defaultProps} favoritesOnly={true} stateFilter={['Nuovo']} />);

      fireEvent.click(screen.getByTestId('filter-chip-all'));

      expect(mockOnFavoritesChange).toHaveBeenCalledWith(false);
      expect(mockOnStateFilterChange).toHaveBeenCalledWith([]);
    });

    it('shows correct aria-pressed for active chips', () => {
      render(
        <LibraryFilters {...defaultProps} stateFilter={['Nuovo', 'Wishlist']} />
      );

      expect(screen.getByTestId('filter-chip-all')).toHaveAttribute('aria-pressed', 'false');
      expect(screen.getByTestId('filter-chip-nuovo')).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByTestId('filter-chip-wishlist')).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByTestId('filter-chip-owned')).toHaveAttribute('aria-pressed', 'false');
    });
  });

  describe('Sort', () => {
    it('shows default sort value', () => {
      render(<LibraryFilters {...defaultProps} />);

      // Default is addedAt-desc
      expect(screen.getByText('Più recenti')).toBeInTheDocument();
    });

    it('renders sort dropdown with label', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.getByLabelText('Sort library')).toBeInTheDocument();
      expect(screen.getByText('Ordina')).toBeInTheDocument();
    });
  });

  describe('Clear Filters', () => {
    it('calls onClearFilters when button clicked', () => {
      render(<LibraryFilters {...defaultProps} searchQuery="test" />);

      fireEvent.click(screen.getByText('Pulisci Filtri'));

      expect(mockOnClearFilters).toHaveBeenCalledTimes(1);
    });
  });

  describe('Labels', () => {
    it('displays Italian labels', () => {
      render(<LibraryFilters {...defaultProps} />);

      expect(screen.getByText('Cerca Giochi')).toBeInTheDocument();
      expect(screen.getByText('Ordina')).toBeInTheDocument();
      expect(screen.getByText('Tutti')).toBeInTheDocument();
      expect(screen.getByText('Preferiti')).toBeInTheDocument();
      expect(screen.getByText('Nuovo')).toBeInTheDocument();
      expect(screen.getByText('In Prestito')).toBeInTheDocument();
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
      expect(screen.getByText('Posseduto')).toBeInTheDocument();
    });
  });

  describe('Custom className', () => {
    it('applies custom className', () => {
      const { container } = render(
        <LibraryFilters {...defaultProps} className="my-custom-class" />
      );

      expect(container.firstChild).toHaveClass('my-custom-class');
    });
  });
});

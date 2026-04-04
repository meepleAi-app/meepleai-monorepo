/**
 * LibraryFilters Component Tests (Issue #2464)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { LibraryFilters } from '@/components/library/LibraryFilters';

describe('LibraryFilters', () => {
  const mockProps = {
    searchQuery: '',
    onSearchChange: vi.fn(),
    favoritesOnly: false,
    onFavoritesChange: vi.fn(),
    stateFilter: [],
    onStateFilterChange: vi.fn(),
    sortBy: 'addedAt' as const,
    sortDescending: true,
    onSortChange: vi.fn(),
    onClearFilters: vi.fn(),
  };

  it('should render search input', () => {
    render(<LibraryFilters {...mockProps} />);
    expect(screen.getByPlaceholderText('Cerca...')).toBeInTheDocument();
  });

  it('should render favorites toggle', () => {
    render(<LibraryFilters {...mockProps} />);
    // Component now uses filter chips instead of toggle
    expect(screen.getByTestId('filter-chip-favorites')).toBeInTheDocument();
  });

  it('should render sort dropdown', () => {
    render(<LibraryFilters {...mockProps} />);
    expect(screen.getByLabelText('Sort library')).toBeInTheDocument();
  });

  it('should call onSearchChange after debounce', async () => {
    render(<LibraryFilters {...mockProps} />);
    const input = screen.getByPlaceholderText('Cerca...');

    fireEvent.change(input, { target: { value: 'Catan' } });

    await waitFor(
      () => {
        expect(mockProps.onSearchChange).toHaveBeenCalledWith('Catan');
      },
      { timeout: 500 }
    );
  });

  it('should call onFavoritesChange when toggle clicked', () => {
    render(<LibraryFilters {...mockProps} />);
    // Component now uses filter chips instead of toggle
    const favoritesChip = screen.getByTestId('filter-chip-favorites');

    fireEvent.click(favoritesChip);

    expect(mockProps.onFavoritesChange).toHaveBeenCalledWith(true);
  });

  it('should show clear filters button when filters are active', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    // The clear button shows "Pulisci" on sm+ screens and an icon on mobile.
    // In test (no breakpoint), the button with X icon is rendered.
    expect(screen.getByLabelText('Clear search')).toBeInTheDocument();
  });

  it('should call onClearFilters when clear button clicked', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    // The clear-all button renders "Pulisci" text (hidden on mobile via sr class).
    // Find by text content which is present in DOM even if visually hidden.
    const clearFilterBtn = screen.getByText('Pulisci');
    fireEvent.click(clearFilterBtn);
    expect(mockProps.onClearFilters).toHaveBeenCalled();
  });

  it('should clear search input when X button clicked', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    const clearButton = screen.getByLabelText('Clear search');

    fireEvent.click(clearButton);

    expect(mockProps.onSearchChange).toHaveBeenCalledWith('');
  });
});

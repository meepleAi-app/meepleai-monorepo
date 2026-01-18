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
    sortBy: 'addedAt' as const,
    sortDescending: true,
    onSortChange: vi.fn(),
    onClearFilters: vi.fn(),
  };

  it('should render search input', () => {
    render(<LibraryFilters {...mockProps} />);
    expect(screen.getByPlaceholderText('Cerca per titolo...')).toBeInTheDocument();
  });

  it('should render favorites toggle', () => {
    render(<LibraryFilters {...mockProps} />);
    expect(screen.getByLabelText('Solo Preferiti')).toBeInTheDocument();
  });

  it('should render sort dropdown', () => {
    render(<LibraryFilters {...mockProps} />);
    expect(screen.getByLabelText('Sort library')).toBeInTheDocument();
  });

  it('should call onSearchChange after debounce', async () => {
    render(<LibraryFilters {...mockProps} />);
    const input = screen.getByPlaceholderText('Cerca per titolo...');

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
    const toggle = screen.getByRole('switch');

    fireEvent.click(toggle);

    expect(mockProps.onFavoritesChange).toHaveBeenCalledWith(true);
  });

  it('should show clear filters button when filters are active', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    expect(screen.getByText('Pulisci Filtri')).toBeInTheDocument();
  });

  it('should call onClearFilters when clear button clicked', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    const clearButton = screen.getByText('Pulisci Filtri');

    fireEvent.click(clearButton);

    expect(mockProps.onClearFilters).toHaveBeenCalled();
  });

  it('should clear search input when X button clicked', () => {
    render(<LibraryFilters {...mockProps} searchQuery="test" />);
    const clearButton = screen.getByLabelText('Clear search');

    fireEvent.click(clearButton);

    expect(mockProps.onSearchChange).toHaveBeenCalledWith('');
  });
});

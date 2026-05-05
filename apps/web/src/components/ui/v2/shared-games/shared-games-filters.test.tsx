import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  SharedGamesFilters,
  type SharedGamesFiltersLabels,
  type SharedGamesFiltersProps,
} from './shared-games-filters';

const labels: SharedGamesFiltersLabels = {
  searchPlaceholder: 'Cerca un gioco…',
  searchAriaLabel: 'Cerca un gioco',
  genreSelectAriaLabel: 'Filtra per genere',
  sortSelectAriaLabel: 'Ordina i giochi',
  resultCount: (shown, total) => `${shown} di ${total} giochi`,
  totalCount: total => `${total} giochi`,
  clearSearchAriaLabel: 'Cancella ricerca',
};

function build(overrides: Partial<SharedGamesFiltersProps> = {}): SharedGamesFiltersProps {
  return {
    searchValue: '',
    onSearchChange: vi.fn(),
    chips: [
      { key: 'with-toolkit', label: 'Con toolkit' },
      { key: 'with-agent', label: 'Con agente' },
      { key: 'top-rated', label: 'Top rated' },
      { key: 'new', label: 'Nuovi' },
    ],
    activeChips: new Set(),
    onChipToggle: vi.fn(),
    genreOptions: [
      { value: 'all', label: 'Tutti i generi' },
      { value: 'strategy', label: 'Strategia' },
    ],
    genreValue: 'all',
    onGenreChange: vi.fn(),
    sortOptions: [
      { value: 'rating', label: 'Per voto' },
      { value: 'contrib', label: 'Più contributori' },
    ],
    sortValue: 'rating',
    onSortChange: vi.fn(),
    shown: 12,
    total: 50,
    hasActiveFilters: false,
    labels,
    ...overrides,
  };
}

describe('SharedGamesFilters (v2)', () => {
  it('renders the search input with placeholder and aria-label', () => {
    render(<SharedGamesFilters {...build()} />);
    const input = screen.getByLabelText('Cerca un gioco');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('placeholder', 'Cerca un gioco…');
  });

  it('emits onSearchChange when the user types', () => {
    const onSearchChange = vi.fn();
    render(<SharedGamesFilters {...build({ onSearchChange })} />);
    fireEvent.change(screen.getByLabelText('Cerca un gioco'), { target: { value: 'catan' } });
    expect(onSearchChange).toHaveBeenCalledWith('catan');
  });

  it('shows clear button only when searchValue is non-empty and clears on click', () => {
    const onSearchChange = vi.fn();
    const { rerender } = render(<SharedGamesFilters {...build({ onSearchChange })} />);
    expect(screen.queryByLabelText('Cancella ricerca')).not.toBeInTheDocument();
    rerender(<SharedGamesFilters {...build({ searchValue: 'catan', onSearchChange })} />);
    fireEvent.click(screen.getByLabelText('Cancella ricerca'));
    expect(onSearchChange).toHaveBeenCalledWith('');
  });

  it('clears the search on Escape when searchValue is non-empty', () => {
    const onSearchChange = vi.fn();
    render(<SharedGamesFilters {...build({ searchValue: 'catan', onSearchChange })} />);
    fireEvent.keyDown(screen.getByLabelText('Cerca un gioco'), { key: 'Escape' });
    expect(onSearchChange).toHaveBeenCalledWith('');
  });

  it('does NOT call onSearchChange on Escape when searchValue is empty', () => {
    const onSearchChange = vi.fn();
    render(<SharedGamesFilters {...build({ searchValue: '', onSearchChange })} />);
    fireEvent.keyDown(screen.getByLabelText('Cerca un gioco'), { key: 'Escape' });
    expect(onSearchChange).not.toHaveBeenCalled();
  });

  it('renders all chips with role=switch and aria-checked reflecting active state', () => {
    render(
      <SharedGamesFilters {...build({ activeChips: new Set(['with-toolkit', 'top-rated']) })} />
    );
    const sws = screen.getAllByRole('switch');
    expect(sws).toHaveLength(4);
    expect(screen.getByRole('switch', { name: 'Con toolkit' })).toHaveAttribute(
      'aria-checked',
      'true'
    );
    expect(screen.getByRole('switch', { name: 'Con agente' })).toHaveAttribute(
      'aria-checked',
      'false'
    );
  });

  it('emits chip toggles via onChipToggle(key)', () => {
    const onChipToggle = vi.fn();
    render(<SharedGamesFilters {...build({ onChipToggle })} />);
    fireEvent.click(screen.getByRole('switch', { name: 'Con toolkit' }));
    expect(onChipToggle).toHaveBeenCalledWith('with-toolkit');
  });

  it('renders genre + sort selects with aria-labels', () => {
    render(<SharedGamesFilters {...build()} />);
    expect(screen.getByLabelText('Filtra per genere')).toBeInTheDocument();
    expect(screen.getByLabelText('Ordina i giochi')).toBeInTheDocument();
  });

  it('forwards sort changes', () => {
    const onSortChange = vi.fn();
    render(<SharedGamesFilters {...build({ onSortChange })} />);
    fireEvent.change(screen.getByLabelText('Ordina i giochi'), { target: { value: 'contrib' } });
    expect(onSortChange).toHaveBeenCalledWith('contrib');
  });

  it('shows totalCount when no filters are active', () => {
    render(<SharedGamesFilters {...build({ hasActiveFilters: false })} />);
    expect(screen.getByText('50 giochi')).toBeInTheDocument();
  });

  it('shows resultCount when filters are active', () => {
    render(<SharedGamesFilters {...build({ hasActiveFilters: true, shown: 12, total: 50 })} />);
    expect(screen.getByText('12 di 50 giochi')).toBeInTheDocument();
  });

  it('counter has role=status with aria-live=polite', () => {
    render(<SharedGamesFilters {...build()} />);
    const counter = screen.getByRole('status');
    expect(counter).toHaveAttribute('aria-live', 'polite');
  });
});

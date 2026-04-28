/**
 * SharedGamesFilters — controlled-input behavior tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered:
 *   - Search input is controlled and forwards keystrokes via `onQChange`.
 *   - Each chip is a toggle button with `aria-pressed` reflecting state.
 *   - Chip clicks emit a single `onChipToggle(chip)` call (parent owns merge).
 *   - Genre / sort selects forward changes via callbacks with the right type.
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  SharedGamesFilters,
  type GenreOption,
  type SharedGamesFiltersLabels,
} from '../shared-games-filters';

const LABELS: SharedGamesFiltersLabels = {
  searchLabel: 'Search',
  searchPlaceholder: 'Type a title…',
  chipToolkit: 'Toolkit',
  chipAgent: 'Agents',
  chipTopRated: 'Top rated',
  chipNew: 'New',
  genreLabel: 'Genre',
  genreAll: 'All genres',
  sortLabel: 'Sort',
  sortRating: 'Rating',
  sortContrib: 'Contributions',
  sortNew: 'Most recent',
  sortTitle: 'Title (A→Z)',
};

const GENRES: ReadonlyArray<GenreOption> = [
  { slug: 'strategy', label: 'Strategy' },
  { slug: 'family', label: 'Family' },
];

function renderFilters(overrides: Partial<React.ComponentProps<typeof SharedGamesFilters>> = {}) {
  const props: React.ComponentProps<typeof SharedGamesFilters> = {
    q: '',
    chips: [],
    genre: '',
    sort: 'rating',
    genres: GENRES,
    labels: LABELS,
    onQChange: vi.fn(),
    onChipToggle: vi.fn(),
    onGenreChange: vi.fn(),
    onSortChange: vi.fn(),
    ...overrides,
  };
  return { ...render(<SharedGamesFilters {...props} />), props };
}

describe('SharedGamesFilters', () => {
  it('forwards search keystrokes through onQChange', () => {
    const onQChange = vi.fn();
    renderFilters({ onQChange });

    const input = screen.getByTestId('shared-games-search') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'catan' } });

    expect(onQChange).toHaveBeenCalledWith('catan');
  });

  it('renders chips with aria-pressed reflecting active state', () => {
    renderFilters({ chips: ['top'] });

    expect(screen.getByTestId('shared-games-chip-tk')).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByTestId('shared-games-chip-top')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('shared-games-chip-new')).toHaveAttribute('aria-pressed', 'false');
  });

  it('emits onChipToggle with the chip slug on click', () => {
    const onChipToggle = vi.fn();
    renderFilters({ onChipToggle });

    fireEvent.click(screen.getByTestId('shared-games-chip-ag'));
    fireEvent.click(screen.getByTestId('shared-games-chip-new'));

    expect(onChipToggle).toHaveBeenNthCalledWith(1, 'ag');
    expect(onChipToggle).toHaveBeenNthCalledWith(2, 'new');
  });

  it('forwards genre change as the slug string', () => {
    const onGenreChange = vi.fn();
    renderFilters({ onGenreChange });

    fireEvent.change(screen.getByTestId('shared-games-genre'), {
      target: { value: 'family' },
    });

    expect(onGenreChange).toHaveBeenCalledWith('family');
  });

  it('forwards sort change as a typed SharedGamesSort', () => {
    const onSortChange = vi.fn();
    renderFilters({ onSortChange });

    fireEvent.change(screen.getByTestId('shared-games-sort'), {
      target: { value: 'title' },
    });

    expect(onSortChange).toHaveBeenCalledWith('title');
  });

  it('renders the "All genres" option plus every supplied genre', () => {
    renderFilters({ genre: 'strategy' });

    const select = screen.getByTestId('shared-games-genre') as HTMLSelectElement;
    const optionValues = Array.from(select.options).map(o => o.value);

    expect(optionValues).toEqual(['', 'strategy', 'family']);
    expect(select.value).toBe('strategy');
  });
});

/**
 * SharedGamesGrid — variant decision-tree tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered (5 variants — see `pickBodyVariant` in shared-games-grid.tsx):
 *   1. default          — has games to render.
 *   2. loading          — first fetch (no data yet).
 *   3. empty-search     — `q` set but zero hits.
 *   4. filtered-empty   — chips/genre set but zero hits.
 *   5. api-error        — query failed (no data fallback).
 *
 * Each test asserts:
 *   - `data-variant` attribute on the root grid container,
 *   - the right body sub-region (`shared-games-grid-loading`,
 *     `shared-games-grid-list`, `shared-games-empty-*`) is present,
 *   - retry / clear-filters callbacks fire when their buttons are clicked.
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { GenreOption, SharedGamesFiltersLabels } from '../shared-games-filters';
import { SharedGamesGrid, type SharedGamesGridLabels } from '../shared-games-grid';

import type { SharedGame } from '@/lib/api';

const FILTER_LABELS: SharedGamesFiltersLabels = {
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

const GRID_LABELS: SharedGamesGridLabels = {
  emptySearchTitle: 'Nessun gioco trovato',
  emptySearchDescription: 'Prova un altro titolo.',
  filteredEmptyTitle: 'Filtri attivi',
  filteredEmptyDescription: 'Cancella i filtri.',
  filteredEmptyAction: 'Cancella filtri',
  errorTitle: 'Errore',
  errorDescription: 'Riprova tra un istante.',
  errorAction: 'Riprova',
};

const GENRES: ReadonlyArray<GenreOption> = [{ slug: 'strategy', label: 'Strategy' }];

function makeGame(overrides: Partial<SharedGame> = {}): SharedGame {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    bggId: 1,
    title: 'Catan',
    yearPublished: 1995,
    description: '',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: null,
    averageRating: 7.2,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Active',
    isRagPublic: false,
    hasKnowledgeBase: false,
    createdAt: '2026-01-01T00:00:00Z',
    modifiedAt: null,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    newThisWeekCount: 0,
    contributorsCount: 0,
    isTopRated: false,
    isNew: false,
    ...overrides,
  };
}

function renderGrid(overrides: Partial<React.ComponentProps<typeof SharedGamesGrid>> = {}) {
  const props: React.ComponentProps<typeof SharedGamesGrid> = {
    q: '',
    chips: [],
    genre: '',
    sort: 'rating',
    genres: GENRES,
    filterLabels: FILTER_LABELS,
    onQChange: vi.fn(),
    onChipToggle: vi.fn(),
    onGenreChange: vi.fn(),
    onSortChange: vi.fn(),
    games: undefined,
    isLoading: false,
    isError: false,
    hasActiveSearch: false,
    hasActiveFilters: false,
    gridLabels: GRID_LABELS,
    onClearFilters: vi.fn(),
    onRetry: vi.fn(),
    contributors: undefined,
    contributorsLoading: false,
    contributorsError: false,
    ...overrides,
  };
  return { ...render(<SharedGamesGrid {...props} />), props };
}

describe('SharedGamesGrid', () => {
  it('renders variant="default" when games are present', () => {
    renderGrid({
      games: [makeGame({ id: '11111111-1111-1111-1111-111111111111' })],
    });

    const root = screen.getByTestId('shared-games-grid');
    expect(root).toHaveAttribute('data-variant', 'default');
    expect(screen.getByTestId('shared-games-grid-list')).toBeInTheDocument();
    expect(screen.getByTestId('shared-games-card')).toBeInTheDocument();
  });

  it('renders variant="loading" with skeleton list when isLoading and no games', () => {
    renderGrid({ isLoading: true, skeletonCount: 4 });

    const root = screen.getByTestId('shared-games-grid');
    expect(root).toHaveAttribute('data-variant', 'loading');
    const skeletons = screen.getByTestId('shared-games-grid-loading');
    expect(skeletons).toHaveAttribute('aria-busy', 'true');
    expect(skeletons.children).toHaveLength(4);
  });

  it('renders variant="error" with retry action when isError and no games', () => {
    const onRetry = vi.fn();
    renderGrid({ isError: true, onRetry });

    const root = screen.getByTestId('shared-games-grid');
    expect(root).toHaveAttribute('data-variant', 'error');
    expect(screen.getByTestId('shared-games-empty-api-error')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders variant="empty-search" when q is set, no chips/genre, zero hits', () => {
    renderGrid({
      games: [],
      q: 'unknown game',
      hasActiveSearch: true,
      hasActiveFilters: false,
    });

    const root = screen.getByTestId('shared-games-grid');
    expect(root).toHaveAttribute('data-variant', 'empty-search');
    expect(screen.getByTestId('shared-games-empty-empty-search')).toBeInTheDocument();
    expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
    // empty-search variant has NO action button.
    expect(screen.queryByRole('button', { name: 'Cancella filtri' })).not.toBeInTheDocument();
  });

  it('renders variant="filtered-empty" with clear-filters action when chips set', () => {
    const onClearFilters = vi.fn();
    renderGrid({
      games: [],
      chips: ['top'],
      hasActiveSearch: false,
      hasActiveFilters: true,
      onClearFilters,
    });

    const root = screen.getByTestId('shared-games-grid');
    expect(root).toHaveAttribute('data-variant', 'filtered-empty');
    expect(screen.getByTestId('shared-games-empty-filtered-empty')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancella filtri' }));
    expect(onClearFilters).toHaveBeenCalledTimes(1);
  });

  it('prefers error variant over loading when both are true and there is no data', () => {
    renderGrid({ isLoading: true, isError: true });

    expect(screen.getByTestId('shared-games-grid')).toHaveAttribute('data-variant', 'error');
  });

  it('keeps showing games when isError but stale data is present (placeholderData)', () => {
    renderGrid({
      isError: true,
      games: [makeGame()],
    });

    expect(screen.getByTestId('shared-games-grid')).toHaveAttribute('data-variant', 'default');
    expect(screen.getByTestId('shared-games-grid-list')).toBeInTheDocument();
  });
});

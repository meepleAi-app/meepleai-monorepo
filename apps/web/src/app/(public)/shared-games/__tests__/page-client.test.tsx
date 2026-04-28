/**
 * SharedGamesPageClient — page-level integration tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered:
 *   - Initial render with an empty URL hash uses default state
 *     (q='', chips=[], genre='', sort='rating') and forwards it to
 *     `useSharedGamesSearch`.
 *   - Typing into the search input updates UI state synchronously but the
 *     React Query hook only sees the new `q` after the 250ms debounce
 *     window — guaranteeing we don't fire a request per keystroke.
 *   - Clicking a chip emits a single `useSharedGamesSearch` re-call with the
 *     chip toggled in (no debounce on chips).
 *   - The retry button (rendered when isError) calls `search.refetch()`.
 *
 * The test mocks the React Query hooks so we can drive the state machine
 * deterministically without spinning up a real server. We do NOT mock
 * `useUrlHashState` — its SSR-safe implementation is what makes the page
 * predictable on first paint, and we want the full hash → state → query
 * pipeline exercised end-to-end.
 */
import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SharedGame, TopContributor } from '@/lib/api';

// --- Mocks --------------------------------------------------------------

// `useTranslation` is mocked to identity so we can assert against i18n keys
// directly without spinning up the IntlProvider. The page itself doesn't
// care about the resolved copy — only that it routes the right keys to the
// right slots.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (id: string) => id }),
}));

const refetchMock = vi.fn();
const useSharedGamesSearchMock = vi.fn();
const useTopContributorsMock = vi.fn();

vi.mock('@/hooks/queries/useSharedGamesSearch', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/queries/useSharedGamesSearch')>(
    '@/hooks/queries/useSharedGamesSearch'
  );
  return {
    ...actual,
    useSharedGamesSearch: (...args: unknown[]) => useSharedGamesSearchMock(...args),
  };
});

vi.mock('@/hooks/queries/useTopContributors', () => ({
  useTopContributors: (...args: unknown[]) => useTopContributorsMock(...args),
}));

// Imported AFTER vi.mock so the page-client picks up the mocked modules.
import { SharedGamesPageClient } from '../page-client';

// --- Helpers ------------------------------------------------------------

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

function makeContributor(overrides: Partial<TopContributor> = {}): TopContributor {
  return {
    userId: '22222222-2222-2222-2222-222222222222',
    displayName: 'Mario',
    avatarUrl: null,
    totalSessions: 1,
    totalWins: 0,
    score: 1,
    ...overrides,
  };
}

function setSearchResult(opts: {
  data?: { items: ReadonlyArray<SharedGame>; total?: number } | undefined;
  isLoading?: boolean;
  isError?: boolean;
}) {
  useSharedGamesSearchMock.mockReturnValue({
    data: opts.data,
    isLoading: opts.isLoading ?? false,
    isError: opts.isError ?? false,
    refetch: refetchMock,
  });
}

function setContributorsResult(opts: {
  data?: ReadonlyArray<TopContributor> | undefined;
  isLoading?: boolean;
  isError?: boolean;
}) {
  useTopContributorsMock.mockReturnValue({
    data: opts.data,
    isLoading: opts.isLoading ?? false,
    isError: opts.isError ?? false,
  });
}

// --- Tests --------------------------------------------------------------

describe('SharedGamesPageClient', () => {
  beforeEach(() => {
    refetchMock.mockReset();
    useSharedGamesSearchMock.mockReset();
    useTopContributorsMock.mockReset();
    // Reset hash so each test starts from defaults.
    window.location.hash = '';
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders default state and forwards defaults to the search hook', () => {
    setSearchResult({ data: { items: [makeGame()] } });
    setContributorsResult({ data: [makeContributor()] });

    render(<SharedGamesPageClient />);

    // Last call args -> [state, options]
    const lastCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [state] = lastCall as [
      { q: string; chips: ReadonlyArray<string>; genre: string; sort: string },
    ];
    expect(state.q).toBe('');
    expect(state.chips).toEqual([]);
    expect(state.genre).toBe('');
    expect(state.sort).toBe('rating');

    // Top contributors hook called with the default size (5).
    expect(useTopContributorsMock).toHaveBeenCalledWith(5);

    // Grid should render in default variant since we provided games.
    expect(screen.getByTestId('shared-games-grid')).toHaveAttribute('data-variant', 'default');
  });

  it('debounces search keystrokes by 250ms before forwarding to the query hook', () => {
    vi.useFakeTimers();
    setSearchResult({ data: { items: [makeGame()] } });
    setContributorsResult({ data: [] });

    render(<SharedGamesPageClient />);

    const initialCalls = useSharedGamesSearchMock.mock.calls.length;

    const input = screen.getByTestId('shared-games-search') as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'cat' } });

    // The hook re-renders synchronously to keep input controlled, but the
    // *queryState* still sees the previous (debounced) `q`. So the most
    // recent useSharedGamesSearch call should still have q=''.
    const beforeFlushCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [beforeFlushState] = beforeFlushCall as [{ q: string }];
    expect(beforeFlushState.q).toBe('');

    // Advance only 100ms — still inside the 250ms window.
    act(() => {
      vi.advanceTimersByTime(100);
    });
    const partialCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [partialState] = partialCall as [{ q: string }];
    expect(partialState.q).toBe('');

    // Cross the threshold.
    act(() => {
      vi.advanceTimersByTime(200);
    });
    const afterFlushCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [afterFlushState] = afterFlushCall as [{ q: string }];
    expect(afterFlushState.q).toBe('cat');

    // Sanity: we got at least one extra render after the timer fired.
    expect(useSharedGamesSearchMock.mock.calls.length).toBeGreaterThan(initialCalls);
  });

  it('updates chips synchronously (no debounce) when a chip is toggled', () => {
    setSearchResult({ data: { items: [makeGame()] } });
    setContributorsResult({ data: [] });

    render(<SharedGamesPageClient />);

    fireEvent.click(screen.getByTestId('shared-games-chip-top'));

    const lastCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [state] = lastCall as [{ chips: ReadonlyArray<string> }];
    expect(state.chips).toContain('top');
  });

  it('renders the retry button when search errors and calls refetch on click', () => {
    setSearchResult({ data: undefined, isError: true });
    setContributorsResult({ data: [] });

    render(<SharedGamesPageClient />);

    expect(screen.getByTestId('shared-games-grid')).toHaveAttribute('data-variant', 'error');

    // The retry button label routes through i18n; with our identity mock
    // it equals the i18n key.
    fireEvent.click(
      screen.getByRole('button', {
        name: 'pages.sharedGames.grid.errorAction',
      })
    );

    expect(refetchMock).toHaveBeenCalledTimes(1);
  });

  it('shows the filtered-empty state with a clear-filters button when chips are active and zero hits', () => {
    setSearchResult({ data: { items: [], total: 0 } });
    setContributorsResult({ data: [] });

    render(<SharedGamesPageClient />);

    // Activate the "top" chip — chips are not debounced.
    fireEvent.click(screen.getByTestId('shared-games-chip-top'));

    expect(screen.getByTestId('shared-games-grid')).toHaveAttribute(
      'data-variant',
      'filtered-empty'
    );

    // Click clear filters → chips list should reset to [].
    fireEvent.click(
      screen.getByRole('button', {
        name: 'pages.sharedGames.grid.filteredEmptyAction',
      })
    );

    const lastCall =
      useSharedGamesSearchMock.mock.calls[useSharedGamesSearchMock.mock.calls.length - 1];
    const [state] = lastCall as [{ chips: ReadonlyArray<string> }];
    expect(state.chips).toEqual([]);
  });
});

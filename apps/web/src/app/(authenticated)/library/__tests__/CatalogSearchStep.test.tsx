/**
 * CatalogSearchStep Component Tests — Issue #5169
 *
 * Tests for the catalog search step in AddGameDrawer.
 * Covers:
 * - Renders search input and grid
 * - Shows loading skeletons while fetching
 * - Shows game cards with results
 * - Shows empty state when no results
 * - Calls onSelect + mutation on game selection
 * - Disables already-in-library cards
 * - Shows pagination when multiple pages
 * - Back button calls onBack
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { renderWithIntl } from '@/__tests__/fixtures/common-fixtures';

import { CatalogSearchStep } from '../CatalogSearchStep';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries', () => ({
  useSharedGames: vi.fn(),
  useGameInLibraryStatus: vi.fn(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useAddGameToLibrary: vi.fn(),
}));

vi.mock('@/components/layout', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('@/components/catalog/CatalogPagination', () => ({
  CatalogPagination: ({
    currentPage,
    totalPages,
    onPageChange,
  }: {
    currentPage: number;
    totalPages: number;
    onPageChange: (page: number) => void;
    totalResults?: number;
  }) => (
    <div data-testid="catalog-pagination">
      <span data-testid="pagination-current">{currentPage}</span>
      <span data-testid="pagination-total">{totalPages}</span>
      <button data-testid="pagination-next" onClick={() => onPageChange(currentPage + 1)}>
        Next
      </button>
    </div>
  ),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

import { useSharedGames, useGameInLibraryStatus } from '@/hooks/queries';
import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import { toast } from '@/components/layout';

const mockUseSharedGames = useSharedGames as Mock;
const mockUseGameInLibraryStatus = useGameInLibraryStatus as Mock;
const mockUseAddGameToLibrary = useAddGameToLibrary as Mock;

// ── Shared fixtures ────────────────────────────────────────────────────────────

const GAME_1 = {
  id: 'g1',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.jpg',
};
const GAME_2 = {
  id: 'g2',
  title: 'Ticket to Ride',
  yearPublished: 2004,
  thumbnailUrl: null,
};

function makeGamesData(items = [GAME_1, GAME_2], total = 2) {
  return { items, total };
}

// ── Helpers ───────────────────────────────────────────────────────────────────

// F2.2 #1974 T1: CatalogSearchStep is now i18n-aware; provide the new
// `pages.library.addGame.catalog.*` keys to the IntlProvider so the
// EN-default assertion strings below (e.g. "2 games found") still match.
const I18N_MESSAGES: Record<string, string> = {
  'pages.library.addGame.catalog.backAriaLabel': 'Back to choice',
  'pages.library.addGame.catalog.searchPlaceholder': 'Search games…',
  'pages.library.addGame.catalog.searchAriaLabel': 'Search games',
  'pages.library.addGame.catalog.noImage': 'No image',
  'pages.library.addGame.catalog.inLibraryBadge': 'In library',
  'pages.library.addGame.catalog.alreadyInLibrary': 'Already in library',
  'pages.library.addGame.catalog.selectCta': 'Select',
  'pages.library.addGame.catalog.resultCount':
    '{count, plural, =0 {No games found} =1 {1 game found} other {# games found}}',
  'pages.library.addGame.catalog.emptyResults': 'No games found for "{search}".',
  'pages.library.addGame.catalog.emptyResultsHeading': 'No results',
  'pages.library.addGame.catalog.emptyResultsSubtitle':
    'Try a different title or check the spelling.',
  'pages.library.addGame.catalog.toastAdded': '"{gameName}" added to your library!',
  'pages.library.addGame.catalog.toastError': 'Could not add "{gameName}". Please try again.',
  // #2269 P0-1 (M1) — empty state bridge to manual step
  'pages.library.addGame.catalog.emptyManualCta': 'Add manually',
  // #2269 P0-2 (M2) — already-in-library inline alert
  'pages.library.addGame.catalog.blockedMessage': 'You already have "{gameTitle}" in your library.',
  'pages.library.addGame.catalog.blockedCta': 'Go to game page',
  'pages.library.addGame.catalog.blockedDismiss': 'Dismiss',
};

function renderStep(
  props: {
    onSelect?: (id: string) => void;
    onBack?: () => void;
    onGoToManual?: () => void;
    onNavigateToGame?: (gameId: string) => void;
  } = {}
) {
  const onSelect = props.onSelect ?? vi.fn();
  const onBack = props.onBack ?? vi.fn();
  const onGoToManual = props.onGoToManual;
  const onNavigateToGame = props.onNavigateToGame;
  const result = renderWithIntl(
    <CatalogSearchStep
      onSelect={onSelect}
      onBack={onBack}
      onGoToManual={onGoToManual}
      onNavigateToGame={onNavigateToGame}
    />,
    undefined,
    I18N_MESSAGES
  );
  return { onSelect, onBack, onGoToManual, onNavigateToGame, ...result };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CatalogSearchStep', () => {
  const mutateAsync = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseSharedGames.mockReturnValue({ data: makeGamesData(), isLoading: false });
    mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: false } });
    mockUseAddGameToLibrary.mockReturnValue({ mutateAsync });
  });

  describe('Layout', () => {
    it('renders the search step container', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-step')).toBeInTheDocument();
    });

    it('renders the search input', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-input')).toBeInTheDocument();
    });

    it('renders the results grid', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-grid')).toBeInTheDocument();
    });

    it('renders the back button', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-back')).toBeInTheDocument();
    });

    it('calls onBack when back button is clicked', async () => {
      const user = userEvent.setup();
      const { onBack } = renderStep();

      await user.click(screen.getByTestId('catalog-search-back'));

      expect(onBack).toHaveBeenCalledTimes(1);
    });
  });

  describe('Loading state', () => {
    it('shows skeletons while loading', () => {
      mockUseSharedGames.mockReturnValue({ data: undefined, isLoading: true });
      renderStep();

      // No game cards should be rendered
      expect(screen.queryByTestId('catalog-card-g1')).not.toBeInTheDocument();
      // Grid should still be present
      expect(screen.getByTestId('catalog-search-grid')).toBeInTheDocument();
    });
  });

  describe('Results', () => {
    it('shows game cards for each result', () => {
      renderStep();
      expect(screen.getByTestId('catalog-card-g1')).toBeInTheDocument();
      expect(screen.getByTestId('catalog-card-g2')).toBeInTheDocument();
    });

    it('shows result count', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-count')).toHaveTextContent('2 games found');
    });

    it('shows singular "game" when total is 1', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1], 1),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-search-count')).toHaveTextContent('1 game found');
    });

    it('shows empty state when no results', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([], 0),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-search-empty')).toBeInTheDocument();
    });

    // #2269 P0-1 (M1) — empty state must bridge to the manual step so users
    // searching for a game that does not exist in the catalog have an obvious
    // way to add it manually instead of hitting a dead-end. Mockup ref:
    // admin-mockups/design_files/sp4-add-game-drawer.jsx:337-378 (CatalogEmpty).
    it('shows "Add manually" CTA in empty state when onGoToManual is provided', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([], 0),
        isLoading: false,
      });
      renderStep({ onGoToManual: vi.fn() });
      expect(screen.getByTestId('catalog-search-empty-manual-cta')).toBeInTheDocument();
      expect(screen.getByText('Add manually')).toBeInTheDocument();
    });

    it('does not show "Add manually" CTA in empty state when onGoToManual is omitted', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([], 0),
        isLoading: false,
      });
      renderStep();
      expect(screen.queryByTestId('catalog-search-empty-manual-cta')).not.toBeInTheDocument();
    });

    it('calls onGoToManual when empty-state CTA is clicked', async () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([], 0),
        isLoading: false,
      });
      const onGoToManual = vi.fn();
      const user = userEvent.setup();
      renderStep({ onGoToManual });

      await user.click(screen.getByTestId('catalog-search-empty-manual-cta'));

      expect(onGoToManual).toHaveBeenCalledTimes(1);
    });

    it('shows game title on card', () => {
      renderStep();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('shows year published when > 0', () => {
      renderStep();
      expect(screen.getByText('1995')).toBeInTheDocument();
    });

    it('shows "No image" placeholder when no thumbnail', () => {
      renderStep();
      expect(screen.getByText('No image')).toBeInTheDocument();
    });
  });

  describe('Game selection', () => {
    it('shows Select button on each game card', () => {
      renderStep();
      expect(screen.getByTestId('catalog-card-g1-select-btn')).toBeInTheDocument();
    });

    it('calls mutation and onSelect when Select is clicked', async () => {
      const user = userEvent.setup();
      const { onSelect } = renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(mutateAsync).toHaveBeenCalledWith({ gameId: 'g1' });
        expect(onSelect).toHaveBeenCalledWith('g1');
      });
    });

    it('shows success toast after selection', async () => {
      const user = userEvent.setup();
      renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith('"Catan" added to your library!');
      });
    });

    it('shows error toast when mutation fails', async () => {
      mutateAsync.mockRejectedValueOnce(new Error('Network error'));
      const user = userEvent.setup();
      renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Could not add "Catan". Please try again.');
      });
    });
  });

  describe('Already in library', () => {
    it('disables Select button for games already in library', () => {
      mockUseGameInLibraryStatus.mockImplementation((gameId: string) => ({
        data: { inLibrary: gameId === 'g1' },
      }));
      renderStep();

      expect(screen.getByTestId('catalog-card-g1-select-btn')).toBeDisabled();
      expect(screen.getByTestId('catalog-card-g2-select-btn')).not.toBeDisabled();
    });

    it('shows "Already in library" button label for in-library games', () => {
      mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: true } });
      renderStep();

      expect(screen.getAllByText('Already in library').length).toBeGreaterThan(0);
    });

    it('shows "In library" badge on in-library cards', () => {
      mockUseGameInLibraryStatus.mockImplementation((gameId: string) => ({
        data: { inLibrary: gameId === 'g1' },
      }));
      renderStep();

      expect(screen.getByTestId('catalog-card-g1-in-library-badge')).toBeInTheDocument();
      expect(screen.queryByTestId('catalog-card-g2-in-library-badge')).not.toBeInTheDocument();
    });

    // #2269 P0-2 (M2) — clicking on an already-in-library card surfaces a
    // non-intrusive inline alert with a CTA that navigates to the game
    // detail page. The alert is opt-in via the optional onNavigateToGame
    // prop so the routing concern stays at the parent boundary. Mockup ref:
    // admin-mockups/design_files/sp4-add-game-drawer.jsx:506-541.
    describe('Blocked: already in library', () => {
      beforeEach(() => {
        mockUseGameInLibraryStatus.mockImplementation((gameId: string) => ({
          data: { inLibrary: gameId === 'g1' },
        }));
      });

      it('does not show alert by default before any click', () => {
        renderStep({ onNavigateToGame: vi.fn() });
        expect(screen.queryByTestId('catalog-blocked-alert')).not.toBeInTheDocument();
      });

      it('shows alert when an already-in-library card is clicked', async () => {
        const user = userEvent.setup();
        renderStep({ onNavigateToGame: vi.fn() });

        await user.click(screen.getByTestId('catalog-card-g1'));

        const alert = screen.getByTestId('catalog-blocked-alert');
        expect(alert).toBeInTheDocument();
        expect(alert).toHaveAttribute('role', 'status');
        expect(alert).toHaveTextContent('Catan');
      });

      it('does not show alert if onNavigateToGame prop is omitted', async () => {
        const user = userEvent.setup();
        renderStep();

        await user.click(screen.getByTestId('catalog-card-g1'));

        expect(screen.queryByTestId('catalog-blocked-alert')).not.toBeInTheDocument();
      });

      it('does not show alert when a non-blocked card is clicked', async () => {
        const user = userEvent.setup();
        renderStep({ onNavigateToGame: vi.fn() });

        // g2 is NOT in library; clicking its body should not pop the alert
        await user.click(screen.getByTestId('catalog-card-g2'));

        expect(screen.queryByTestId('catalog-blocked-alert')).not.toBeInTheDocument();
      });

      it('navigates to game detail when alert CTA is clicked', async () => {
        const user = userEvent.setup();
        const onNavigateToGame = vi.fn();
        renderStep({ onNavigateToGame });

        await user.click(screen.getByTestId('catalog-card-g1'));
        await user.click(screen.getByTestId('catalog-blocked-cta'));

        expect(onNavigateToGame).toHaveBeenCalledWith('g1');
      });

      it('dismisses alert when close button is clicked', async () => {
        const user = userEvent.setup();
        renderStep({ onNavigateToGame: vi.fn() });

        await user.click(screen.getByTestId('catalog-card-g1'));
        expect(screen.getByTestId('catalog-blocked-alert')).toBeInTheDocument();

        await user.click(screen.getByTestId('catalog-blocked-dismiss'));

        expect(screen.queryByTestId('catalog-blocked-alert')).not.toBeInTheDocument();
      });

      it('auto-dismisses alert after 4500ms', async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        try {
          const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
          renderStep({ onNavigateToGame: vi.fn() });

          await user.click(screen.getByTestId('catalog-card-g1'));
          expect(screen.getByTestId('catalog-blocked-alert')).toBeInTheDocument();

          vi.advanceTimersByTime(4501);

          await waitFor(() => {
            expect(screen.queryByTestId('catalog-blocked-alert')).not.toBeInTheDocument();
          });
        } finally {
          vi.useRealTimers();
        }
      });
    });
  });

  describe('Pagination', () => {
    it('does not show pagination when only one page', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1], 1),
        isLoading: false,
      });
      renderStep();
      expect(screen.queryByTestId('catalog-pagination')).not.toBeInTheDocument();
    });

    it('shows pagination when total exceeds page size', () => {
      // PAGE_SIZE = 9, total = 20 → 3 pages
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1, GAME_2], 20),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-pagination')).toBeInTheDocument();
    });
  });
});

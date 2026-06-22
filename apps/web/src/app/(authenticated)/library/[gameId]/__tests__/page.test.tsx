/**
 * WS-D Exemplar (Issue #1070, umbrella #1066): coverage tests for the four
 * declared states of `/library/[gameId]` — default · loading · error · not-found.
 *
 * Drives the `?state=` URL override via the WS-D Foundation helper. Each test
 * configures `mockUseStateOverride` to return one canonical state, then
 * asserts that the corresponding branch renders the expected `data-testid`.
 *
 * Mirror of: `apps/web/src/lib/visual-test/__tests__/state-override.test.ts`
 * (which validates the helper itself).
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import LibraryGameDetailPage from '../page';

const mockUseLibraryGameDetail = vi.hoisted(() => vi.fn());
const mockUseStateOverride = vi.hoisted(() => vi.fn());
const mockTryLoadVisualTestFixture = vi.hoisted(() => vi.fn());
const mockPush = vi.hoisted(() => vi.fn());
const mockRefresh = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: vi.fn(() => ({ gameId: 'game-123' })),
  useRouter: vi.fn(() => ({ push: mockPush, replace: vi.fn(), refresh: mockRefresh })),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: mockUseLibraryGameDetail,
}));

vi.mock('@/lib/visual-test/state-override', () => ({
  useStateOverride: mockUseStateOverride,
}));

vi.mock('@/lib/games/game-detail-visual-test-fixture', () => ({
  tryLoadVisualTestFixture: mockTryLoadVisualTestFixture,
}));

// Avoid rendering heavy descendant components in the default branch:
// the test exercises the page's branching logic, not the LibroGameDetailView
// internals (already covered by their own unit tests).
vi.mock('@/components/features/gamebook/LibroGameDetailView', () => ({
  LibroGameDetailView: () => <div data-testid="libro-game-detail-view" />,
}));

vi.mock('@/components/game-detail/GameDetailDesktop', () => ({
  GameDetailDesktop: () => <div data-testid="game-detail-desktop" />,
}));

vi.mock('./game-detail-mobile', () => ({
  default: () => <div data-testid="game-detail-mobile" />,
}));

vi.mock('@/components/library/game-table', () => ({
  GameTableSkeleton: () => <div data-testid="loading-skeleton" />,
}));

describe('LibraryGameDetailPage state coverage (WS-D Exemplar #1070)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseLibraryGameDetail.mockReturnValue({ data: null, isLoading: false, error: null });
    mockUseStateOverride.mockReturnValue(null);
    mockTryLoadVisualTestFixture.mockReturnValue(null);
  });

  it('renders loading skeleton when stateOverride === "loading"', async () => {
    mockUseStateOverride.mockReturnValue('loading');
    renderWithQuery(<LibraryGameDetailPage />);
    await waitFor(() => expect(screen.getByTestId('loading-skeleton')).toBeInTheDocument());
  });

  it('renders error surface distinct from not-found when stateOverride === "error"', async () => {
    mockUseStateOverride.mockReturnValue('error');
    renderWithQuery(<LibraryGameDetailPage />);
    await waitFor(() => expect(screen.getByTestId('error-state')).toBeInTheDocument());
    expect(screen.getByText(/Errore di caricamento/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova/i })).toBeInTheDocument();
    expect(screen.queryByTestId('not-found-state')).not.toBeInTheDocument();
  });

  it('renders not-found surface distinct from error when stateOverride === "not-found"', async () => {
    mockUseStateOverride.mockReturnValue('not-found');
    renderWithQuery(<LibraryGameDetailPage />);
    await waitFor(() => expect(screen.getByTestId('not-found-state')).toBeInTheDocument());
    expect(screen.getByText(/Gioco non trovato/i)).toBeInTheDocument();
    expect(screen.queryByTestId('error-state')).not.toBeInTheDocument();
  });

  it('renders default surface with fixture data when stateOverride === "default" + fixture available', async () => {
    mockUseStateOverride.mockReturnValue('default');
    mockTryLoadVisualTestFixture.mockReturnValue({
      libraryEntryId: 'fixture-entry',
      userId: 'fixture-user',
      gameId: 'game-123',
      gameTitle: 'Wingspan', // non-libro → renders GameDetailDesktop branch
      addedAt: '2026-04-15T08:00:00.000Z',
      notes: null,
      isFavorite: false,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      isAvailableForPlay: true,
      hasCustomPdf: false,
      hasRagAccess: false,
      gamePublisher: null,
      gameYearPublished: null,
      gameIconUrl: null,
      gameImageUrl: null,
      description: null,
      minPlayers: 1,
      maxPlayers: 5,
      playingTimeMinutes: 70,
      complexityRating: 2.4,
      averageRating: null,
      timesPlayed: 0,
      lastPlayed: null,
      winRate: null,
      avgDuration: null,
    });
    renderWithQuery(<LibraryGameDetailPage />);
    await waitFor(() => expect(screen.getByTestId('game-detail-desktop')).toBeInTheDocument());
  });

  it('falls back to real fetch when stateOverride === null (production flow)', async () => {
    mockUseStateOverride.mockReturnValue(null);
    mockUseLibraryGameDetail.mockReturnValue({ data: null, isLoading: true, error: null });
    renderWithQuery(<LibraryGameDetailPage />);
    // Real isLoading=true → skeleton (same render as stateOverride === 'loading')
    await waitFor(() => expect(screen.getByTestId('loading-skeleton')).toBeInTheDocument());
    expect(mockUseLibraryGameDetail).toHaveBeenCalledWith('game-123', true);
  });

  it('disables real fetch when stateOverride is set (avoid wasted backend calls in test mode)', async () => {
    mockUseStateOverride.mockReturnValue('default');
    mockTryLoadVisualTestFixture.mockReturnValue({
      libraryEntryId: 'fixture-entry',
      userId: 'fixture-user',
      gameId: 'game-123',
      gameTitle: 'Wingspan',
      addedAt: '2026-04-15T08:00:00.000Z',
      notes: null,
      isFavorite: false,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      isAvailableForPlay: true,
      hasCustomPdf: false,
      hasRagAccess: false,
      gamePublisher: null,
      gameYearPublished: null,
      gameIconUrl: null,
      gameImageUrl: null,
      description: null,
      minPlayers: 1,
      maxPlayers: 5,
      playingTimeMinutes: 70,
      complexityRating: 2.4,
      averageRating: null,
      timesPlayed: 0,
      lastPlayed: null,
      winRate: null,
      avgDuration: null,
    });
    renderWithQuery(<LibraryGameDetailPage />);
    await waitFor(() => expect(mockUseLibraryGameDetail).toHaveBeenCalled());
    // Second arg `enabled` should be false when stateOverride is non-null
    expect(mockUseLibraryGameDetail).toHaveBeenCalledWith('game-123', false);
  });
});

/**
 * @vitest-environment jsdom
 *
 * Tests for LibraryGameDetailPage route component.
 *
 * S4 (library-to-game epic): the page was migrated from GameTableLayout + zones
 * to GameDetailDesktop with a 5-tab panel. The previous integration tests for
 * GameTableLayout composition have moved to
 * `src/components/game-detail/__tests__/GameDetailDesktop.test.tsx`. This file
 * now only covers page-level routing concerns: loading/error/not-found states,
 * tab deep-linking via `?tab=` query param, and the responsive layout switch.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

const mocks = vi.hoisted(() => ({
  useParams: vi.fn(() => ({ gameId: 'test-game-id' })),
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn() })),
  useSearchParams: vi.fn(() => new URLSearchParams()),
  useLibraryGameDetail: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useParams: mocks.useParams,
  useRouter: mocks.useRouter,
  useSearchParams: mocks.useSearchParams,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: mocks.useLibraryGameDetail,
  libraryKeys: {
    all: ['library'],
    lists: () => ['library', 'list'],
    gameDetail: (id: string) => ['library', 'detail', id],
  },
}));

// Stub the heavy child components so this test focuses on page routing only
vi.mock('@/components/game-detail/GameDetailDesktop', () => ({
  GameDetailDesktop: ({ gameId, initialTab }: { gameId: string; initialTab?: string }) => (
    <div data-testid="game-detail-desktop-stub">
      {gameId}|{initialTab ?? 'default'}
    </div>
  ),
}));

vi.mock('../../../../app/(authenticated)/library/games/[gameId]/game-detail-mobile', () => ({
  default: ({ gameId }: { gameId: string }) => (
    <div data-testid="game-detail-mobile-stub">{gameId}</div>
  ),
}));

import LibraryGameDetailPage from '@/app/(authenticated)/library/games/[gameId]/page';

const mockGame: LibraryGameDetail = {
  libraryEntryId: 'entry-1',
  userId: 'user-1',
  gameId: 'test-game-id',
  addedAt: '2025-01-01T00:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: null,
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: true,
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: null,
  description: null,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.2,
  timesPlayed: 5,
  lastPlayed: null,
  winRate: null,
  avgDuration: null,
};

describe('LibraryGameDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.useParams.mockReturnValue({ gameId: 'test-game-id' });
    mocks.useRouter.mockReturnValue({ push: vi.fn(), replace: vi.fn() });
    mocks.useSearchParams.mockReturnValue(new URLSearchParams());
  });

  it('renders skeleton while loading', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: null,
      isLoading: true,
      error: null,
    });
    const { container } = render(<LibraryGameDetailPage />);
    // Skeleton is implemented by GameTableSkeleton — assert that something
    // other than the not-found/error alert is rendered.
    expect(container.querySelector('[data-testid="error-state"]')).toBeNull();
    expect(container.querySelector('[data-testid="not-found-state"]')).toBeNull();
  });

  it('renders error alert when hook returns error', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: null,
      isLoading: false,
      error: new Error('Something went wrong'),
    });
    render(<LibraryGameDetailPage />);
    expect(screen.getByTestId('error-state')).toBeInTheDocument();
    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
  });

  it('renders not-found state when data is null', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
    });
    render(<LibraryGameDetailPage />);
    expect(screen.getByTestId('not-found-state')).toBeInTheDocument();
  });

  it('renders GameDetailDesktop with default Info tab when data loaded', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGame,
      isLoading: false,
      error: null,
    });
    render(<LibraryGameDetailPage />);
    const stub = screen.getByTestId('game-detail-desktop-stub');
    expect(stub).toHaveTextContent('test-game-id|info');
  });

  it('respects ?tab=aiChat deep-link query param', () => {
    mocks.useSearchParams.mockReturnValue(new URLSearchParams('tab=aiChat'));
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGame,
      isLoading: false,
      error: null,
    });
    render(<LibraryGameDetailPage />);
    expect(screen.getByTestId('game-detail-desktop-stub')).toHaveTextContent('test-game-id|aiChat');
  });

  it('falls back to Info tab when ?tab= has invalid value', () => {
    mocks.useSearchParams.mockReturnValue(new URLSearchParams('tab=invalid'));
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGame,
      isLoading: false,
      error: null,
    });
    render(<LibraryGameDetailPage />);
    expect(screen.getByTestId('game-detail-desktop-stub')).toHaveTextContent('test-game-id|info');
  });
});

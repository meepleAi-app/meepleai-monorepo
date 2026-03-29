/**
 * @vitest-environment jsdom
 *
 * Integration tests for the LibraryGameDetailPage assembly.
 * Verifies that the page correctly wires data to GameTableLayout + MeepleCard.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ---------------------------------------------------------------------------
// Mocks (vi.hoisted for vi.mock compatibility)
// ---------------------------------------------------------------------------

const mocks = vi.hoisted(() => ({
  useParams: vi.fn(() => ({ gameId: 'test-game-id' })),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
  useLibraryGameDetail: vi.fn(),
  useGameTableDrawer: vi.fn(() => ({
    isOpen: false,
    content: null,
    open: vi.fn(),
    close: vi.fn(),
  })),
}));

vi.mock('next/navigation', () => ({
  useParams: mocks.useParams,
  useRouter: mocks.useRouter,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: mocks.useLibraryGameDetail,
  libraryKeys: {
    all: ['library'],
    lists: () => ['library', 'list'],
    gameDetail: (id: string) => ['library', 'detail', id],
  },
}));

vi.mock('@/lib/stores/gameTableDrawerStore', () => ({
  useGameTableDrawer: mocks.useGameTableDrawer,
}));

// Mock zone components to isolate page assembly logic
vi.mock('@/components/library/game-table/GameTableZoneTools', () => ({
  GameTableZoneTools: ({ gameId }: { gameId: string }) => (
    <div data-testid="zone-tools">Tools:{gameId}</div>
  ),
}));

vi.mock('@/components/library/game-table/GameTableZoneKnowledge', () => ({
  GameTableZoneKnowledge: ({ gameId, agentId }: { gameId: string; agentId: string }) => (
    <div data-testid="zone-knowledge">
      KB:{gameId} Agent:{agentId}
    </div>
  ),
}));

vi.mock('@/components/library/game-table/GameTableZoneSessions', () => ({
  GameTableZoneSessions: ({ gameId }: { gameId: string }) => (
    <div data-testid="zone-sessions">Sessions:{gameId}</div>
  ),
}));

vi.mock('@/components/library/game-table/GameTableDrawer', () => ({
  GameTableDrawer: ({ content }: { content: { type: string } }) => (
    <div data-testid="drawer">Drawer:{content.type}</div>
  ),
}));

vi.mock('@/components/library/game-table/GameTableSkeleton', () => ({
  GameTableSkeleton: () => <div data-testid="game-table-skeleton">Loading...</div>,
}));

// Mock GameTableLayout to expose its props for assertion
vi.mock('@/components/library/game-table/GameTableLayout', () => ({
  GameTableLayout: ({
    card,
    toolsZone,
    knowledgeZone,
    sessionsZone,
    drawer,
  }: {
    card: React.ReactNode;
    toolsZone: React.ReactNode;
    knowledgeZone: React.ReactNode;
    sessionsZone: React.ReactNode;
    drawer?: React.ReactNode;
  }) => (
    <div data-testid="game-table-layout">
      <div data-testid="layout-card">{card}</div>
      <div data-testid="layout-tools">{toolsZone}</div>
      <div data-testid="layout-knowledge">{knowledgeZone}</div>
      <div data-testid="layout-sessions">{sessionsZone}</div>
      {drawer && <div data-testid="layout-drawer">{drawer}</div>}
    </div>
  ),
}));

// Mock MeepleCard to expose received props
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => (
    <div
      data-testid="meeple-card"
      data-title={props.title as string}
      data-entity={props.entity as string}
      data-variant={props.variant as string}
    >
      MeepleCard:{props.title as string}
    </div>
  ),
}));

vi.mock('@/components/icons/mechanics/MechanicIcon', () => ({
  MechanicIcon: ({ mechanic }: { mechanic: string }) => (
    <span data-testid="mechanic-icon">{mechanic}</span>
  ),
}));

// Mock GameDetailMobile to avoid QueryClientProvider dependency
vi.mock('@/app/(authenticated)/library/games/[gameId]/game-detail-mobile', () => ({
  default: ({ gameId }: { gameId: string }) => (
    <div data-testid="game-detail-mobile">Mobile:{gameId}</div>
  ),
}));

// Mock framer-motion (some nested components may use it)
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => {
      const {
        initial: _i,
        animate: _a,
        exit: _e,
        variants: _v,
        transition: _t,
        layout: _l,
        ...rest
      } = props;
      return <div {...rest}>{children}</div>;
    },
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------

const mockGameDetail: LibraryGameDetail = {
  libraryEntryId: 'entry-1',
  userId: 'user-1',
  gameId: 'test-game-id',
  addedAt: '2026-01-01T00:00:00Z',
  notes: 'Great game!',
  isFavorite: true,
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
  gameImageUrl: 'https://example.com/catan.jpg',
  description: 'A game about trading and building.',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.3,
  averageRating: 7.2,
  timesPlayed: 12,
  lastPlayed: '2026-03-01T00:00:00Z',
  winRate: '42%',
  avgDuration: '85 min',
  categories: [{ id: 'c1', name: 'Strategy', slug: 'strategy' }],
  mechanics: [{ id: 'm1', name: 'Trading', slug: 'trading' }],
  designers: [{ id: 'd1', name: 'Klaus Teuber' }],
  publishers: [{ id: 'p1', name: 'Kosmos' }],
  bggId: 13,
  minAge: 10,
};

// ---------------------------------------------------------------------------
// Import page component (AFTER mocks)
// ---------------------------------------------------------------------------

// Dynamic import to ensure mocks are in place
const { default: LibraryGameDetailPage } =
  await import('@/app/(authenticated)/library/games/[gameId]/page');

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('LibraryGameDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.useParams.mockReturnValue({ gameId: 'test-game-id' });
    mocks.useRouter.mockReturnValue({ push: vi.fn() });
    mocks.useGameTableDrawer.mockReturnValue({
      isOpen: false,
      content: null,
      open: vi.fn(),
      close: vi.fn(),
    });
  });

  it('renders skeleton when loading', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('game-table-skeleton')).toBeInTheDocument();
  });

  it('renders error alert on error', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('error-state')).toBeInTheDocument();
    expect(screen.getByText('Network error')).toBeInTheDocument();
    expect(screen.getByText('Torna alla Libreria')).toBeInTheDocument();
  });

  it('renders not-found state when data is null', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('not-found-state')).toBeInTheDocument();
    expect(screen.getByText('Gioco non trovato')).toBeInTheDocument();
  });

  it('renders GameTableLayout with all zones when data loaded', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGameDetail,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('game-table-layout')).toBeInTheDocument();
    expect(screen.getByTestId('layout-card')).toBeInTheDocument();
    expect(screen.getByTestId('layout-tools')).toBeInTheDocument();
    expect(screen.getByTestId('layout-knowledge')).toBeInTheDocument();
    expect(screen.getByTestId('layout-sessions')).toBeInTheDocument();
  });

  it('passes correct title to MeepleCard', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGameDetail,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-title', 'Catan');
    expect(card).toHaveAttribute('data-entity', 'game');
    expect(card).toHaveAttribute('data-variant', 'hero');
  });

  it('passes gameId to zone components', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGameDetail,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('zone-tools')).toHaveTextContent('Tools:test-game-id');
    expect(screen.getByTestId('zone-knowledge')).toHaveTextContent('KB:test-game-id');
    expect(screen.getByTestId('zone-sessions')).toHaveTextContent('Sessions:test-game-id');
  });

  it('does not render drawer when drawer state is closed', () => {
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGameDetail,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.queryByTestId('layout-drawer')).not.toBeInTheDocument();
  });

  it('renders drawer when drawer state has content', () => {
    mocks.useGameTableDrawer.mockReturnValue({
      isOpen: true,
      content: { type: 'chat', agentId: 'test-game-id' },
      open: vi.fn(),
      close: vi.fn(),
    });
    mocks.useLibraryGameDetail.mockReturnValue({
      data: mockGameDetail,
      isLoading: false,
      error: null,
    });

    render(<LibraryGameDetailPage />);

    expect(screen.getByTestId('layout-drawer')).toBeInTheDocument();
    expect(screen.getByTestId('drawer')).toHaveTextContent('Drawer:chat');
  });
});

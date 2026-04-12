import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameDetailDesktop } from '../GameDetailDesktop';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// Mockable stub for the library game detail hook
const mockHookState = {
  data: null as LibraryGameDetail | null,
  isLoading: false,
  isError: false,
};

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => mockHookState,
}));

// Stub the MeepleCard so we don't need its full render tree
vi.mock('@/components/ui/data-display/meeple-card/MeepleCard', () => ({
  MeepleCard: (props: { title?: string }) => (
    <div data-testid="meeple-card">{props.title ?? 'no title'}</div>
  ),
}));

vi.mock('@/hooks/useConnectionBarNav', () => ({
  useConnectionBarNav: () => ({ handlePipClick: vi.fn() }),
}));

vi.mock('@/components/ui/data-display/connection-bar', () => ({
  ConnectionBar: ({ connections }: { connections: Array<{ count: number }> }) =>
    connections.length > 0 ? <div data-testid="connection-bar" /> : null,
  buildGameConnections: (counts: Record<string, number>) =>
    Object.values(counts).some(v => v > 0) ? [{ count: 1 }] : [],
}));

const GAME_ID = '00000000-0000-4000-8000-000000000001';

function createGame(overrides: Partial<LibraryGameDetail> = {}): LibraryGameDetail {
  return {
    libraryEntryId: 'entry-1',
    userId: 'user-1',
    gameId: GAME_ID,
    addedAt: '2025-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'owned',
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
    description: 'Build, trade, settle',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.2,
    timesPlayed: 5,
    lastPlayed: '2025-01-15T00:00:00Z',
    winRate: '40%',
    avgDuration: '95 min',
    ...overrides,
  };
}

describe('GameDetailDesktop', () => {
  beforeEach(() => {
    mockHookState.data = null;
    mockHookState.isLoading = false;
    mockHookState.isError = false;
  });

  it('renders loading state while data is loading', () => {
    mockHookState.isLoading = true;
    render(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-desktop-loading')).toBeInTheDocument();
  });

  it('renders error state when the hook reports an error', () => {
    mockHookState.isError = true;
    render(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-desktop-error')).toBeInTheDocument();
  });

  it('renders MeepleCard hero and tabs panel when game is loaded', () => {
    mockHookState.data = createGame();
    render(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('meeple-card')).toHaveTextContent('Catan');
    expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
  });

  it('renders connection-bar with session pip when game has sessions played', () => {
    mockHookState.data = createGame({ timesPlayed: 3 });
    render(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('connection-bar')).toBeInTheDocument();
  });

  it('renders with fallback title when game is not in library', () => {
    mockHookState.data = null;
    // isLoading false, isError false, data null → isNotInLibrary path
    render(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('meeple-card')).toHaveTextContent(/non in libreria/i);
    // Tabs still render (they handle isNotInLibrary internally)
    expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
  });
});

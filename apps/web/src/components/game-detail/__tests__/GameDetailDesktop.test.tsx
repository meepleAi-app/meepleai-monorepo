import { screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameDetailDesktop } from '../GameDetailDesktop';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// Mockable stub for the library game detail hook
const mockHookState = {
  data: null as LibraryGameDetail | null,
  isLoading: false,
  isError: false,
};

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: vi.fn(),
    push: vi.fn(),
    refresh: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => mockHookState,
}));

// Stub GameHero (#2100 M1 replaced legacy MeepleCard hero). F3 #1974
// regression tests need to assert on the metadata strip; render each
// metadata `label` inside the stub so `toHaveTextContent` can match
// without pulling the full primitive (includes Next/Image which needs
// extra mocking).
vi.mock('@/components/game-detail/GameHero', () => ({
  GameHero: (props: { title?: string; metadata?: Array<{ label: string }> }) => (
    <div data-testid="game-detail-hero-card">
      {props.title ?? 'no title'}
      {props.metadata?.map((m, i) => (
        <span key={`meta-${i}`} data-testid={`game-hero-meta-${i}`}>
          {m.label}
        </span>
      ))}
    </div>
  ),
}));

vi.mock('@/hooks/useConnectionBarNav', () => ({
  useConnectionBarNav: () => ({ handlePipClick: vi.fn() }),
}));

vi.mock('@/components/ui/data-display/connection-bar', () => ({
  ConnectionBar: ({ connections }: { connections: Array<{ count: number }> }) =>
    connections.length > 0 ? <div data-testid="connection-bar" /> : null,
  buildGameConnectionPips: (counts: Record<string, number>) =>
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
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-desktop-loading')).toBeInTheDocument();
  });

  it('renders error state when the hook reports an error', () => {
    mockHookState.isError = true;
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-desktop-error')).toBeInTheDocument();
  });

  it('renders GameHero v2 and tabs panel when game is loaded', () => {
    mockHookState.data = createGame();
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-hero-card')).toHaveTextContent('Catan');
    expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
  });

  it('renders connection-bar with session pip when game has sessions played', () => {
    mockHookState.data = createGame({ timesPlayed: 3 });
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('connection-bar')).toBeInTheDocument();
  });

  it('renders with fallback title when game is not in library', () => {
    mockHookState.data = null;
    // isLoading false, isError false, data null → isNotInLibrary path
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    expect(screen.getByTestId('game-detail-hero-card')).toHaveTextContent(/non in libreria/i);
    // Tabs still render (they handle isNotInLibrary internally)
    expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
  });

  it('extends the hero meta strip with designer + complexity when surfaced by the BE (F3 #1974)', () => {
    // F3 partial: the live page used to show only "anno · giocatori · durata".
    // The mockup ships the full identifier strip "designer · anno · durata
    // · giocatori · complessità". This test asserts the additive labels are
    // present when the catalog fallback enriches the library detail with the
    // shared-game payload.
    mockHookState.data = createGame({
      designers: [{ id: 'd1', name: 'Klaus Teuber' }],
      complexityRating: 2.3,
    });
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    const hero = screen.getByTestId('game-detail-hero-card');
    expect(hero).toHaveTextContent('Klaus Teuber');
    expect(hero).toHaveTextContent(/Complessit.*2\.3/);
  });

  it('skips the designer entry when the BE payload omits it (catalog fallback miss)', () => {
    // Private games / non-catalog entries don't ship designers — the strip
    // must degrade gracefully (no empty label, no crash).
    mockHookState.data = createGame({ designers: undefined });
    renderWithQuery(<GameDetailDesktop gameId={GAME_ID} />);
    const hero = screen.getByTestId('game-detail-hero-card');
    expect(hero).not.toHaveTextContent('Klaus Teuber');
    // The other strip items still render.
    expect(hero).toHaveTextContent('1995');
    expect(hero).toHaveTextContent('90 min');
  });
});

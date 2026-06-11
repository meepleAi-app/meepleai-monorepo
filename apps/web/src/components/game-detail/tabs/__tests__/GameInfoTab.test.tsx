/**
 * GameInfoTab — F3 partial #1974 regression tests.
 *
 * Validates the spec-list extensions (Designer / Categorie / Meccaniche)
 * + the new Descrizione section heading introduced as part of the F3
 * audit follow-on. The mock returns a minimal LibraryGameDetail shape so
 * the suite doesn't need a real QueryClient or a full BE payload.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameInfoTab } from '../GameInfoTab';

// ─── Mocks ──────────────────────────────────────────────────────────────────

const mockState = { data: null as unknown, isLoading: false, isError: false };

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    replace: vi.fn(),
    push: vi.fn(),
    refresh: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: () => mockState,
}));

// ─── Fixtures ───────────────────────────────────────────────────────────────

const GAME_ID = '00000000-0000-4000-8000-000000000001';

function baseGame(overrides: Record<string, unknown> = {}): Record<string, unknown> {
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
    hasRagAccess: false,
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameIconUrl: null,
    gameImageUrl: null,
    description: 'Build, trade, settle.',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexityRating: 2.3,
    averageRating: 7.2,
    timesPlayed: 5,
    lastPlayed: null,
    winRate: null,
    avgDuration: null,
    ...overrides,
  };
}

beforeEach(() => {
  mockState.data = null;
  mockState.isLoading = false;
  mockState.isError = false;
});

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('GameInfoTab — F3 partial polish (#1974)', () => {
  it('renders the Descrizione heading + body when the BE surfaces a description', () => {
    mockState.data = baseGame();
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.getByText('Descrizione')).toBeInTheDocument();
    expect(screen.getByText('Build, trade, settle.')).toBeInTheDocument();
    expect(screen.getByTestId('game-info-description')).toBeInTheDocument();
  });

  it('omits the Descrizione section entirely when the BE returns no description', () => {
    mockState.data = baseGame({ description: null });
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.queryByText('Descrizione')).not.toBeInTheDocument();
    expect(screen.queryByTestId('game-info-description')).not.toBeInTheDocument();
  });

  it('renders the Designer row when surfaced (catalog fallback)', () => {
    mockState.data = baseGame({
      designers: [{ id: 'd1', name: 'Klaus Teuber' }],
    });
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.getByText('Designer')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('joins multiple designers with comma-separated names', () => {
    mockState.data = baseGame({
      designers: [
        { id: 'd1', name: 'Klaus Teuber' },
        { id: 'd2', name: 'Benjamin Teuber' },
      ],
    });
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.getByText('Klaus Teuber, Benjamin Teuber')).toBeInTheDocument();
  });

  it('renders the Categorie row when the BE surfaces categories', () => {
    mockState.data = baseGame({
      categories: [
        { id: 'c1', name: 'Strategy', slug: 'strategy' },
        { id: 'c2', name: 'Economic', slug: 'economic' },
      ],
    });
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.getByText('Categorie')).toBeInTheDocument();
    expect(screen.getByText('Strategy, Economic')).toBeInTheDocument();
  });

  it('renders the Meccaniche row when the BE surfaces mechanics', () => {
    mockState.data = baseGame({
      mechanics: [{ id: 'm1', name: 'Trading', slug: 'trading' }],
    });
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.getByText('Meccaniche')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
  });

  it('omits Designer / Categorie / Meccaniche rows when the BE omits them', () => {
    mockState.data = baseGame(); // no designers, categories, mechanics
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={false} />);
    expect(screen.queryByText('Designer')).not.toBeInTheDocument();
    expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
    expect(screen.queryByText('Meccaniche')).not.toBeInTheDocument();
  });

  it('omits the description block when the catalog fallback miss yields an empty payload', () => {
    mockState.data = baseGame({ description: null, designers: undefined, categories: undefined });
    render(<GameInfoTab gameId={GAME_ID} variant="mobile" isNotInLibrary={false} />);
    expect(screen.queryByText('Descrizione')).not.toBeInTheDocument();
  });

  it('returns the not-in-library copy when the gate is on', () => {
    render(<GameInfoTab gameId={GAME_ID} variant="desktop" isNotInLibrary={true} />);
    expect(screen.getByText(/Aggiungi questo gioco alla tua libreria/i)).toBeInTheDocument();
  });
});

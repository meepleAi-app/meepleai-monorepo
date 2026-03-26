/**
 * PersonalLibraryPage Component Tests
 *
 * Tests for the vetrina-layout personal library page that splits games into
 * catalog (shared) and custom (private) sections.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PersonalLibraryPage } from '@/components/library/PersonalLibraryPage';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// Mock useLibrary hook
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
}));

// Mock next/navigation (required by CreateGameCtaCard)
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock useReducedMotion (used by EmptyState)
vi.mock('@/lib/animations', () => ({
  useReducedMotion: () => false,
}));

const mockUseLibrary = vi.mocked(useLibrary);

// ── Fixtures ──────────────────────────────────────────────────────────────────

function makeCatalogEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id: 'entry-catalog-1',
    userId: 'user-1',
    gameId: 'game-shared-1',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: true,
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    complexityRating: 2.3,
    averageRating: 7.0,
    isPrivateGame: false,
    privateGameId: null,
    canProposeToCatalog: false,
    ...overrides,
  };
}

function makeCustomEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id: 'entry-custom-1',
    userId: 'user-1',
    gameId: 'game-private-1',
    gameTitle: 'Il Mio Gioco',
    gamePublisher: null,
    gameYearPublished: null,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: '2026-01-02T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: true,
    minPlayers: null,
    maxPlayers: null,
    playingTimeMinutes: null,
    complexityRating: null,
    averageRating: null,
    isPrivateGame: true,
    privateGameId: 'private-game-uuid-1',
    canProposeToCatalog: true,
    ...overrides,
  };
}

function makePaginatedResponse(items: UserLibraryEntry[]) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}

// ── Tests ──────────────────────────────────────────────────────────────────────

describe('PersonalLibraryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // Empty state
  // --------------------------------------------------------------------------

  it('renders empty state when library has no games', () => {
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByTestId('personal-library-page')).toBeInTheDocument();
    // EmptyState should appear — use getAllByText to handle sr-only duplicate
    expect(screen.getAllByText(/la tua libreria è vuota/i).length).toBeGreaterThan(0);
  });

  it('renders loading skeleton when isLoading is true', () => {
    mockUseLibrary.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByTestId('personal-library-page')).toBeInTheDocument();
    // No section headings during loading
    expect(screen.queryByText(/dal catalogo/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/giochi personalizzati/i)).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Two sections rendered
  // --------------------------------------------------------------------------

  it('renders catalog section when shared catalog games exist', () => {
    const catalogGame = makeCatalogEntry();
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([catalogGame]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByText(/dal catalogo/i)).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders custom games section when private games exist', () => {
    const customGame = makeCustomEntry();
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([customGame]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByText(/giochi personalizzati/i)).toBeInTheDocument();
    expect(screen.getByText('Il Mio Gioco')).toBeInTheDocument();
  });

  it('renders both sections when both catalog and custom games are present', () => {
    const catalogGame = makeCatalogEntry();
    const customGame = makeCustomEntry();
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([catalogGame, customGame]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByText(/dal catalogo/i)).toBeInTheDocument();
    expect(screen.getByText(/giochi personalizzati/i)).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Il Mio Gioco')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // ShelfCards rendered
  // --------------------------------------------------------------------------

  it('renders ShelfCards for each catalog game', () => {
    const games = [
      makeCatalogEntry({ id: 'e1', gameTitle: 'Catan', gameId: 'g1' }),
      makeCatalogEntry({ id: 'e2', gameTitle: 'Pandemic', gameId: 'g2' }),
    ];
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse(games),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    // At minimum, expect cards for both catalog games (testids are shelf-card-catalog-{id})
    const cards = document.querySelectorAll('[data-testid^="shelf-card-catalog-"]');
    const titles = Array.from(cards).map(c => c.textContent);
    expect(titles.some(t => t?.includes('Catan'))).toBe(true);
    expect(titles.some(t => t?.includes('Pandemic'))).toBe(true);
  });

  it('renders ShelfCards for each custom game', () => {
    const games = [
      makeCustomEntry({ id: 'e3', gameTitle: 'Gioco A', gameId: 'p1', privateGameId: 'pg1' }),
      makeCustomEntry({ id: 'e4', gameTitle: 'Gioco B', gameId: 'p2', privateGameId: 'pg2' }),
    ];
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse(games),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByText('Gioco A')).toBeInTheDocument();
    expect(screen.getByText('Gioco B')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // "Crea gioco" CTA card
  // --------------------------------------------------------------------------

  it('renders "Crea gioco" CTA card at end of custom games section', () => {
    const customGame = makeCustomEntry();
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([customGame]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByTestId('create-game-cta')).toBeInTheDocument();
    expect(screen.getByText(/crea gioco/i)).toBeInTheDocument();
  });

  it('renders "Crea gioco" CTA even when no custom games exist', () => {
    const catalogGame = makeCatalogEntry();
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse([catalogGame]),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    // The custom games section appears with CTA even when empty
    expect(screen.getByTestId('create-game-cta')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Toolbar
  // --------------------------------------------------------------------------

  it('renders the library toolbar with game count', () => {
    const items = [makeCatalogEntry(), makeCustomEntry()];
    mockUseLibrary.mockReturnValue({
      data: makePaginatedResponse(items),
      isLoading: false,
    } as ReturnType<typeof useLibrary>);

    render(<PersonalLibraryPage />);

    expect(screen.getByTestId('library-toolbar')).toBeInTheDocument();
    expect(screen.getByTestId('library-game-count')).toBeInTheDocument();
    // totalCount = 2 (from paginated response)
    expect(screen.getByText(/2 giochi/i)).toBeInTheDocument();
  });
});

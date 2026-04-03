/**
 * MeepleGameCatalogCard Tests
 *
 * Verifies catalog-specific card behavior:
 * - Only 1 quick action (Add to library / Already in library)
 * - Only 1 nav link ("Regolamento")
 * - No "Crea Agente", KB, Agents, Chats, Sessions links
 * - N/A fallback for missing data
 * - Rating defaults to 0 (not undefined)
 * - Subtitle shows "N/A" when no year/bggId
 * - "In Libreria" badge when in library
 * - Info button links to /games/:id
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { MeepleGameCatalogCard, MeepleGameCatalogCardSkeleton } from '../MeepleGameCatalogCard';
import { useGameInLibraryStatus } from '@/hooks/queries';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

// ─── Mocks ──────────────────────────────────────────────────────────────────

const mockOpenWizard = vi.fn();
vi.mock('@/components/library/add-game-sheet/AddGameWizardProvider', () => ({
  useAddGameWizard: () => ({ openWizard: mockOpenWizard }),
}));

vi.mock('@/hooks/queries', () => ({
  useGameInLibraryStatus: vi.fn(),
}));

// MeepleCard: render a simplified version that exposes props as data-* and text
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({
    title,
    subtitle,
    badge,
    rating,
    loading,
    entityQuickActions,
    linkedEntities,
    showInfoButton,
    entityId,
    infoTooltip,
    'data-testid': testId,
    ...rest
  }: Record<string, unknown>) => (
    <div data-testid={testId as string}>
      {loading && <span data-testid="loading-indicator">Loading</span>}
      {title && <span data-testid="card-title">{title as string}</span>}
      {subtitle && <span data-testid="card-subtitle">{subtitle as string}</span>}
      {badge && <span data-testid="card-badge">{badge as string}</span>}
      <span data-testid="card-rating">{String(rating)}</span>
      {/* Render quick actions */}
      {Array.isArray(entityQuickActions) &&
        (entityQuickActions as Array<{ label: string; disabled?: boolean }>).map((qa, i) => (
          <span
            key={i}
            data-testid={`quick-action-${i}`}
            data-disabled={qa.disabled ? 'true' : 'false'}
          >
            {qa.label}
          </span>
        ))}
      {/* Render linked entity pips */}
      {Array.isArray(linkedEntities) &&
        (linkedEntities as Array<{ entityType: string; count: number }>).map((le, i) => (
          <span key={i} data-testid={`linked-entity-${i}`} data-entity-type={le.entityType}>
            {le.entityType}
          </span>
        ))}
      {/* Info button */}
      {showInfoButton && entityId && (
        <button data-testid="info-button" title={infoTooltip as string}>
          Info
        </button>
      )}
    </div>
  ),
}));

// ─── Test data ──────────────────────────────────────────────────────────────

const mockGame: SharedGame = {
  id: 'game-1',
  title: 'Catan',
  bggId: 13,
  description: 'A game of trading and building',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.32,
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  yearPublished: 1995,
  averageRating: 7.2,
  status: 1,
};

const mockGameNoData: SharedGame = {
  id: 'game-chess',
  title: 'Chess',
  bggId: undefined as unknown as number,
  description: null as unknown as string,
  minPlayers: null as unknown as number,
  maxPlayers: null as unknown as number,
  playingTimeMinutes: null as unknown as number,
  complexityRating: null as unknown as number,
  imageUrl: null as unknown as string,
  thumbnailUrl: null as unknown as string,
  yearPublished: null as unknown as number,
  averageRating: null as unknown as number,
  status: 1,
};

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('MeepleGameCatalogCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (useGameInLibraryStatus as Mock).mockReturnValue({
      data: { inLibrary: false },
      isLoading: false,
    });
  });

  // ── Quick Actions ──────────────────────────────────────────────────────

  describe('Quick Actions', () => {
    it('renders exactly 1 quick action', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.getByTestId('quick-action-0')).toBeInTheDocument();
      expect(screen.queryByTestId('quick-action-1')).not.toBeInTheDocument();
    });

    it('shows "Aggiungi alla libreria" when not in library', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      const qa = screen.getByTestId('quick-action-0');
      expect(qa).toHaveTextContent('Aggiungi alla libreria');
      expect(qa).toHaveAttribute('data-disabled', 'false');
    });

    it('shows disabled "Gioco gia nella tua libreria" when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      const qa = screen.getByTestId('quick-action-0');
      expect(qa).toHaveTextContent('Gioco gia nella tua libreria');
      expect(qa).toHaveAttribute('data-disabled', 'true');
    });

    it('uses batch libraryStatus when provided (skips individual fetch)', () => {
      renderWithProviders(
        <MeepleGameCatalogCard game={mockGame} libraryStatus={{ inLibrary: true }} />
      );
      // useGameInLibraryStatus should be called with enabled=false
      expect(useGameInLibraryStatus).toHaveBeenCalledWith('game-1', false);
      const qa = screen.getByTestId('quick-action-0');
      expect(qa).toHaveTextContent('Gioco gia nella tua libreria');
    });

    it('does NOT render "Crea Agente" or agent-related actions', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.queryByText(/Crea Agente/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Avvia Sessione/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Condividi/i)).not.toBeInTheDocument();
    });
  });

  // ── Linked Entities ────────────────────────────────────────────────────

  describe('Linked Entities', () => {
    it('renders 1 linked entity (kb) when not in library', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.getByTestId('linked-entity-0')).toBeInTheDocument();
      expect(screen.getByTestId('linked-entity-0')).toHaveAttribute('data-entity-type', 'kb');
      expect(screen.queryByTestId('linked-entity-1')).not.toBeInTheDocument();
    });

    it('renders multiple linked entities when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.getByTestId('linked-entity-0')).toBeInTheDocument();
      expect(screen.getByTestId('linked-entity-1')).toBeInTheDocument();
    });
  });

  // ── Info Button ────────────────────────────────────────────────────────

  describe('Info Button', () => {
    it('renders info button with drawer trigger', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      const info = screen.getByTestId('info-button');
      expect(info).toBeInTheDocument();
      expect(info).toHaveAttribute('title', 'Vai al dettaglio');
    });
  });

  // ── Badge ──────────────────────────────────────────────────────────────

  describe('Badge', () => {
    it('shows "In Libreria" badge when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.getByTestId('card-badge')).toHaveTextContent('In Libreria');
    });

    it('does not show badge when not in library', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.queryByTestId('card-badge')).not.toBeInTheDocument();
    });
  });

  // ── N/A Fallbacks ─────────────────────────────────────────────────────

  describe('N/A Fallbacks', () => {
    it('shows subtitle "N/A" when no year or bggId', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGameNoData} />);
      expect(screen.getByTestId('card-subtitle')).toHaveTextContent('N/A');
    });

    it('shows rating 0 when averageRating is null', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGameNoData} />);
      expect(screen.getByTestId('card-rating')).toHaveTextContent('0');
    });

    it('shows year and ID in subtitle when available', () => {
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      const subtitle = screen.getByTestId('card-subtitle');
      expect(subtitle).toHaveTextContent('1995');
      expect(subtitle).toHaveTextContent('ID: 13');
    });
  });

  // ── Loading State ─────────────────────────────────────────────────────

  describe('Loading State', () => {
    it('shows loading when fetching individual status', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      });
      renderWithProviders(<MeepleGameCatalogCard game={mockGame} />);
      expect(screen.getByTestId('loading-indicator')).toBeInTheDocument();
    });

    it('does NOT show loading when batch status is provided', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      });
      renderWithProviders(
        <MeepleGameCatalogCard game={mockGame} libraryStatus={{ inLibrary: false }} />
      );
      expect(screen.queryByTestId('loading-indicator')).not.toBeInTheDocument();
    });
  });
});

// ── Skeleton ──────────────────────────────────────────────────────────────

describe('MeepleGameCatalogCardSkeleton', () => {
  it('renders skeleton with loading indicator', () => {
    render(<MeepleGameCatalogCardSkeleton />);
    expect(screen.getByTestId('catalog-game-card-skeleton')).toBeInTheDocument();
  });
});

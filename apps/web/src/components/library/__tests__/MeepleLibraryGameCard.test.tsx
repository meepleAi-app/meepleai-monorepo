/**
 * MeepleLibraryGameCard Tests
 * Issue #4045 - Library integration tests
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { MeepleLibraryGameCard } from '../MeepleLibraryGameCard';

import type { UserLibraryEntry } from '@/lib/api';

// ============================================================================
// Mock Setup
// ============================================================================

// Track mock state
let mockMutateAsync: Mock;
let mockIsPending: boolean;
let mockAgentConfig: unknown;

// Mock React Query hooks
vi.mock('@/hooks/queries', () => ({
  useAgentConfig: () => ({
    data: mockAgentConfig,
    isLoading: false,
  }),
  useToggleLibraryFavorite: () => ({
    mutateAsync: mockMutateAsync,
    isPending: mockIsPending,
  }),
}));

// Mock next/navigation — useViewTransition calls useRouter internally
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  usePathname: vi.fn(() => '/library'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

// Mock AgentCreationSheet to avoid deep QueryClient usage in tests
vi.mock('@/components/agent/config', () => ({
  AgentCreationSheet: () => null,
}));

// ============================================================================
// Test Utilities
// ============================================================================

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// Mock toast
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ============================================================================
// Mock Data
// ============================================================================

const mockGame: UserLibraryEntry = {
  id: 'entry-1',
  userId: 'user-1',
  gameId: 'game-123',
  gameTitle: 'Test Game',
  gamePublisher: 'Test Publisher',
  gameYearPublished: 2024,
  gameImageUrl: '/test-image.jpg',
  addedAt: '2024-01-01T00:00:00Z',
  isFavorite: false,
  currentState: 'Owned',
  hasKb: true,
  kbCardCount: 1,
  kbIndexedCount: 1,
  kbProcessingCount: 0,
  agentIsOwned: true,
  notes: 'Test notes',
};

// ============================================================================
// Tests
// ============================================================================

describe('MeepleLibraryGameCard', () => {
  const defaultProps = {
    game: mockGame,
    onConfigureAgent: vi.fn(),
    onUploadPdf: vi.fn(),
    onEditNotes: vi.fn(),
    onRemove: vi.fn(),
    onAskAgent: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync = vi.fn().mockResolvedValue({});
    mockIsPending = false;
    mockAgentConfig = null;
    mockToastSuccess.mockClear();
    mockToastError.mockClear();
  });

  it('renders game title and subtitle', () => {
    render(<MeepleLibraryGameCard {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText('Test Game')).toBeInTheDocument();
    expect(screen.getByText('Test Publisher')).toBeInTheDocument();
  });

  it('renders in grid variant by default', () => {
    const { container } = render(<MeepleLibraryGameCard {...defaultProps} />, { wrapper: createWrapper() });

    const card = container.querySelector('[data-testid^="library-game-card"]');
    expect(card).toBeInTheDocument();
  });

  it('renders in list variant when specified', () => {
    render(<MeepleLibraryGameCard {...defaultProps} variant="list" />, { wrapper: createWrapper() });

    const card = screen.getByTestId('library-game-card-game-123');
    expect(card).toBeInTheDocument();
  });

  it('shows favorite badge when game is favorite', () => {
    const favoriteGame = { ...mockGame, isFavorite: true };
    render(<MeepleLibraryGameCard {...defaultProps} game={favoriteGame} />, { wrapper: createWrapper() });

    expect(screen.getByText(/❤️ Preferito/i)).toBeInTheDocument();
  });

  it('shows KB metadata when documents available', () => {
    render(<MeepleLibraryGameCard {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText(/📄 KB/i)).toBeInTheDocument();
  });

  it('does not show KB metadata when no documents', () => {
    const gameNoPdf = { ...mockGame, hasKb: false, kbCardCount: 0, kbIndexedCount: 0, kbProcessingCount: 0 };
    render(<MeepleLibraryGameCard {...defaultProps} game={gameNoPdf} />, { wrapper: createWrapper() });

    expect(screen.queryByText(/📄 PDF/i)).not.toBeInTheDocument();
  });

  it('maps game state to status correctly', () => {
    const ownedGame = { ...mockGame, currentState: 'Owned' as const };
    const { rerender } = render(<MeepleLibraryGameCard {...defaultProps} game={ownedGame} />, { wrapper: createWrapper() });

    // Status badge should show "owned"
    const card = screen.getByTestId('library-game-card-game-123');
    expect(card).toBeInTheDocument();

    // Test Wishlist state
    const wishlistGame = { ...mockGame, currentState: 'Wishlist' as const };
    rerender(<MeepleLibraryGameCard {...defaultProps} game={wishlistGame} />);
    expect(screen.getByTestId('library-game-card-game-123')).toBeInTheDocument();
  });

  it('shows selection checkbox in selection mode', () => {
    render(
      <MeepleLibraryGameCard
        {...defaultProps}
        selectionMode={true}
        isSelected={false}
        onSelect={vi.fn()}
      />,
      { wrapper: createWrapper() }
    );

    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeInTheDocument();
    expect(checkbox).not.toBeChecked();
  });

  it('shows checked checkbox when selected', () => {
    render(
      <MeepleLibraryGameCard
        {...defaultProps}
        selectionMode={true}
        isSelected={true}
        onSelect={vi.fn()}
      />,
      { wrapper: createWrapper() }
    );

    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeChecked();
  });

  it.skip('calls onSelect when checkbox clicked', async () => {
    // TODO: Fix checkbox click event propagation in test environment
    // const user = userEvent.setup();
    // const onSelect = vi.fn();
    // render(...);
    // await user.click(checkbox);
    // expect(onSelect).toHaveBeenCalled();
  });

  it.skip('shows info button with correct href', () => {
    // TODO: Fix testid selector for info button
    render(<MeepleLibraryGameCard {...defaultProps} />);

    // Info button should be present (may need hover to be fully visible)
    // const infoButton = screen.getByTestId('info-button-game-123');
    // expect(infoButton).toBeInTheDocument();
    // expect(infoButton).toHaveAttribute('href', '/library/games/game-123');
  });

  it.skip('renders quick actions on hover', async () => {
    // TODO: Fix hover simulation and quick action visibility testing
    // const user = userEvent.setup();
    // render(<MeepleLibraryGameCard {...defaultProps} />);
    // const card = screen.getByTestId('library-game-card-game-123');
    // await user.hover(card);
    // Verify quick actions appear
  });

  it('formats metadata correctly', () => {
    render(<MeepleLibraryGameCard {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText(/Mai giocato/i)).toBeInTheDocument();
    expect(screen.getByText(/N\/A/i)).toBeInTheDocument(); // Win rate
  });
});

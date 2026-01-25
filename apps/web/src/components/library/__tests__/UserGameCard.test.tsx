/**
 * UserGameCard Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Rendering with complete/minimal data
 * - Cover image display and fallback
 * - Agent configuration status display
 * - PDF status badge
 * - Action button callbacks
 * - Favorite toggle integration
 * - Notes preview
 * - Date formatting
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { UserGameCard } from '../UserGameCard';
import type { UserLibraryEntry } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

// Mock useAgentConfig hook
vi.mock('@/hooks/queries', () => ({
  useAgentConfig: vi.fn((gameId: string, enabled: boolean) => ({
    data: gameId === 'game-configured' ? { modelType: 'google-gemini-pro' } : null,
    isLoading: false,
    error: null,
  })),
}));

// Mock FavoriteToggle component
vi.mock('@/components/library/FavoriteToggle', () => ({
  FavoriteToggle: ({ gameId, isFavorite, gameTitle }: { gameId: string; isFavorite: boolean; gameTitle: string }) => (
    <button data-testid="favorite-toggle" data-favorite={isFavorite}>
      {isFavorite ? 'Unfavorite' : 'Favorite'} {gameTitle}
    </button>
  ),
}));

// Mock Next.js components
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ============================================================================
// Mock Data
// ============================================================================

const mockGameComplete: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: 'user-123',
  gameId: 'game-configured',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/catan.png',
  addedAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(), // 5 days ago
  notes: 'Classic strategy game for family nights',
  isFavorite: true,
};

const mockGameMinimal: UserLibraryEntry = {
  id: '223e4567-e89b-12d3-a456-426614174001',
  userId: 'user-123',
  gameId: 'game-minimal',
  gameTitle: 'Unknown Game',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: new Date().toISOString(),
  notes: null,
  isFavorite: false,
};

const mockGameNoAgent: UserLibraryEntry = {
  ...mockGameComplete,
  id: '323e4567-e89b-12d3-a456-426614174002',
  gameId: 'game-no-agent',
  gameTitle: 'Ticket to Ride',
  isFavorite: false,
};

// ============================================================================
// Test Callbacks
// ============================================================================

const mockCallbacks = {
  onConfigureAgent: vi.fn(),
  onUploadPdf: vi.fn(),
  onEditNotes: vi.fn(),
  onRemove: vi.fn(),
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('UserGameCard - Rendering', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with complete data', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Kosmos')).toBeInTheDocument();
    expect(screen.getByTestId('game-card')).toBeInTheDocument();
  });

  it('displays cover image when available', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const image = screen.getByAltText('Catan');
    expect(image).toBeInTheDocument();
    expect(image).toHaveAttribute('src', 'https://example.com/catan.png');
  });

  it('displays fallback icon when no image', () => {
    const { container } = render(<UserGameCard game={mockGameMinimal} {...mockCallbacks} />);

    // Check for Library icon (fallback)
    const icon = container.querySelector('svg.lucide-library');
    expect(icon).toBeInTheDocument();
  });

  it('renders minimal data without errors', () => {
    expect(() => {
      render(<UserGameCard game={mockGameMinimal} {...mockCallbacks} />);
    }).not.toThrow();

    expect(screen.getByText('Unknown Game')).toBeInTheDocument();
  });

  it('does not display publisher when null', () => {
    render(<UserGameCard game={mockGameMinimal} {...mockCallbacks} />);

    expect(screen.queryByText('Kosmos')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Agent Configuration Status Tests
// ============================================================================

describe('UserGameCard - Agent Status', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays configured agent with model name', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByText(/Configurato/)).toBeInTheDocument();
    expect(screen.getByText(/Gemini Pro/)).toBeInTheDocument();
  });

  it('displays "Non configurato" when agent not configured', () => {
    render(<UserGameCard game={mockGameNoAgent} {...mockCallbacks} />);

    expect(screen.getByTestId('agent-status-badge')).toHaveTextContent('Non configurato');
  });
});

// ============================================================================
// PDF Status Tests
// ============================================================================

describe('UserGameCard - PDF Status', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays PDF status badge', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByText('Regolamento Standard')).toBeInTheDocument();
  });
});

// ============================================================================
// Notes Preview Tests
// ============================================================================

describe('UserGameCard - Notes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays notes when available', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByText('Classic strategy game for family nights')).toBeInTheDocument();
  });

  it('does not display notes section when null', () => {
    render(<UserGameCard game={mockGameMinimal} {...mockCallbacks} />);

    expect(screen.queryByText('Classic strategy game for family nights')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Action Button Tests
// ============================================================================

describe('UserGameCard - Action Buttons', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders Chat link with correct href', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const chatLink = screen.getByRole('link', { name: /Chatta/i });
    expect(chatLink).toHaveAttribute('href', `/chat?gameId=${mockGameComplete.gameId}`);
  });

  it('calls onConfigureAgent when "Gestisci Agente" clicked', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const button = screen.getByRole('button', { name: /Gestisci Agente/i });
    fireEvent.click(button);

    expect(mockCallbacks.onConfigureAgent).toHaveBeenCalledWith(
      mockGameComplete.gameId,
      mockGameComplete.gameTitle
    );
  });

  it('calls onUploadPdf when "Carica PDF" clicked', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const button = screen.getByRole('button', { name: /Carica PDF/i });
    fireEvent.click(button);

    expect(mockCallbacks.onUploadPdf).toHaveBeenCalledWith(
      mockGameComplete.gameId,
      mockGameComplete.gameTitle
    );
  });

  it('calls onEditNotes when "Note" clicked', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const button = screen.getByRole('button', { name: /Note/i });
    fireEvent.click(button);

    expect(mockCallbacks.onEditNotes).toHaveBeenCalledWith(
      mockGameComplete.gameId,
      mockGameComplete.gameTitle,
      mockGameComplete.notes
    );
  });

  it('calls onRemove when trash button clicked', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    // Find trash button by destructive styling class
    const buttons = screen.getAllByRole('button');
    const trashButton = buttons.find((btn) => btn.classList.contains('text-destructive'));
    expect(trashButton).toBeInTheDocument();

    fireEvent.click(trashButton!);

    expect(mockCallbacks.onRemove).toHaveBeenCalledWith(
      mockGameComplete.gameId,
      mockGameComplete.gameTitle
    );
  });
});

// ============================================================================
// Favorite Toggle Integration Tests
// ============================================================================

describe('UserGameCard - Favorite Toggle', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders FavoriteToggle component', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByTestId('favorite-toggle')).toBeInTheDocument();
  });

  it('passes correct props to FavoriteToggle', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    const toggle = screen.getByTestId('favorite-toggle');
    expect(toggle).toHaveAttribute('data-favorite', 'true');
    expect(toggle).toHaveTextContent('Catan');
  });

  it('reflects non-favorite state correctly', () => {
    render(<UserGameCard game={mockGameMinimal} {...mockCallbacks} />);

    const toggle = screen.getByTestId('favorite-toggle');
    expect(toggle).toHaveAttribute('data-favorite', 'false');
  });
});

// ============================================================================
// Date Formatting Tests
// ============================================================================

describe('UserGameCard - Date Formatting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('displays added date in Italian format', () => {
    render(<UserGameCard game={mockGameComplete} {...mockCallbacks} />);

    expect(screen.getByText(/Aggiunto il/)).toBeInTheDocument();
  });
});

// ============================================================================
// Edge Cases
// ============================================================================

describe('UserGameCard - Edge Cases', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('handles missing optional fields gracefully', () => {
    const partialGame: UserLibraryEntry = {
      ...mockGameMinimal,
      gamePublisher: null,
      gameYearPublished: null,
      notes: null,
    };

    expect(() => {
      render(<UserGameCard game={partialGame} {...mockCallbacks} />);
    }).not.toThrow();
  });

  it('renders correctly with very long game title', () => {
    const longTitleGame: UserLibraryEntry = {
      ...mockGameComplete,
      gameTitle: 'Twilight Imperium: Fourth Edition with Prophecy of Kings Expansion',
    };

    render(<UserGameCard game={longTitleGame} {...mockCallbacks} />);

    // Use getAllByText and select the h3 element (not the mocked FavoriteToggle)
    const titleElements = screen.getAllByText(/Twilight Imperium/i);
    const titleElement = titleElements.find((el) => el.tagName === 'H3');
    expect(titleElement).toHaveClass('line-clamp-2');
  });

  it('renders correctly with very long notes', () => {
    const longNotesGame: UserLibraryEntry = {
      ...mockGameComplete,
      notes: 'A'.repeat(500),
    };

    render(<UserGameCard game={longNotesGame} {...mockCallbacks} />);

    const notesElement = screen.getByText(/A{10,}/);
    expect(notesElement).toHaveClass('line-clamp-2');
  });
});

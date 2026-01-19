/**
 * RecentLibraryCard Component Tests (Issue #2612)
 *
 * Test Coverage:
 * - Rendering with complete/minimal data
 * - Cover image display and fallback
 * - Favorite star badge visibility
 * - Relative time formatting
 * - Manage CTA link
 * - Edge cases (missing data, long titles)
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RecentLibraryCard } from '../RecentLibraryCard';
import type { UserLibraryEntry } from '@/lib/api';

// ============================================================================
// Mock Data
// ============================================================================

const mockGameComplete: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: 'user-123',
  gameId: 'game-456',
  gameTitle: 'Azul',
  gamePublisher: 'Plan B Games',
  gameYearPublished: 2017,
  gameIconUrl: null,
  gameImageUrl: 'https://example.com/azul.png',
  addedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago
  notes: 'Great family game',
  isFavorite: true,
};

const mockGameMinimal: UserLibraryEntry = {
  id: '223e4567-e89b-12d3-a456-426614174001',
  userId: 'user-123',
  gameId: 'game-789',
  gameTitle: 'Mystery Game',
  gamePublisher: null,
  gameYearPublished: null,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: new Date().toISOString(), // Just now
  notes: null,
  isFavorite: false,
};

const mockGameLongTitle: UserLibraryEntry = {
  ...mockGameComplete,
  id: '323e4567-e89b-12d3-a456-426614174002',
  gameTitle: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion Pack',
  isFavorite: false,
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('RecentLibraryCard - Rendering', () => {
  it('renders with complete data', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/Aggiunto/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Gestisci/i })).toBeInTheDocument();
  });

  it('displays cover image when available', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    const image = screen.getByAltText('Azul');
    expect(image).toBeInTheDocument();
  });

  it('displays fallback icon when no image', () => {
    const { container } = render(<RecentLibraryCard game={mockGameMinimal} />);

    // Check for Library icon (fallback)
    const icon = container.querySelector('svg.lucide-library');
    expect(icon).toBeInTheDocument();
  });

  it('renders minimal data without errors', () => {
    expect(() => {
      render(<RecentLibraryCard game={mockGameMinimal} />);
    }).not.toThrow();

    expect(screen.getByText('Mystery Game')).toBeInTheDocument();
  });

  it('truncates long titles with line-clamp-2', () => {
    render(<RecentLibraryCard game={mockGameLongTitle} />);

    const titleElement = screen.getByText(/Twilight Imperium/i);
    expect(titleElement).toHaveClass('line-clamp-2');
  });

  it('provides title attribute for accessibility on long titles', () => {
    render(<RecentLibraryCard game={mockGameLongTitle} />);

    const titleElement = screen.getByTitle(/Twilight Imperium/i);
    expect(titleElement).toBeInTheDocument();
  });
});

// ============================================================================
// Favorite Badge Tests
// ============================================================================

describe('RecentLibraryCard - Favorite Badge', () => {
  it('displays favorite star when isFavorite is true', () => {
    const { container } = render(<RecentLibraryCard game={mockGameComplete} />);

    const starIcon = container.querySelector('svg.lucide-star');
    expect(starIcon).toBeInTheDocument();
    expect(starIcon).toHaveClass('fill-yellow-400');
  });

  it('does not display star when isFavorite is false', () => {
    const { container } = render(<RecentLibraryCard game={mockGameMinimal} />);

    const starIcon = container.querySelector('svg.lucide-star');
    expect(starIcon).not.toBeInTheDocument();
  });
});

// ============================================================================
// Time Formatting Tests
// ============================================================================

describe('RecentLibraryCard - Time Formatting', () => {
  it('displays relative time for recently added games', () => {
    const recentGame: UserLibraryEntry = {
      ...mockGameMinimal,
      addedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 minutes ago
    };
    render(<RecentLibraryCard game={recentGame} />);

    expect(screen.getByText(/Aggiunto.*fa/i)).toBeInTheDocument();
  });

  it('displays relative time for older games', () => {
    const oldGame: UserLibraryEntry = {
      ...mockGameComplete,
      addedAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
    };
    render(<RecentLibraryCard game={oldGame} />);

    expect(screen.getByText(/Aggiunto.*fa/i)).toBeInTheDocument();
  });
});

// ============================================================================
// Navigation Tests
// ============================================================================

describe('RecentLibraryCard - Navigation', () => {
  it('has "Gestisci" link pointing to /library', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    const link = screen.getByRole('link', { name: /Gestisci/i });
    expect(link).toHaveAttribute('href', '/library');
  });

  it('renders as a link with correct styling', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    const button = screen.getByRole('link', { name: /Gestisci/i });
    expect(button).toBeInTheDocument();
  });
});

// ============================================================================
// Styling Tests
// ============================================================================

describe('RecentLibraryCard - Styling', () => {
  it('applies hover shadow transition', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    const card = screen.getByTestId('recent-library-card');
    expect(card).toHaveClass('hover:shadow-md');
    expect(card).toHaveClass('transition-shadow');
  });

  it('has correct image container height', () => {
    render(<RecentLibraryCard game={mockGameComplete} />);

    const imageContainer = screen.getByAltText('Azul').closest('div');
    expect(imageContainer).toHaveClass('h-32');
  });
});

// ============================================================================
// Edge Cases
// ============================================================================

describe('RecentLibraryCard - Edge Cases', () => {
  it('handles null notes gracefully', () => {
    render(<RecentLibraryCard game={mockGameMinimal} />);

    // Should not display notes section
    expect(screen.queryByText('Great family game')).not.toBeInTheDocument();
  });

  it('handles null publisher gracefully', () => {
    render(<RecentLibraryCard game={mockGameMinimal} />);

    // Component should render without publisher
    expect(screen.queryByText('Plan B Games')).not.toBeInTheDocument();
    expect(screen.getByText('Mystery Game')).toBeInTheDocument();
  });

  it('handles future dates gracefully', () => {
    const futureGame: UserLibraryEntry = {
      ...mockGameComplete,
      addedAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(), // Tomorrow
    };

    expect(() => {
      render(<RecentLibraryCard game={futureGame} />);
    }).not.toThrow();
  });
});

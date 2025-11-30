/**
 * GameCard Component Tests (Issue #1830: UI-003)
 *
 * Test Coverage:
 * - Grid and List variants rendering
 * - Rating stars display
 * - Badges (BGG, FAQ count)
 * - Click handlers and keyboard navigation
 * - Image loading with placeholder
 * - Responsive metadata display
 * - Edge cases (missing data, long titles)
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameCard } from '../GameCard';
import type { Game } from '@/lib/api';

// ============================================================================
// Mock Data
// ============================================================================

const mockGameComplete: Game = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  title: 'Azul',
  publisher: 'Plan B Games',
  yearPublished: 2017,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 45,
  bggId: 230802,
  createdAt: '2024-01-15T10:00:00Z',
  imageUrl: 'https://example.com/azul.png',
  averageRating: 7.8,
  faqCount: 12,
};

const mockGameMinimal: Game = {
  id: '223e4567-e89b-12d3-a456-426614174001',
  title: 'Mystery Game',
  publisher: null,
  yearPublished: null,
  minPlayers: null,
  maxPlayers: null,
  minPlayTimeMinutes: null,
  maxPlayTimeMinutes: null,
  bggId: null,
  createdAt: '2024-01-15T10:00:00Z',
  imageUrl: null,
  averageRating: null,
  faqCount: null,
};

const mockGameLongTitle: Game = {
  ...mockGameComplete,
  id: '323e4567-e89b-12d3-a456-426614174002',
  title: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion Pack',
};

// ============================================================================
// Grid Variant Tests
// ============================================================================

describe('GameCard - Grid Variant', () => {
  it('renders grid variant with all data', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Plan B Games')).toBeInTheDocument();
    expect(screen.getByText('BGG')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument(); // FAQ count
    expect(screen.getByText('2017')).toBeInTheDocument();
  });

  it('displays rating stars in grid variant', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    const ratingContainer = screen.getByRole('img', { name: /Rating: 7.8/i });
    expect(ratingContainer).toBeInTheDocument();
  });

  it('shows placeholder image when imageUrl is null', () => {
    render(<GameCard game={mockGameMinimal} variant="grid" />);

    const image = screen.getByAltText('Mystery Game');
    expect(image).toHaveAttribute('src', expect.stringContaining('data:image/svg+xml'));
  });

  it('renders minimal data without errors', () => {
    render(<GameCard game={mockGameMinimal} variant="grid" showRating={false} />);

    expect(screen.getByText('Mystery Game')).toBeInTheDocument();
    expect(screen.queryByText('BGG')).not.toBeInTheDocument();
    expect(screen.queryByRole('img', { name: /Rating/i })).not.toBeInTheDocument();
  });

  it('truncates long titles with line-clamp-2', () => {
    const { container } = render(<GameCard game={mockGameLongTitle} variant="grid" />);

    const titleElement = screen.getByText(/Twilight Imperium/i);
    expect(titleElement).toHaveClass('line-clamp-2');
  });

  it('displays BGG badge when bggId is present', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);
    expect(screen.getByText('BGG')).toBeInTheDocument();
  });

  it('displays FAQ badge when faqCount > 0', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('does not display FAQ badge when faqCount is 0', () => {
    const gameWithZeroFaq = { ...mockGameComplete, faqCount: 0 };
    render(<GameCard game={gameWithZeroFaq} variant="grid" />);

    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });

  it('formats player count correctly for same min/max', () => {
    const gameSamePlayers = { ...mockGameComplete, minPlayers: 4, maxPlayers: 4 };
    render(<GameCard game={gameSamePlayers} variant="grid" />);

    expect(screen.getByText('4')).toBeInTheDocument();
  });

  it('formats player count correctly for range', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    expect(screen.getByText('2–4')).toBeInTheDocument();
  });

  it('formats play time correctly for range', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    expect(screen.getByText('30–45 min')).toBeInTheDocument();
  });

  it('formats play time correctly for same min/max', () => {
    const gameSameTime = { ...mockGameComplete, minPlayTimeMinutes: 60, maxPlayTimeMinutes: 60 };
    render(<GameCard game={gameSameTime} variant="grid" />);

    expect(screen.getByText('60 min')).toBeInTheDocument();
  });
});

// ============================================================================
// List Variant Tests
// ============================================================================

describe('GameCard - List Variant', () => {
  it('renders list variant with all data', () => {
    render(<GameCard game={mockGameComplete} variant="list" />);

    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Plan B Games')).toBeInTheDocument();
    expect(screen.getByText('BGG')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('displays rating with value in list variant', () => {
    render(<GameCard game={mockGameComplete} variant="list" />);

    expect(screen.getByText('7.8')).toBeInTheDocument();
  });

  it('uses smaller image in list variant', () => {
    render(<GameCard game={mockGameComplete} variant="list" />);

    const image = screen.getByAltText('Azul');
    expect(image).toHaveAttribute('sizes', '48px');
  });

  it('displays publisher in content area for list variant', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="list" />);

    const publisherElements = screen.getAllByText('Plan B Games');
    // Should appear in content area (not header)
    expect(publisherElements.length).toBeGreaterThan(0);
  });
});

// ============================================================================
// Interaction Tests
// ============================================================================

describe('GameCard - Interactions', () => {
  it('calls onClick when card is clicked', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(<GameCard game={mockGameComplete} variant="grid" onClick={handleClick} />);

    const card = screen.getByRole('button', { name: /Game: Azul/i });
    await user.click(card);

    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('calls onClick when Enter key is pressed', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(<GameCard game={mockGameComplete} variant="grid" onClick={handleClick} />);

    const card = screen.getByRole('button', { name: /Game: Azul/i });
    card.focus();
    await user.keyboard('{Enter}');

    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('calls onClick when Space key is pressed', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(<GameCard game={mockGameComplete} variant="grid" onClick={handleClick} />);

    const card = screen.getByRole('button', { name: /Game: Azul/i });
    card.focus();
    await user.keyboard(' ');

    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('does not have button role when onClick is not provided', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('is not focusable when onClick is not provided', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="grid" />);

    const card = container.firstChild as HTMLElement;
    expect(card).not.toHaveAttribute('tabindex');
  });
});

// ============================================================================
// Rating Display Tests
// ============================================================================

describe('GameCard - Rating Display', () => {
  it('shows rating when averageRating is present and showRating is true', () => {
    render(<GameCard game={mockGameComplete} variant="grid" showRating={true} />);

    expect(screen.getByRole('img', { name: /Rating: 7.8/i })).toBeInTheDocument();
  });

  it('hides rating when showRating is false', () => {
    render(<GameCard game={mockGameComplete} variant="grid" showRating={false} />);

    expect(screen.queryByRole('img', { name: /Rating/i })).not.toBeInTheDocument();
  });

  it('does not show rating when averageRating is null', () => {
    render(<GameCard game={mockGameMinimal} variant="grid" showRating={true} />);

    expect(screen.queryByRole('img', { name: /Rating/i })).not.toBeInTheDocument();
  });

  it('uses correct star size for grid variant', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="grid" />);

    const stars = container.querySelectorAll('svg');
    const starElement = stars[0]; // First star icon
    expect(starElement).toHaveClass('h-3', 'w-3'); // sm size
  });

  it('uses correct star size for list variant', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="list" />);

    // Skip first SVG (HelpCircle icon from FAQ badge), get star icons
    const stars = container.querySelectorAll('svg');
    // Find the first star icon (after HelpCircle)
    const starElement = Array.from(stars).find(svg =>
      svg.className.baseVal.includes('lucide-star')
    );
    expect(starElement).toHaveClass('h-4', 'w-4'); // md size
  });
});

// ============================================================================
// Styling and Classes Tests
// ============================================================================

describe('GameCard - Styling', () => {
  it('applies hover animation classes', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="grid" />);

    const card = container.firstChild as HTMLElement;
    expect(card).toHaveClass('hover:-translate-y-1');
    expect(card).toHaveClass('transition-all');
  });

  it('applies grid-specific layout classes', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="grid" />);

    const card = container.firstChild as HTMLElement;
    expect(card).toHaveClass('flex', 'flex-col');
  });

  it('applies list-specific layout classes', () => {
    const { container } = render(<GameCard game={mockGameComplete} variant="list" />);

    const card = container.firstChild as HTMLElement;
    expect(card).toHaveClass('flex', 'flex-row');
  });

  it('applies Quicksand font to title', () => {
    render(<GameCard game={mockGameComplete} variant="grid" />);

    const title = screen.getByText('Azul');
    expect(title).toHaveClass('font-quicksand');
  });

  it('accepts custom className prop', () => {
    const { container } = render(
      <GameCard game={mockGameComplete} variant="grid" className="custom-class" />
    );

    const card = container.firstChild as HTMLElement;
    expect(card).toHaveClass('custom-class');
  });
});

// ============================================================================
// Edge Cases and Error Handling
// ============================================================================

describe('GameCard - Edge Cases', () => {
  it('handles null publisher gracefully', () => {
    render(<GameCard game={mockGameMinimal} variant="grid" />);

    expect(screen.queryByText('Publisher')).not.toBeInTheDocument();
  });

  it('handles null yearPublished gracefully', () => {
    render(<GameCard game={mockGameMinimal} variant="grid" />);

    expect(screen.queryByText(/\d{4}/)).not.toBeInTheDocument();
  });

  it('handles null player counts gracefully', () => {
    const { container } = render(<GameCard game={mockGameMinimal} variant="grid" />);

    const usersIcon = container.querySelector('svg[class*="lucide-users"]');
    expect(usersIcon).not.toBeInTheDocument();
  });

  it('handles partial player count data (only min)', () => {
    const gamePartialPlayers = { ...mockGameComplete, minPlayers: 2, maxPlayers: null };
    render(<GameCard game={gamePartialPlayers} variant="grid" />);

    expect(screen.getByText('2–?')).toBeInTheDocument();
  });

  it('handles partial player count data (only max)', () => {
    const gamePartialPlayers = { ...mockGameComplete, minPlayers: null, maxPlayers: 4 };
    render(<GameCard game={gamePartialPlayers} variant="grid" />);

    expect(screen.getByText('?–4')).toBeInTheDocument();
  });

  it('renders without crashing when all optional fields are null', () => {
    expect(() => {
      render(<GameCard game={mockGameMinimal} variant="grid" showRating={false} />);
    }).not.toThrow();
  });
});

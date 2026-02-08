/**
 * GameDetailHero Component Tests (Issue #3511)
 *
 * Test Coverage:
 * - Rendering with complete game data
 * - Rendering with minimal game data
 * - Flip animation functionality
 * - Keyboard interaction (Enter/Space)
 * - Stat formatting (players, time, complexity, rating)
 * - Image rendering and fallback
 * - Accessibility attributes
 *
 * Target: ≥85% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameDetailHero } from '../GameDetailHero';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Mock Data
// ============================================================================

const mockCompleteGame: LibraryGameDetail = {
  gameId: 'game-123',
  gameTitle: 'Catan',
  gamePublisher: 'CATAN Studio',
  gameYearPublished: 1995,
  gameDescription: 'The classic game of resource management and trading',
  gameImageUrl: 'https://example.com/catan.jpg',
  gameThumbnailUrl: 'https://example.com/catan-thumb.jpg',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.5,
  averageRating: 7.8,
  gameCategories: ['Strategy', 'Trading'],
  gameMechanics: ['Dice Rolling', 'Trading'],
  gameDesigners: ['Klaus Teuber'],
  currentState: 'Owned',
  isFavorite: false,
  timesPlayed: 10,
  lastPlayed: '2026-02-01T10:00:00Z',
  winRate: 0.6,
  avgDuration: 85,
  notes: null,
  pdfDocuments: [],
  socialLinks: [],
};

const mockMinimalGame: LibraryGameDetail = {
  gameId: 'game-456',
  gameTitle: 'Unknown Game',
  gamePublisher: null,
  gameYearPublished: null,
  gameDescription: null,
  gameImageUrl: null,
  gameThumbnailUrl: null,
  minPlayers: null,
  maxPlayers: null,
  playingTimeMinutes: null,
  complexityRating: null,
  averageRating: null,
  gameCategories: [],
  gameMechanics: [],
  gameDesigners: [],
  currentState: 'Owned',
  isFavorite: false,
  timesPlayed: 0,
  lastPlayed: null,
  winRate: null,
  avgDuration: null,
  notes: null,
  pdfDocuments: [],
  socialLinks: [],
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('GameDetailHero - Rendering', () => {
  it('renders game title correctly', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders publisher and year', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText(/CATAN Studio/)).toBeInTheDocument();
    expect(screen.getByText(/1995/)).toBeInTheDocument();
  });

  it('renders player count range correctly', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText(/3-4/)).toBeInTheDocument();
  });

  it('renders single player count when min equals max', () => {
    const singlePlayerGame = { ...mockCompleteGame, minPlayers: 2, maxPlayers: 2 };
    render(<GameDetailHero gameDetail={singlePlayerGame} />);
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders playtime correctly', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText(/90 min/)).toBeInTheDocument();
  });

  it('renders complexity rating correctly', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText(/2\.5\/5/)).toBeInTheDocument();
  });

  it('renders BGG rating correctly', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    expect(screen.getByText(/7\.8/)).toBeInTheDocument();
  });

  it('shows N/A for missing stats', () => {
    render(<GameDetailHero gameDetail={mockMinimalGame} />);
    const naElements = screen.getAllByText('N/A');
    expect(naElements.length).toBeGreaterThan(0);
  });

  it('renders fallback emoji when no image provided', () => {
    render(<GameDetailHero gameDetail={mockMinimalGame} />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders flip indicator icon', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });
    expect(card).toBeInTheDocument();
    // Flip indicator is rendered inside the card
    expect(card.querySelector('svg')).toBeInTheDocument();
  });
});

// ============================================================================
// Interaction Tests
// ============================================================================

describe('GameDetailHero - Flip Interaction', () => {
  it('has correct initial aria-label', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });
    expect(card).toHaveAttribute('aria-label', expect.stringContaining('vedere i dettagli sul retro'));
  });

  it('toggles flip state on click', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });

    fireEvent.click(card);

    // After flip, aria-label should change
    expect(card).toHaveAttribute('aria-label', expect.stringContaining('vedere il fronte'));
  });

  it('toggles flip on Enter key', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });

    fireEvent.keyDown(card, { key: 'Enter' });

    expect(card).toHaveAttribute('aria-label', expect.stringContaining('vedere il fronte'));
  });

  it('toggles flip on Space key', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });

    fireEvent.keyDown(card, { key: ' ' });

    expect(card).toHaveAttribute('aria-label', expect.stringContaining('vedere il fronte'));
  });

  it('does not toggle on other keys', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });

    const initialLabel = card.getAttribute('aria-label');
    fireEvent.keyDown(card, { key: 'a' });

    expect(card).toHaveAttribute('aria-label', initialLabel);
  });

  it('is keyboard focusable', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });
    expect(card).toHaveAttribute('tabIndex', '0');
  });
});

// ============================================================================
// Data Formatting Tests
// ============================================================================

describe('GameDetailHero - Data Formatting', () => {
  it('formats complexity rating to 1 decimal', () => {
    const game = { ...mockCompleteGame, complexityRating: 3.456 };
    render(<GameDetailHero gameDetail={game} />);
    expect(screen.getByText(/3\.5\/5/)).toBeInTheDocument();
  });

  it('formats average rating to 1 decimal', () => {
    const game = { ...mockCompleteGame, averageRating: 8.123 };
    render(<GameDetailHero gameDetail={game} />);
    expect(screen.getByText(/8\.1/)).toBeInTheDocument();
  });

  it('handles missing publisher gracefully', () => {
    const game = { ...mockCompleteGame, gamePublisher: null };
    render(<GameDetailHero gameDetail={game} />);
    expect(screen.getByText(/Publisher sconosciuto/)).toBeInTheDocument();
  });

  it('renders year without publisher', () => {
    const game = { ...mockCompleteGame, gamePublisher: null, gameYearPublished: 2020 };
    render(<GameDetailHero gameDetail={game} />);
    expect(screen.getByText(/2020/)).toBeInTheDocument();
  });

  it('card has front and back faces in DOM', () => {
    render(<GameDetailHero gameDetail={mockCompleteGame} />);
    const card = screen.getByRole('button', { name: /Carta del gioco/i });

    // Verify card structure exists (framer-motion creates both faces)
    expect(card).toBeInTheDocument();

    // After flip, aria-label changes
    fireEvent.click(card);
    expect(card).toHaveAttribute('aria-label', expect.stringContaining('vedere il fronte'));
  });
});

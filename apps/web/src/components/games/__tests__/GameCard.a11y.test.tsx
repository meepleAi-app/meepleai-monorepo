/**
 * Accessibility Tests for GameCard (Issue #2929)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 *
 * Coverage:
 * - Grid and list variants
 * - Interactive states (clickable vs static)
 * - Rating accessibility
 * - Image alt text
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { GameCard } from '../GameCard';
import type { Game } from '@/lib/api';

// Mock data
const mockGame: Game = {
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

describe('GameCard - Accessibility', () => {
  describe('Grid Variant', () => {
    it('should have no accessibility violations (grid variant)', async () => {
      const { container } = render(<GameCard game={mockGame} variant="grid" />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations (grid with click handler)', async () => {
      const { container } = render(
        <GameCard game={mockGame} variant="grid" onClick={() => {}} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations (minimal data)', async () => {
      const { container } = render(
        <GameCard game={mockGameMinimal} variant="grid" showRating={false} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('List Variant', () => {
    it('should have no accessibility violations (list variant)', async () => {
      const { container } = render(<GameCard game={mockGame} variant="list" />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations (list with click handler)', async () => {
      const { container } = render(
        <GameCard game={mockGame} variant="list" onClick={() => {}} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Interactive States', () => {
    it('should have proper aria-label when interactive', () => {
      render(<GameCard game={mockGame} variant="grid" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /game: azul/i });
      expect(card).toHaveAttribute('aria-label', 'Game: Azul');
    });

    it('should have aria-label even when not interactive', () => {
      render(<GameCard game={mockGame} variant="grid" />);

      const card = screen.getByLabelText(/game: azul/i);
      expect(card).toBeInTheDocument();
    });

    it('should be keyboard accessible when interactive', () => {
      render(<GameCard game={mockGame} variant="grid" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /game: azul/i });
      expect(card).toHaveAttribute('tabindex', '0');
    });

    it('should not be tabbable when not interactive', () => {
      render(<GameCard game={mockGame} variant="grid" />);

      const card = screen.getByLabelText(/game: azul/i);
      expect(card).not.toHaveAttribute('tabindex');
    });
  });

  describe('Image Accessibility', () => {
    it('should have alt text on game image', () => {
      render(<GameCard game={mockGame} variant="grid" />);

      const image = screen.getByAltText('Azul');
      expect(image).toBeInTheDocument();
    });

    it('should have alt text even with placeholder image', () => {
      render(<GameCard game={mockGameMinimal} variant="grid" />);

      const image = screen.getByAltText('Mystery Game');
      expect(image).toBeInTheDocument();
    });
  });

  describe('Rating Accessibility', () => {
    it('should have accessible rating display', () => {
      render(<GameCard game={mockGame} variant="grid" showRating={true} />);

      // Rating stars should have accessible name
      const rating = screen.getByRole('img', { name: /rating: 7.8/i });
      expect(rating).toBeInTheDocument();
    });
  });
});

/**
 * SharedLibraryGameCard Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Game card rendering with image
 * - Game card rendering without image (fallback)
 * - Favorite badge display
 * - Publisher and year display
 * - Notes display when enabled
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { SharedLibraryGameCard } from '../SharedLibraryGameCard';
import { createMockSharedLibraryGame } from '@/__tests__/fixtures/common-fixtures';

describe('SharedLibraryGameCard', () => {
  describe('Basic Rendering', () => {
    it('renders game title', () => {
      const game = createMockSharedLibraryGame({ title: 'Settlers of Catan' });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('Settlers of Catan')).toBeInTheDocument();
    });

    it('renders publisher when available', () => {
      const game = createMockSharedLibraryGame({ publisher: 'Kosmos Games' });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('Kosmos Games')).toBeInTheDocument();
    });

    it('does not render publisher when null', () => {
      const game = createMockSharedLibraryGame({ publisher: null });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.queryByText('Kosmos Games')).not.toBeInTheDocument();
    });

    it('renders year published when available', () => {
      const game = createMockSharedLibraryGame({ yearPublished: 1995 });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('1995')).toBeInTheDocument();
    });

    it('does not render year when null', () => {
      const game = createMockSharedLibraryGame({ yearPublished: null });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.queryByText('1995')).not.toBeInTheDocument();
    });
  });

  describe('Image Handling', () => {
    it('renders image when imageUrl is provided', () => {
      const game = createMockSharedLibraryGame({
        title: 'Chess',
        imageUrl: 'https://example.com/chess.jpg',
      });
      render(<SharedLibraryGameCard game={game} />);

      const img = screen.getByRole('img', { name: 'Chess' });
      expect(img).toBeInTheDocument();
    });

    it('renders fallback initial when no imageUrl', () => {
      const game = createMockSharedLibraryGame({
        title: 'Monopoly',
        imageUrl: null,
      });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('M')).toBeInTheDocument();
    });

    it('renders uppercase first letter as fallback', () => {
      const game = createMockSharedLibraryGame({
        title: 'risk',
        imageUrl: null,
      });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('R')).toBeInTheDocument();
    });
  });

  describe('Favorite Badge', () => {
    it('shows favorite badge when isFavorite is true', () => {
      const game = createMockSharedLibraryGame({ isFavorite: true });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.getByText('Preferito')).toBeInTheDocument();
    });

    it('does not show favorite badge when isFavorite is false', () => {
      const game = createMockSharedLibraryGame({ isFavorite: false });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.queryByText('Preferito')).not.toBeInTheDocument();
    });
  });

  describe('Notes Display', () => {
    it('shows notes when showNotes is true and notes exist', () => {
      const game = createMockSharedLibraryGame({
        notes: 'Great family game for weekends',
      });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(screen.getByText('Note')).toBeInTheDocument();
      expect(screen.getByText('Great family game for weekends')).toBeInTheDocument();
    });

    it('does not show notes section when showNotes is false', () => {
      const game = createMockSharedLibraryGame({
        notes: 'Great family game',
      });
      render(<SharedLibraryGameCard game={game} showNotes={false} />);

      expect(screen.queryByText('Note')).not.toBeInTheDocument();
    });

    it('does not show notes section when notes are null', () => {
      const game = createMockSharedLibraryGame({ notes: null });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(screen.queryByText('Note')).not.toBeInTheDocument();
    });

    it('does not show notes by default (showNotes defaults to false)', () => {
      const game = createMockSharedLibraryGame({
        notes: 'Some notes here',
      });
      render(<SharedLibraryGameCard game={game} />);

      expect(screen.queryByText('Note')).not.toBeInTheDocument();
    });
  });

  describe('Card Structure', () => {
    it('has hover shadow transition', () => {
      const game = createMockSharedLibraryGame();
      const { container } = render(<SharedLibraryGameCard game={game} />);

      const card = container.querySelector('.hover\\:shadow-md');
      expect(card).toBeInTheDocument();
    });

    it('renders with overflow hidden', () => {
      const game = createMockSharedLibraryGame();
      const { container } = render(<SharedLibraryGameCard game={game} />);

      const card = container.querySelector('.overflow-hidden');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Complete Game Card', () => {
    it('renders all elements for a complete game', () => {
      const game = createMockSharedLibraryGame({
        title: 'Ticket to Ride',
        publisher: 'Days of Wonder',
        yearPublished: 2004,
        imageUrl: 'https://example.com/ttr.jpg',
        isFavorite: true,
        notes: 'Best travel-themed game!',
      });
      render(<SharedLibraryGameCard game={game} showNotes={true} />);

      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      expect(screen.getByText('Days of Wonder')).toBeInTheDocument();
      expect(screen.getByText('2004')).toBeInTheDocument();
      expect(screen.getByText('Preferito')).toBeInTheDocument();
      expect(screen.getByText('Best travel-themed game!')).toBeInTheDocument();
      expect(screen.getByRole('img', { name: 'Ticket to Ride' })).toBeInTheDocument();
    });
  });
});

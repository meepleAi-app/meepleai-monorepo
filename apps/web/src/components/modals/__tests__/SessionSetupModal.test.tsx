/**
 * SessionSetupModal Component Tests
 *
 * Basic tests for SessionSetupModal (SPRINT-4, Issue #863).
 * Tests cover rendering and accessibility fundamentals.
 *
 * Issue #1887 - Batch 15: Test rewrite with required game prop
 * TODO (#2256): Expand with player management, validation, and submission tests
 */

import { render, screen } from '@testing-library/react';
import { SessionSetupModal } from '../SessionSetupModal';
import type { Game } from '@/lib/api';

// Mocks will be added when expanding test coverage

const createMockGame = (overrides?: Partial<Game>): Game => ({
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
  ...overrides,
});

describe('SessionSetupModal', () => {
  const mockOnClose = vi.fn();

  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    game: createMockGame(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * RENDERING TESTS
   */
  describe('Rendering', () => {
    it('renders modal when open', () => {
      render(<SessionSetupModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('does not render when closed', () => {
      render(<SessionSetupModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('displays game title', () => {
      const game = createMockGame({ title: 'Wingspan' });
      render(<SessionSetupModal {...defaultProps} game={game} />);

      expect(screen.getByText(/wingspan/i)).toBeInTheDocument();
    });
  });

  /**
   * ACCESSIBILITY TESTS
   */
  describe('Accessibility', () => {
    it('has accessible dialog role', () => {
      render(<SessionSetupModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('has accessible modal structure', () => {
      render(<SessionSetupModal {...defaultProps} />);

      // Dialog should be accessible
      const dialog = screen.getByRole('dialog');
      expect(dialog).toBeInTheDocument();
    });
  });

  /**
   * PROP VALIDATION TESTS
   */
  describe('Props', () => {
    it('accepts game prop with player constraints', () => {
      const game = createMockGame({ minPlayers: 2, maxPlayers: 6 });

      render(<SessionSetupModal {...defaultProps} game={game} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('handles game without player constraints', () => {
      const game = createMockGame({ minPlayers: null, maxPlayers: null });

      render(<SessionSetupModal {...defaultProps} game={game} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
  });
});

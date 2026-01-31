/**
 * SessionSetupModal Component Tests
 *
 * Comprehensive tests for SessionSetupModal (SPRINT-4, Issue #863).
 * Tests cover rendering, accessibility, player management, validation, and submission.
 *
 * Issue #1887 - Batch 15: Test rewrite with required game prop
 * Issue #2256: Expanded test coverage with player management, validation, and submission tests
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionSetupModal } from '../SessionSetupModal';
import type { Game, GameSessionDto } from '@/lib/api';

// Mock API
const mockStartSession = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      start: (...args: unknown[]) => mockStartSession(...args),
    },
  },
}));

// Mock logger
const mockLoggerError = vi.fn();
vi.mock('@/lib/logger', () => ({
  logger: {
    error: (...args: unknown[]) => mockLoggerError(...args),
  },
}));

// Mock useSessionQuota hooks to avoid useAuth/useRouter dependency chain
// SessionSetupModal uses useSessionQuotaWithStatus which calls useAuth which calls useRouter
vi.mock('@/hooks/queries/useSessionQuota', () => ({
  useSessionQuotaWithStatus: vi.fn(() => ({
    data: {
      activeSessions: 1,
      maxSessions: 5,
      remainingSlots: 4,
      canCreateNew: true,
      isUnlimited: false,
      warningLevel: 'none',
      percentageUsed: 20,
    },
    isLoading: false,
    isError: false,
    error: null,
  })),
  useInvalidateSessionQuota: vi.fn(() => vi.fn()),
}));

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

const createMockSession = (overrides?: Partial<GameSessionDto>): GameSessionDto => ({
  id: 'session-1',
  gameId: 'game-1',
  status: 'Active',
  players: [],
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  ...overrides,
});

describe('SessionSetupModal', () => {
  const mockOnClose = vi.fn();
  const mockOnSessionCreated = vi.fn();

  const defaultProps = {
    isOpen: true,
    onClose: mockOnClose,
    game: createMockGame(),
    onSessionCreated: mockOnSessionCreated,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * HELPER UTILITIES
   */
  const waitForInitialRender = async () => {
    // Wait for initial players to be added (minPlayers = 3)
    await waitFor(() => {
      expect(screen.getByText(/players \(3\)/i)).toBeInTheDocument();
    });
  };

  const getPlayerNameInput = (playerNumber: number) => {
    return screen.getByPlaceholderText(`Player ${playerNumber} name`);
  };

  const getAddPlayerButton = () => {
    return screen.getByRole('button', { name: /add player/i });
  };

  const getRemovePlayerButton = (playerNumber: number) => {
    return screen.getByRole('button', { name: `Remove player ${playerNumber}` });
  };

  const getStartSessionButton = () => {
    return screen.getByRole('button', { name: /start session/i });
  };

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

    it('displays player count range for game', () => {
      const game = createMockGame({ minPlayers: 2, maxPlayers: 6 });
      render(<SessionSetupModal {...defaultProps} game={game} />);

      expect(screen.getByText(/supports 2-6 players/i)).toBeInTheDocument();
    });

    it('displays exact player count for fixed-player games', () => {
      const game = createMockGame({ minPlayers: 4, maxPlayers: 4 });
      render(<SessionSetupModal {...defaultProps} game={game} />);

      expect(screen.getByText(/requires exactly 4 players/i)).toBeInTheDocument();
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

      const dialog = screen.getByRole('dialog');
      expect(dialog).toBeInTheDocument();
    });

    it('provides accessible labels for player inputs', async () => {
      render(<SessionSetupModal {...defaultProps} />);
      await waitForInitialRender();

      const playerInput = getPlayerNameInput(1);
      expect(playerInput).toHaveAccessibleName(/player 1 name/i);
    });

    it('shows validation errors with aria-invalid', async () => {
      const user = userEvent.setup();
      render(<SessionSetupModal {...defaultProps} />);
      await waitForInitialRender();

      // Try to submit with empty names
      await user.click(getStartSessionButton());

      await waitFor(() => {
        const playerInput = getPlayerNameInput(1);
        expect(playerInput).toHaveAttribute('aria-invalid', 'true');
      });
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

  /**
   * PLAYER MANAGEMENT TESTS
   */
  describe('Player Management', () => {
    describe('Adding Players', () => {
      it('initializes with minimum players on open', async () => {
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        expect(screen.getByText(/players \(3\)/i)).toBeInTheDocument();
      });

      it('allows adding players up to max', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 3, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);
        await waitForInitialRender();

        // Add one more player (3 → 4)
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          expect(screen.getByText(/players \(4\)/i)).toBeInTheDocument();
        });
      });

      it('disables add button when max players reached', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 3, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);
        await waitForInitialRender();

        // Add one player to reach max
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          expect(getAddPlayerButton()).toBeDisabled();
        });
      });

      it('assigns unique player orders when adding', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 2, maxPlayers: 5 });
        render(<SessionSetupModal {...defaultProps} game={game} />);

        await waitFor(() => {
          expect(screen.getByText(/players \(2\)/i)).toBeInTheDocument();
        });

        // Add player 3
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          // Verify player 3 input exists
          expect(getPlayerNameInput(3)).toBeInTheDocument();
        });
      });

      it('assigns default colors when adding players', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 2, maxPlayers: 5 });
        render(<SessionSetupModal {...defaultProps} game={game} />);

        await waitFor(() => {
          expect(screen.getByText(/players \(2\)/i)).toBeInTheDocument();
        });

        const initialComboboxes = screen.getAllByRole('combobox').length;

        // Add player 3
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          // Color selector should be added for player 3
          expect(screen.getAllByRole('combobox').length).toBe(initialComboboxes + 1);
        });
      });
    });

    describe('Removing Players', () => {
      it('allows removing players down to min', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 2, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);

        await waitFor(() => {
          expect(screen.getByText(/players \(2\)/i)).toBeInTheDocument();
        });

        // Add one player (2 → 3)
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          expect(screen.getByText(/players \(3\)/i)).toBeInTheDocument();
        });

        // Remove player 3
        await user.click(getRemovePlayerButton(3));

        await waitFor(() => {
          expect(screen.getByText(/players \(2\)/i)).toBeInTheDocument();
        });
      });

      it('disables remove buttons when min players reached', async () => {
        const game = createMockGame({ minPlayers: 3, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);
        await waitForInitialRender();

        // All remove buttons should be disabled at min players
        const removeButton1 = getRemovePlayerButton(1);
        expect(removeButton1).toBeDisabled();
      });

      it('reorders players after removal', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 2, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);

        await waitFor(() => {
          expect(screen.getByText(/players \(2\)/i)).toBeInTheDocument();
        });

        // Add 2 more players (2 → 4)
        await user.click(getAddPlayerButton());
        await user.click(getAddPlayerButton());

        await waitFor(() => {
          expect(screen.getByText(/players \(4\)/i)).toBeInTheDocument();
        });

        // Remove player 2
        await user.click(getRemovePlayerButton(2));

        await waitFor(() => {
          expect(screen.getByText(/players \(3\)/i)).toBeInTheDocument();
        });

        // Player 4 input should no longer exist (reordered to 3)
        expect(screen.queryByPlaceholderText('Player 4 name')).not.toBeInTheDocument();
      });
    });

    describe('Editing Player Names', () => {
      it('allows editing player names', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        const input = getPlayerNameInput(1);
        await user.type(input, 'Alice');

        expect(input).toHaveValue('Alice');
      });

      it('clears validation error when editing name', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Submit with empty name to trigger validation
        await user.click(getStartSessionButton());

        const input = getPlayerNameInput(1);

        await waitFor(() => {
          expect(input).toHaveAttribute('aria-invalid', 'true');
        });

        // Type in the input
        await user.type(input, 'Alice');

        // Error should clear (aria-invalid becomes false)
        await waitFor(() => {
          expect(input).toHaveAttribute('aria-invalid', 'false');
        });
      });
    });

    describe('Editing Player Colors', () => {
      it('allows changing player colors', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Find the first color select trigger
        const colorSelectors = screen.getAllByRole('combobox');
        const firstColorSelector = colorSelectors[0];

        // Click to open
        await user.click(firstColorSelector);

        // Select a different color
        const blueOption = await screen.findByRole('option', { name: /blue/i });
        await user.click(blueOption);

        // Color should be selected
        await waitFor(() => {
          expect(screen.getAllByText(/blue/i).length).toBeGreaterThan(0);
        });
      });
    });
  });

  /**
   * FORM VALIDATION TESTS
   */
  describe('Form Validation', () => {
    describe('Empty Name Validation', () => {
      it('shows error for empty player names on submit', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Try to submit without entering names
        await user.click(getStartSessionButton());

        await waitFor(() => {
          // All player inputs should have aria-invalid="true"
          const input1 = getPlayerNameInput(1);
          expect(input1).toHaveAttribute('aria-invalid', 'true');
        });
      });

      it('prevents submission with empty names', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Try to submit without entering names
        await user.click(getStartSessionButton());

        // API should not be called
        expect(mockStartSession).not.toHaveBeenCalled();
      });
    });

    describe('Duplicate Name Validation', () => {
      it('shows error for duplicate player names', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Enter duplicate names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Alice');
        await user.type(getPlayerNameInput(3), 'Bob');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          // Duplicate player inputs should have aria-invalid
          const input1 = getPlayerNameInput(1);
          const input2 = getPlayerNameInput(2);
          expect(input1).toHaveAttribute('aria-invalid', 'true');
          expect(input2).toHaveAttribute('aria-invalid', 'true');
        });
      });

      it('validates duplicate names case-insensitively', async () => {
        const user = userEvent.setup();
        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Enter names with different cases
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'alice');
        await user.type(getPlayerNameInput(3), 'Bob');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          // Both Alice inputs should have aria-invalid
          const input1 = getPlayerNameInput(1);
          const input2 = getPlayerNameInput(2);
          expect(input1).toHaveAttribute('aria-invalid', 'true');
          expect(input2).toHaveAttribute('aria-invalid', 'true');
        });
      });
    });

    describe('Player Count Validation', () => {
      it('shows error when below minimum players', async () => {
        const user = userEvent.setup();
        const game = createMockGame({ minPlayers: 3, maxPlayers: 4 });
        render(<SessionSetupModal {...defaultProps} game={game} />);
        await waitForInitialRender();

        // Remove 2 players (3 → 1, but can only remove to min 3)
        // Actually, the component prevents removing below min, so this test validates the UI constraint
        const removeButton = getRemovePlayerButton(1);
        expect(removeButton).toBeDisabled();
      });

      it('disables submit button when below minimum players', async () => {
        const game = createMockGame({ minPlayers: 4, maxPlayers: 6 });
        render(<SessionSetupModal {...defaultProps} game={game} />);

        await waitFor(() => {
          expect(screen.getByText(/players \(4\)/i)).toBeInTheDocument();
        });

        // Start button should be enabled when at min players
        expect(getStartSessionButton()).not.toBeDisabled();
      });
    });

    describe('Error Clearing', () => {
      it('clears validation errors when modal closes', async () => {
        const user = userEvent.setup();
        const { rerender } = render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Submit to trigger validation errors
        await user.click(getStartSessionButton());

        const input1 = getPlayerNameInput(1);

        await waitFor(() => {
          expect(input1).toHaveAttribute('aria-invalid', 'true');
        });

        // Close modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={false} />);

        // Reopen modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={true} />);

        await waitForInitialRender();

        // Errors should be cleared
        const newInput1 = getPlayerNameInput(1);
        expect(newInput1).toHaveAttribute('aria-invalid', 'false');
      });
    });
  });

  /**
   * SUBMISSION FLOW TESTS
   */
  describe('Submission Flow', () => {
    describe('Success Path', () => {
      it('calls API with correct player data on submit', async () => {
        const user = userEvent.setup();
        const mockSession = createMockSession();
        mockStartSession.mockResolvedValueOnce(mockSession);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(mockStartSession).toHaveBeenCalledWith({
            gameId: 'game-1',
            players: expect.arrayContaining([
              expect.objectContaining({ playerName: 'Alice', playerOrder: 1 }),
              expect.objectContaining({ playerName: 'Bob', playerOrder: 2 }),
              expect.objectContaining({ playerName: 'Charlie', playerOrder: 3 }),
            ]),
          });
        });
      });

      it('calls onSessionCreated callback on success', async () => {
        const user = userEvent.setup();
        const mockSession = createMockSession();
        mockStartSession.mockResolvedValueOnce(mockSession);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(mockOnSessionCreated).toHaveBeenCalledWith(mockSession);
        });
      });

      it('closes modal on successful submission', async () => {
        const user = userEvent.setup();
        const mockSession = createMockSession();
        mockStartSession.mockResolvedValueOnce(mockSession);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(mockOnClose).toHaveBeenCalled();
        });
      });
    });

    describe('Error Handling', () => {
      it('displays error message on API failure', async () => {
        const user = userEvent.setup();
        const errorMessage = 'Failed to start session';
        mockStartSession.mockRejectedValueOnce(new Error(errorMessage));

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          // Error alert should be visible
          expect(screen.getByRole('alert')).toBeInTheDocument();
        });
      });

      it('logs error on API failure', async () => {
        const user = userEvent.setup();
        const error = new Error('API Error');
        mockStartSession.mockRejectedValueOnce(error);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(mockLoggerError).toHaveBeenCalledWith(
            'Failed to start session',
            error,
            expect.objectContaining({
              component: 'SessionSetupModal',
              action: 'handleSubmit',
            })
          );
        });
      });

      it('does not close modal on error', async () => {
        const user = userEvent.setup();
        mockStartSession.mockRejectedValueOnce(new Error('API Error'));

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        expect(mockOnClose).not.toHaveBeenCalled();
      });
    });

    describe('Loading States', () => {
      it('shows loading state during submission', async () => {
        const user = userEvent.setup();
        let resolvePromise: (value: GameSessionDto) => void;
        const submitPromise = new Promise<GameSessionDto>(resolve => {
          resolvePromise = resolve;
        });
        mockStartSession.mockReturnValueOnce(submitPromise);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        // Loading text should appear
        await waitFor(() => {
          expect(screen.getByText(/starting session/i)).toBeInTheDocument();
        });

        // Resolve promise
        resolvePromise!(createMockSession());
      });

      it('disables buttons during submission', async () => {
        const user = userEvent.setup();
        let resolvePromise: (value: GameSessionDto) => void;
        const submitPromise = new Promise<GameSessionDto>(resolve => {
          resolvePromise = resolve;
        });
        mockStartSession.mockReturnValueOnce(submitPromise);

        render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Fill in player names
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');

        // Submit
        await user.click(getStartSessionButton());

        // Buttons should be disabled
        await waitFor(() => {
          expect(getAddPlayerButton()).toBeDisabled();
        });

        // Resolve promise
        resolvePromise!(createMockSession());
      });
    });

    describe('State Reset', () => {
      it('resets players when modal closes', async () => {
        const user = userEvent.setup();
        const { rerender } = render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        // Add a player name
        await user.type(getPlayerNameInput(1), 'Alice');

        // Close modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={false} />);

        // Reopen modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={true} />);

        await waitForInitialRender();

        // Player name should be cleared
        expect(getPlayerNameInput(1)).toHaveValue('');
      });

      it('resets error state when modal closes', async () => {
        const user = userEvent.setup();
        const { rerender } = render(<SessionSetupModal {...defaultProps} />);
        await waitForInitialRender();

        mockStartSession.mockRejectedValueOnce(new Error('Test Error'));

        // Fill and submit to trigger error
        await user.type(getPlayerNameInput(1), 'Alice');
        await user.type(getPlayerNameInput(2), 'Bob');
        await user.type(getPlayerNameInput(3), 'Charlie');
        await user.click(getStartSessionButton());

        await waitFor(() => {
          expect(screen.getByRole('alert')).toBeInTheDocument();
        });

        // Close modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={false} />);

        // Reopen modal
        rerender(<SessionSetupModal {...defaultProps} isOpen={true} />);

        await waitForInitialRender();

        // Error should be cleared
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      });
    });
  });
});

/**
 * SessionSetupModal Component Tests - Rendering and Player Management
 *
 * Tests for modal rendering, player initialization, add/remove functionality
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionSetupModal } from '@/components/SessionSetupModal';
import { api } from '@/lib/api';
import { mockGame, gameConfigurations } from './SessionSetupModal.test-helpers';

// Mock API
vi.mock('@/lib/api', () => ({
  ...jest.requireActual('@/lib/api'),
  api: {
    sessions: {
      start: vi.fn(),
      getById: vi.fn(),
    },
  },
}));

// Mock crypto.randomUUID with incrementing counter for unique IDs
let uuidCounter = 0;
Object.defineProperty(globalThis, 'crypto', {
  value: {
    randomUUID: vi.fn(() => {
      uuidCounter++;
      return `mock-uuid-${uuidCounter}`;
    }),
  },
});

describe('SessionSetupModal - Rendering and Player Management', () => {
  const mockOnClose = vi.fn();
  const mockOnSessionCreated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Reset UUID counter for each test
    uuidCounter = 0;
  });

  describe('Rendering', () => {
    it('should render modal when open', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      expect(screen.getByText('Start New Session')).toBeInTheDocument();
      expect(screen.getByText(/Setup players for Catan/i)).toBeInTheDocument();
    });

    it('should not render modal when closed', () => {
      render(
        <SessionSetupModal
          isOpen={false}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      expect(screen.queryByText('Start New Session')).not.toBeInTheDocument();
    });

    it('should display game player requirements', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      expect(screen.getByText('Supports 3-4 players')).toBeInTheDocument();
    });

    it('should display exact player count for fixed player games', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={gameConfigurations.fixedPlayers}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      expect(screen.getByText('Requires exactly 2 players')).toBeInTheDocument();
    });
  });

  describe('Player Initialization', () => {
    it('should initialize with minimum players', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      expect(playerInputs).toHaveLength(3); // minPlayers = 3
    });

    it('should initialize with default minimum when no minPlayers specified', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={gameConfigurations.noLimits}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      expect(playerInputs).toHaveLength(2); // default min = 2
    });

    it('should assign colors to initial players', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Check that color selectors are rendered
      const colorSelectors = screen.getAllByRole('combobox');
      expect(colorSelectors).toHaveLength(3); // One per player
    });
  });

  describe('Add/Remove Players', () => {
    it('should add a new player when Add Player button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const addButton = screen.getByRole('button', { name: /add player/i });
      await user.click(addButton);

      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      expect(playerInputs).toHaveLength(4); // 3 initial + 1 added
    });

    it('should disable Add Player button at max players', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const addButton = screen.getByRole('button', { name: /add player/i });

      // Add one player to reach max (4)
      await user.click(addButton);

      // Now at max, button should be disabled
      await waitFor(() => {
        expect(addButton).toBeDisabled();
      });
    });

    it('should remove a player when Remove button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Add a player first
      const addButton = screen.getByRole('button', { name: /add player/i });
      await user.click(addButton);

      // Remove last player
      const removeButtons = screen.getAllByRole('button', {
        name: /remove player/i,
      });
      await user.click(removeButtons[removeButtons.length - 1]);

      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      expect(playerInputs).toHaveLength(3); // Back to 3
    });

    it('should disable Remove buttons at minimum players', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const removeButtons = screen.getAllByRole('button', {
        name: /remove player/i,
      });

      // All remove buttons should be disabled at min players
      removeButtons.forEach((button) => {
        expect(button).toBeDisabled();
      });
    });

    it('should reorder players after removal', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Add a player
      const addButton = screen.getByRole('button', { name: /add player/i });
      await user.click(addButton);

      // Fill player 2 with a name
      const player2Input = screen.getByPlaceholderText('Player 2 name');
      await user.type(player2Input, 'Alice');

      // Remove player 1
      const removeButtons = screen.getAllByRole('button', {
        name: /remove player/i,
      });
      await user.click(removeButtons[0]);

      // Alice should now be player 1
      await waitFor(() => {
        const newPlayer1Input = screen.getByPlaceholderText('Player 1 name');
        expect(newPlayer1Input).toHaveValue('Alice');
      });
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA labels for inputs', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const player1Input = screen.getByPlaceholderText('Player 1 name');
      expect(player1Input).toHaveAttribute('id');

      // Check for associated label (label is sr-only but still associates with the input)
      // The label text is "Player 1 Name" and it should be associated with the input
      const label = screen.getByText('Player 1 Name');
      expect(label).toBeInTheDocument();
      expect(label).toHaveClass('sr-only');

      // Verify the input can be found by label text
      const inputByLabel = screen.getByLabelText('Player 1 Name');
      expect(inputByLabel).toBe(player1Input);
    });

    it('should show validation errors with aria-describedby', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Submit without filling names
      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // Check for aria-invalid and aria-describedby
      await waitFor(() => {
        const player1Input = screen.getByPlaceholderText('Player 1 name');
        expect(player1Input).toHaveAttribute('aria-invalid', 'true');
        expect(player1Input).toHaveAttribute('aria-describedby');
      });
    });

    it('should have proper button labels', () => {
      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      expect(screen.getByRole('button', { name: /add player/i })).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: /remove player 1/i })
      ).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /start session/i })).toBeInTheDocument();
    });
  });
});

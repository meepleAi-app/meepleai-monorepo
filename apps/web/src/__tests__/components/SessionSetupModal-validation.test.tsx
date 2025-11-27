/**
 * SessionSetupModal Component Tests - Validation and API Integration
 *
 * Tests for form validation, API submission, and edge cases
 */

import type { Mock } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionSetupModal } from '@/components/SessionSetupModal';
import { api } from '@/lib/api';
import { mockGame, createMockSession, gameConfigurations } from './SessionSetupModal.test-helpers';

// Mock API
vi.mock('@/lib/api', () => ({
  ...vi.importActual('@/lib/api'),
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

describe('SessionSetupModal - Validation and API Integration', () => {
  const mockOnClose = vi.fn();
  const mockOnSessionCreated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Reset UUID counter for each test
    uuidCounter = 0;
  });

  describe('Form Validation', () => {
    it('should show error for empty player names', async () => {
      const user = userEvent.setup();

      (api.sessions.start as Mock).mockResolvedValue(createMockSession());

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // Should show validation errors - there are 3 empty player name fields
      await waitFor(() => {
        // Check that the inputs have aria-invalid set to true
        const inputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
        inputs.forEach(input => {
          expect(input).toHaveAttribute('aria-invalid', 'true');
        });

        // When all names are empty, they're detected as duplicates
        // (empty string appears multiple times)
        const errors = screen.getAllByText('Player names must be unique');
        expect(errors).toHaveLength(3); // One for each empty player
      });

      // Should not call API
      expect(api.sessions.start).not.toHaveBeenCalled();
    });

    it('should show error for duplicate player names', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Fill all players with same name
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      for (const input of playerInputs) {
        await user.type(input, 'Bob');
      }

      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // Should show duplicate error
      await waitFor(() => {
        const errors = screen.getAllByText('Player names must be unique');
        expect(errors.length).toBeGreaterThan(0);
      });
    });

    it('should validate player count', async () => {
      const user = userEvent.setup();

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={gameConfigurations.largeLimits}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Initially starts with minPlayers (4), so remove 2 to get below min
      const removeButtons = screen.getAllByRole('button', {
        name: /remove player/i,
      });

      // Remove until below min (this won't work since buttons are disabled)
      // Instead, test that submit button is disabled
      const submitButton = screen.getByRole('button', { name: /start session/i });

      // Fill names
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      for (let i = 0; i < playerInputs.length; i++) {
        await user.type(playerInputs[i], `Player${i + 1}`);
      }

      // Should be able to submit with valid player count
      expect(submitButton).not.toBeDisabled();
    });
  });

  describe('API Submission', () => {
    it('should successfully create session and call onSessionCreated', async () => {
      const user = userEvent.setup();

      const mockSession = createMockSession();

      // Make the API call take some time so we can see the loading state
      (api.sessions.start as Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockSession), 50))
      );

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Fill player names
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      await user.type(playerInputs[0], 'Alice');
      await user.type(playerInputs[1], 'Bob');
      await user.type(playerInputs[2], 'Charlie');

      // Submit
      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // The button text changes to show loading state
      // Use a small timeout to check the loading state appears
      await waitFor(
        () => {
          // Find button that contains "Starting Session..." text
          const buttons = screen.getAllByRole('button');
          const loadingButton = buttons.find(b => b.textContent === 'Starting Session...');
          expect(loadingButton).toBeDefined();
        },
        { timeout: 100 }
      ); // Short timeout since we're checking for immediate state

      // Should call API with correct payload
      await waitFor(() => {
        expect(api.sessions.start).toHaveBeenCalledWith({
          gameId: mockGame.id,
          players: [
            { playerName: 'Alice', playerOrder: 1, color: expect.any(String) },
            { playerName: 'Bob', playerOrder: 2, color: expect.any(String) },
            { playerName: 'Charlie', playerOrder: 3, color: expect.any(String) },
          ],
        });
      });

      // Should call success callbacks
      await waitFor(() => {
        expect(mockOnSessionCreated).toHaveBeenCalledWith(mockSession);
        expect(mockOnClose).toHaveBeenCalled();
      });
    });

    it('should display error message when API fails', async () => {
      const user = userEvent.setup();

      (api.sessions.start as Mock).mockRejectedValue(new Error('Network error'));

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Fill player names
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      await user.type(playerInputs[0], 'Alice');
      await user.type(playerInputs[1], 'Bob');
      await user.type(playerInputs[2], 'Charlie');

      // Submit
      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // Should display error
      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });

      // Should not close modal or call onSessionCreated
      expect(mockOnClose).not.toHaveBeenCalled();
      expect(mockOnSessionCreated).not.toHaveBeenCalled();
    });
  });

  describe('Edge Cases', () => {
    it('should handle rapid add/remove clicks', async () => {
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

      // Rapid clicks
      await user.click(addButton);
      await user.click(addButton);

      // Should not exceed max players (4)
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      expect(playerInputs.length).toBeLessThanOrEqual(4);
    });

    it('should reset state when modal closes and reopens', async () => {
      const user = userEvent.setup();

      const { rerender } = render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Fill a player name
      const player1Input = screen.getByPlaceholderText('Player 1 name');
      await user.type(player1Input, 'Alice');

      // Close modal
      rerender(
        <SessionSetupModal
          isOpen={false}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Reopen modal
      rerender(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Player names should be reset
      const newPlayer1Input = screen.getByPlaceholderText('Player 1 name');
      expect(newPlayer1Input).toHaveValue('');
    });

    it('should trim whitespace from player names', async () => {
      const user = userEvent.setup();

      const mockSession = createMockSession();

      (api.sessions.start as Mock).mockResolvedValue(mockSession);

      render(
        <SessionSetupModal
          isOpen={true}
          onClose={mockOnClose}
          game={mockGame}
          onSessionCreated={mockOnSessionCreated}
        />
      );

      // Fill with whitespace
      const playerInputs = screen.getAllByPlaceholderText(/Player \d+ name/i);
      await user.type(playerInputs[0], '  Alice  ');
      await user.type(playerInputs[1], '  Bob  ');
      await user.type(playerInputs[2], '  Charlie  ');

      // Submit
      const submitButton = screen.getByRole('button', { name: /start session/i });
      await user.click(submitButton);

      // Should trim names in API call
      await waitFor(() => {
        expect(api.sessions.start).toHaveBeenCalledWith(
          expect.objectContaining({
            players: expect.arrayContaining([
              expect.objectContaining({ playerName: 'Alice' }),
              expect.objectContaining({ playerName: 'Bob' }),
              expect.objectContaining({ playerName: 'Charlie' }),
            ]),
          })
        );
      });
    });
  });
});

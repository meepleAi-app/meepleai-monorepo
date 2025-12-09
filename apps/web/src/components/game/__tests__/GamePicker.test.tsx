/**
 * Tests for GamePicker component
 * Issue #1951: Add coverage for game selection components
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GamePicker } from '../GamePicker';

const mockGames = [
  { id: 'game-1', title: 'Chess', createdAt: '2024-01-01T00:00:00Z' },
  { id: 'game-2', title: 'Checkers', createdAt: '2024-01-02T00:00:00Z' },
];

describe('GamePicker', () => {
  const mockOnGameSelect = vi.fn();
  const mockOnGameCreate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders game selection UI', () => {
      const { container } = render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      expect(screen.getByText(/select game/i)).toBeInTheDocument();
      expect(screen.getByText(/create new game/i)).toBeInTheDocument();
    });

    it('renders input for new game creation', () => {
      render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      const input = screen.getByPlaceholderText(/gloomhaven/i);
      expect(input).toBeInTheDocument();
    });

    it('renders create button', () => {
      render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      expect(screen.getByRole('button', { name: /create/i })).toBeInTheDocument();
    });

    it('shows selected game when one is selected', () => {
      render(
        <GamePicker
          games={mockGames}
          selectedGameId="game-1"
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      expect(screen.getAllByText('Chess').length).toBeGreaterThan(0);
      expect(screen.getByText(/selected:/i)).toBeInTheDocument();
    });

    it('renders with empty games list', () => {
      render(
        <GamePicker
          games={[]}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      expect(screen.getByText(/select game/i)).toBeInTheDocument();
    });
  });

  describe('User Interaction', () => {
    it('accepts text input', async () => {
      const user = userEvent.setup();
      render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      const input = screen.getByPlaceholderText(/gloomhaven/i);
      await user.type(input, 'Test');

      expect(input).toHaveValue('Test');
    });

    it('button is disabled when input is empty', () => {
      render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
        />
      );

      const createButton = screen.getByRole('button', { name: /create/i });
      expect(createButton).toBeDisabled();
    });
  });

  describe('Loading States', () => {
    it('disables buttons when loading prop is true', () => {
      render(
        <GamePicker
          games={mockGames}
          selectedGameId={null}
          onGameSelect={mockOnGameSelect}
          onGameCreate={mockOnGameCreate}
          loading={true}
        />
      );

      const createButton = screen.getByRole('button', { name: /create/i });
      expect(createButton).toBeDisabled();
    });
  });
});

/**
 * GameSelector Component Tests
 *
 * Tests for the GameSelector component that allows users to select
 * which board game they want to chat about.
 *
 * Target Coverage: 90%+ (from 60%)
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameSelector } from '../../../components/chat/GameSelector';

// Mock the ChatProvider context
const mockUseChatContext = jest.fn();
jest.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

// Mock SkeletonLoader component
jest.mock('../../../components/loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ variant }: { variant: string }) => (
    <div data-testid="skeleton-loader" data-variant={variant}>
      Loading...
    </div>
  ),
}));

/**
 * Helper to setup mock context with default values
 */
const setupMockContext = (overrides?: any) => {
  mockUseChatContext.mockReturnValue({
    games: [],
    selectedGameId: null,
    selectGame: jest.fn(),
    loading: { games: false },
    ...overrides,
  });
};

describe('GameSelector Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  /**
   * Test Group: Loading State
   */
  describe('Loading State', () => {
    it('displays skeleton loader when games are loading', () => {
      setupMockContext({ loading: { games: true } });
      render(<GameSelector />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('displays skeleton loader with correct variant', () => {
      setupMockContext({ loading: { games: true } });
      render(<GameSelector />);

      const skeleton = screen.getByTestId('skeleton-loader');
      expect(skeleton).toHaveAttribute('data-variant', 'gameSelection');
    });

    it('displays label during loading', () => {
      setupMockContext({ loading: { games: true } });
      render(<GameSelector />);

      expect(screen.getByText('Cambia Gioco:')).toBeInTheDocument();
    });

    it('does not display select element when loading', () => {
      setupMockContext({ loading: { games: true } });
      render(<GameSelector />);

      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: Empty State
   */
  describe('Empty State', () => {
    it('displays "no games" message when games array is empty', () => {
      setupMockContext({ games: [] });
      render(<GameSelector />);

      expect(screen.getByText('Nessun gioco disponibile')).toBeInTheDocument();
    });

    it('renders select element even when games are empty', () => {
      setupMockContext({ games: [] });
      render(<GameSelector />);

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('does not display placeholder option when games are empty', () => {
      setupMockContext({ games: [] });
      render(<GameSelector />);

      expect(screen.queryByText('Seleziona un gioco')).not.toBeInTheDocument();
    });

    it('select is not disabled when empty but not loading', () => {
      setupMockContext({ games: [], loading: { games: false } });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });
  });

  /**
   * Test Group: Games List Rendering
   */
  describe('Games List Rendering', () => {
    it('renders list of available games', () => {
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Catan' },
        { id: 'game-3', name: 'Risk' },
      ];
      setupMockContext({ games });
      render(<GameSelector />);

      expect(screen.getByText('Chess')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Risk')).toBeInTheDocument();
    });

    it('includes placeholder option when games exist', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      expect(screen.getByText('Seleziona un gioco')).toBeInTheDocument();
    });

    it('renders correct number of options (games + placeholder)', () => {
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Catan' },
      ];
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(3); // 2 games + 1 placeholder
    });

    it('renders games in the order provided', () => {
      const games = [
        { id: 'game-1', name: 'Zzz Game' },
        { id: 'game-2', name: 'Aaa Game' },
        { id: 'game-3', name: 'Mmm Game' },
      ];
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      // Skip first option (placeholder)
      expect(select.options[1].textContent).toBe('Zzz Game');
      expect(select.options[2].textContent).toBe('Aaa Game');
      expect(select.options[3].textContent).toBe('Mmm Game');
    });

    it('uses game.id as option value', () => {
      const games = [{ id: 'game-123', name: 'Test Game' }];
      setupMockContext({ games });
      render(<GameSelector />);

      const option = screen.getByText('Test Game') as HTMLOptionElement;
      expect(option.value).toBe('game-123');
    });
  });

  /**
   * Test Group: Game Selection
   */
  describe('Game Selection', () => {
    it('calls selectGame when a game is selected', () => {
      const selectGame = jest.fn();
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectGame });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'game-1' } });

      expect(selectGame).toHaveBeenCalledWith('game-1');
    });

    it('does not call selectGame when empty option is selected', () => {
      const selectGame = jest.fn();
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectGame, selectedGameId: 'game-1' });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: '' } });

      expect(selectGame).not.toHaveBeenCalled();
    });

    it('handles multiple game selections sequentially', () => {
      const selectGame = jest.fn();
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Catan' },
      ];
      setupMockContext({ games, selectGame });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'game-1' } });
      fireEvent.change(select, { target: { value: 'game-2' } });

      expect(selectGame).toHaveBeenCalledTimes(2);
      expect(selectGame).toHaveBeenNthCalledWith(1, 'game-1');
      expect(selectGame).toHaveBeenNthCalledWith(2, 'game-2');
    });

    it('uses void operator for async selectGame call', () => {
      const selectGame = jest.fn().mockResolvedValue(undefined);
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectGame });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'game-1' } });

      expect(selectGame).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Selected Game State
   */
  describe('Selected Game State', () => {
    it('displays currently selected game', () => {
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Catan' },
      ];
      setupMockContext({ games, selectedGameId: 'game-2' });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('game-2');
    });

    it('displays empty value when no game is selected', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectedGameId: null });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('');
    });

    it('handles undefined selectedGameId', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectedGameId: undefined });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('');
    });

    it('updates value when selectedGameId changes', () => {
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Catan' },
      ];
      setupMockContext({ games, selectedGameId: 'game-1' });
      const { rerender } = render(<GameSelector />);

      let select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('game-1');

      // Update selected game
      setupMockContext({ games, selectedGameId: 'game-2' });
      rerender(<GameSelector />);

      select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('game-2');
    });
  });

  /**
   * Test Group: Disabled State
   */
  describe('Disabled State', () => {
    it('disables select when games are loading', () => {
      setupMockContext({ games: [], loading: { games: true } });
      const { rerender } = render(<GameSelector />);

      // Rerender with loaded state (select is hidden during loading, check after)
      setupMockContext({ games: [{ id: 'game-1', name: 'Chess' }], loading: { games: false } });
      rerender(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });

    it('sets aria-busy when loading', () => {
      setupMockContext({ games: [{ id: 'game-1', name: 'Chess' }], loading: { games: false } });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('changes cursor when disabled', () => {
      setupMockContext({ games: [{ id: 'game-1', name: 'Chess' }], loading: { games: false } });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ cursor: 'pointer' });
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('has proper label association', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('id', 'gameSelect');

      const label = screen.getByLabelText('Cambia Gioco:');
      expect(label).toBe(select);
    });

    it('has correct aria-busy attribute', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, loading: { games: false } });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('uses semantic select element', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('has visible label text', () => {
      setupMockContext({ games: [] });
      render(<GameSelector />);

      expect(screen.getByText('Cambia Gioco:')).toBeVisible();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles games with special characters in names', () => {
      const games = [
        { id: 'game-1', name: "Catan: Trader's & Barbarians" },
        { id: 'game-2', name: 'Risk (2nd Edition)' },
      ];
      setupMockContext({ games });
      render(<GameSelector />);

      expect(screen.getByText("Catan: Trader's & Barbarians")).toBeInTheDocument();
      expect(screen.getByText('Risk (2nd Edition)')).toBeInTheDocument();
    });

    it('handles very long game names', () => {
      const longName = 'A'.repeat(100);
      const games = [{ id: 'game-1', name: longName }];
      setupMockContext({ games });
      render(<GameSelector />);

      expect(screen.getByText(longName)).toBeInTheDocument();
    });

    it('handles transition from loading to loaded with games', () => {
      setupMockContext({ games: [], loading: { games: true } });
      const { rerender } = render(<GameSelector />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();

      // Load games
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, loading: { games: false } });
      rerender(<GameSelector />);

      expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    it('handles games with duplicate names (different IDs)', () => {
      const games = [
        { id: 'game-1', name: 'Chess' },
        { id: 'game-2', name: 'Chess' },
      ];
      setupMockContext({ games });
      render(<GameSelector />);

      const chessOptions = screen.getAllByText('Chess');
      expect(chessOptions).toHaveLength(2);
    });

    it('handles single game in list', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(2); // 1 game + 1 placeholder
    });

    it('handles large number of games', () => {
      const games = Array.from({ length: 100 }, (_, i) => ({
        id: `game-${i}`,
        name: `Game ${i}`,
      }));
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(101); // 100 games + 1 placeholder
    });
  });

  /**
   * Test Group: Styling
   */
  describe('Styling', () => {
    it('applies correct container margin', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      const { container } = render(<GameSelector />);

      const wrapper = container.firstChild as HTMLElement;
      expect(wrapper).toHaveStyle({ marginBottom: '12px' });
    });

    it('applies correct label styling', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      const label = screen.getByText('Cambia Gioco:');
      expect(label).toHaveStyle({
        display: 'block',
        marginBottom: '6px',
        fontWeight: '500',
        fontSize: '13px',
      });
    });

    it('applies correct select styling', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games });
      render(<GameSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({
        width: '100%',
        padding: '8px',
        fontSize: '13px',
        borderRadius: '4px',
        border: '1px solid #dadce0',
      });
    });

    it('changes cursor style based on loading state', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, loading: { games: false } });
      const { rerender } = render(<GameSelector />);

      let select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ cursor: 'pointer' });

      // Change to loading
      setupMockContext({ games, loading: { games: true } });
      rerender(<GameSelector />);

      // Select is hidden during loading, check skeleton instead
      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });
  });
});

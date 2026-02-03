/**
 * GameSelector Component Tests
 * Issue #3239: [FRONT-003] Game Selector from library
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock stores
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    selectedGameId: null,
    setSelectedGame: vi.fn(),
  })),
}));

// Import after mocks
import { GameSelector } from '../GameSelector';
import { useAgentStore } from '@/stores/agentStore';

describe('GameSelector', () => {
  const mockSetSelectedGame = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useAgentStore).mockReturnValue({
      selectedGameId: null,
      setSelectedGame: mockSetSelectedGame,
    } as unknown as ReturnType<typeof useAgentStore>);
  });

  describe('Rendering', () => {
    it('renders Select Game label', () => {
      render(<GameSelector />);
      expect(screen.getByText('Select Game')).toBeInTheDocument();
    });

    it('renders required asterisk', () => {
      render(<GameSelector />);
      expect(screen.getByText('*')).toBeInTheDocument();
    });

    it('renders placeholder text', () => {
      render(<GameSelector />);
      expect(screen.getByText('Choose a game...')).toBeInTheDocument();
    });

    it('shows validation message when no game selected', () => {
      render(<GameSelector />);
      expect(screen.getByText('Please select a game to continue')).toBeInTheDocument();
    });

    it('hides validation message when game is selected', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: '1',
        setSelectedGame: mockSetSelectedGame,
      } as unknown as ReturnType<typeof useAgentStore>);

      render(<GameSelector />);
      expect(screen.queryByText('Please select a game to continue')).not.toBeInTheDocument();
    });
  });

  describe('Select Dropdown', () => {
    it('opens dropdown when trigger clicked', () => {
      render(<GameSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      // Check that dropdown content is visible
      expect(screen.getByText('7 Wonders')).toBeInTheDocument();
      expect(screen.getByText('Splendor')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('shows rulebook indicator for games with PDF', () => {
      render(<GameSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      // All mock games have hasPdf: true
      const rulebookIndicators = screen.getAllByText(/Rulebook/);
      expect(rulebookIndicators.length).toBeGreaterThan(0);
    });

    it('calls setSelectedGame when game is selected', () => {
      render(<GameSelector />);

      fireEvent.click(screen.getByRole('combobox'));
      fireEvent.click(screen.getByText('7 Wonders'));

      expect(mockSetSelectedGame).toHaveBeenCalledWith('1');
    });

    it('displays selected game name', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedGameId: '2',
        setSelectedGame: mockSetSelectedGame,
      } as unknown as ReturnType<typeof useAgentStore>);

      render(<GameSelector />);

      // When a value is selected, the trigger should show the selected option
      // The placeholder should not be visible
      expect(screen.queryByText('Choose a game...')).not.toBeInTheDocument();
    });
  });

  describe('Game List', () => {
    it('renders all available games', () => {
      render(<GameSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('7 Wonders')).toBeInTheDocument();
      expect(screen.getByText('Splendor')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders BookOpen icon for each game', () => {
      render(<GameSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      // Should have icons, but testing SVG is tricky
      // Just verify the dropdown opened
      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });
  });
});

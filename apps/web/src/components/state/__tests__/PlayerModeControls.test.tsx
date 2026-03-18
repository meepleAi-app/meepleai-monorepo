/**
 * Tests for PlayerModeControls component
 * Issue #2421: Player Mode UI Controls
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { PlayerModeControls } from '../PlayerModeControls';

import type {
  PlayerAISuggestionState,
  PlayerAISuggestionControls,
} from '@/lib/domain-hooks/usePlayerAISuggestion';

// Mock the hook
vi.mock('@/lib/domain-hooks/usePlayerAISuggestion', () => ({
  usePlayerAISuggestion: vi.fn(),
}));

// Import after mock to get mocked version
import { usePlayerAISuggestion } from '@/lib/domain-hooks/usePlayerAISuggestion';

describe('PlayerModeControls', () => {
  const mockGameState = {
    players: [{ id: '1', name: 'Alice', score: 10 }],
  };

  const mockSuggestMove = vi.fn();
  const mockApplySuggestion = vi.fn();
  const mockIgnoreSuggestion = vi.fn();
  const mockReset = vi.fn();
  const mockOnSuggestionApplied = vi.fn();
  const mockOnSuggestionIgnored = vi.fn();

  const defaultMockState: PlayerAISuggestionState = {
    isLoading: false,
    error: null,
    suggestion: null,
    alternatives: [],
    confidence: null,
    strategicContext: null,
    processingTimeMs: null,
  };

  const defaultMockControls: PlayerAISuggestionControls = {
    suggestMove: mockSuggestMove,
    applySuggestion: mockApplySuggestion,
    ignoreSuggestion: mockIgnoreSuggestion,
    reset: mockReset,
  };

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock implementation
    (usePlayerAISuggestion as any).mockReturnValue([defaultMockState, defaultMockControls]);
  });

  describe('Initial Render', () => {
    it('should render Suggest Move button', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Suggerisci Mossa')).toBeInTheDocument();
    });

    it('should show empty state message when no suggestion', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(
        screen.getByText(/Clicca "Suggerisci Mossa" per ricevere un suggerimento/)
      ).toBeInTheDocument();
    });

    it('should not show suggestion panel initially', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.queryByText('Suggerimento AI')).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('should show loading state when isLoading is true', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        { ...defaultMockState, isLoading: true },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Analizzando...')).toBeInTheDocument();
    });

    it('should disable button when loading', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        { ...defaultMockState, isLoading: true },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      const button = screen.getByRole('button', { name: /Analizzando/ });
      expect(button).toBeDisabled();
    });
  });

  describe('Error State', () => {
    it('should show error message when error occurs', () => {
      const errorMessage = 'Backend endpoint not implemented';
      (usePlayerAISuggestion as any).mockReturnValue([
        { ...defaultMockState, error: errorMessage },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Errore')).toBeInTheDocument();
      expect(screen.getByText(errorMessage)).toBeInTheDocument();
    });
  });

  describe('Suggestion Display', () => {
    const mockSuggestion = {
      action: 'Place resource token on space 5',
      rationale: 'This maximizes your resource generation',
      expectedOutcome: 'Gain 2 wood per turn',
      confidence: 0.85,
    };

    it('should show suggestion panel when suggestion exists', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: mockSuggestion,
          confidence: 0.85,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Suggerimento AI')).toBeInTheDocument();
      expect(screen.getByText(mockSuggestion.action)).toBeInTheDocument();
      expect(screen.getByText(mockSuggestion.rationale)).toBeInTheDocument();
    });

    it('should show expected outcome when provided', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: mockSuggestion,
          confidence: 0.85,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText(/Risultato Atteso:/)).toBeInTheDocument();
      expect(screen.getByText(mockSuggestion.expectedOutcome!)).toBeInTheDocument();
    });

    it('should show Apply and Ignore buttons with suggestion', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: mockSuggestion,
          confidence: 0.85,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Applica Mossa')).toBeInTheDocument();
      expect(screen.getByText('Ignora')).toBeInTheDocument();
    });
  });

  describe('Confidence Meter', () => {
    it('should show high confidence (green) for >80%', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.9,
          },
          confidence: 0.9,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Alto (90%)')).toBeInTheDocument();
    });

    it('should show medium confidence (yellow) for 50-80%', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.65,
          },
          confidence: 0.65,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Medio (65%)')).toBeInTheDocument();
    });

    it('should show low confidence (red) for <50%', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.35,
          },
          confidence: 0.35,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Basso (35%)')).toBeInTheDocument();
    });
  });

  describe('Alternative Moves', () => {
    it('should show alternative moves when provided', () => {
      const mockAlternatives = [
        {
          action: 'Build settlement',
          rationale: 'Secure territory',
          confidence: 0.7,
        },
        {
          action: 'Collect resources',
          rationale: 'Prepare for next phase',
          confidence: 0.65,
        },
      ];

      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Primary action',
            rationale: 'Best choice',
            confidence: 0.85,
          },
          alternatives: mockAlternatives,
          confidence: 0.85,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('Mosse Alternative')).toBeInTheDocument();
      expect(screen.getByText('Build settlement')).toBeInTheDocument();
      expect(screen.getByText('Collect resources')).toBeInTheDocument();
    });
  });

  describe('Strategic Context', () => {
    it('should show strategic context when provided', () => {
      const strategicContext = 'Focus on resource generation in early game';

      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.8,
          },
          strategicContext,
          confidence: 0.8,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText(/Contesto Strategico:/)).toBeInTheDocument();
      expect(screen.getByText(strategicContext)).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call suggestMove when Suggest Move button clicked', async () => {
      render(<PlayerModeControls gameId="game-456" gameState={mockGameState} />);

      const button = screen.getByText('Suggerisci Mossa');
      fireEvent.click(button);

      await waitFor(() => {
        expect(mockSuggestMove).toHaveBeenCalledWith('game-456', mockGameState, undefined);
      });
    });

    it('should call applySuggestion when Apply button clicked', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.8,
          },
          confidence: 0.8,
        },
        defaultMockControls,
      ]);

      render(
        <PlayerModeControls
          gameId="game-123"
          gameState={mockGameState}
          onSuggestionApplied={mockOnSuggestionApplied}
        />
      );

      const applyButton = screen.getByText('Applica Mossa');
      fireEvent.click(applyButton);

      expect(mockApplySuggestion).toHaveBeenCalled();
    });

    it('should call ignoreSuggestion when Ignore button clicked', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.8,
          },
          confidence: 0.8,
        },
        defaultMockControls,
      ]);

      render(
        <PlayerModeControls
          gameId="game-123"
          gameState={mockGameState}
          onSuggestionIgnored={mockOnSuggestionIgnored}
        />
      );

      const ignoreButton = screen.getByText('Ignora');
      fireEvent.click(ignoreButton);

      expect(mockIgnoreSuggestion).toHaveBeenCalled();
    });
  });

  describe('Readonly Mode', () => {
    it('should disable Suggest Move button in readonly mode', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} readonly={true} />);

      const button = screen.getByText('Suggerisci Mossa');
      expect(button).toBeDisabled();
    });

    it('should disable Apply and Ignore buttons in readonly mode', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          suggestion: {
            action: 'Test',
            rationale: 'Test',
            confidence: 0.8,
          },
          confidence: 0.8,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} readonly={true} />);

      const applyButton = screen.getByText('Applica Mossa');
      const ignoreButton = screen.getByText('Ignora');

      expect(applyButton).toBeDisabled();
      expect(ignoreButton).toBeDisabled();
    });
  });

  describe('Processing Time', () => {
    it('should show processing time when available', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          processingTimeMs: 1250,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.getByText('1250ms')).toBeInTheDocument();
    });

    it('should not show processing time when loading', () => {
      (usePlayerAISuggestion as any).mockReturnValue([
        {
          ...defaultMockState,
          isLoading: true,
          processingTimeMs: 1250,
        },
        defaultMockControls,
      ]);

      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      expect(screen.queryByText('1250ms')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible button roles', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      const button = screen.getByRole('button', { name: /Suggerisci Mossa/ });
      expect(button).toBeInTheDocument();
    });

    it('should have tooltip on Suggest Move button', () => {
      render(<PlayerModeControls gameId="game-123" gameState={mockGameState} />);

      // Tooltip trigger should be present
      const button = screen.getByText('Suggerisci Mossa');
      expect(button).toBeInTheDocument();
    });
  });
});

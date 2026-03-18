/**
 * GameStateEditor Tests
 * Issue #2766: Sprint 6 - Test Coverage Push
 *
 * Tests for editable game state form component.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { GameState, GameStateTemplate, PlayerState } from '@/types/game-state';

import { GameStateEditor } from '../GameStateEditor';

// Mock RJSF Form component
vi.mock('@rjsf/core', () => ({
  default: ({
    formData,
    onChange,
    schema,
  }: {
    formData: unknown;
    onChange: (e: { formData: unknown }) => void;
    schema: unknown;
  }) => (
    <div data-testid="rjsf-form">
      <span data-testid="form-schema">{JSON.stringify(schema)}</span>
      <span data-testid="form-data">{JSON.stringify(formData)}</span>
      <button
        data-testid="mock-form-change"
        onClick={() => onChange({ formData: { ...formData, phase: 'Changed' } as unknown })}
      >
        Trigger Change
      </button>
    </div>
  ),
}));

// Mock RJSF validator
vi.mock('@rjsf/validator-ajv8', () => ({
  default: {},
}));

// Mock MeeplePlayerStateCard (migrated from PlayerStateCard)
vi.mock('../MeeplePlayerStateCard', () => ({
  MeeplePlayerStateCard: ({
    player,
    isCurrentPlayer,
    editable,
    onScoreChange,
    onResourceChange,
  }: {
    player: PlayerState;
    isCurrentPlayer?: boolean;
    editable: boolean;
    onScoreChange?: (score: number) => void;
    onResourceChange?: (key: string, value: number) => void;
  }) => (
    <div data-testid={`player-card-${player.playerName}`}>
      <span>{player.playerName}</span>
      {isCurrentPlayer && <span data-testid="current-player-indicator">Current</span>}
      {editable && <span data-testid="editable-indicator">Editable</span>}
      {onScoreChange && (
        <button data-testid="change-score-btn" onClick={() => onScoreChange(100)}>
          Change Score
        </button>
      )}
      {onResourceChange && (
        <button data-testid="change-resource-btn" onClick={() => onResourceChange('gold', 50)}>
          Change Resource
        </button>
      )}
    </div>
  ),
}));

// Mock the game state store - defined as vi.hoisted to ensure availability in factory
const {
  mockSetState,
  mockSaveState,
  mockUpdateField,
  mockUndo,
  mockRedo,
  mockCanUndo,
  mockCanRedo,
  mockGetState,
} = vi.hoisted(() => ({
  mockSetState: vi.fn(),
  mockSaveState: vi.fn(),
  mockUpdateField: vi.fn(),
  mockUndo: vi.fn(),
  mockRedo: vi.fn(),
  mockCanUndo: vi.fn(() => true),
  mockCanRedo: vi.fn(() => false),
  mockGetState: vi.fn(() => ({
    setState: vi.fn(),
    saveState: vi.fn(),
  })),
}));

vi.mock('@/lib/stores/game-state-store', () => ({
  useGameStateStore: Object.assign(
    vi.fn(() => ({
      currentState: null,
      template: null,
      updateField: mockUpdateField,
      canUndo: mockCanUndo,
      canRedo: mockCanRedo,
      undo: mockUndo,
      redo: mockRedo,
    })),
    {
      getState: mockGetState,
    }
  ),
}));

import { useGameStateStore } from '@/lib/stores/game-state-store';
const mockUseGameStateStore = useGameStateStore as unknown as ReturnType<typeof vi.fn>;

const createMockGameState = (overrides?: Partial<GameState>): GameState => ({
  sessionId: 'session-123',
  gameId: 'game-456',
  templateId: 'template-789',
  version: '1.0.0',
  phase: 'Action',
  currentPlayerIndex: 0,
  roundNumber: 1,
  players: [
    {
      playerName: 'Alice',
      playerOrder: 1,
      color: '#FF6B6B',
      score: 25,
      resources: { gold: 10, wood: 5 },
    },
    {
      playerName: 'Bob',
      playerOrder: 2,
      color: '#4ECDC4',
      score: 20,
      resources: { gold: 8, wood: 7 },
    },
  ],
  ...overrides,
});

const createMockTemplate = (overrides?: Partial<GameStateTemplate>): GameStateTemplate => ({
  id: 'template-123',
  sharedGameId: 'game-456',
  name: 'Test Template',
  schemaJson: JSON.stringify({
    type: 'object',
    properties: {
      phase: { type: 'string' },
      roundNumber: { type: 'number' },
    },
  }),
  version: '1.0.0',
  isActive: true,
  source: 'Manual',
  confidenceScore: null,
  generatedAt: '2024-01-15T10:00:00Z',
  createdBy: 'user-123',
  ...overrides,
});

describe('GameStateEditor - Issue #2766', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetState.mockReturnValue({
      setState: mockSetState,
      saveState: mockSaveState,
    });
  });

  describe('No State Available', () => {
    it('should render empty state message when no currentState', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: null,
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByText('No State Available')).toBeInTheDocument();
      expect(screen.getByText('Load a game state to start editing')).toBeInTheDocument();
    });
  });

  describe('Manual Editor (No Schema)', () => {
    beforeEach(() => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null, // No template = no schema
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });
    });

    it('should render manual editor when no schema available', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByTestId('game-state-editor')).toBeInTheDocument();
      expect(screen.getByText('Game State Editor')).toBeInTheDocument();
      expect(screen.getByText('Manually edit game state')).toBeInTheDocument();
    });

    it('should render players with correct count', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByText('Players (2)')).toBeInTheDocument();
    });

    it('should render PlayerStateCard for each player in editable mode', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByTestId('player-card-Alice')).toBeInTheDocument();
      expect(screen.getByTestId('player-card-Bob')).toBeInTheDocument();

      // Check editable indicators
      const aliceCard = screen.getByTestId('player-card-Alice');
      expect(aliceCard.querySelector('[data-testid="editable-indicator"]')).toBeInTheDocument();
    });

    it('should render Undo button', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
    });

    it('should render Redo button', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByRole('button', { name: /redo/i })).toBeInTheDocument();
    });

    it('should render Save button', () => {
      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
    });

    it('should disable Undo when canUndo returns false', () => {
      mockCanUndo.mockReturnValue(false);
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByRole('button', { name: /undo/i })).toBeDisabled();
    });

    it('should disable Redo when canRedo returns false', () => {
      mockCanRedo.mockReturnValue(false);

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByRole('button', { name: /redo/i })).toBeDisabled();
    });

    it('should call undo when Undo button clicked', () => {
      mockCanUndo.mockReturnValue(true);
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      fireEvent.click(screen.getByRole('button', { name: /undo/i }));

      expect(mockUndo).toHaveBeenCalled();
    });

    it('should call redo when Redo button clicked', () => {
      mockCanRedo.mockReturnValue(true);
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      fireEvent.click(screen.getByRole('button', { name: /redo/i }));

      expect(mockRedo).toHaveBeenCalled();
    });
  });

  describe('Cancel Button', () => {
    it('should render Cancel button when onCancel provided', () => {
      const onCancel = vi.fn();
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" onCancel={onCancel} />);

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('should not render Cancel button when onCancel not provided', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });

    it('should call onCancel when Cancel button clicked', () => {
      const onCancel = vi.fn();
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" onCancel={onCancel} />);

      fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(onCancel).toHaveBeenCalled();
    });
  });

  describe('Save Functionality', () => {
    it('should call saveState and onSave when Save clicked', async () => {
      const onSave = vi.fn();
      const gameState = createMockGameState();
      mockSaveState.mockResolvedValue(undefined);

      mockUseGameStateStore.mockReturnValue({
        currentState: gameState,
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" onSave={onSave} />);

      fireEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(mockSaveState).toHaveBeenCalledWith('session-123');
      });

      await waitFor(() => {
        expect(onSave).toHaveBeenCalledWith(gameState);
      });
    });

    it('should handle save error gracefully', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockSaveState.mockRejectedValue(new Error('Save failed'));

      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      fireEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalledWith('Failed to save state:', expect.any(Error));
      });

      consoleSpy.mockRestore();
    });
  });

  describe('Player Score Updates', () => {
    it('should call updateField when score changes', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      // Find first player's change score button
      const changeScoreBtn = screen.getAllByTestId('change-score-btn')[0];
      fireEvent.click(changeScoreBtn);

      expect(mockUpdateField).toHaveBeenCalledWith(['players', '0', 'score'], 100, 'Update score');
    });
  });

  describe('Player Resource Updates', () => {
    it('should call updateField when resource changes', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      // Find first player's change resource button
      const changeResourceBtn = screen.getAllByTestId('change-resource-btn')[0];
      fireEvent.click(changeResourceBtn);

      expect(mockUpdateField).toHaveBeenCalledWith(
        ['players', '0', 'resources', 'gold'],
        50,
        'Update gold'
      );
    });
  });

  describe('Schema-Driven Editor', () => {
    it('should render RJSF form when schema available', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: createMockTemplate(),
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByTestId('game-state-editor-schema')).toBeInTheDocument();
      expect(screen.getByTestId('rjsf-form')).toBeInTheDocument();
    });

    it('should show template name and version in header', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: createMockTemplate({ name: 'Catan Template', version: '2.0.0' }),
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      expect(screen.getByText(/Editing with schema: Catan Template/)).toBeInTheDocument();
      expect(screen.getByText(/v2.0.0/)).toBeInTheDocument();
    });

    it('should call setState when form changes', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState(),
        template: createMockTemplate(),
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      // Trigger mock form change
      fireEvent.click(screen.getByTestId('mock-form-change'));

      expect(mockSetState).toHaveBeenCalled();
    });
  });

  describe('Current Player Highlight', () => {
    it('should mark current player in manual editor', () => {
      mockUseGameStateStore.mockReturnValue({
        currentState: createMockGameState({ currentPlayerIndex: 1 }),
        template: null,
        updateField: mockUpdateField,
        canUndo: mockCanUndo,
        canRedo: mockCanRedo,
        undo: mockUndo,
        redo: mockRedo,
      });

      render(<GameStateEditor sessionId="session-123" />);

      const bobCard = screen.getByTestId('player-card-Bob');
      expect(bobCard.querySelector('[data-testid="current-player-indicator"]')).toBeInTheDocument();

      const aliceCard = screen.getByTestId('player-card-Alice');
      expect(
        aliceCard.querySelector('[data-testid="current-player-indicator"]')
      ).not.toBeInTheDocument();
    });
  });
});

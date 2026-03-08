/**
 * Unit tests for AiToolkitGenerator — AI-powered toolkit generation component.
 * Issue P0-9.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import React from 'react';

import { AiToolkitGenerator } from '../AiToolkitGenerator';
import type { AiToolkitSuggestion } from '@/lib/api/schemas/toolkit.schemas';

// ── Mock data ────────────────────────────────────────────────────────────────

const mockSuggestion: AiToolkitSuggestion = {
  toolkitName: 'Catan Toolkit',
  diceTools: [
    {
      name: 'Resource Dice',
      diceType: 'D6',
      quantity: 2,
      customFaces: null,
      isInteractive: true,
      color: null,
    },
  ],
  counterTools: [
    {
      name: 'Wood',
      minValue: 0,
      maxValue: 99,
      defaultValue: 0,
      isPerPlayer: true,
      icon: null,
      color: null,
    },
    {
      name: 'Brick',
      minValue: 0,
      maxValue: 99,
      defaultValue: 0,
      isPerPlayer: true,
      icon: null,
      color: null,
    },
  ],
  timerTools: [],
  scoringTemplate: {
    dimensions: ['Victory Points'],
    defaultUnit: 'points',
    scoreType: 'Points',
  },
  turnTemplate: {
    turnOrderType: 'RoundRobin',
    phases: [],
  },
  overrides: {
    overridesTurnOrder: true,
    overridesScoreboard: true,
    overridesDiceSet: true,
  },
  reasoning: 'Catan uses 2D6 for resource production and has multiple resource types to track.',
};

const GAME_ID = '00000000-0000-0000-0000-000000000001';

// ── Helpers ──────────────────────────────────────────────────────────────────

function createMocks() {
  return {
    onGenerate: vi.fn<[string], Promise<AiToolkitSuggestion>>(),
    onApply: vi.fn<[AiToolkitSuggestion], Promise<void>>(),
    onDismiss: vi.fn(),
  };
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('AiToolkitGenerator', () => {
  let mocks: ReturnType<typeof createMocks>;

  beforeEach(() => {
    mocks = createMocks();
  });

  // ── State 1: Initial ────────────────────────────────────────────────────

  it('renders "Genera con AI" button initially', () => {
    render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} />);

    expect(screen.getByRole('button', { name: /genera con ai/i })).toBeInTheDocument();
  });

  it('calls onGenerate with gameId when button is clicked', async () => {
    mocks.onGenerate.mockResolvedValue(mockSuggestion);
    render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} />);

    fireEvent.click(screen.getByRole('button', { name: /genera con ai/i }));

    await waitFor(() => {
      expect(mocks.onGenerate).toHaveBeenCalledWith(GAME_ID);
    });
  });

  // ── State 2: Loading ────────────────────────────────────────────────────

  it('shows loading state when isGenerating is true', () => {
    render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} isGenerating={true} />);

    expect(screen.getByText(/analisi regole del gioco in corso/i)).toBeInTheDocument();
    // The initial button should not be visible during loading
    expect(screen.queryByRole('button', { name: /genera con ai/i })).not.toBeInTheDocument();
  });

  // ── State 3: Review panel ───────────────────────────────────────────────

  describe('review panel (after generation)', () => {
    async function renderWithSuggestion(extraProps: Record<string, unknown> = {}) {
      mocks.onGenerate.mockResolvedValue(mockSuggestion);
      const result = render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} {...extraProps} />);

      fireEvent.click(screen.getByRole('button', { name: /genera con ai/i }));
      await waitFor(() => {
        expect(screen.getByText(/catan toolkit/i)).toBeInTheDocument();
      });

      return result;
    }

    it('displays suggestion toolkit name', async () => {
      await renderWithSuggestion();

      expect(screen.getByText(/suggerimento ai: catan toolkit/i)).toBeInTheDocument();
    });

    it('displays dice count badge', async () => {
      await renderWithSuggestion();

      // 1 dice tool -> singular "dado"
      expect(screen.getByText('1 dado')).toBeInTheDocument();
    });

    it('displays counter count badge', async () => {
      await renderWithSuggestion();

      // 2 counter tools -> plural "contatori"
      expect(screen.getByText('2 contatori')).toBeInTheDocument();
    });

    it('displays scoring and turn badges', async () => {
      await renderWithSuggestion();

      expect(screen.getByText('Punteggio')).toBeInTheDocument();
      expect(screen.getByText('Turni')).toBeInTheDocument();
    });

    it('does not display timer badge when timerTools is empty', async () => {
      await renderWithSuggestion();

      expect(screen.queryByText(/timer/i)).not.toBeInTheDocument();
    });

    it('displays reasoning text', async () => {
      await renderWithSuggestion();

      expect(screen.getByText(/catan uses 2d6 for resource production/i)).toBeInTheDocument();
    });

    it('displays counter details', async () => {
      await renderWithSuggestion();

      expect(screen.getByText('Dettagli contatori:')).toBeInTheDocument();
      expect(screen.getByText('Wood')).toBeInTheDocument();
      expect(screen.getByText('Brick')).toBeInTheDocument();
    });

    it('calls onApply with suggestion when "Applica" is clicked', async () => {
      mocks.onApply.mockResolvedValue(undefined);
      await renderWithSuggestion();

      fireEvent.click(screen.getByRole('button', { name: /applica/i }));

      await waitFor(() => {
        expect(mocks.onApply).toHaveBeenCalledWith(mockSuggestion);
      });
    });

    it('calls onDismiss when "Ignora" is clicked', async () => {
      await renderWithSuggestion();

      fireEvent.click(screen.getByRole('button', { name: /ignora/i }));

      expect(mocks.onDismiss).toHaveBeenCalledTimes(1);
    });

    it('calls onGenerate again when "Rigenera" is clicked', async () => {
      await renderWithSuggestion();

      // First call was during renderWithSuggestion
      expect(mocks.onGenerate).toHaveBeenCalledTimes(1);

      mocks.onGenerate.mockResolvedValue({
        ...mockSuggestion,
        toolkitName: 'Catan Toolkit v2',
      });

      fireEvent.click(screen.getByRole('button', { name: /rigenera/i }));

      await waitFor(() => {
        expect(mocks.onGenerate).toHaveBeenCalledTimes(2);
      });
    });
  });

  // ── Error handling ──────────────────────────────────────────────────────

  describe('error handling', () => {
    it('shows error message when onGenerate rejects with Error', async () => {
      mocks.onGenerate.mockRejectedValue(new Error('AI service unavailable'));
      render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} />);

      fireEvent.click(screen.getByRole('button', { name: /genera con ai/i }));

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('AI service unavailable');
      });
    });

    it('shows fallback error message when onGenerate rejects with non-Error', async () => {
      mocks.onGenerate.mockRejectedValue('unknown error');
      render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} />);

      fireEvent.click(screen.getByRole('button', { name: /genera con ai/i }));

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('Errore nella generazione AI');
      });
    });

    it('shows error when onApply rejects', async () => {
      mocks.onGenerate.mockResolvedValue(mockSuggestion);
      mocks.onApply.mockRejectedValue(new Error('Apply failed'));
      render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} />);

      // Generate first
      fireEvent.click(screen.getByRole('button', { name: /genera con ai/i }));
      await waitFor(() => {
        expect(screen.getByText(/catan toolkit/i)).toBeInTheDocument();
      });

      // Then apply
      fireEvent.click(screen.getByRole('button', { name: /applica/i }));

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('Apply failed');
      });
    });
  });

  // ── Disabled states ─────────────────────────────────────────────────────

  it('disables initial button when isApplying is true', () => {
    render(<AiToolkitGenerator gameId={GAME_ID} {...mocks} isApplying={true} />);

    expect(screen.getByRole('button', { name: /genera con ai/i })).toBeDisabled();
  });
});

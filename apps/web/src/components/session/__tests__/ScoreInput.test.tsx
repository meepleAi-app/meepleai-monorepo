/**
 * ScoreInput Component Tests
 *
 * Test Coverage:
 * - Participant selection
 * - Round and category selection
 * - Score input and quick adjustment buttons
 * - Form submission
 * - Validation and error handling
 * - Sync status display
 * - Undo functionality
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ScoreInput } from '../ScoreInput';
import type { Participant } from '../types';
import { createMockParticipantList } from '@/__tests__/fixtures/common-fixtures';

// The component's strings come from the real locale files, so a missing or misnamed key fails here instead of
// rendering as a raw key id in the browser. English is the default (the assertions below are in English); the Italian
// block at the end switches the locale. It was English-only and hardcoded: an Italian user saw PLAYER, ROUND,
// CATEGORY, "Add Score" in an otherwise Italian live-session screen.
const i18n = vi.hoisted(() => ({ locale: 'en' as 'en' | 'it' }));
vi.mock('@/hooks/useTranslation', async () => {
  const { createIntl } = await import('react-intl');
  const en = (await import('@/locales/en.json')).default as Record<string, unknown>;
  const it = (await import('@/locales/it.json')).default as Record<string, unknown>;
  const flatten = (
    node: Record<string, unknown>,
    prefix = '',
    out: Record<string, string> = {}
  ) => {
    for (const [key, value] of Object.entries(node)) {
      const path = prefix ? `${prefix}.${key}` : key;
      if (value && typeof value === 'object') flatten(value as Record<string, unknown>, path, out);
      else out[path] = String(value);
    }
    return out;
  };
  const intls = {
    en: createIntl({ locale: 'en', messages: flatten(en) }),
    it: createIntl({ locale: 'it', messages: flatten(it) }),
  };
  return {
    useTranslation: () => ({
      t: (id: string, values?: Record<string, string | number>) =>
        intls[i18n.locale].formatMessage({ id }, values),
    }),
  };
});

describe('ScoreInput', () => {
  const mockOnSubmit = vi.fn();
  const mockOnUndo = vi.fn();

  // Use fixture factory instead of magic strings
  const defaultParticipants: Participant[] = createMockParticipantList(3);

  const defaultProps = {
    participants: defaultParticipants,
    rounds: [1, 2, 3],
    onSubmit: mockOnSubmit,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockOnSubmit.mockResolvedValue(undefined);
  });

  describe('Rendering', () => {
    it('renders all participant buttons', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.getByRole('button', { name: /alice/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /bob/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /charlie/i })).toBeInTheDocument();
    });

    it('renders score input field', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.getByRole('spinbutton')).toBeInTheDocument();
    });

    it('renders submit button', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.getByRole('button', { name: /add score/i })).toBeInTheDocument();
    });

    it('renders round selector when rounds are provided', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.getByRole('combobox', { name: /round/i })).toBeInTheDocument();
    });

    it('does not render round selector when no rounds', () => {
      render(<ScoreInput {...defaultProps} rounds={[]} />);

      expect(screen.queryByRole('combobox', { name: /round/i })).not.toBeInTheDocument();
    });

    it('renders category selector when categories are provided', () => {
      render(<ScoreInput {...defaultProps} categories={['Points', 'Bonus']} />);

      expect(screen.getByRole('combobox', { name: /category/i })).toBeInTheDocument();
    });

    it('does not render category selector when no categories', () => {
      render(<ScoreInput {...defaultProps} categories={[]} />);

      expect(screen.queryByRole('combobox', { name: /category/i })).not.toBeInTheDocument();
    });

    it('renders undo button when onUndo is provided', () => {
      render(<ScoreInput {...defaultProps} onUndo={mockOnUndo} />);

      expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
    });

    it('does not render undo button when onUndo is not provided', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.queryByRole('button', { name: /undo/i })).not.toBeInTheDocument();
    });
  });

  describe('Participant Selection', () => {
    it('auto-selects first participant on mount', () => {
      render(<ScoreInput {...defaultProps} />);

      const aliceButton = screen.getByRole('button', { name: /alice/i });
      // First participant should be visually selected (different styling)
      expect(aliceButton).toHaveClass('border-amber-600');
    });

    it('switches participant on button click', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: /bob/i }));

      const bobButton = screen.getByRole('button', { name: /bob/i });
      expect(bobButton).toHaveClass('border-amber-600');
    });
  });

  describe('Score Input', () => {
    it('allows entering score manually', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '42');

      expect(scoreInput).toHaveValue(42);
    });

    it('increases score by 1 when +1 button clicked', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '10');

      await user.click(screen.getByRole('button', { name: /increase by 1/i }));

      expect(scoreInput).toHaveValue(11);
    });

    it('increases score by 5 when +5 button clicked', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '10');

      await user.click(screen.getByRole('button', { name: /increase by 5/i }));

      expect(scoreInput).toHaveValue(15);
    });

    it('decreases score by 1 when -1 button clicked', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '10');

      await user.click(screen.getByRole('button', { name: /decrease by 1/i }));

      expect(scoreInput).toHaveValue(9);
    });

    it('decreases score by 5 when -5 button clicked', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '10');

      await user.click(screen.getByRole('button', { name: /decrease by 5/i }));

      expect(scoreInput).toHaveValue(5);
    });

    it('handles adjustment from empty score', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: /increase by 1/i }));

      const scoreInput = screen.getByRole('spinbutton');
      expect(scoreInput).toHaveValue(1);
    });
  });

  describe('Form Submission', () => {
    it('submits score with correct data', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '25');

      const submitButton = screen.getByRole('button', { name: /add score/i });
      await user.click(submitButton);

      expect(mockOnSubmit).toHaveBeenCalledWith({
        participantId: 'p1',
        roundNumber: null,
        category: null,
        scoreValue: 25,
      });
    });

    it('clears score after successful submission', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '25');

      const submitButton = screen.getByRole('button', { name: /add score/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(scoreInput).toHaveValue(null);
      });
    });

    it('shows loading state during submission', async () => {
      // Use never-resolving promise so isSubmitting stays true during assertions
      mockOnSubmit.mockImplementation(() => new Promise(() => {}));
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '25');

      const submitButton = screen.getByRole('button', { name: /add score/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });
    });

    it('disables submit button when score is empty', () => {
      render(<ScoreInput {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /add score/i });
      expect(submitButton).toBeDisabled();
    });

    it('enables submit button when score is entered', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '10');

      const submitButton = screen.getByRole('button', { name: /add score/i });
      expect(submitButton).not.toBeDisabled();
    });
  });

  describe('Validation', () => {
    it('shows error when submitting without participant', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} participants={[]} />);

      const scoreInput = screen.getByRole('spinbutton');
      fireEvent.change(scoreInput, { target: { value: '10' } });

      const form = scoreInput.closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(screen.getByText(/please select a participant/i)).toBeInTheDocument();
      });
    });

    it('shows error when submitting with invalid score', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      fireEvent.change(scoreInput, { target: { value: 'abc' } });

      const form = scoreInput.closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(screen.getByText(/please enter a valid score/i)).toBeInTheDocument();
      });
    });

    it('shows error when submission fails', async () => {
      mockOnSubmit.mockRejectedValue(new Error('Network error'));
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '25');

      const submitButton = screen.getByRole('button', { name: /add score/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });
    });
  });

  describe('Sync Status', () => {
    it('does not show sync indicator when idle', () => {
      render(<ScoreInput {...defaultProps} syncStatus="idle" />);

      expect(screen.queryByText(/synced/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/saving/i)).not.toBeInTheDocument();
    });

    it('shows saving indicator when syncStatus is saving', () => {
      render(<ScoreInput {...defaultProps} syncStatus="saving" />);

      expect(screen.getByText(/saving/i)).toBeInTheDocument();
    });

    it('shows synced indicator when syncStatus is synced', () => {
      render(<ScoreInput {...defaultProps} syncStatus="synced" />);

      expect(screen.getByText(/synced/i)).toBeInTheDocument();
    });

    it('shows error indicator when syncStatus is error', () => {
      render(<ScoreInput {...defaultProps} syncStatus="error" />);

      expect(screen.getByText(/error/i)).toBeInTheDocument();
    });
  });

  describe('Undo Functionality', () => {
    it('calls onUndo when undo button is clicked', async () => {
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} onUndo={mockOnUndo} />);

      await user.click(screen.getByRole('button', { name: /undo/i }));

      expect(mockOnUndo).toHaveBeenCalledTimes(1);
    });

    it('disables undo button during submission', async () => {
      // Use never-resolving promise so isSubmitting stays true during assertions
      mockOnSubmit.mockImplementation(() => new Promise(() => {}));
      const user = userEvent.setup();
      render(<ScoreInput {...defaultProps} onUndo={mockOnUndo} />);

      const scoreInput = screen.getByRole('spinbutton');
      await user.type(scoreInput, '25');

      await user.click(screen.getByRole('button', { name: /add score/i }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /undo/i })).toBeDisabled();
      });
    });
  });

  describe('Current Round', () => {
    it('uses currentRound as initial round value', () => {
      render(<ScoreInput {...defaultProps} currentRound={2} />);

      // The round selector should have round 2 selected
      expect(screen.getByRole('combobox', { name: /round/i })).toHaveTextContent('2');
    });

    it('updates round when currentRound prop changes', () => {
      const { rerender } = render(<ScoreInput {...defaultProps} currentRound={1} />);

      rerender(<ScoreInput {...defaultProps} currentRound={3} />);

      expect(screen.getByRole('combobox', { name: /round/i })).toHaveTextContent('3');
    });
  });

  describe('Labels', () => {
    it('displays Player label', () => {
      render(<ScoreInput {...defaultProps} />);

      expect(screen.getByText(/player/i)).toBeInTheDocument();
    });

    it('displays Score label', () => {
      render(<ScoreInput {...defaultProps} />);

      // Use getByLabelText to specifically find the score input label
      expect(screen.getByLabelText(/score/i)).toBeInTheDocument();
    });
  });

  describe('Italian locale', () => {
    beforeEach(() => {
      i18n.locale = 'it';
    });
    afterEach(() => {
      i18n.locale = 'en';
    });

    it('labels the form in Italian', () => {
      render(<ScoreInput {...defaultProps} categories={['Strategia']} onUndo={mockOnUndo} />);

      expect(screen.getByText('Giocatore')).toBeInTheDocument();
      expect(screen.getByText('Categoria')).toBeInTheDocument();
      expect(screen.getByLabelText('Punteggio')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aggiungi punteggio' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Diminuisci di 5' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aumenta di 1' })).toBeInTheDocument();
      expect(screen.queryByText('Player')).not.toBeInTheDocument();
      expect(screen.queryByText('Add Score')).not.toBeInTheDocument();
    });

    it('reports a validation error in Italian', async () => {
      render(<ScoreInput {...defaultProps} participants={[]} />);

      fireEvent.change(screen.getByLabelText('Punteggio'), { target: { value: '5' } });
      fireEvent.click(screen.getByRole('button', { name: 'Aggiungi punteggio' }));

      expect(await screen.findByText('Seleziona un giocatore')).toBeInTheDocument();
    });

    it('shows the sync status in Italian', () => {
      render(<ScoreInput {...defaultProps} syncStatus="synced" />);

      expect(screen.getByText('Sincronizzato')).toBeInTheDocument();
    });
  });
});

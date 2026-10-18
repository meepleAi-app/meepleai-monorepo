/**
 * SessionCreationWizard tests.
 * - The icon-only "add dimension" button in the scoring step must have an accessible
 *   name (axe button-name / WCAG 4.1.2).
 * - #4156: after "Crea Sessione" the wizard lands on the live page of the session it created.
 */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { beforeEach, describe, it, expect, vi } from 'vitest';

import { SessionCreationWizard } from '../SessionCreationWizard';

const { push, createSession, addPlayer, startSession } = vi.hoisted(() => ({
  push: vi.fn(),
  createSession: vi.fn(),
  addPlayer: vi.fn(),
  startSession: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, back: vi.fn() }),
}));

vi.mock('@/lib/api', () => ({ api: { liveSessions: { createSession, addPlayer, startSession } } }));

// GamePicker fetches the library catalogue; replace it with a button that selects a game
// so the wizard can advance from the game step to the scoring step.
vi.mock('@/components/features/game-picker', () => ({
  GamePicker: ({
    onChange,
  }: {
    onChange: (game: { id: string; title: string; manual: boolean }) => void;
  }) => (
    <button type="button" onClick={() => onChange({ id: 'g1', title: 'Test Game', manual: true })}>
      mock-select-game
    </button>
  ),
}));

describe('SessionCreationWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function advanceToScoringStep() {
    render(<SessionCreationWizard />);
    // Step 1 (game) → pick a game so "Avanti" enables → Step 2 (scoring).
    fireEvent.click(screen.getByText('mock-select-game'));
    fireEvent.click(screen.getByText('Avanti'));
  }

  it('exposes an accessible name on the add-dimension icon button', () => {
    advanceToScoringStep();
    expect(screen.getByRole('button', { name: /aggiungi dimensione/i })).toBeInTheDocument();
  });

  it('lands on the live page of the session it created (#4156)', async () => {
    // L'id che il wizard riceve è quello di una LiveGameSession, e le pagine che la leggono
    // stanno sotto /live. Prima navigava su /sessions/{id}, che leggeva la GameSession: 404.
    createSession.mockResolvedValue('live-1');
    addPlayer.mockResolvedValue('player-1');
    startSession.mockResolvedValue(undefined);

    advanceToScoringStep();
    fireEvent.click(screen.getByText('Avanti')); // scoring → players
    fireEvent.change(screen.getByPlaceholderText('Nome giocatore'), {
      target: { value: 'Marco' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));
    fireEvent.click(screen.getByText('Avanti')); // players → review
    fireEvent.click(screen.getByText('Crea Sessione'));

    await waitFor(() => expect(push).toHaveBeenCalledWith('/sessions/live-1/live'));
    expect(startSession).toHaveBeenCalledWith('live-1');
  });
});

/**
 * SessionCreationWizard accessibility tests.
 * The icon-only "add dimension" button in the scoring step must have an accessible
 * name (axe button-name / WCAG 4.1.2).
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SessionCreationWizard } from '../SessionCreationWizard';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/lib/api', () => ({ api: { liveSessions: {} } }));

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
});

/**
 * SetupWizard Tests
 *
 * Setup Wizard — Task 8
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SetupWizard } from '../SetupWizard';

// ─── Mock API ─────────────────────────────────────────────────────────────────

const mockGenerateSetupChecklist = vi.hoisted(() => vi.fn());
const mockUpdateSetupChecklist = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      generateSetupChecklist: mockGenerateSetupChecklist,
      updateSetupChecklist: mockUpdateSetupChecklist,
    },
  },
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockChecklist = {
  components: [
    { name: 'Tabellone', quantity: 1, checked: false },
    { name: 'Pedine', quantity: 4, checked: false },
    { name: 'Carte', quantity: 52, checked: false },
  ],
  steps: [
    { order: 1, instruction: 'Apri il tabellone al centro del tavolo', completed: false },
    { order: 2, instruction: 'Distribuisci 5 carte a ogni giocatore', completed: false },
    { order: 3, instruction: 'Posiziona le pedine sulla casella di partenza', completed: false },
  ],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SetupWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUpdateSetupChecklist.mockResolvedValue(undefined);
  });

  it('renders loading state initially', () => {
    mockGenerateSetupChecklist.mockReturnValue(new Promise(() => {}));

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    expect(screen.getByText(/generazione checklist/i)).toBeInTheDocument();
  });

  it('shows tabs after API response', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Componenti')).toBeInTheDocument();
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });
  });

  it('calls generateSetupChecklist with correct params', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(mockGenerateSetupChecklist).toHaveBeenCalledWith('sess-1', 4);
    });
  });

  it('shows components in the Componenti tab by default', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Tabellone (x1)')).toBeInTheDocument();
      expect(screen.getByText('Pedine (x4)')).toBeInTheDocument();
      expect(screen.getByText('Carte (x52)')).toBeInTheDocument();
    });
  });

  it('toggling a component calls PUT with updated data', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);
    const user = userEvent.setup();

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Tabellone (x1)')).toBeInTheDocument();
    });

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[0]);

    await waitFor(() => {
      expect(mockUpdateSetupChecklist).toHaveBeenCalledWith(
        'sess-1',
        expect.objectContaining({
          components: expect.arrayContaining([
            expect.objectContaining({ name: 'Tabellone', checked: true }),
          ]),
        })
      );
    });
  });

  it('switches to Setup tab and shows steps', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);
    const user = userEvent.setup();

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Setup'));

    expect(screen.getByText('Apri il tabellone al centro del tavolo')).toBeInTheDocument();
    expect(screen.getByText('Distribuisci 5 carte a ogni giocatore')).toBeInTheDocument();
  });

  it('completing a step advances to the next step', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(mockChecklist);
    const user = userEvent.setup();

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Setup'));

    // Click "Done, next step" for the first step
    const doneButton = screen.getByRole('button', { name: /done, next step/i });
    await user.click(doneButton);

    await waitFor(() => {
      expect(mockUpdateSetupChecklist).toHaveBeenCalledWith(
        'sess-1',
        expect.objectContaining({
          steps: expect.arrayContaining([
            expect.objectContaining({ order: 1, completed: true }),
            expect.objectContaining({ order: 2, completed: false }),
          ]),
        })
      );
    });
  });

  it('shows fallback message on API error', async () => {
    mockGenerateSetupChecklist.mockRejectedValue(new Error('Network error'));

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText(/setup non disponibile/i)).toBeInTheDocument();
    });
  });

  it('shows fallback message when API returns empty data', async () => {
    mockGenerateSetupChecklist.mockResolvedValue({ components: [], steps: [] });

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText(/setup non disponibile/i)).toBeInTheDocument();
    });
  });

  it('shows fallback message when API returns null', async () => {
    mockGenerateSetupChecklist.mockResolvedValue(null);

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText(/setup non disponibile/i)).toBeInTheDocument();
    });
  });

  it('shows "All steps completed!" when all steps are done', async () => {
    const allCompleted = {
      ...mockChecklist,
      steps: mockChecklist.steps.map(s => ({ ...s, completed: true })),
    };
    mockGenerateSetupChecklist.mockResolvedValue(allCompleted);
    const user = userEvent.setup();

    render(<SetupWizard sessionId="sess-1" playerCount={4} />);

    await waitFor(() => {
      expect(screen.getByText('Setup')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Setup'));

    expect(screen.getByText('All steps completed!')).toBeInTheDocument();
  });
});

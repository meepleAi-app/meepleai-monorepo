/**
 * ArbitroModal Tests
 *
 * Game Night Improvvisata — Task 17
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ArbitroModal } from '../ArbitroModal';

// ─── Mock API ─────────────────────────────────────────────────────────────────

const mockSubmitDispute = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      submitDispute: mockSubmitDispute,
    },
  },
}));

// ─── Mock store ───────────────────────────────────────────────────────────────

const mockAddDispute = vi.hoisted(() => vi.fn());

vi.mock('@/lib/stores/live-session-store', () => ({
  useLiveSessionStore: (selector: (s: Record<string, unknown>) => unknown) => {
    const state = { addDispute: mockAddDispute };
    return selector(state);
  },
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockPlayers = [{ name: 'Alice' }, { name: 'Bob' }];

const mockVerdict = {
  id: 'verdict-uuid',
  verdict: 'Sì, la carta può essere giocata.',
  ruleReferences: ['Regola 4.2', 'Regola 7.1'],
  note: null,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ArbitroModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the trigger button', () => {
    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    expect(screen.getByRole('button', { name: /arbitro/i })).toBeInTheDocument();
  });

  it('opens the dialog on trigger click', async () => {
    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    const trigger = screen.getByRole('button', { name: /arbitro/i });
    await userEvent.click(trigger);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('shows textarea and player select inside the dialog', async () => {
    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    expect(screen.getByLabelText('Descrivi la disputa')).toBeInTheDocument();
    expect(screen.getByLabelText('Sollevata da')).toBeInTheDocument();
  });

  it('shows player options in the select', async () => {
    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    expect(screen.getByRole('option', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Bob' })).toBeInTheDocument();
  });

  it('submit button is disabled when form is empty', async () => {
    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    const submitBtn = screen.getByRole('button', { name: /chiedi all'arbitro/i });
    expect(submitBtn).toBeDisabled();
  });

  it('submit calls api with correct params', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);

    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(
      screen.getByLabelText('Descrivi la disputa'),
      'Posso giocare questa carta?'
    );

    fireEvent.change(screen.getByLabelText('Sollevata da'), {
      target: { value: 'Alice' },
    });

    const submitBtn = screen.getByRole('button', { name: /chiedi all'arbitro/i });
    await userEvent.click(submitBtn);

    expect(mockSubmitDispute).toHaveBeenCalledWith(
      'sess-1',
      'Posso giocare questa carta?',
      'Alice'
    );
  });

  it('shows loading state while API call is in progress', async () => {
    // Never resolves to keep loading state
    mockSubmitDispute.mockReturnValue(new Promise(() => {}));

    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test dispute');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Bob' } });

    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByText(/analizzando il regolamento/i)).toBeInTheDocument();
    });
  });

  it('displays the verdict card on successful API response', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);

    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Alice' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByText('Verdetto')).toBeInTheDocument();
      expect(screen.getByText('Sì, la carta può essere giocata.')).toBeInTheDocument();
    });
  });

  it('calls onVerdictReceived callback with verdict', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);
    const onVerdictReceived = vi.fn();

    render(
      <ArbitroModal
        sessionId="sess-1"
        players={mockPlayers}
        onVerdictReceived={onVerdictReceived}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Alice' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(onVerdictReceived).toHaveBeenCalledWith(mockVerdict);
    });
  });

  it('shows error message on API failure', async () => {
    mockSubmitDispute.mockRejectedValue(new Error('Network error'));

    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Bob' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Network error');
    });
  });

  it('adds the dispute to the store after successful API call', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);

    render(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test dispute');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Alice' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(mockAddDispute).toHaveBeenCalledWith(
        expect.objectContaining({
          id: mockVerdict.id,
          verdict: mockVerdict.verdict,
          raisedByPlayerName: 'Alice',
        })
      );
    });
  });
});

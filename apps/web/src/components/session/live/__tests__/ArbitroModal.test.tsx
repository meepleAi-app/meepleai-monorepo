/**
 * ArbitroModal Tests
 *
 * Game Night Improvvisata — Task 17
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactElement } from 'react';

import { ConflictError } from '@/lib/api/core/errors';

import { ArbitroModal } from '../ArbitroModal';

// #4844: the refusals of a game that is paused, not started or over read by code.
const MESSAGES: Record<string, string> = {
  'pages.sessionLive.conflicts.notStarted':
    'La partita non è ancora avviata: avviala prima di continuare.',
  'pages.sessionLive.conflicts.paused':
    'La partita è in pausa: questa azione vale solo a partita in corso.',
  'pages.sessionLive.conflicts.completed': 'La partita è già conclusa.',
  'pages.sessionLive.conflicts.alreadyStarted': 'La partita è già avviata.',
  'pages.sessionLive.conflicts.notPaused': 'La partita non è in pausa.',
  'pages.sessionLive.conflicts.generic':
    'Operazione non consentita nello stato attuale della partita.',
};

function renderIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

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
    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    expect(screen.getByRole('button', { name: /arbitro/i })).toBeInTheDocument();
  });

  it('opens the dialog on trigger click', async () => {
    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    const trigger = screen.getByRole('button', { name: /arbitro/i });
    await userEvent.click(trigger);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('shows textarea and player select inside the dialog', async () => {
    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    expect(screen.getByLabelText('Descrivi la disputa')).toBeInTheDocument();
    expect(screen.getByLabelText('Sollevata da')).toBeInTheDocument();
  });

  it('shows player options in the select', async () => {
    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    expect(screen.getByRole('option', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Bob' })).toBeInTheDocument();
  });

  it('submit button is disabled when form is empty', async () => {
    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    const submitBtn = screen.getByRole('button', { name: /chiedi all'arbitro/i });
    expect(submitBtn).toBeDisabled();
  });

  it('submit calls api with correct params', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
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

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
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

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
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

    renderIntl(
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

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Bob' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Network error');
    });
  });

  // #4844: a dispute on a PAUSED game is refused (ending it is allowed): the player reads «in pausa»,
  // not the server's «Cannot submit a rule dispute for session in Paused status».
  it('a 409 with a known code reads in Italian, by code, never as the server text', async () => {
    mockSubmitDispute.mockRejectedValue(
      new ConflictError({
        message:
          'Cannot submit a rule dispute for session in Paused status. Session must be InProgress.',
        code: 'session_paused',
      })
    );

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Bob' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'La partita è in pausa: questa azione vale solo a partita in corso.'
      );
    });
    expect(screen.getByRole('alert')).not.toHaveTextContent(/Cannot submit/);
  });

  it('a 409 with a code the client does not know falls back to the generic sentence, not the server text', async () => {
    mockSubmitDispute.mockRejectedValue(
      new ConflictError({ message: 'Some new refusal nobody mapped yet', code: 'session_frozen' })
    );

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
    await userEvent.click(screen.getByRole('button', { name: /arbitro/i }));

    await userEvent.type(screen.getByLabelText('Descrivi la disputa'), 'Test');
    fireEvent.change(screen.getByLabelText('Sollevata da'), { target: { value: 'Bob' } });
    await userEvent.click(screen.getByRole('button', { name: /chiedi all'arbitro/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Operazione non consentita nello stato attuale della partita.'
      );
    });
    expect(screen.getByRole('alert')).not.toHaveTextContent(/nobody mapped/);
  });

  it('adds the dispute to the store after successful API call', async () => {
    mockSubmitDispute.mockResolvedValue(mockVerdict);

    renderIntl(<ArbitroModal sessionId="sess-1" players={mockPlayers} />);
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

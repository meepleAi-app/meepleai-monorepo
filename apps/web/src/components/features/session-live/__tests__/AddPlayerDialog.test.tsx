/**
 * Tests for AddPlayerDialog (Issue #2505).
 *
 * Covers:
 *   - Renders nothing when open=false
 *   - Renders dialog when open=true
 *   - Guest mode: submit with displayName calls mutate with color auto (first free)
 *   - Guest mode: color auto picks first free from PlayerColorSchema.options
 *   - All colors taken → shows errorNoColorAvailable, does NOT call mutate
 *   - Registered mode: search, select user, submit passes userId
 *   - onClose called on success (via mutation callback)
 *   - 409 error with "name" → shows errorDuplicateName
 *   - Cancel button calls onClose
 *   - ESC key calls onClose
 */

import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { AddPlayerDialogLabels } from '../AddPlayerDialog';
import { AddPlayerDialog } from '../AddPlayerDialog';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mutateMock = vi.fn();
const isPendingMock = { value: false };

vi.mock('@/hooks/mutations/useAddLivePlayer', () => ({
  useAddLivePlayer: () => ({
    mutate: mutateMock,
    isPending: isPendingMock.value,
  }),
}));

vi.mock('@/lib/game-nights/hooks/usePlayerSearch', () => ({
  usePlayerSearch: ({ query }: { query: string }) => ({
    data:
      query.length > 0
        ? [{ id: 'user-uuid-001', displayName: 'Giulia', email: 'giulia@example.com' }]
        : [],
    isFetching: false,
  }),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const SESSION_ID = '00000000-0000-4000-8000-000000002505';

const LABELS: AddPlayerDialogLabels = {
  dialogTitle: 'Aggiungi giocatore',
  guestTab: 'Ospite',
  registeredTab: 'Utente registrato',
  displayNameLabel: 'Nome',
  displayNamePlaceholder: 'Es. Marco',
  searchUserPlaceholder: 'Cerca per nome…',
  confirmCta: 'Aggiungi',
  cancelCta: 'Annulla',
  errorNoColorAvailable: 'Nessun colore disponibile.',
  errorDuplicateName: 'Nome duplicato.',
  errorColorTaken: 'Colore preso.',
  errorGeneric: 'Errore generico.',
};

function makePlayer(overrides: Partial<LiveSessionPlayerDto> = {}): LiveSessionPlayerDto {
  return {
    id: 'player-' + Math.random().toString(36).slice(2, 6),
    userId: null,
    displayName: 'Player',
    name: 'Player',
    role: 'Player',
    color: 'Red',
    score: 0,
    isOnline: true,
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('AddPlayerDialog (#2505)', () => {
  beforeEach(() => {
    mutateMock.mockReset();
    isPendingMock.value = false;
  });

  it('renders nothing when open=false', () => {
    const qc = new QueryClient();
    const { container } = render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={false}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders the dialog when open=true', () => {
    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Aggiungi giocatore')).toBeInTheDocument();
  });

  it('guest mode: submits with displayName and auto-selects first free color', async () => {
    // Players occupy Red and Blue → first free should be Green
    const players = [makePlayer({ color: 'Red' }), makePlayer({ color: 'Blue' })];

    mutateMock.mockImplementation((_req, { onSuccess }) => onSuccess?.());

    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={players}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );

    fireEvent.change(screen.getByPlaceholderText('Es. Marco'), {
      target: { value: 'Marco' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));

    await waitFor(() => expect(mutateMock).toHaveBeenCalledOnce());
    expect(mutateMock).toHaveBeenCalledWith(
      expect.objectContaining({ displayName: 'Marco', color: 'Green' }),
      expect.any(Object)
    );
  });

  it('auto-selects Red when no players exist (first color)', async () => {
    mutateMock.mockImplementation((_req, { onSuccess }) => onSuccess?.());
    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    fireEvent.change(screen.getByPlaceholderText('Es. Marco'), {
      target: { value: 'Anna' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));
    await waitFor(() => expect(mutateMock).toHaveBeenCalledOnce());
    expect(mutateMock).toHaveBeenCalledWith(
      expect.objectContaining({ color: 'Red' }),
      expect.any(Object)
    );
  });

  it('shows errorNoColorAvailable when all 10 colors are taken', async () => {
    const ALL_COLORS = [
      'Red',
      'Blue',
      'Green',
      'Yellow',
      'Purple',
      'Orange',
      'White',
      'Black',
      'Pink',
      'Teal',
    ] as const;
    const players = ALL_COLORS.map(color => makePlayer({ color }));

    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={players}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );

    fireEvent.change(screen.getByPlaceholderText('Es. Marco'), {
      target: { value: 'OverflowPlayer' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));

    await waitFor(() => expect(screen.getByText('Nessun colore disponibile.')).toBeInTheDocument());
    expect(mutateMock).not.toHaveBeenCalled();
  });

  it('registered mode: selects user and passes userId on submit', async () => {
    mutateMock.mockImplementation((_req, { onSuccess }) => onSuccess?.());
    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );

    // Switch to registered tab
    fireEvent.click(screen.getByText('Utente registrato'));

    // Type search query
    fireEvent.change(screen.getByPlaceholderText('Cerca per nome…'), {
      target: { value: 'Giulia' },
    });

    // Select the user result
    await waitFor(() => expect(screen.getByText('Giulia')).toBeInTheDocument());
    fireEvent.click(screen.getByText('Giulia'));

    // Submit
    fireEvent.click(screen.getByText('Aggiungi'));

    await waitFor(() => expect(mutateMock).toHaveBeenCalledOnce());
    expect(mutateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        displayName: 'Giulia',
        userId: 'user-uuid-001',
        color: 'Red',
      }),
      expect.any(Object)
    );
  });

  it('calls onClose when cancel is clicked', () => {
    const onClose = vi.fn();
    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={onClose}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    fireEvent.click(screen.getByText('Annulla'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose when Escape is pressed', () => {
    const onClose = vi.fn();
    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={onClose}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );
    const dialog = screen.getByRole('dialog');
    fireEvent.keyDown(dialog, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('shows errorDuplicateName on 409 with "name" in message', async () => {
    const { ApiError } = await import('@/lib/api/core/errors');
    mutateMock.mockImplementation((_req, { onError }) =>
      onError?.(new ApiError({ message: 'Duplicate name', statusCode: 409 }))
    );

    const qc = new QueryClient();
    render(
      <AddPlayerDialog
        sessionId={SESSION_ID}
        players={[]}
        open={true}
        onClose={vi.fn()}
        labels={LABELS}
      />,
      { wrapper: makeWrapper(qc) }
    );

    fireEvent.change(screen.getByPlaceholderText('Es. Marco'), {
      target: { value: 'Marco' },
    });
    fireEvent.click(screen.getByText('Aggiungi'));

    await waitFor(() => expect(screen.getByText('Nome duplicato.')).toBeInTheDocument());
  });
});

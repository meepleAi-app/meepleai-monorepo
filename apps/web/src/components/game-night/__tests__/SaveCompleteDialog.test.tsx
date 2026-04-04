import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockSaveComplete = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      saveComplete: (...args: unknown[]) => mockSaveComplete(...args),
    },
  },
}));

import { SaveCompleteDialog } from '../SaveCompleteDialog';

describe('SaveCompleteDialog', () => {
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    sessionId: 'sess-123',
    onSaveComplete: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the dialog with confirm phase', () => {
    render(<SaveCompleteDialog {...defaultProps} />);

    expect(screen.getByText('Salva Stato Completo')).toBeInTheDocument();
    expect(screen.getByText(/vuoi salvare lo stato/i)).toBeInTheDocument();
    expect(screen.getByTestId('save-complete-confirm')).toBeInTheDocument();
  });

  it('shows save checklist items', () => {
    render(<SaveCompleteDialog {...defaultProps} />);

    expect(screen.getByText(/punteggi e stato del turno/i)).toBeInTheDocument();
    expect(screen.getByText(/memoria dell'agente ai/i)).toBeInTheDocument();
    expect(screen.getByText(/snapshot della partita/i)).toBeInTheDocument();
  });

  it('calls saveComplete on confirm and shows success', async () => {
    const user = userEvent.setup();
    mockSaveComplete.mockResolvedValue({
      sessionId: 'sess-123',
      snapshotIndex: 5,
      recap: 'Partita salvata al turno 5. In testa: Marco con 45 punti.',
      photoCount: 2,
      savedAt: new Date().toISOString(),
    });

    render(<SaveCompleteDialog {...defaultProps} />);

    await user.click(screen.getByTestId('save-complete-confirm'));

    await waitFor(() => {
      expect(mockSaveComplete).toHaveBeenCalledWith('sess-123');
    });

    const successEl = await screen.findByTestId('save-complete-success');
    expect(successEl).toBeInTheDocument();
    expect(successEl).toHaveTextContent(/partita salvata/i);
    expect(screen.getByText(/snapshot #5/i)).toBeInTheDocument();
    expect(screen.getByText(/2 foto/i)).toBeInTheDocument();
  });

  it('shows error message on failure and allows retry', async () => {
    const user = userEvent.setup();
    mockSaveComplete.mockRejectedValueOnce(new Error('Network error'));

    render(<SaveCompleteDialog {...defaultProps} />);

    await user.click(screen.getByTestId('save-complete-confirm'));

    await waitFor(() => {
      expect(screen.getByText(/errore durante il salvataggio/i)).toBeInTheDocument();
    });

    // Should show retry button
    expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
  });

  it('calls onSaveComplete when closing after success', async () => {
    const user = userEvent.setup();
    const onSaveComplete = vi.fn();
    mockSaveComplete.mockResolvedValue({
      sessionId: 'sess-123',
      snapshotIndex: 1,
      recap: 'Partita salvata.',
      photoCount: 0,
      savedAt: new Date().toISOString(),
    });

    render(<SaveCompleteDialog {...defaultProps} onSaveComplete={onSaveComplete} />);

    await user.click(screen.getByTestId('save-complete-confirm'));

    await waitFor(() => {
      expect(screen.getByTestId('save-complete-close')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('save-complete-close'));

    expect(onSaveComplete).toHaveBeenCalled();
  });

  it('does not call onSaveComplete when cancelling before save', async () => {
    const user = userEvent.setup();
    const onSaveComplete = vi.fn();

    render(<SaveCompleteDialog {...defaultProps} onSaveComplete={onSaveComplete} />);

    await user.click(screen.getByRole('button', { name: /annulla/i }));

    expect(onSaveComplete).not.toHaveBeenCalled();
  });
});

/**
 * OwnershipDeclarationDialog Component Tests
 *
 * Tests: rendering, checkbox interaction, confirm flow, cancel, loading state
 */

import { useState } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotFoundError } from '@/lib/api/core/errors';

import { OwnershipDeclarationDialog } from '../OwnershipDeclarationDialog';

// ============================================================================
// Mock Setup
// ============================================================================

const mockDeclareOwnership = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      declareOwnership: (...args: unknown[]) => mockDeclareOwnership(...args),
    },
  },
}));

const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    error: (...args: unknown[]) => mockToastError(...args),
    success: vi.fn(),
  },
}));

// ============================================================================
// Helpers
// ============================================================================

const defaultProps = {
  gameId: 'game-1',
  gameName: 'Catan',
  open: true,
  onOpenChange: vi.fn(),
  onOwnershipDeclared: vi.fn(),
};

const mockOwnershipResult = {
  gameState: 'Owned',
  ownershipDeclaredAt: '2026-03-14T10:00:00Z',
  hasRagAccess: true,
  kbCardCount: 3,
  isRagPublic: false,
};

// ============================================================================
// Tests
// ============================================================================

describe('OwnershipDeclarationDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockDeclareOwnership.mockResolvedValue(mockOwnershipResult);
  });

  it('renders dialog with game name and benefits', () => {
    render(<OwnershipDeclarationDialog {...defaultProps} />);

    const dialog = screen.getByRole('alertdialog');
    expect(dialog).toBeInTheDocument();
    expect(screen.getByText(/Possiedi Catan\?/)).toBeInTheDocument();
    expect(screen.getByText(/Tutor AI personalizzato/)).toBeInTheDocument();
    expect(screen.getByText(/Sessioni di gioco/)).toBeInTheDocument();
    expect(screen.getByText(/Prestiti/)).toBeInTheDocument();
  });

  it('renders accented text, not escape sequences (#4155)', () => {
    // Nel testo JSX `é` non è una sequenza di escape: arrivava all'utente così com'era.
    render(<OwnershipDeclarationDialog {...defaultProps} />);

    expect(screen.getByText('Perché lo chiediamo?')).toBeInTheDocument();
    expect(screen.getByRole('alertdialog')).not.toHaveTextContent(/\\u00/);
  });

  it('checkbox enables confirm button', async () => {
    const user = userEvent.setup();
    render(<OwnershipDeclarationDialog {...defaultProps} />);

    const confirmButton = screen.getByRole('button', { name: /Conferma Possesso/i });
    expect(confirmButton).toBeDisabled();

    const checkbox = screen.getByTestId('ownership-checkbox');
    await user.click(checkbox);

    expect(confirmButton).toBeEnabled();
  });

  it('confirm calls API and fires onOwnershipDeclared', async () => {
    const user = userEvent.setup();
    const onOwnershipDeclared = vi.fn();
    render(
      <OwnershipDeclarationDialog {...defaultProps} onOwnershipDeclared={onOwnershipDeclared} />
    );

    const checkbox = screen.getByTestId('ownership-checkbox');
    await user.click(checkbox);

    const confirmButton = screen.getByRole('button', { name: /Conferma Possesso/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(mockDeclareOwnership).toHaveBeenCalledWith('game-1');
      expect(onOwnershipDeclared).toHaveBeenCalledWith(mockOwnershipResult);
    });
  });

  it('"Non ancora" closes dialog without API call', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(<OwnershipDeclarationDialog {...defaultProps} onOpenChange={onOpenChange} />);

    const cancelButton = screen.getByRole('button', { name: /Non ancora/i });
    await user.click(cancelButton);

    expect(mockDeclareOwnership).not.toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('shows error toast on API failure and stays open', async () => {
    const user = userEvent.setup();
    mockDeclareOwnership.mockRejectedValueOnce(new Error('Network error'));
    const onOpenChange = vi.fn();

    render(<OwnershipDeclarationDialog {...defaultProps} onOpenChange={onOpenChange} />);

    const checkbox = screen.getByTestId('ownership-checkbox');
    await user.click(checkbox);

    const confirmButton = screen.getByRole('button', { name: /Conferma Possesso/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network error');
    });

    // Dialog should not have been closed via onOpenChange(false) from the confirm path
    // (the cancel calls onOpenChange(false) but confirm only calls it on success)
    expect(defaultProps.onOwnershipDeclared).not.toHaveBeenCalled();
    // #4155: Radix AlertDialog.Action chiude il dialogo al click, prima che la richiesta finisca.
    // Al fallimento il dialogo deve restare aperto, cosi' l'utente puo' riprovare.
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  });

  it('explains that the game must be in the library when the declaration answers 404 (#4155)', async () => {
    const user = userEvent.setup();
    mockDeclareOwnership.mockRejectedValueOnce(
      new NotFoundError({ message: 'Game game-1 not found in your library' })
    );

    render(<OwnershipDeclarationDialog {...defaultProps} />);
    await user.click(screen.getByTestId('ownership-checkbox'));
    await user.click(screen.getByRole('button', { name: /Conferma Possesso/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        'Catan non è nella tua libreria: aggiungilo prima di dichiararne il possesso.'
      );
    });
  });

  // #4756: the dialog is offered again on every upload that needs it, from a parent that stays
  // mounted. A ticked box left over would turn the next offer into a one-click declaration.
  describe('the confirmation does not outlive the offer (#4756)', () => {
    function Reopenable() {
      const [open, setOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setOpen(true)}>
            offer again
          </button>
          <OwnershipDeclarationDialog
            {...defaultProps}
            open={open}
            onOpenChange={setOpen}
            onOwnershipDeclared={vi.fn()}
          />
        </>
      );
    }

    it('starts unticked again after being closed with Escape', async () => {
      const user = userEvent.setup();
      render(<Reopenable />);
      await user.click(screen.getByTestId('ownership-checkbox'));
      expect(screen.getByTestId('ownership-checkbox')).toBeChecked();

      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      await user.click(screen.getByRole('button', { name: 'offer again' }));

      expect(await screen.findByRole('alertdialog')).toBeInTheDocument();
      expect(screen.getByTestId('ownership-checkbox')).not.toBeChecked();
    });

    it('starts unticked again after a declaration went through', async () => {
      const user = userEvent.setup();
      render(<Reopenable />);
      await user.click(screen.getByTestId('ownership-checkbox'));
      await user.click(screen.getByRole('button', { name: /Conferma Possesso/i }));
      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());

      await user.click(screen.getByRole('button', { name: 'offer again' }));

      expect(await screen.findByRole('alertdialog')).toBeInTheDocument();
      expect(screen.getByTestId('ownership-checkbox')).not.toBeChecked();
    });
  });
});

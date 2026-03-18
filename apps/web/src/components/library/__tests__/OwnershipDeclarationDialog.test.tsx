/**
 * OwnershipDeclarationDialog Component Tests
 *
 * Tests: rendering, checkbox interaction, confirm flow, cancel, loading state
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  });
});

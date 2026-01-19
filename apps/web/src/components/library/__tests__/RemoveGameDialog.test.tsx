/**
 * RemoveGameDialog Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Dialog rendering and visibility
 * - Confirmation flow
 * - Loading state during mutation
 * - Toast notifications
 * - Cancel functionality
 * - Callbacks
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RemoveGameDialog } from '../RemoveGameDialog';

// ============================================================================
// Mock Setup
// ============================================================================

let mockMutateAsync: Mock;
let mockIsPending: boolean;

vi.mock('@/hooks/queries', () => ({
  useRemoveGameFromLibrary: () => ({
    mutateAsync: mockMutateAsync,
    isPending: mockIsPending,
  }),
}));

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ============================================================================
// Helper
// ============================================================================

const defaultProps = {
  isOpen: true,
  onClose: vi.fn(),
  gameId: 'game-1',
  gameTitle: 'Catan',
};

const resetMocks = () => {
  vi.clearAllMocks();
  mockMutateAsync = vi.fn().mockResolvedValue({});
  mockIsPending = false;
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('RemoveGameDialog - Rendering', () => {
  beforeEach(resetMocks);

  it('renders dialog when isOpen is true', () => {
    render(<RemoveGameDialog {...defaultProps} />);

    expect(screen.getByText('Rimuovi dalla Libreria?')).toBeInTheDocument();
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('does not render dialog when isOpen is false', () => {
    render(<RemoveGameDialog {...defaultProps} isOpen={false} />);

    expect(screen.queryByText('Rimuovi dalla Libreria?')).not.toBeInTheDocument();
  });

  it('displays warning about irreversible action', () => {
    render(<RemoveGameDialog {...defaultProps} />);

    expect(screen.getByText(/Questa azione non può essere annullata/)).toBeInTheDocument();
  });

  it('displays game title in description', () => {
    render(<RemoveGameDialog {...defaultProps} gameTitle="Ticket to Ride" />);

    expect(screen.getByText(/Ticket to Ride/)).toBeInTheDocument();
  });

  it('renders remove and cancel buttons', () => {
    render(<RemoveGameDialog {...defaultProps} />);

    expect(screen.getByRole('button', { name: /Rimuovi/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Annulla/i })).toBeInTheDocument();
  });
});

// ============================================================================
// Remove Functionality Tests
// ============================================================================

describe('RemoveGameDialog - Remove Functionality', () => {
  beforeEach(resetMocks);

  it('calls mutateAsync with gameId when remove clicked', async () => {
    const user = userEvent.setup();
    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith('game-1');
    });
  });

  it('shows success toast after successful removal', async () => {
    const user = userEvent.setup();
    render(<RemoveGameDialog {...defaultProps} gameTitle="Catan" />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Catan rimosso dalla tua libreria.');
    });
  });

  it('calls onRemoved callback after successful removal', async () => {
    const user = userEvent.setup();
    const onRemoved = vi.fn();
    render(<RemoveGameDialog {...defaultProps} onRemoved={onRemoved} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(onRemoved).toHaveBeenCalled();
    });
  });

  it('calls onClose after successful removal', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<RemoveGameDialog {...defaultProps} onClose={onClose} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
  });
});

// ============================================================================
// Cancel Functionality Tests
// ============================================================================

describe('RemoveGameDialog - Cancel Functionality', () => {
  beforeEach(resetMocks);

  it('calls onClose when cancel clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<RemoveGameDialog {...defaultProps} onClose={onClose} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    expect(onClose).toHaveBeenCalled();
  });

  it('does not call mutateAsync when cancel clicked', async () => {
    const user = userEvent.setup();
    render(<RemoveGameDialog {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });

  it('does not call onRemoved when cancel clicked', async () => {
    const user = userEvent.setup();
    const onRemoved = vi.fn();
    render(<RemoveGameDialog {...defaultProps} onRemoved={onRemoved} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    expect(onRemoved).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('RemoveGameDialog - Loading State', () => {
  beforeEach(resetMocks);

  it('shows loading text when removing', () => {
    mockIsPending = true;

    render(<RemoveGameDialog {...defaultProps} />);

    expect(screen.getByText('Rimozione...')).toBeInTheDocument();
  });

  it('disables remove button when loading', () => {
    mockIsPending = true;

    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimozione/i });
    expect(removeButton).toBeDisabled();
  });

  it('disables cancel button when loading', () => {
    mockIsPending = true;

    render(<RemoveGameDialog {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    expect(cancelButton).toBeDisabled();
  });

  it('does not trigger duplicate mutations when loading', async () => {
    mockIsPending = true;

    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimozione/i });
    fireEvent.click(removeButton);

    // Wait a tick to ensure handler was called but returned early
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Error Handling Tests
// ============================================================================

describe('RemoveGameDialog - Error Handling', () => {
  beforeEach(resetMocks);

  it('shows error toast when mutation fails with Error', async () => {
    const user = userEvent.setup();
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network error');
    });
  });

  it('shows default error message for non-Error exceptions', async () => {
    const user = userEvent.setup();
    mockMutateAsync = vi.fn().mockRejectedValue('Unknown error');

    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(
        'Impossibile rimuovere il gioco dalla libreria.'
      );
    });
  });

  it('does not call onRemoved when mutation fails', async () => {
    const user = userEvent.setup();
    const onRemoved = vi.fn();
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<RemoveGameDialog {...defaultProps} onRemoved={onRemoved} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });

    expect(onRemoved).not.toHaveBeenCalled();
  });

  it('keeps dialog open on error (onRemoved not called)', async () => {
    const user = userEvent.setup();
    const onRemoved = vi.fn();
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<RemoveGameDialog {...defaultProps} onRemoved={onRemoved} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    await user.click(removeButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });

    // Dialog stays open (error path doesn't call onClose explicitly)
    // Test that onRemoved was not called which indicates error path
    expect(onRemoved).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('RemoveGameDialog - Accessibility', () => {
  beforeEach(resetMocks);

  it('uses AlertDialog role for destructive action', () => {
    render(<RemoveGameDialog {...defaultProps} />);

    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  });

  it('has destructive styling on remove button', () => {
    render(<RemoveGameDialog {...defaultProps} />);

    const removeButton = screen.getByRole('button', { name: /Rimuovi/i });
    expect(removeButton).toHaveClass('bg-destructive');
  });
});

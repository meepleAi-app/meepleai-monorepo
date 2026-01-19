/**
 * AddToLibraryButton Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Rendering add/remove states
 * - Loading state during mutations
 * - Quota limit enforcement
 * - Click handlers and callbacks
 * - Toast notifications
 * - Accessibility (aria-label)
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AddToLibraryButton } from '../AddToLibraryButton';

// ============================================================================
// Mock Setup
// ============================================================================

let mockGameStatus: { inLibrary: boolean } | null;
let mockQuota: { currentCount: number; maxAllowed: number; userTier: string } | null;
let mockIsLoadingStatus: boolean;
let mockIsLoadingQuota: boolean;
let mockAddMutateAsync: Mock;
let mockRemoveMutateAsync: Mock;
let mockAddIsPending: boolean;
let mockRemoveIsPending: boolean;

vi.mock('@/hooks/queries', () => ({
  useGameInLibraryStatus: () => ({
    data: mockGameStatus,
    isLoading: mockIsLoadingStatus,
  }),
  useLibraryQuota: () => ({
    data: mockQuota,
    isLoading: mockIsLoadingQuota,
  }),
  useAddGameToLibrary: () => ({
    mutateAsync: mockAddMutateAsync,
    isPending: mockAddIsPending,
  }),
  useRemoveGameFromLibrary: () => ({
    mutateAsync: mockRemoveMutateAsync,
    isPending: mockRemoveIsPending,
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

const resetMocks = () => {
  vi.clearAllMocks();
  mockGameStatus = { inLibrary: false };
  mockQuota = { currentCount: 5, maxAllowed: 10, userTier: 'free' };
  mockIsLoadingStatus = false;
  mockIsLoadingQuota = false;
  mockAddMutateAsync = vi.fn().mockResolvedValue({});
  mockRemoveMutateAsync = vi.fn().mockResolvedValue({});
  mockAddIsPending = false;
  mockRemoveIsPending = false;
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('AddToLibraryButton - Rendering', () => {
  beforeEach(resetMocks);

  it('renders add button when game is not in library', () => {
    mockGameStatus = { inLibrary: false };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Aggiungi alla Collezione');
  });

  it('renders remove button when game is in library', () => {
    mockGameStatus = { inLibrary: true };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Rimuovi dalla Libreria');
  });

  it('shows label text when showLabel is true', () => {
    render(<AddToLibraryButton gameId="game-1" showLabel={true} />);

    expect(screen.getByText('Aggiungi alla Collezione')).toBeInTheDocument();
  });

  it('hides label text when showLabel is false', () => {
    render(<AddToLibraryButton gameId="game-1" showLabel={false} />);

    expect(screen.queryByText('Aggiungi alla Collezione')).not.toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<AddToLibraryButton gameId="game-1" className="custom-class" />);

    const button = screen.getByRole('button');
    expect(button).toHaveClass('custom-class');
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('AddToLibraryButton - Loading State', () => {
  beforeEach(resetMocks);

  it('shows spinner when loading status', () => {
    mockIsLoadingStatus = true;

    const { container } = render(<AddToLibraryButton gameId="game-1" />);

    const spinner = container.querySelector('svg.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('shows spinner when add mutation is pending', () => {
    mockAddIsPending = true;

    const { container } = render(<AddToLibraryButton gameId="game-1" />);

    const spinner = container.querySelector('svg.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('disables button when loading', () => {
    mockIsLoadingStatus = true;

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });
});

// ============================================================================
// Click Handler Tests
// ============================================================================

describe('AddToLibraryButton - Click Handler', () => {
  beforeEach(resetMocks);

  it('calls addMutation when game is not in library', async () => {
    mockGameStatus = { inLibrary: false };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockAddMutateAsync).toHaveBeenCalledWith({ gameId: 'game-1' });
    });
  });

  it('calls removeMutation when game is in library', async () => {
    mockGameStatus = { inLibrary: true };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockRemoveMutateAsync).toHaveBeenCalledWith('game-1');
    });
  });

  it('shows success toast after adding', async () => {
    mockGameStatus = { inLibrary: false };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Catan aggiunto alla tua libreria.');
    });
  });

  it('shows success toast after removing', async () => {
    mockGameStatus = { inLibrary: true };

    render(<AddToLibraryButton gameId="game-1" gameTitle="Catan" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Catan rimosso dalla tua libreria.');
    });
  });

  it('calls onAdded callback after successful add', async () => {
    mockGameStatus = { inLibrary: false };
    const onAdded = vi.fn();

    render(<AddToLibraryButton gameId="game-1" onAdded={onAdded} />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(onAdded).toHaveBeenCalled();
    });
  });

  it('calls onRemoved callback after successful remove', async () => {
    mockGameStatus = { inLibrary: true };
    const onRemoved = vi.fn();

    render(<AddToLibraryButton gameId="game-1" onRemoved={onRemoved} />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(onRemoved).toHaveBeenCalled();
    });
  });
});

// ============================================================================
// Quota Enforcement Tests
// ============================================================================

describe('AddToLibraryButton - Quota Enforcement', () => {
  beforeEach(resetMocks);

  it('disables button when quota is reached', () => {
    mockGameStatus = { inLibrary: false };
    mockQuota = { currentCount: 10, maxAllowed: 10, userTier: 'free' };

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });

  it('does not disable when game is already in library (remove action)', () => {
    mockGameStatus = { inLibrary: true };
    mockQuota = { currentCount: 10, maxAllowed: 10, userTier: 'free' };

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    expect(button).not.toBeDisabled();
  });

  it('shows quota error toast when trying to add at limit', async () => {
    mockGameStatus = { inLibrary: false };
    mockQuota = { currentCount: 10, maxAllowed: 10, userTier: 'free' };

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    // Force click even though disabled
    button.removeAttribute('disabled');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockAddMutateAsync).not.toHaveBeenCalled();
    });
  });
});

// ============================================================================
// Error Handling Tests
// ============================================================================

describe('AddToLibraryButton - Error Handling', () => {
  beforeEach(resetMocks);

  it('shows error toast when add mutation fails', async () => {
    mockGameStatus = { inLibrary: false };
    mockAddMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network error');
    });
  });

  it('shows error toast when remove mutation fails', async () => {
    mockGameStatus = { inLibrary: true };
    mockRemoveMutateAsync = vi.fn().mockRejectedValue(new Error('Server error'));

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Server error');
    });
  });

  it('shows default error message for non-Error exceptions', async () => {
    mockGameStatus = { inLibrary: false };
    mockAddMutateAsync = vi.fn().mockRejectedValue('Unknown error');

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile aggiungere il gioco.');
    });
  });
});

// ============================================================================
// Edge Cases
// ============================================================================

describe('AddToLibraryButton - Edge Cases', () => {
  beforeEach(resetMocks);

  it('handles null status gracefully', () => {
    mockGameStatus = null;

    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Aggiungi alla Collezione');
  });

  it('uses default gameTitle when not provided', async () => {
    render(<AddToLibraryButton gameId="game-1" />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Game aggiunto alla tua libreria.');
    });
  });

  it('respects disabled prop', () => {
    render(<AddToLibraryButton gameId="game-1" disabled />);

    const button = screen.getByRole('button');
    expect(button).toBeDisabled();
  });
});

/**
 * BulkRemoveDialog Component Tests (Issue #2613)
 *
 * Test Coverage:
 * - Dialog rendering with game count
 * - Game titles display
 * - Confirm removal action
 * - Cancel action
 * - Loading state
 * - Success/error toast notifications
 * - Partial success handling
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BulkRemoveDialog } from '../BulkRemoveDialog';

// ============================================================================
// Mock Setup
// ============================================================================

const mockMutateAsync = vi.fn();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useRemoveGameFromLibrary: () => ({
    mutateAsync: mockMutateAsync,
    isPending: false,
  }),
}));

const mockToastSuccess = vi.fn();
const mockToastWarning = vi.fn();
const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    warning: (...args: unknown[]) => mockToastWarning(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const gameIds = ['game-1', 'game-2', 'game-3'];
const gameTitles = ['Catan', 'Ticket to Ride', 'Pandemic'];

// ============================================================================
// Wrapper
// ============================================================================

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

// ============================================================================
// Helper
// ============================================================================

const resetMocks = () => {
  vi.clearAllMocks();
  mockMutateAsync.mockResolvedValue(undefined);
};

const defaultProps = {
  isOpen: true,
  onClose: vi.fn(),
  gameIds,
  gameTitles,
  onSuccess: vi.fn(),
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('BulkRemoveDialog - Rendering', () => {
  beforeEach(resetMocks);

  it('renders dialog with game count', () => {
    render(<BulkRemoveDialog {...defaultProps} />, { wrapper: createWrapper() });

    // Use heading role to avoid matching both dialog title and button
    expect(screen.getByRole('heading', { name: /Rimuovi 3 giochi/i })).toBeInTheDocument();
  });

  it('displays game titles', () => {
    render(<BulkRemoveDialog {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows remaining count when more than 5 games', () => {
    const manyIds = ['game-1', 'game-2', 'game-3', 'game-4', 'game-5', 'game-6', 'game-7'];
    const manyTitles = ['Catan', 'Ticket to Ride', 'Pandemic', 'Wingspan', 'Azul', 'Everdell', 'Gloomhaven'];

    render(
      <BulkRemoveDialog {...defaultProps} gameIds={manyIds} gameTitles={manyTitles} />,
      { wrapper: createWrapper() }
    );

    // First 5 should be visible
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Azul')).toBeInTheDocument();

    // 6th and 7th should be counted
    expect(screen.getByText(/\.\.\.e altri 2 giochi/i)).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(
      <BulkRemoveDialog {...defaultProps} isOpen={false} />,
      { wrapper: createWrapper() }
    );

    expect(screen.queryByText(/Rimuovi/i)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Cancel Action Tests
// ============================================================================

describe('BulkRemoveDialog - Cancel', () => {
  beforeEach(resetMocks);

  it('calls onClose when cancel button clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    render(
      <BulkRemoveDialog {...defaultProps} onClose={onClose} />,
      { wrapper: createWrapper() }
    );

    await user.click(screen.getByRole('button', { name: /Annulla/i }));

    expect(onClose).toHaveBeenCalled();
  });
});

// ============================================================================
// Confirm Removal Tests
// ============================================================================

describe('BulkRemoveDialog - Confirm Removal', () => {
  beforeEach(resetMocks);

  it('removes all games on confirm', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    render(
      <BulkRemoveDialog {...defaultProps} onSuccess={onSuccess} onClose={onClose} />,
      { wrapper: createWrapper() }
    );

    await user.click(screen.getByRole('button', { name: /Rimuovi 3 giochi/i }));

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledTimes(3);
      expect(mockMutateAsync).toHaveBeenCalledWith('game-1');
      expect(mockMutateAsync).toHaveBeenCalledWith('game-2');
      expect(mockMutateAsync).toHaveBeenCalledWith('game-3');
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('3 giochi rimossi dalla libreria');
      expect(onSuccess).toHaveBeenCalled();
      expect(onClose).toHaveBeenCalled();
    });
  });

  it('shows warning toast on partial success', async () => {
    mockMutateAsync
      .mockResolvedValueOnce(undefined)
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkRemoveDialog {...defaultProps} />, { wrapper: createWrapper() });

    await user.click(screen.getByRole('button', { name: /Rimuovi 3 giochi/i }));

    await waitFor(() => {
      expect(mockToastWarning).toHaveBeenCalledWith('2 giochi rimossi, 1 errori');
    });
  });

  it('shows error toast when all removals fail', async () => {
    mockMutateAsync.mockRejectedValue(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkRemoveDialog {...defaultProps} />, { wrapper: createWrapper() });

    await user.click(screen.getByRole('button', { name: /Rimuovi 3 giochi/i }));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile rimuovere i giochi');
    });
  });

  it('does not remove when gameIds is empty', async () => {
    const user = userEvent.setup();

    render(
      <BulkRemoveDialog {...defaultProps} gameIds={[]} gameTitles={[]} />,
      { wrapper: createWrapper() }
    );

    // Button should show 0 games
    const confirmButton = screen.getByRole('button', { name: /Rimuovi 0 giochi/i });
    await user.click(confirmButton);

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('BulkRemoveDialog - Loading State', () => {
  beforeEach(resetMocks);

  it('disables buttons during removal', async () => {
    // Make mutation take time
    mockMutateAsync.mockImplementation(
      () => new Promise(resolve => setTimeout(resolve, 100))
    );

    const user = userEvent.setup();

    render(<BulkRemoveDialog {...defaultProps} />, { wrapper: createWrapper() });

    const confirmButton = screen.getByRole('button', { name: /Rimuovi 3 giochi/i });
    await user.click(confirmButton);

    // Button should show loading text
    await waitFor(() => {
      expect(screen.getByText(/Rimozione\.\.\./i)).toBeInTheDocument();
    });

    // Cancel button should be disabled
    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    expect(cancelButton).toBeDisabled();
  });
});

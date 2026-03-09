/**
 * BulkActionBar Component Tests (Issue #2613, #2868)
 *
 * Test Coverage:
 * - Rendering with selection count
 * - Bulk favorite action
 * - Bulk change state action (Issue #2868)
 * - Bulk remove action (opens dialog)
 * - Bulk export dropdown
 * - Select all / deselect all toggle
 * - Clear selection button
 * - Mobile vs desktop rendering
 * - Glassmorphism design + safe area (Mobile UX #18)
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BulkActionBar } from '../BulkActionBar';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { getMenuItem, getAlertDialogHeading } from '@/test-utils/locale-queries';

// ============================================================================
// Mock Setup
// ============================================================================

const mockMutateAsync = vi.fn();
const mockUpdateGameStateMutateAsync = vi.fn();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useUpdateLibraryEntry: () => ({
    mutateAsync: mockMutateAsync,
    isPending: false,
  }),
  useUpdateGameState: () => ({
    mutateAsync: mockUpdateGameStateMutateAsync,
    isPending: false,
  }),
  useRemoveGameFromLibrary: () => ({
    mutateAsync: vi.fn().mockResolvedValue(undefined),
    isPending: false,
  }),
}));

const mockExportLibrary = vi.fn();

vi.mock('@/lib/export/libraryExport', () => ({
  exportLibrary: (...args: unknown[]) => mockExportLibrary(...args),
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

const mockGames: UserLibraryEntry[] = [
  {
    id: 'entry-1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameImageUrl: null,
    addedAt: '2024-06-15T10:30:00Z',
    isFavorite: false,
    notes: null,
    currentState: 'Nuovo',
    stateChangedAt: null,
    stateNotes: null,
  },
  {
    id: 'entry-2',
    userId: 'user-1',
    gameId: 'game-2',
    gameTitle: 'Ticket to Ride',
    gamePublisher: 'Days of Wonder',
    gameYearPublished: 2004,
    gameImageUrl: null,
    addedAt: '2024-07-20T14:00:00Z',
    isFavorite: true,
    notes: 'Great game',
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
  },
  {
    id: 'entry-3',
    userId: 'user-1',
    gameId: 'game-3',
    gameTitle: 'Pandemic',
    gamePublisher: 'Z-Man Games',
    gameYearPublished: 2008,
    gameImageUrl: null,
    addedAt: '2024-08-10T09:00:00Z',
    isFavorite: false,
    notes: null,
    currentState: 'Wishlist',
    stateChangedAt: null,
    stateNotes: null,
  },
];

const allGameIds = mockGames.map(g => g.gameId);
const selectedIds = ['game-1', 'game-2'];

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
  mockMutateAsync.mockResolvedValue({});
  mockUpdateGameStateMutateAsync.mockResolvedValue(undefined);
};

const defaultProps = {
  selectedCount: 2,
  selectedIds,
  allGameIds,
  games: mockGames,
  onClearSelection: vi.fn(),
  onSelectAll: vi.fn(),
  onDeselectAll: vi.fn(),
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('BulkActionBar - Rendering', () => {
  beforeEach(resetMocks);

  it('renders with selection count', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByText('2 selezionati')).toBeInTheDocument();
  });

  it('does not render when selectedCount is 0', () => {
    const { container } = render(<BulkActionBar {...defaultProps} selectedCount={0} />, {
      wrapper: createWrapper(),
    });

    expect(container.firstChild).toBeNull();
  });

  it('renders action buttons', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    // Component has both desktop and mobile versions of buttons (responsive design)
    // Using getAllByRole since there are multiple buttons with same name
    expect(screen.getAllByRole('button', { name: /Preferiti/i }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: /Rimuovi/i }).length).toBeGreaterThan(0);
  });

  it('renders clear selection button', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    expect(screen.getByRole('button', { name: /Esci dalla selezione/i })).toBeInTheDocument();
  });
});

// ============================================================================
// Clear Selection Tests
// ============================================================================

describe('BulkActionBar - Clear Selection', () => {
  beforeEach(resetMocks);

  it('calls onClearSelection when X button clicked', async () => {
    const user = userEvent.setup();
    const onClearSelection = vi.fn();

    render(<BulkActionBar {...defaultProps} onClearSelection={onClearSelection} />, {
      wrapper: createWrapper(),
    });

    await user.click(screen.getByRole('button', { name: /Esci dalla selezione/i }));

    expect(onClearSelection).toHaveBeenCalled();
  });
});

// ============================================================================
// Select All / Deselect All Tests
// ============================================================================

describe('BulkActionBar - Select All Toggle', () => {
  beforeEach(resetMocks);

  it('shows "Seleziona tutti" when not all selected', async () => {
    const user = userEvent.setup();
    render(<BulkActionBar {...defaultProps} selectedCount={2} />, { wrapper: createWrapper() });

    const button = screen.getByRole('button', { name: /Seleziona tutti/i });
    expect(button).toBeInTheDocument();

    await user.click(button);

    expect(defaultProps.onSelectAll).toHaveBeenCalledWith(allGameIds);
  });

  it('shows "Deseleziona tutti" when all selected', async () => {
    const user = userEvent.setup();
    render(<BulkActionBar {...defaultProps} selectedCount={3} selectedIds={allGameIds} />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByRole('button', { name: /Deseleziona tutti/i });
    expect(button).toBeInTheDocument();

    await user.click(button);

    expect(defaultProps.onDeselectAll).toHaveBeenCalled();
  });
});

// ============================================================================
// Bulk Favorite Tests
// ============================================================================

describe('BulkActionBar - Bulk Favorite', () => {
  beforeEach(resetMocks);

  it('marks all selected as favorite', async () => {
    const user = userEvent.setup();
    const onClearSelection = vi.fn();

    render(<BulkActionBar {...defaultProps} onClearSelection={onClearSelection} />, {
      wrapper: createWrapper(),
    });

    // Click desktop favorite button
    const buttons = screen.getAllByRole('button', { name: /Preferiti/i });
    await user.click(buttons[0]);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledTimes(2);
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { isFavorite: true },
      });
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-2',
        request: { isFavorite: true },
      });
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi segnati come preferiti');
      expect(onClearSelection).toHaveBeenCalled();
    });
  });

  it('shows warning toast on partial success', async () => {
    mockMutateAsync.mockResolvedValueOnce({}).mockRejectedValueOnce(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const buttons = screen.getAllByRole('button', { name: /Preferiti/i });
    await user.click(buttons[0]);

    await waitFor(() => {
      expect(mockToastWarning).toHaveBeenCalledWith('1 aggiornati, 1 errori');
    });
  });

  it('shows error toast when all fail', async () => {
    mockMutateAsync.mockRejectedValue(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const buttons = screen.getAllByRole('button', { name: /Preferiti/i });
    await user.click(buttons[0]);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile aggiornare i preferiti');
    });
  });
});

// ============================================================================
// Bulk Change State Tests (Issue #2868)
// ============================================================================

describe('BulkActionBar - Bulk Change State', () => {
  beforeEach(resetMocks);

  it('renders change state dropdown button', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    // Desktop button with text
    expect(screen.getAllByRole('button', { name: /Cambia Stato/i }).length).toBeGreaterThan(0);
  });

  it('changes state to Nuovo for all selected games', async () => {
    const user = userEvent.setup();
    const onClearSelection = vi.fn();

    render(<BulkActionBar {...defaultProps} onClearSelection={onClearSelection} />, {
      wrapper: createWrapper(),
    });

    // Open change state dropdown (desktop)
    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);

    // Click Nuovo option
    await user.click(getMenuItem(/^Nuovo$/i));

    await waitFor(() => {
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledTimes(2);
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { newState: 'Nuovo' },
      });
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-2',
        request: { newState: 'Nuovo' },
      });
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi aggiornati a "Nuovo"');
      expect(onClearSelection).toHaveBeenCalled();
    });
  });

  it('changes state to InPrestito for all selected games', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);

    await user.click(getMenuItem(/in prestito/i));

    await waitFor(() => {
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { newState: 'InPrestito' },
      });
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi aggiornati a "In Prestito"');
    });
  });

  it('changes state to Owned for all selected games', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);

    await user.click(getMenuItem(/posseduto/i));

    await waitFor(() => {
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { newState: 'Owned' },
      });
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi aggiornati a "Posseduto"');
    });
  });

  it('changes state to Wishlist for all selected games', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);

    await user.click(getMenuItem(/wishlist/i));

    await waitFor(() => {
      expect(mockUpdateGameStateMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { newState: 'Wishlist' },
      });
    });

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi aggiornati a "Wishlist"');
    });
  });

  it('shows warning toast on partial state change success', async () => {
    mockUpdateGameStateMutateAsync
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);
    await user.click(getMenuItem(/^Nuovo$/i));

    await waitFor(() => {
      expect(mockToastWarning).toHaveBeenCalledWith('1 aggiornati, 1 errori');
    });
  });

  it('shows error toast when all state changes fail', async () => {
    mockUpdateGameStateMutateAsync.mockRejectedValue(new Error('Failed'));

    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const changeStateButtons = screen.getAllByRole('button', { name: /Cambia Stato/i });
    await user.click(changeStateButtons[0]);
    await user.click(getMenuItem(/^Nuovo$/i));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile cambiare lo stato');
    });
  });
});

// ============================================================================
// Bulk Export Tests
// ============================================================================

describe('BulkActionBar - Bulk Export', () => {
  beforeEach(resetMocks);

  it('exports selected games as CSV', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    // Open export dropdown (desktop)
    const exportButtons = screen.getAllByRole('button', { name: /Esporta/i });
    await user.click(exportButtons[0]);

    // Click CSV - Base
    await user.click(getMenuItem(/csv - base/i));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(
        expect.arrayContaining([
          expect.objectContaining({ gameId: 'game-1' }),
          expect.objectContaining({ gameId: 'game-2' }),
        ]),
        expect.objectContaining({
          format: 'csv',
          scope: 'minimal',
        })
      );
    });

    expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi esportati (CSV)');
  });

  it('exports selected games as JSON full', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const exportButtons = screen.getAllByRole('button', { name: /Esporta/i });
    await user.click(exportButtons[0]);

    await user.click(getMenuItem(/json - completo/i));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(
        expect.any(Array),
        expect.objectContaining({
          format: 'json',
          scope: 'full',
        })
      );
    });

    expect(mockToastSuccess).toHaveBeenCalledWith('2 giochi esportati (JSON)');
  });

  it('shows error toast when export fails', async () => {
    mockExportLibrary.mockImplementation(() => {
      throw new Error('Export failed');
    });

    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const exportButtons = screen.getAllByRole('button', { name: /Esporta/i });
    await user.click(exportButtons[0]);
    await user.click(getMenuItem(/csv - base/i));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith("Errore durante l'esportazione");
    });
  });
});

// ============================================================================
// Remove Button Tests
// ============================================================================

describe('BulkActionBar - Remove Button', () => {
  beforeEach(resetMocks);

  it('opens remove dialog when remove button clicked', async () => {
    const user = userEvent.setup();

    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    // Click desktop remove button
    const removeButtons = screen.getAllByRole('button', { name: /Rimuovi/i });
    await user.click(removeButtons[0]);

    // Dialog should appear - use getAlertDialogHeading since it's a destructive action dialog
    await waitFor(() => {
      expect(getAlertDialogHeading(/rimuovi 2 giochi/i)).toBeInTheDocument();
    });
  });
});

// ============================================================================
// Styling Tests (Mobile UX #18 - Glassmorphism + Safe Area)
// ============================================================================

describe('BulkActionBar - Styling', () => {
  beforeEach(resetMocks);

  it('renders with glassmorphism design', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const bar = screen.getByTestId('bulk-action-bar');
    const innerCard = bar.querySelector('.backdrop-blur-md');
    expect(innerCard).toBeInTheDocument();
    expect(innerCard).toHaveClass('rounded-2xl');
  });

  it('has safe area bottom positioning', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const bar = screen.getByTestId('bulk-action-bar');
    expect(bar.className).toContain('safe-area-inset-bottom');
  });

  it('renders selection counter with foreground text', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const counter = screen.getByText('2 selezionati');
    expect(counter).toHaveClass('text-foreground');
  });

  it('renders with slide-in animation', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const bar = screen.getByTestId('bulk-action-bar');
    expect(bar).toHaveClass('animate-in');
  });

  it('mobile action buttons have 44px touch targets', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    const bar = screen.getByTestId('bulk-action-bar');
    const mobileButtons = bar.querySelectorAll('.min-w-\\[44px\\]');
    expect(mobileButtons.length).toBeGreaterThanOrEqual(3);
  });
});

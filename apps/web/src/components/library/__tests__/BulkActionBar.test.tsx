/**
 * BulkActionBar Component Tests (Issue #2613)
 *
 * Test Coverage:
 * - Rendering with selection count
 * - Bulk favorite action
 * - Bulk remove action (opens dialog)
 * - Bulk export dropdown
 * - Select all / deselect all toggle
 * - Clear selection button
 * - Mobile vs desktop rendering
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BulkActionBar } from '../BulkActionBar';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ============================================================================
// Mock Setup
// ============================================================================

const mockMutateAsync = vi.fn();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useUpdateLibraryEntry: () => ({
    mutateAsync: mockMutateAsync,
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
    gameId: 'game-1',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameImageUrl: null,
    addedAt: '2024-06-15T10:30:00Z',
    isFavorite: false,
    notes: null,
  },
  {
    id: 'entry-2',
    gameId: 'game-2',
    gameTitle: 'Ticket to Ride',
    gamePublisher: 'Days of Wonder',
    gameYearPublished: 2004,
    gameImageUrl: null,
    addedAt: '2024-07-20T14:00:00Z',
    isFavorite: true,
    notes: 'Great game',
  },
  {
    id: 'entry-3',
    gameId: 'game-3',
    gameTitle: 'Pandemic',
    gamePublisher: 'Z-Man Games',
    gameYearPublished: 2008,
    gameImageUrl: null,
    addedAt: '2024-08-10T09:00:00Z',
    isFavorite: false,
    notes: null,
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
    const { container } = render(
      <BulkActionBar {...defaultProps} selectedCount={0} />,
      { wrapper: createWrapper() }
    );

    expect(container.firstChild).toBeNull();
  });

  it('renders action buttons', () => {
    render(<BulkActionBar {...defaultProps} />, { wrapper: createWrapper() });

    // Desktop buttons (visible on sm+)
    expect(screen.getByRole('button', { name: /Preferiti/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Rimuovi/i })).toBeInTheDocument();
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

    render(
      <BulkActionBar {...defaultProps} onClearSelection={onClearSelection} />,
      { wrapper: createWrapper() }
    );

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
    render(
      <BulkActionBar
        {...defaultProps}
        selectedCount={3}
        selectedIds={allGameIds}
      />,
      { wrapper: createWrapper() }
    );

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

    render(
      <BulkActionBar {...defaultProps} onClearSelection={onClearSelection} />,
      { wrapper: createWrapper() }
    );

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
    mockMutateAsync
      .mockResolvedValueOnce({})
      .mockRejectedValueOnce(new Error('Failed'));

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
    await user.click(screen.getByText('CSV - Base'));

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

    await user.click(screen.getByText('JSON - Completo'));

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
    await user.click(screen.getByText('CSV - Base'));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Errore durante l\'esportazione');
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

    // Dialog should appear
    await waitFor(() => {
      expect(screen.getByText(/Rimuovi 2 giochi/i)).toBeInTheDocument();
    });
  });
});

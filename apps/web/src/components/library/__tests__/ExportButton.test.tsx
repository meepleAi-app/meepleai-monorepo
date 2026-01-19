/**
 * ExportButton Component Tests (Issue #2611)
 *
 * Test Coverage:
 * - Button rendering (simple and advanced modes)
 * - Dropdown menu functionality
 * - Export triggers for all format/scope combinations
 * - Loading state during export
 * - Toast notifications
 * - Disabled state handling
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ExportButton } from '../ExportButton';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ============================================================================
// Mock Setup
// ============================================================================

const mockExportLibrary = vi.fn();

vi.mock('@/lib/export/libraryExport', () => ({
  exportLibrary: (...args: unknown[]) => mockExportLibrary(...args),
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
// Test Data
// ============================================================================

const mockData: UserLibraryEntry[] = [
  {
    gameId: 'game-1',
    gameTitle: 'Catan',
    gamePublisher: 'Kosmos',
    gameYearPublished: 1995,
    gameImageUrl: null,
    addedAt: '2024-06-15T10:30:00Z',
    isFavorite: true,
    notes: 'Great game',
  },
  {
    gameId: 'game-2',
    gameTitle: 'Ticket to Ride',
    gamePublisher: 'Days of Wonder',
    gameYearPublished: 2004,
    gameImageUrl: null,
    addedAt: '2024-07-20T14:00:00Z',
    isFavorite: false,
    notes: null,
  },
];

// ============================================================================
// Helper
// ============================================================================

const resetMocks = () => {
  vi.clearAllMocks();
  mockExportLibrary.mockImplementation(() => {});
};

// ============================================================================
// Rendering Tests - Simple Mode
// ============================================================================

describe('ExportButton - Simple Mode', () => {
  beforeEach(resetMocks);

  it('renders simple button when showAdvanced is false', () => {
    render(<ExportButton data={mockData} showAdvanced={false} />);

    expect(screen.getByRole('button', { name: /Esporta CSV/i })).toBeInTheDocument();
  });

  it('does not show dropdown chevron in simple mode', () => {
    const { container } = render(<ExportButton data={mockData} showAdvanced={false} />);

    // ChevronDown icon should not be present
    const chevrons = container.querySelectorAll('svg.lucide-chevron-down');
    expect(chevrons).toHaveLength(0);
  });

  it('triggers quick CSV export on click in simple mode', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} showAdvanced={false} />);

    const button = screen.getByRole('button', { name: /Esporta CSV/i });
    await user.click(button);

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(mockData, {
        format: 'csv',
        scope: 'minimal',
      });
    });
  });
});

// ============================================================================
// Rendering Tests - Advanced Mode (Dropdown)
// ============================================================================

describe('ExportButton - Advanced Mode', () => {
  beforeEach(resetMocks);

  it('renders dropdown trigger button when showAdvanced is true', () => {
    render(<ExportButton data={mockData} showAdvanced={true} />);

    expect(screen.getByRole('button', { name: /Esporta/i })).toBeInTheDocument();
  });

  it('shows dropdown chevron in advanced mode', () => {
    const { container } = render(<ExportButton data={mockData} showAdvanced={true} />);

    const chevrons = container.querySelectorAll('svg.lucide-chevron-down');
    expect(chevrons).toHaveLength(1);
  });

  it('opens dropdown menu on click', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} showAdvanced={true} />);

    const button = screen.getByRole('button', { name: /Esporta/i });
    await user.click(button);

    await waitFor(() => {
      expect(screen.getByText('CSV - Base')).toBeInTheDocument();
      expect(screen.getByText('CSV - Completo')).toBeInTheDocument();
      expect(screen.getByText('JSON - Base')).toBeInTheDocument();
      expect(screen.getByText('JSON - Completo')).toBeInTheDocument();
    });
  });

  it('defaults to advanced mode when showAdvanced not specified', () => {
    const { container } = render(<ExportButton data={mockData} />);

    // Should show chevron (advanced mode default)
    const chevrons = container.querySelectorAll('svg.lucide-chevron-down');
    expect(chevrons).toHaveLength(1);
  });
});

// ============================================================================
// Export Functionality Tests
// ============================================================================

describe('ExportButton - Export Functionality', () => {
  beforeEach(resetMocks);

  it('exports CSV with minimal scope', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('CSV - Base'));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(mockData, {
        format: 'csv',
        scope: 'minimal',
      });
    });
  });

  it('exports CSV with full scope', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('CSV - Completo'));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(mockData, {
        format: 'csv',
        scope: 'full',
      });
    });
  });

  it('exports JSON with minimal scope', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('JSON - Base'));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(mockData, {
        format: 'json',
        scope: 'minimal',
      });
    });
  });

  it('exports JSON with full scope', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('JSON - Completo'));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledWith(mockData, {
        format: 'json',
        scope: 'full',
      });
    });
  });
});

// ============================================================================
// Toast Notification Tests
// ============================================================================

describe('ExportButton - Toast Notifications', () => {
  beforeEach(resetMocks);

  it('shows success toast after CSV export', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('CSV - Base'));

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Libreria esportata con successo (CSV base)'
      );
    });
  });

  it('shows success toast after JSON full export', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('JSON - Completo'));

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Libreria esportata con successo (JSON completo)'
      );
    });
  });

  it('shows error toast when export fails with Error', async () => {
    mockExportLibrary.mockImplementation(() => {
      throw new Error('Export failed');
    });

    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('CSV - Base'));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Export failed');
    });
  });

  it('shows default error toast for non-Error exceptions', async () => {
    mockExportLibrary.mockImplementation(() => {
      throw 'Unknown error';
    });

    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));
    await user.click(screen.getByText('CSV - Base'));

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile esportare la libreria.');
    });
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('ExportButton - Loading State', () => {
  beforeEach(resetMocks);

  it('calls export function successfully', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} showAdvanced={false} />);

    await user.click(screen.getByRole('button', { name: /Esporta CSV/i }));

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalled();
    });
  });

  it('shows success toast after export completes', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} showAdvanced={false} />);

    await user.click(screen.getByRole('button', { name: /Esporta CSV/i }));

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith(
        'Libreria esportata con successo (CSV base)'
      );
    });
  });

  it('handles multiple rapid exports', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} showAdvanced={false} />);

    const button = screen.getByRole('button', { name: /Esporta CSV/i });

    // Click once
    await user.click(button);

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledTimes(1);
    });

    // Click again after export completes
    await user.click(button);

    await waitFor(() => {
      expect(mockExportLibrary).toHaveBeenCalledTimes(2);
    });
  });
});

// ============================================================================
// Disabled State Tests
// ============================================================================

describe('ExportButton - Disabled State', () => {
  beforeEach(resetMocks);

  it('disables button when disabled prop is true', () => {
    render(<ExportButton data={mockData} disabled={true} />);

    const button = screen.getByRole('button', { name: /Esporta/i });
    expect(button).toBeDisabled();
  });

  it('disables button when data is empty', () => {
    render(<ExportButton data={[]} />);

    const button = screen.getByRole('button', { name: /Esporta/i });
    expect(button).toBeDisabled();
  });

  it('does not trigger export when disabled', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} disabled={true} showAdvanced={false} />);

    const button = screen.getByRole('button', { name: /Esporta CSV/i });
    // Force click even though disabled
    button.removeAttribute('disabled');
    await user.click(button);

    // Export should still be blocked by internal check
    await waitFor(() => {
      expect(mockExportLibrary).not.toHaveBeenCalled();
    });
  });
});

// ============================================================================
// Count Display Tests
// ============================================================================

describe('ExportButton - Count Display', () => {
  beforeEach(resetMocks);

  it('displays total count when provided', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} totalCount={50} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));

    await waitFor(() => {
      expect(screen.getByText('50 giochi')).toBeInTheDocument();
    });
  });

  it('displays filtered count when different from total', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} filteredCount={10} totalCount={50} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));

    await waitFor(() => {
      expect(screen.getByText('10 di 50 giochi')).toBeInTheDocument();
    });
  });

  it('uses data length as fallback when only totalCount is undefined', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} filteredCount={2} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));

    // When only filteredCount is provided (no totalCount), shows data.length as fallback
    await waitFor(() => {
      expect(screen.getByText('2 giochi')).toBeInTheDocument();
    });
  });

  it('does not display count section when no counts provided', async () => {
    const user = userEvent.setup();
    render(<ExportButton data={mockData} />);

    await user.click(screen.getByRole('button', { name: /Esporta/i }));

    // Count section should not be rendered when neither filteredCount nor totalCount is provided
    await waitFor(() => {
      expect(screen.queryByText(/giochi/)).not.toBeInTheDocument();
    });
  });
});

// ============================================================================
// Custom ClassName Tests
// ============================================================================

describe('ExportButton - Styling', () => {
  beforeEach(resetMocks);

  it('applies custom className', () => {
    render(<ExportButton data={mockData} className="custom-class" />);

    const button = screen.getByRole('button', { name: /Esporta/i });
    expect(button).toHaveClass('custom-class');
  });
});

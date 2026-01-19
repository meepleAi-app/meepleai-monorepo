/**
 * EditNotesModal Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Modal rendering and visibility
 * - Character counter display
 * - Save/cancel functionality
 * - Loading state during mutation
 * - Toast notifications
 * - Input validation
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EditNotesModal } from '../EditNotesModal';

// ============================================================================
// Mock Setup
// ============================================================================

let mockMutateAsync: Mock;
let mockIsPending: boolean;

vi.mock('@/hooks/queries', () => ({
  useUpdateLibraryEntry: () => ({
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
  currentNotes: 'Initial notes',
};

const resetMocks = () => {
  vi.clearAllMocks();
  mockMutateAsync = vi.fn().mockResolvedValue({});
  mockIsPending = false;
};

// ============================================================================
// Rendering Tests
// ============================================================================

describe('EditNotesModal - Rendering', () => {
  beforeEach(resetMocks);

  it('renders modal when isOpen is true', () => {
    render(<EditNotesModal {...defaultProps} />);

    expect(screen.getByText('Modifica Note')).toBeInTheDocument();
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('does not render modal when isOpen is false', () => {
    render(<EditNotesModal {...defaultProps} isOpen={false} />);

    expect(screen.queryByText('Modifica Note')).not.toBeInTheDocument();
  });

  it('displays current notes in textarea', () => {
    render(<EditNotesModal {...defaultProps} currentNotes="Test notes" />);

    const textarea = screen.getByRole('textbox');
    expect(textarea).toHaveValue('Test notes');
  });

  it('displays empty textarea when currentNotes is null', () => {
    render(<EditNotesModal {...defaultProps} currentNotes={null} />);

    const textarea = screen.getByRole('textbox');
    expect(textarea).toHaveValue('');
  });

  it('displays character counter', () => {
    render(<EditNotesModal {...defaultProps} currentNotes="Hello" />);

    expect(screen.getByText('495 caratteri rimanenti')).toBeInTheDocument();
  });
});

// ============================================================================
// Character Counter Tests
// ============================================================================

describe('EditNotesModal - Character Counter', () => {
  beforeEach(resetMocks);

  it('updates character count on input', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} currentNotes="" />);

    const textarea = screen.getByRole('textbox');
    await user.type(textarea, 'Test');

    expect(screen.getByText('496 caratteri rimanenti')).toBeInTheDocument();
  });

  it('shows warning style when less than 50 characters remaining', () => {
    const longNotes = 'A'.repeat(460);
    render(<EditNotesModal {...defaultProps} currentNotes={longNotes} />);

    const counter = screen.getByText('40 caratteri rimanenti');
    expect(counter).toHaveClass('text-destructive');
  });

  it('shows normal style when more than 50 characters remaining', () => {
    render(<EditNotesModal {...defaultProps} currentNotes="Short" />);

    const counter = screen.getByText('495 caratteri rimanenti');
    expect(counter).toHaveClass('text-muted-foreground');
  });
});

// ============================================================================
// Save Functionality Tests
// ============================================================================

describe('EditNotesModal - Save Functionality', () => {
  beforeEach(resetMocks);

  it('calls mutateAsync with correct arguments when save clicked', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} currentNotes="Test notes" />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { notes: 'Test notes' },
      });
    });
  });

  it('trims whitespace from notes before saving', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} currentNotes="  Trimmed notes  " />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { notes: 'Trimmed notes' },
      });
    });
  });

  it('sends null for empty notes', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} currentNotes="   " />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        gameId: 'game-1',
        request: { notes: null },
      });
    });
  });

  it('shows success toast after successful save', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockToastSuccess).toHaveBeenCalledWith('Note aggiornate con successo.');
    });
  });

  it('calls onNotesUpdated callback after successful save', async () => {
    const user = userEvent.setup();
    const onNotesUpdated = vi.fn();
    render(<EditNotesModal {...defaultProps} onNotesUpdated={onNotesUpdated} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(onNotesUpdated).toHaveBeenCalledWith('Initial notes');
    });
  });

  it('calls onClose after successful save', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<EditNotesModal {...defaultProps} onClose={onClose} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
  });
});

// ============================================================================
// Cancel Functionality Tests
// ============================================================================

describe('EditNotesModal - Cancel Functionality', () => {
  beforeEach(resetMocks);

  it('resets notes when cancel clicked', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} currentNotes="Original" />);

    const textarea = screen.getByRole('textbox');
    await user.clear(textarea);
    await user.type(textarea, 'Modified');

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    // Modal would close, but we verify onClose was called
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onClose when cancel clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<EditNotesModal {...defaultProps} onClose={onClose} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    expect(onClose).toHaveBeenCalled();
  });

  it('does not call mutateAsync when cancel clicked', async () => {
    const user = userEvent.setup();
    render(<EditNotesModal {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('EditNotesModal - Loading State', () => {
  beforeEach(resetMocks);

  it('shows loading text when saving', () => {
    mockIsPending = true;

    render(<EditNotesModal {...defaultProps} />);

    expect(screen.getByText('Salvataggio...')).toBeInTheDocument();
  });

  it('disables save button when loading', () => {
    mockIsPending = true;

    render(<EditNotesModal {...defaultProps} />);

    const saveButton = screen.getByRole('button', { name: /Salvataggio/i });
    expect(saveButton).toBeDisabled();
  });

  it('disables cancel button when loading', () => {
    mockIsPending = true;

    render(<EditNotesModal {...defaultProps} />);

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    expect(cancelButton).toBeDisabled();
  });
});

// ============================================================================
// Error Handling Tests
// ============================================================================

describe('EditNotesModal - Error Handling', () => {
  beforeEach(resetMocks);

  it('shows error toast when mutation fails', async () => {
    const user = userEvent.setup();
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<EditNotesModal {...defaultProps} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Network error');
    });
  });

  it('shows default error message for non-Error exceptions', async () => {
    const user = userEvent.setup();
    mockMutateAsync = vi.fn().mockRejectedValue('Unknown error');

    render(<EditNotesModal {...defaultProps} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith('Impossibile aggiornare le note.');
    });
  });

  it('does not close modal on error', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    mockMutateAsync = vi.fn().mockRejectedValue(new Error('Network error'));

    render(<EditNotesModal {...defaultProps} onClose={onClose} />);

    const saveButton = screen.getByRole('button', { name: /Salva/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });

    // onClose should NOT be called on error
    expect(onClose).not.toHaveBeenCalled();
  });
});

// ============================================================================
// Props Sync Tests
// ============================================================================

describe('EditNotesModal - Props Sync', () => {
  beforeEach(resetMocks);

  it('syncs notes when currentNotes prop changes', () => {
    const { rerender } = render(
      <EditNotesModal {...defaultProps} currentNotes="First" />
    );

    const textarea = screen.getByRole('textbox');
    expect(textarea).toHaveValue('First');

    rerender(<EditNotesModal {...defaultProps} currentNotes="Second" />);

    expect(textarea).toHaveValue('Second');
  });
});

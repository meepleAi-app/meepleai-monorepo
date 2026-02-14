/**
 * EditLedgerEntryModal Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { LedgerEntryDto } from '@/lib/api/schemas/financial-ledger.schemas';

import { EditLedgerEntryModal } from '../EditLedgerEntryModal';

const mockEntry: LedgerEntryDto = {
  id: 'entry-1',
  date: '2026-02-01T00:00:00Z',
  type: 0,
  category: 0,
  amount: 299.99,
  currency: 'EUR',
  source: 1,
  description: 'Monthly subscription',
  metadata: null,
  createdByUserId: 'user-1',
  createdAt: '2026-02-01T10:00:00Z',
  updatedAt: null,
};

describe('EditLedgerEntryModal', () => {
  const defaultProps = {
    entry: mockEntry,
    onClose: vi.fn(),
    onSubmit: vi.fn().mockResolvedValue(undefined),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders modal when entry is provided', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    expect(screen.getByTestId('edit-ledger-modal')).toBeInTheDocument();
  });

  it('does not render when entry is null', () => {
    render(<EditLedgerEntryModal {...defaultProps} entry={null} />);
    expect(screen.queryByTestId('edit-ledger-modal')).not.toBeInTheDocument();
  });

  it('renders header with title', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    expect(screen.getByText('Edit Ledger Entry')).toBeInTheDocument();
  });

  it('renders form fields', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    expect(screen.getByTestId('edit-category')).toBeInTheDocument();
    expect(screen.getByTestId('edit-description')).toBeInTheDocument();
    expect(screen.getByTestId('edit-metadata')).toBeInTheDocument();
  });

  it('pre-fills description from entry', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    const descInput = screen.getByTestId('edit-description') as HTMLTextAreaElement;
    expect(descInput.value).toBe('Monthly subscription');
  });

  it('pre-fills category from entry', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    const catSelect = screen.getByTestId('edit-category') as HTMLSelectElement;
    expect(catSelect.value).toBe('0');
  });

  it('shows read-only entry info', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    expect(screen.getByText(/Current category: Subscription/)).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    fireEvent.click(screen.getByTestId('close-edit-modal'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onClose when cancel clicked', () => {
    render(<EditLedgerEntryModal {...defaultProps} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('submits changes successfully', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<EditLedgerEntryModal {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByTestId('edit-description'), { target: { value: 'Updated description' } });
    fireEvent.click(screen.getByTestId('submit-edit-entry'));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith('entry-1', expect.objectContaining({
        description: 'Updated description',
      }));
    });
  });

  it('shows error when API fails', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('Update failed'));
    render(<EditLedgerEntryModal {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.click(screen.getByTestId('submit-edit-entry'));

    await waitFor(() => {
      expect(screen.getByTestId('edit-entry-error')).toBeInTheDocument();
      expect(screen.getByText('Update failed')).toBeInTheDocument();
    });
  });
});

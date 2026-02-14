/**
 * DeleteLedgerEntryDialog Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { LedgerEntryDto } from '@/lib/api/schemas/financial-ledger.schemas';

import { DeleteLedgerEntryDialog } from '../DeleteLedgerEntryDialog';

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

describe('DeleteLedgerEntryDialog', () => {
  const defaultProps = {
    entry: mockEntry,
    onClose: vi.fn(),
    onConfirm: vi.fn().mockResolvedValue(undefined),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders dialog when entry is provided', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    expect(screen.getByTestId('delete-ledger-dialog')).toBeInTheDocument();
  });

  it('does not render when entry is null', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} entry={null} />);
    expect(screen.queryByTestId('delete-ledger-dialog')).not.toBeInTheDocument();
  });

  it('renders warning title', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    expect(screen.getByText('Delete Ledger Entry')).toBeInTheDocument();
    expect(screen.getByText('This action cannot be undone.')).toBeInTheDocument();
  });

  it('displays entry details', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    expect(screen.getByText('Income')).toBeInTheDocument();
    expect(screen.getByText('Subscription')).toBeInTheDocument();
    expect(screen.getByText('Monthly subscription')).toBeInTheDocument();
  });

  it('displays formatted amount', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    expect(screen.getByText(/299\.99/)).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    fireEvent.click(screen.getByTestId('close-delete-dialog'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onClose when cancel clicked', () => {
    render(<DeleteLedgerEntryDialog {...defaultProps} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onConfirm when delete confirmed', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<DeleteLedgerEntryDialog {...defaultProps} onConfirm={onConfirm} />);

    fireEvent.click(screen.getByTestId('confirm-delete-entry'));

    await waitFor(() => {
      expect(onConfirm).toHaveBeenCalledWith('entry-1');
    });
  });

  it('shows error when delete fails', async () => {
    const onConfirm = vi.fn().mockRejectedValue(new Error('Delete failed'));
    render(<DeleteLedgerEntryDialog {...defaultProps} onConfirm={onConfirm} />);

    fireEvent.click(screen.getByTestId('confirm-delete-entry'));

    await waitFor(() => {
      expect(screen.getByTestId('delete-entry-error')).toBeInTheDocument();
      expect(screen.getByText('Delete failed')).toBeInTheDocument();
    });
  });

  it('shows deleting state on confirm button', async () => {
    const onConfirm = vi.fn().mockImplementation(() => new Promise(() => {}));
    render(<DeleteLedgerEntryDialog {...defaultProps} onConfirm={onConfirm} />);

    fireEvent.click(screen.getByTestId('confirm-delete-entry'));

    await waitFor(() => {
      expect(screen.getByText('Deleting...')).toBeInTheDocument();
    });
  });
});

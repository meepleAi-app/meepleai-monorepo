/**
 * CreateLedgerEntryModal Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CreateLedgerEntryModal } from '../CreateLedgerEntryModal';

describe('CreateLedgerEntryModal', () => {
  const defaultProps = {
    open: true,
    onClose: vi.fn(),
    onSubmit: vi.fn().mockResolvedValue(undefined),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders modal when open', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    expect(screen.getByTestId('create-ledger-modal')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<CreateLedgerEntryModal {...defaultProps} open={false} />);
    expect(screen.queryByTestId('create-ledger-modal')).not.toBeInTheDocument();
  });

  it('renders form fields', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    expect(screen.getByTestId('entry-date')).toBeInTheDocument();
    expect(screen.getByTestId('entry-type')).toBeInTheDocument();
    expect(screen.getByTestId('entry-category')).toBeInTheDocument();
    expect(screen.getByTestId('entry-amount')).toBeInTheDocument();
    expect(screen.getByTestId('entry-currency')).toBeInTheDocument();
    expect(screen.getByTestId('entry-description')).toBeInTheDocument();
  });

  it('renders header with title', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    expect(screen.getByText('New Ledger Entry')).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    fireEvent.click(screen.getByTestId('close-create-modal'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('calls onClose when cancel clicked', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(defaultProps.onClose).toHaveBeenCalled();
  });

  it('shows error for empty amount', async () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    const submitBtn = screen.getByTestId('submit-create-entry');
    expect(submitBtn).toBeDisabled();
  });

  it('shows error for invalid amount', async () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    const amountInput = screen.getByTestId('entry-amount');
    fireEvent.change(amountInput, { target: { value: '0' } });
    fireEvent.click(screen.getByTestId('submit-create-entry'));
    await waitFor(() => {
      expect(screen.getByTestId('create-entry-error')).toBeInTheDocument();
    });
  });

  it('submits valid entry successfully', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<CreateLedgerEntryModal {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByTestId('entry-amount'), { target: { value: '99.99' } });
    fireEvent.click(screen.getByTestId('submit-create-entry'));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          amount: 99.99,
          currency: 'EUR',
          type: 0,
          category: 0,
        })
      );
    });
  });

  it('displays entry types in select', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    const typeSelect = screen.getByTestId('entry-type');
    expect(typeSelect).toBeInTheDocument();
    expect(screen.getByText('Income')).toBeInTheDocument();
  });

  it('displays categories in select', () => {
    render(<CreateLedgerEntryModal {...defaultProps} />);
    expect(screen.getByText('Subscription')).toBeInTheDocument();
    expect(screen.getByText('Infrastructure')).toBeInTheDocument();
  });

  it('shows submit error when API fails', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('API error'));
    render(<CreateLedgerEntryModal {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByTestId('entry-amount'), { target: { value: '50' } });
    fireEvent.click(screen.getByTestId('submit-create-entry'));

    await waitFor(() => {
      expect(screen.getByTestId('create-entry-error')).toBeInTheDocument();
      expect(screen.getByText('API error')).toBeInTheDocument();
    });
  });
});

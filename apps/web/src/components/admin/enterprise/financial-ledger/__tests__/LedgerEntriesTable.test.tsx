/**
 * LedgerEntriesTable Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { LedgerEntryDto } from '@/lib/api/schemas/financial-ledger.schemas';

import { LedgerEntriesTable } from '../LedgerEntriesTable';

const mockEntries: LedgerEntryDto[] = [
  {
    id: 'entry-1',
    date: '2026-02-01T00:00:00Z',
    type: 0, // Income
    category: 0, // Subscription
    amount: 299.99,
    currency: 'EUR',
    source: 1, // Manual
    description: 'Monthly subscription',
    metadata: null,
    createdByUserId: 'user-1',
    createdAt: '2026-02-01T10:00:00Z',
    updatedAt: null,
  },
  {
    id: 'entry-2',
    date: '2026-02-03T00:00:00Z',
    type: 1, // Expense
    category: 7, // Infrastructure
    amount: 45.50,
    currency: 'EUR',
    source: 0, // Auto
    description: 'Cloud hosting',
    metadata: null,
    createdByUserId: null,
    createdAt: '2026-02-03T05:00:00Z',
    updatedAt: null,
  },
];

describe('LedgerEntriesTable', () => {
  const defaultProps = {
    entries: mockEntries,
    total: 2,
    page: 1,
    pageSize: 20,
    loading: false,
    onEdit: vi.fn(),
    onDelete: vi.fn(),
    onPageChange: vi.fn(),
  };

  it('renders the table container', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByTestId('ledger-entries-table')).toBeInTheDocument();
  });

  it('renders table headers', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText('Date')).toBeInTheDocument();
    expect(screen.getByText('Type')).toBeInTheDocument();
    expect(screen.getByText('Category')).toBeInTheDocument();
    expect(screen.getByText('Amount')).toBeInTheDocument();
    expect(screen.getByText('Source')).toBeInTheDocument();
    expect(screen.getByText('Description')).toBeInTheDocument();
    expect(screen.getByText('Actions')).toBeInTheDocument();
  });

  it('renders all entries', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByTestId('ledger-entry-entry-1')).toBeInTheDocument();
    expect(screen.getByTestId('ledger-entry-entry-2')).toBeInTheDocument();
  });

  it('displays type badges correctly', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText('Income')).toBeInTheDocument();
    expect(screen.getByText('Expense')).toBeInTheDocument();
  });

  it('displays category names', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText('Subscription')).toBeInTheDocument();
    expect(screen.getByText('Infrastructure')).toBeInTheDocument();
  });

  it('displays source badges', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText('Manual')).toBeInTheDocument();
    expect(screen.getByText('Auto')).toBeInTheDocument();
  });

  it('displays descriptions', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText('Monthly subscription')).toBeInTheDocument();
    expect(screen.getByText('Cloud hosting')).toBeInTheDocument();
  });

  it('shows edit button for all entries', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByTestId('edit-entry-entry-1')).toBeInTheDocument();
    expect(screen.getByTestId('edit-entry-entry-2')).toBeInTheDocument();
  });

  it('shows delete button only for manual entries', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    // Manual entry (entry-1) should have delete button
    expect(screen.getByTestId('delete-entry-entry-1')).toBeInTheDocument();
    // Auto entry (entry-2) should not have delete button
    expect(screen.queryByTestId('delete-entry-entry-2')).not.toBeInTheDocument();
  });

  it('calls onEdit when edit button clicked', () => {
    const onEdit = vi.fn();
    render(<LedgerEntriesTable {...defaultProps} onEdit={onEdit} />);
    fireEvent.click(screen.getByTestId('edit-entry-entry-1'));
    expect(onEdit).toHaveBeenCalledWith(mockEntries[0]);
  });

  it('calls onDelete when delete button clicked', () => {
    const onDelete = vi.fn();
    render(<LedgerEntriesTable {...defaultProps} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('delete-entry-entry-1'));
    expect(onDelete).toHaveBeenCalledWith(mockEntries[0]);
  });

  it('shows empty state when no entries', () => {
    render(<LedgerEntriesTable {...defaultProps} entries={[]} total={0} />);
    expect(screen.getByText('No ledger entries found.')).toBeInTheDocument();
  });

  it('shows loading skeleton', () => {
    render(<LedgerEntriesTable {...defaultProps} loading={true} />);
    expect(screen.getByTestId('ledger-table-loading')).toBeInTheDocument();
  });

  it('displays pagination info', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    expect(screen.getByText(/Showing 1–2 of 2/)).toBeInTheDocument();
  });

  it('calls onPageChange when next page clicked', () => {
    const onPageChange = vi.fn();
    render(<LedgerEntriesTable {...defaultProps} total={40} onPageChange={onPageChange} />);
    fireEvent.click(screen.getByTestId('next-page'));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('disables previous button on first page', () => {
    render(<LedgerEntriesTable {...defaultProps} />);
    const prevBtn = screen.getByTestId('prev-page');
    expect(prevBtn).toBeDisabled();
  });

  it('disables next button on last page', () => {
    render(<LedgerEntriesTable {...defaultProps} total={2} />);
    const nextBtn = screen.getByTestId('next-page');
    expect(nextBtn).toBeDisabled();
  });
});

/**
 * FinancialLedgerTab Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getLedgerEntries: vi.fn().mockRejectedValue(new Error('Not available')),
      getLedgerSummary: vi.fn().mockRejectedValue(new Error('Not available')),
      createLedgerEntry: vi.fn().mockResolvedValue({ id: 'new-id' }),
      updateLedgerEntry: vi.fn().mockResolvedValue(undefined),
      deleteLedgerEntry: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import { FinancialLedgerTab } from '../FinancialLedgerTab';

describe('FinancialLedgerTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the tab container', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('financial-ledger-tab')).toBeInTheDocument();
    });
  });

  it('renders action buttons', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
      expect(screen.getByTestId('refresh-ledger')).toBeInTheDocument();
      expect(screen.getByTestId('new-entry-btn')).toBeInTheDocument();
    });
  });

  it('renders summary cards with mock data', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('ledger-summary-cards')).toBeInTheDocument();
    });
  });

  it('renders entries table with mock data', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('ledger-entries-table')).toBeInTheDocument();
    });
  });

  it('toggles filter row visibility', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('filter-row')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('toggle-filters'));
    expect(screen.getByTestId('filter-row')).toBeInTheDocument();
  });

  it('renders filter dropdowns', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('toggle-filters'));
    expect(screen.getByTestId('filter-type')).toBeInTheDocument();
    expect(screen.getByTestId('filter-category')).toBeInTheDocument();
    expect(screen.getByTestId('filter-source')).toBeInTheDocument();
  });

  it('opens create entry modal', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('new-entry-btn')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('new-entry-btn'));
    expect(screen.getByTestId('create-ledger-modal')).toBeInTheDocument();
  });

  it('opens edit modal when edit clicked on an entry', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('ledger-entries-table')).toBeInTheDocument();
    });

    // Mock entries have deterministic IDs
    const editBtn = screen.getByTestId('edit-entry-00000000-0000-0000-0000-000000000001');
    fireEvent.click(editBtn);
    expect(screen.getByTestId('edit-ledger-modal')).toBeInTheDocument();
  });

  it('opens delete dialog when delete clicked on manual entry', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('ledger-entries-table')).toBeInTheDocument();
    });

    // First entry is manual (source: 1), fifth is also manual
    const deleteBtn = screen.getByTestId('delete-entry-00000000-0000-0000-0000-000000000001');
    fireEvent.click(deleteBtn);
    expect(screen.getByTestId('delete-ledger-dialog')).toBeInTheDocument();
  });

  it('shows error banner when all APIs fail', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('ledger-error')).toBeInTheDocument();
    });
  });

  it('shows clear filters button when filters active', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('toggle-filters'));
    fireEvent.change(screen.getByTestId('filter-type'), { target: { value: '0' } });

    expect(screen.getByTestId('clear-filters')).toBeInTheDocument();
  });

  it('clears filters when clear button clicked', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByTestId('toggle-filters')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('toggle-filters'));
    fireEvent.change(screen.getByTestId('filter-type'), { target: { value: '0' } });
    fireEvent.click(screen.getByTestId('clear-filters'));

    expect(screen.queryByTestId('clear-filters')).not.toBeInTheDocument();
  });

  it('displays mock entry descriptions in the table', async () => {
    render(<FinancialLedgerTab />);
    await waitFor(() => {
      expect(screen.getByText('Monthly Pro subscription - Acme Corp')).toBeInTheDocument();
      expect(screen.getByText('Cloud hosting - February')).toBeInTheDocument();
    });
  });
});

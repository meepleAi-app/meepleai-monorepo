/**
 * ShareRequestFilters Component Tests
 *
 * Issue #2744: Frontend - Dashboard Contributi Utente
 *
 * Tests:
 * - Filter rendering
 * - Status checkbox toggling
 * - Clear filters functionality
 * - Active filter count badge
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ShareRequestFilters } from '../ShareRequestFilters';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

describe('ShareRequestFilters', () => {
  const defaultProps = {
    selectedStatuses: [] as ShareRequestStatus[],
    onStatusChange: vi.fn(),
    onClearFilters: vi.fn(),
  };

  it('renders all status checkboxes', () => {
    render(<ShareRequestFilters {...defaultProps} />);

    expect(screen.getByLabelText('Pending')).toBeInTheDocument();
    expect(screen.getByLabelText('In Review')).toBeInTheDocument();
    expect(screen.getByLabelText('Changes Requested')).toBeInTheDocument();
    expect(screen.getByLabelText('Approved')).toBeInTheDocument();
    expect(screen.getByLabelText('Rejected')).toBeInTheDocument();
    expect(screen.getByLabelText('Withdrawn')).toBeInTheDocument();
  });

  it('checks selected statuses', () => {
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending', 'Approved']}
      />
    );

    // Radix UI Checkbox uses data-state="checked" instead of native .checked property
    const pendingCheckbox = screen.getByRole('checkbox', { name: 'Pending' });
    const approvedCheckbox = screen.getByRole('checkbox', { name: 'Approved' });

    expect(pendingCheckbox).toHaveAttribute('data-state', 'checked');
    expect(approvedCheckbox).toHaveAttribute('data-state', 'checked');
  });

  it('calls onStatusChange when checkbox is clicked', () => {
    const onStatusChange = vi.fn();
    render(<ShareRequestFilters {...defaultProps} onStatusChange={onStatusChange} />);

    const pendingCheckbox = screen.getByLabelText('Pending');
    fireEvent.click(pendingCheckbox);

    expect(onStatusChange).toHaveBeenCalledWith(['Pending']);
  });

  it('removes status when checkbox is unchecked', () => {
    const onStatusChange = vi.fn();
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending', 'Approved']}
        onStatusChange={onStatusChange}
      />
    );

    const pendingCheckbox = screen.getByLabelText('Pending');
    fireEvent.click(pendingCheckbox);

    expect(onStatusChange).toHaveBeenCalledWith(['Approved']);
  });

  it('shows active filter count badge', () => {
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending', 'Approved']}
      />
    );

    expect(screen.getByText('2')).toBeInTheDocument(); // 2 statuses
  });

  it('hides badge when no filters are active', () => {
    render(<ShareRequestFilters {...defaultProps} />);

    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });

  it('shows Clear All Filters button when filters are active', () => {
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending']}
      />
    );

    expect(screen.getByText('Clear All Filters')).toBeInTheDocument();
  });

  it('hides Clear All Filters button when no filters are active', () => {
    render(<ShareRequestFilters {...defaultProps} />);

    expect(screen.queryByText('Clear All Filters')).not.toBeInTheDocument();
  });

  it('calls onClearFilters when Clear All Filters is clicked', () => {
    const onClearFilters = vi.fn();
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending']}
        onClearFilters={onClearFilters}
      />
    );

    const clearButton = screen.getByText('Clear All Filters');
    fireEvent.click(clearButton);

    expect(onClearFilters).toHaveBeenCalled();
  });

  it('shows Clear button for status filters when statuses are selected', () => {
    render(
      <ShareRequestFilters
        {...defaultProps}
        selectedStatuses={['Pending', 'Approved']}
      />
    );

    const clearButtons = screen.getAllByText('Clear');
    expect(clearButtons.length).toBeGreaterThan(0);
  });
});
